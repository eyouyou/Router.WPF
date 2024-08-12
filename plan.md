# Router.WPF 优化落地方案

> 范围：`Router.WPF`（库）+ `Router.WPF.Sample`（示例）。
> 原则：先修真 Bug，再优化性能与跳转核心算法，最后做 API 与示例改造。每一阶段独立可合并、可回滚。

---

## 阶段 1：真 Bug 修复（小改动、低风险）

### 1.1 `WpfTreeExtension.FindVisualParentUntil` 递归错误
- 位置：`Router.WPF/WpfTreeExtension.cs:47`
- 现象：命中 U 之外的节点时，递归调用的是 `FindVisualParent<T>`（无 U 终止条件），越过 U 边界继续向上查找。
- 改动：递归改为 `FindVisualParentUntil<T, U>(parentObject)`。

### 1.2 修 `BackStack` / `ForwardStack` 实现
- 位置：`Router.WPF/Router.cs:48-50`
- 改动：
  - `BackStack => _pathRecord.Take(_pathIndex)`
  - `ForwardStack => _pathRecord.Skip(_pathIndex + 1)`

### 1.3 GoBack 后 Navigate 截断 forward stack
- 位置：`Router.WPF/Router.cs:84-88`（`PushRecord`）
- 改动：push 前 `_pathRecord.RemoveRange(_pathIndex + 1, _pathRecord.Count - _pathIndex - 1)`。

### 1.4 修 `PathMatch.GetHashCode` / `RouteMatch.GetHashCode`
- 位置：`Router.WPF/Abstractions/Parser.cs:35` 与 `Parser.cs:77`
- 现象：`<<` 与 `&`（位与）合并哈希，会大量碰撞。
- 改动：
  - `GetHashCode` → `HashCode.Combine(PathName, Parameters.Count)`
  - `Equals` 中 `Parameters.SequenceEqual` 改为按键值比较，避免依赖字典顺序。

### 1.5 多窗体 `RefWindows` 内存泄漏
- 位置：`Router.WPF/Contexts/WindowRouteContext.cs:111`
- 现象：`Window_Closed` 仅在 `!IsMultiple` 才清理，IsMultiple=true 的窗口永不释放。
- 改动：无条件 `RefWindows.Remove(window)`；`!IsMultiple` 时额外清 `CurrentWindow`。

### 1.6 修 `_dynamicTokens.Add` 重复键
- 位置：`Router.WPF/Resolver/PathRouting.cs:112`
- 改动：用索引器 `_dynamicTokens[name] = token`；解析时如有重名参数抛带描述的异常。

### 1.7 异常信息
- 替换全部 `throw new Exception("")`：
  - `Contexts/Context.cs:67`
  - `Contexts/WindowRouteContext.cs:34, 77, 134`
- 改为：`throw new InvalidOperationException("...具体上下文...")`。

### 1.8 `Refresh()` 实现
- 位置：`Router.WPF/Router.cs:59`
- 改动：实现为 `Navigate(CurrentTarget, false, null)`（不入栈），并在 GenericRouter 内额外触发顶层 OnMatchChanged。

---

## 阶段 2：性能优化

### 2.1 Lucene 索引批量构建
- 位置：`Router.WPF/LuceneRouteCollection.cs`
- 现象：每条路由 add 都新开 `IndexWriter` + `Commit()`，路由量稍大启动会卡顿。
- 改动：
  - `LuceneMemoryIndexer` 持有长期 `IndexWriter`、`Analyzer`，构造时打开。
  - 新增 `Seal()`：批量 add 完成一次性 commit。
  - `IndexRouteCollection.Add` 只 push 文档，不 commit。
  - `GenericRouter_Loaded` 末尾调用 `Seal()`。
  - `Search` 改用 `SearcherManager` + `MaybeRefresh()`，不再每次新开 reader。
  - 增加 `IDisposable` 实现，关闭 writer/analyzer。
  - `Search(query, 10)` 中 `topN` 参数化。

### 2.2 `IndexerExtensions` 静态字典换 `ConcurrentDictionary`
- 位置：`Router.WPF/LuceneRouteCollection.cs:41-42`
- 改动：`Dictionary<,>` → `ConcurrentDictionary<,>`，反射缓存线程安全。

