using System.Collections.Generic;
using System.Windows.Controls;
using Router.Wpf.Abstractions;
using Router.Wpf.Abstractions.Routing;
using Router.Wpf.Handlers;

namespace Router.Wpf.Routers
{
    class GenericRouter : Router
    {
        /// <summary>
        /// 当前活跃的路由 context 链（root → leaf）。替代了原先的
        /// <c>Dictionary&lt;Route, IRouteContext&gt;</c> 缓存 —— 那个缓存
        /// 永不淘汰、且仅按 <c>Route.Key</c> 比较，导致同 Route 不同参数的导航
        /// 总是拿到带过期参数的旧 context。
        /// </summary>
        private readonly List<IRouteContext> _activeChain = new();

        // 给测试用的内部探针 —— 在不构造完整 WPF 视觉树的情况下观测链状态。
        internal IReadOnlyList<IRouteContext> ActiveChainForTests => _activeChain;
        internal void RegisterRoutesForTests(System.Collections.Generic.IEnumerable<Route> routes)
        {
            var collection = RouteCollection;
            foreach (var route in routes)
                RecursiveGenerateRoute(route, "/", ref collection);
        }

        public override RouteCollection RouteCollection { get; } = new IndexRouteCollection();
        readonly ContentControl host;

        internal GenericRouter(string? target = null, params Route[] routes)
        {
            CurrentTarget = target ?? "/";
            host = CreateHost();
            Content = host;

            // routes 直接被 Loaded 闭包捕获，不再用字段保存 —— 这些数据
            // 只在 Router 进入视觉树那一刻才需要用一次。再用一个布尔标志
            // 防止 Router 被搬到别的父节点时重复初始化、把索引加双份。
            var initialized = false;
            Loaded += (_, _) =>
            {
                if (initialized) return;
                initialized = true;

                var collection = RouteCollection;
                foreach (var route in routes)
                    RecursiveGenerateRoute(route, "/", ref collection);

                Navigate(CurrentTarget, null);
            };
        }

        static void RecursiveGenerateRoute(Route route, string parentPath, ref RouteCollection collection)
        {
            var pathPattern = PathUtil.Combine(parentPath, route.Path);
            collection.Add(pathPattern, route);
            if (route.Children != null)
            {
                foreach (var item in route.Children)
                {
                    RecursiveGenerateRoute(item, pathPattern, ref collection);
                }
            }
        }

        /// <summary>
        /// LCA 差分跳转。把当前活跃链与新匹配链对齐，划分成三段：
        ///   [0, keep)             Route + Match 完全一致，原样复用
        ///   [keep, updateUntil)   Route 相同但参数不同，原地 Update（Match 替换）
        ///   [updateUntil, ...)    Route 不同或长度不同，Replace（Dispose 旧 + 新建）
        /// 带 <c>IsMultiple=true</c> 的 WindowHandler 是"强制替换锚点"：它对应的节点
        /// 永不复用，同时把 keep 和 updateUntil 都截断在它之前，所以每次跳转都会
        /// 弹出一个新窗体。
        /// </summary>
        protected override bool Navigate(string pathName, bool addHistory, object? extraData)
        {
            var matches = RouteCollection.Match(pathName);
            if (matches.Count == 0) return false;

            int keep = 0;
            while (keep < matches.Count && keep < _activeChain.Count)
            {
                var nm = matches[keep];
                var oldMatch = _activeChain[keep].Match;
                if (!oldMatch.Route.Equals(nm.Route)) break;
                if (!oldMatch.Equals(nm)) break;
                if (IsForceReplaceAnchor(nm)) break;
                keep++;
            }

            int updateUntil = keep;
            while (updateUntil < matches.Count && updateUntil < _activeChain.Count)
            {
                var nm = matches[updateUntil];
                if (!_activeChain[updateUntil].Match.Route.Equals(nm.Route)) break;
                if (IsForceReplaceAnchor(nm)) break;
                updateUntil++;
            }

            if (addHistory) PushRecord(pathName);
            CurrentTarget = pathName;
            var args = new NavigationEventArgs(pathName, extraData, matches, addHistory);

            // 整条链完全相同、长度也一致 —— 不需要动 chain。
            // 历史依然要 push（按浏览器语义，重复 URL 也算一次跳转），
            // NavigationRequested 也照常发出，保证订阅者感知得到。
            if (keep == matches.Count && keep == _activeChain.Count)
            {
                NavigationRequested.OnNext(args);
                return true;
            }

            // 1. [keep, updateUntil) 区间内的 Match 原地替换。
            //    这里不发 OnMatchChanged —— 最后只在最高变化点发一次，
            //    其余层级靠 Outlet/Loaded 链自然向下级联渲染。
            for (int i = keep; i < updateUntil; i++)
                _activeChain[i].Match = matches[i];

            // 2. 释放旧链尾 [updateUntil, _activeChain.Count)，叶子先释放。
            for (int i = _activeChain.Count - 1; i >= updateUntil; i--)
                _activeChain[i].Dispose();
            if (_activeChain.Count > updateUntil)
                _activeChain.RemoveRange(updateUntil, _activeChain.Count - updateUntil);

            // 3. 构造新链尾 [updateUntil, matches.Count)，从叶子向根反向构造，
            //    这样在创建每个 context 时都能把已经做好的子节点作为它的 outlet。
            var newTail = new List<IRouteContext>(matches.Count - updateUntil);
            IRouteContext? outletContext = null;
            for (int i = matches.Count - 1; i >= updateUntil; i--)
            {
                var ctx = matches[i].Route.Handler.CreateContext(matches[i], outletContext);
                newTail.Insert(0, ctx);
                outletContext = ctx;
            }

            // 4. 把"幸存的最后一个节点"（pivot，即最后一个被复用或更新过的 context）
            //    的 OutletRouteContext 接到新链头；如果只是单纯截短，则置 null。
            if (updateUntil > 0)
                _activeChain[updateUntil - 1].OutletRouteContext = newTail.Count > 0 ? newTail[0] : null;

            // 5. 拼接新链。
            _activeChain.AddRange(newTail);

            // 6. 在最高变化点触发一次渲染。三个分支互斥：
            //    - 根被替换         -> 直接换 host.Content
            //    - 存在 Update 区   -> 在它的最顶端发 OnMatchChanged，子层通过 Loaded 级联
            //    - 否则纯 Replace   -> 在 pivot 上发 OnOutletChanged，让它的 Outlet 拿到新子链
            if (updateUntil == 0)
            {
                host.Content = _activeChain.Count > 0 ? _activeChain[0] : null;
            }
            else if (updateUntil > keep)
            {
                _activeChain[keep].OnMatchChanged.OnNext(_activeChain[keep]);
            }
            else if (_activeChain[updateUntil - 1] is IParentRouteContext parent)
            {
                parent.OnOutletChanged.OnNext(parent);
            }

            NavigationRequested.OnNext(args);
            return true;
        }

        public override void Refresh()
        {
            // 把整条链全 Dispose 掉，让每一层都从头重建；
            // 子级 Outlet 会在 Loaded 时重新挂上来。
            for (int i = _activeChain.Count - 1; i >= 0; i--)
                _activeChain[i].Dispose();
            _activeChain.Clear();
            host.Content = null;
            base.Refresh();
        }

        private static bool IsForceReplaceAnchor(RouteMatch m)
            => m.Route.Handler is WindowHandler { IsMultiple: true };
    }
}