### 2.3 `RouteCollection.Match` 短路
- 现状：`O(N × regex)` 全表扫描。
- 当前阶段：`Match` 命中 IndexRoute 后允许立刻替换；后续阶段引入 Trie。

### 2.4（可选，后置）依赖瘦身
- Lucene + Pidgin + System.Reactive 三个大库服务的功能很小，后续可评估替换为 Trie / 手写解析 / `event`。本阶段不动。

---

## 阶段 3：跳转内部逻辑优化（核心重构）

### 3.0 现状梳理
跳转算法集中在 `Routers/GenericRouter.Navigate`，存在以下问题：

1. **`_contexts: Dictionary<Route, IRouteContext>` 全局缓存**：
   - 永不清理 → 内存泄漏。
   - `Route.Equals` 仅比较 `Key`，同一 Route 不同参数（`plan/123` vs `plan/456`）会拿到旧 context，旧的 `Match` 残留。
2. **反向循环 + 三段式 if-else + `isUnLoaded` 状态机** 难读。`i + 1 >= matches.Count - index` 的边界判断不直观。
3. **`FrameNavigated` 里写了 `break`**：注释 "是不是就刷一个顶级的就够了" 表明作者自己也不确定。逻辑上依赖 Outlet 加载链触发子级渲染，对中间层"参数变化但 Route 不变"的场景会强制顶层重建，造成多余 view 重建。
4. **`frame.Navigate(outletContext)` 使用 WPF Frame 自带 navigation**：Frame 内部维持 journal，与我们自己的 `_pathRecord` 双重历史。
5. **没有显式的"路由切换关闭旧 Window"逻辑**：`IsRouteRef=true` 的语义在 `WindowRouteContext_Unloaded` 才生效，依赖卸载时机不可控。
6. **`outletContext.Match = match` 直接赋值**：不会触发任何通知，依赖外层逐个 `OnMatchChanged.OnNext` 模拟，遗漏即静默失效。
7. **Frame、Subject、Window 都没有 dispose 流程**。

### 3.1 引入"匹配链 + LCA Diff"算法
将 `RouteCollection.Match` 的返回值视为**从根到叶的匹配链 `Chain`**（rename `matches` → `newChain`，并在文档明确）。

维护字段：
```csharp
private List<IRouteContext> _activeChain = new();   // 当前活动 context 链（root → leaf）
```

`Navigate` 步骤：

1. **解析新链**：`newChain = RouteCollection.Match(target)`。如为空，直接返回 false（不入栈、不通知）。
2. **找 LCA（Lowest Common Ancestor）**：
   - 自顶向下与 `_activeChain` 比较，求最深的"同 Route + 同 Match"前缀长度 `keep`。
   - `keep == _activeChain.Count == newChain.Count`：完全一致，直接 return（除非强制 refresh）。
3. **分类处理**：
   - `[0, keep)`：Reuse，不动 context。
   - `[keep, ...)`：
     - 若同 Route 但 Match 不同 → **Update**：复用 context，赋新 `Match`，触发 `OnMatchChanged.OnNext(this)`。
     - 否则 → **Replace**：销毁旧 context（关 Window、dispose Subject、清 Frame.Content），新建 context。
4. **触发渲染**：
   - 计算"最高变化点" `pivot = keep - 1`（如果 keep == 0 则为 -1，意味着根换了）。
   - `pivot < 0` → 用根级容器（`Router.Content` 或 `Router` 自带 frame）直接挂新链根 context；后续子级走 `Outlet.Loaded` 链自然加载。
   - `pivot >= 0` → 给 `_activeChain[pivot]`（必为 `IParentRouteContext`）触发一次 `OnOutletChanged.OnNext(this)`，让其 Outlet 重设 Content 为新链 `[pivot+1]`。
5. **更新 `_activeChain`**：用新链替换。
6. **触发 `NavigationRequested.OnNext(args)`**。

### 3.2 销毁与生命周期
新增 `IRouteContext.Dispose()`（或 `IDisposable` 实现）：

- `FrameRouteContext.Dispose`：`Frame.Content = null`，`OnMatchChanged.OnCompleted()` + `Dispose()`，`OnOutletChanged.OnCompleted()` + `Dispose()`。
- `WindowRouteContext.Dispose`：关闭所有 `RefWindows`（无论 `IsRouteRef`），清 `RefContexts`，dispose subjects。
- 在 Replace 路径上调用 `Dispose`，避免泄漏。

### 3.3 Window 与路由的耦合明确化
- `IsRouteRef = true`：进入 Replace 时**关闭**该窗口；通过 `_activeChain` diff 触发，不再依赖 Unloaded。
- `IsRouteRef = false`：保留窗口（独立生命周期），但在 Replace 时把它从 `_activeChain` 摘下，不再受跳转影响。
- `IsMultiple = true` 始终 Replace（不复用），每次都新链。

### 3.4 用 `ContentPresenter` 替代 `Frame.Navigate`
- `Router` 内部由 `Frame` 改为 `ContentControl`/`ContentPresenter`，避免 WPF Frame 自带 journal。
- 同样适用于 `FrameRouteContext.Frame` 的位置。
- 好处：`Refresh()` 实现简单（Content 置空再赋）；不再触发 Frame 的 Navigated 事件二次副作用。
- 注：保留外部 `Frame` 兼容性的过渡方案——`CreateFrame()` 设置 `JournalOwnership = JournalOwnership.OwnsJournal` + `RemoveBackEntry()` 清理；如果重构成本可接受则一步换掉。

### 3.5 异步与可取消（前瞻接口，本阶段仅留位）
- 新增 `Task<bool> NavigateAsync(string target, object? extraData, CancellationToken ct = default)`。
- 旧 `Navigate(...)` 内部调 `NavigateAsync(...).GetAwaiter().GetResult()`，行为兼容。
- 为后续路由守卫（CanActivate）、异步加载预留入口。

### 3.6 移除 `_contexts: Dictionary<Route, IRouteContext>`
- 完全由 `_activeChain` 替代，避免按 Route Key 错配。

### 3.7 `OnMatchChanged` 自动化
- `RouteMatch` 改为属性，setter 内自动 `OnMatchChanged.OnNext(this)`，调用方不用手动触发，避免遗漏。
- 或者保持显式调用，但加 XML doc 强调约定。优选自动化。

---

## 阶段 4：API & 设计修整

### 4.1 命名空间统一
现状混用四个根命名空间：
- `Unity.UI.WPF`、`Unity.UI.Core`、`Unity.UI.Core.Abstractions(.Routing)`、`Router.WPF.Abstractions`。

统一为：
- `Router.Wpf`
- `Router.Wpf.Core`
- `Router.Wpf.Abstractions(.Routing)`

涉及全部 `.cs` 文件 `namespace` + 调用方 `using`。**Breaking change**，独立 commit 便于回滚。

### 4.2 路径拼接去掉 `Path.GetFullPath`
- 位置：`GenericRouter.cs:95`、`PathRouting.cs:130-153`。
- 现状：用 `Path.Combine` + `Path.GetFullPath` + `ConvertToLinuxPath`（hack）。在 Windows 上对盘符、`..`、特殊字符易错，且依赖文件系统语义。
- 改动：`PathUtil.Combine(parent, child)` 纯字符串实现：
  - 处理前导 `/`
  - 折叠 `//`
  - 解析 `..`、`.`
  - 全程 `/`，不引入文件系统。

### 4.3 移除未用字段、修正 `CurrentRouter` API
- `GenericRouter._routes` 加载后未再使用，改为 `Loaded` 内 local。
- `RouterExtension.CurrentRouter(this FrameworkElement element)` 的 `element` 未使用——要么改为查找最近的 `Router` 视觉父节点（支持多 Router scope），要么直接改成静态 `Router.Current`，去掉误导参数。倾向前者，预留多 Router 能力。

### 4.4 `Navigate` 返回真实成功/失败
- `RouteCollection.Match` 无结果时 return false，不入栈。
- WindowHandler 的多窗体场景失败也返回 false。

### 4.5 Subject 释放
- 在 `Router`、`FrameRouteContext`、`WindowRouteContext` 的 Unloaded（或 3.2 中新加 Dispose）路径上 `OnCompleted()` + `Dispose()`，断开订阅者持有，便于 GC。

---

## 阶段 5：Sample 改造

### 5.1 拆 XAML + ViewModel
- 新增 `Views/`：`ProfileView.xaml`、`AccountView.xaml`、`AppearenceView.xaml`、`BillingView.xaml`、`PlanView.xaml`、`ModerationView.xaml`、`BlockedUsersView.xaml`、`BlockedUserView.xaml`、`PlansAndUsageWindowView.xaml` 等 UserControl/Window。
- 新增 `ViewModels/`：至少 1-2 个完整 MVVM 示例（`BillingViewModel`、`PlanViewModel`），演示参数注入、`ICommand`、跳转触发。
- `MainWindow.xaml` 中放 `<wpf:Outlet/>`，去掉 `Content = this.CurrentRouter()` 的 code-behind。

### 5.2 路由注册迁移
- `App.xaml.cs.OnStartup` 调用 `Router.InitRouter`，handler 用 `() => new ProfileView()`，不再用 `new TextBlock`。
- 演示 `WindowHandler` 中传入 ViewModel 的写法。

### 5.3 移除调试残留
- 去掉所有 `MessageBox.Show("Button clicked!")`。

### 5.4 README
- 加"快速开始"段落，对应新 sample。
- 标注三种 Handler 的语义（`FuncComponentHandler` / `WindowHandler` / `TaskHandler`）。
- 补 `IsMultiple` 与 `IsRouteRef` 的真值表与典型用例。

---

## 阶段 6：测试基础设施

新增 `Router.WPF.Tests`（xUnit + WPF host）：
- `BackStack` / `ForwardStack` / `GoBack` 后 Navigate 截断
- `TypePathParser.Match`：静态 / 动态 / 可空 / 多动态段
- `RouteCollection.Match`：嵌套 + IndexRoute + 不匹配
- `LuceneMemoryIndexer`：批量索引、增量、检索
- `GenericRouter`（核心）：
  - 同 Route 不同参数（Update 路径）
  - 不同 Route（Replace 路径）
  - 多窗体打开/关闭
  - LCA 计算正确（mock UI tree）

测试工程作为阶段 1 的子任务建立。后续每修一个 Bug 配一个用例。

---

## 提交节奏与回滚策略

| Commit | 内容 | Breaking? | 回滚成本 |
|--------|------|-----------|---------|
| C1 | 阶段 1（除 1.8 Refresh）+ 阶段 6 测试骨架 | 否 | 低 |
| C2 | 阶段 2.1 + 2.2 | 否 | 低 |
| C3 | 阶段 3（跳转算法重构）| 行为兼容（API 不变）| 中，依赖测试覆盖 |
| C4 | 阶段 4.1 命名空间统一 | **是** | 中（仅 sample 内部需调） |
| C5 | 阶段 4.2 - 4.5 + 1.8 Refresh | 否 | 低 |
| C6 | 阶段 5 Sample 重写 | sample 内部 | 低 |

每个 commit 单独可合并；建议先合 C1+C2 让稳定性 / 性能见效，再启动 C3。

---

## 当前不做（明确 out of scope）

- 异步路由守卫（CanActivate / CanDeactivate）：阶段 3.5 仅留接口。
- 路由懒加载（按需 import view assembly）。
- 路由动画/过渡。
- 替换 Lucene/Pidgin/Reactive 三个大依赖（阶段 2.4 后置评估）。
- 多 Router 实例的完整支持（阶段 4.3 留接口，行为不实现）。

---

## 待确认（需要 owner 拍板）

1. 阶段 4.1 命名空间统一是否接受 breaking？如果不接受，仅做新文件用新命名空间，旧的保留。
2. 阶段 3.4 `Frame` → `ContentPresenter` 是否影响外部对 `IRouteContext.Frame` 的访问？目前 sample 不依赖此属性，库外是否有其它使用方？
3. 阶段 5 Sample 是否保留代码动态构造的 demo 作为对比，还是完全替换？
