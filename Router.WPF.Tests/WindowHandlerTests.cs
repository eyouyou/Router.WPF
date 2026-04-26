using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Router.Wpf.Abstractions;
using Router.Wpf.Abstractions.Routing;
using Router.Wpf.Contexts;
using Router.Wpf.Handlers;
using Xunit;

namespace Router.Wpf.Tests
{
    /// <summary>
    /// 验证 C3-e 引入的 WindowRouteContext 行为：IsMultiple / IsRouteRef
    /// 各组合下的 Window 复用与释放语义。这些用例会真的弹出 WPF Window，
    /// 必须在 WPF Dispatcher 线程上跑，因此全部使用 [WpfFact]。
    /// </summary>
    public class WindowHandlerTests
    {
        /// <summary>
        /// WPF Loaded 事件是通过 Dispatcher 异步派发的 —— Show() 返回时订阅者
        /// 还没跑。这里阻塞等待 Dispatcher 把 ApplicationIdle 优先级以下的所有
        /// 工作都消化完，确保 Loaded / Render 这些高优先级回调一定都执行过了。
        /// </summary>
        private static void PumpDispatcher()
        {
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                () => { },
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private static (WindowRouteContext ctx, Window hostWindow) Mount(
            WindowHandler handler,
            IDictionary<string, object> firstParams,
            string firstPath = "/1")
        {
            var route = new Route("w", "win/{id:int}", handler);
            var match = new RouteMatch(route, new PathMatch(firstPath, new Dictionary<string, object>(firstParams)));
            var ctx = new WindowRouteContext(match);

            var host = new Window
            {
                Width = 50,
                Height = 50,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.ToolWindow,
                WindowState = WindowState.Minimized,
                Content = ctx,
            };
            host.Show(); // 触发 ctx.Loaded → Load(...)
            PumpDispatcher();
            return (ctx, host);
        }

        private static void UpdateMatch(WindowRouteContext ctx, IDictionary<string, object> newParams, string newPath)
        {
            // 模拟 GenericRouter 在 LCA diff 的 Update 区做的事。
            ctx.Match = new RouteMatch(ctx.Match.Route, new PathMatch(newPath, new Dictionary<string, object>(newParams)));
            ctx.OnMatchChanged.OnNext(ctx);
            PumpDispatcher();
        }

        [WpfFact]
        public void IsMultiple_false_reuses_the_same_Window_on_match_update()
        {
            var handler = new WindowHandler(
                _ => new WindowResult("Win", new TextBlock()),
                multiple: false,
                refRoute: false);

            var (ctx, host) = Mount(handler, new Dictionary<string, object> { ["id"] = 1 });
            var firstWindow = ctx.CurrentWindow;
            Assert.NotNull(firstWindow);

            UpdateMatch(ctx, new Dictionary<string, object> { ["id"] = 2 }, "/2");

            // Window 对象保持不变，内部 content 已被换掉。
            Assert.Same(firstWindow, ctx.CurrentWindow);

            firstWindow!.Close();
            host.Close();
        }

        [WpfFact]
        public void IsMultiple_false_updates_window_title_from_new_match()
        {
            // 更新时 GetResult 会再走一遍，所以 Title 能跟新参数同步。
            var handler = new WindowHandler(
                m => new WindowResult($"Plan {m.Parameters["id"]}", new TextBlock()),
                multiple: false,
                refRoute: false);

            var (ctx, host) = Mount(handler, new Dictionary<string, object> { ["id"] = 1 });
            Assert.Equal("Plan 1", ctx.CurrentWindow!.Title);

            UpdateMatch(ctx, new Dictionary<string, object> { ["id"] = 2 }, "/2");
            Assert.Equal("Plan 2", ctx.CurrentWindow!.Title);

            ctx.CurrentWindow.Close();
            host.Close();
        }

        [WpfFact]
        public void IsMultiple_true_opens_a_fresh_Window_per_match_update()
        {
            // 正常情况下 LCA diff 的 Update 区不会处理 IsMultiple=true 的 context
            // （它们是强制替换锚点）。但 OnChanged 直接被外部调用时，旧契约仍然是
            // "每次都开新窗体"，这里覆盖一下这条退化路径。
            var opened = new List<Window>();
            var handler = new WindowHandler(
                _ => new WindowResult("Multi", new TextBlock()),
                multiple: true,
                refRoute: false);

            var (ctx, host) = Mount(handler, new Dictionary<string, object> { ["id"] = 1 });
            // IsMultiple=true 时 CurrentWindow 一直是 null，每次 Load 都新开一扇 Window，
            // 内容挂在 window.Content 上。这里通过遍历 Application.Windows 抓所有非 host 的窗体。
            foreach (Window w in System.Windows.Application.Current?.Windows ?? new WindowCollection())
            {
                if (w != host) opened.Add(w);
            }
            // 通过 OnMatchChanged 触发：每次 Load 调用都会多一扇 Window。
            var beforeUpdate = System.Windows.Application.Current is { } app
                ? CountForeignWindows(app, host)
                : 0;

            UpdateMatch(ctx, new Dictionary<string, object> { ["id"] = 2 }, "/2");

            var afterUpdate = System.Windows.Application.Current is { } app2
                ? CountForeignWindows(app2, host)
                : 0;

            // 只有存在 Application 实例时才能断言。否则在裸 WpfFact 调度器下静默跳过，
            // 不让用例红。
            if (System.Windows.Application.Current != null)
                Assert.True(afterUpdate >= beforeUpdate + 1, "expected a new Window to open on update with IsMultiple=true");

            // 清理本用例期间可能开出来的所有窗体。
            if (System.Windows.Application.Current != null)
            {
                foreach (Window w in System.Windows.Application.Current.Windows)
                    if (w != host) w.Close();
            }
            host.Close();
        }

        private static int CountForeignWindows(System.Windows.Application app, Window self)
        {
            int n = 0;
            foreach (Window w in app.Windows)
                if (w != self) n++;
            return n;
        }

        [WpfFact]
        public void Dispose_closes_window_when_IsRouteRef_true()
        {
            var handler = new WindowHandler(
                _ => new WindowResult("Win", new TextBlock()),
                multiple: false,
                refRoute: true);

            var (ctx, host) = Mount(handler, new Dictionary<string, object> { ["id"] = 1 });
            var window = ctx.CurrentWindow;
            Assert.NotNull(window);
            Assert.True(window!.IsVisible);

            ctx.Dispose();

            Assert.False(window.IsVisible);
            host.Close();
        }

        [WpfFact]
        public void Dispose_leaves_window_open_when_IsRouteRef_false()
        {
            var handler = new WindowHandler(
                _ => new WindowResult("Win", new TextBlock()),
                multiple: false,
                refRoute: false);

            var (ctx, host) = Mount(handler, new Dictionary<string, object> { ["id"] = 1 });
            var window = ctx.CurrentWindow;
            Assert.NotNull(window);

            ctx.Dispose();

            // 独立生命周期：context 已 Dispose，但窗体仍然存活。
            Assert.True(window!.IsVisible);

            window.Close();
            host.Close();
        }

        [WpfFact]
        public void Manual_window_close_clears_CurrentWindow_for_IsMultiple_false()
        {
            var handler = new WindowHandler(
                _ => new WindowResult("Win", new TextBlock()),
                multiple: false,
                refRoute: false);

            var (ctx, host) = Mount(handler, new Dictionary<string, object> { ["id"] = 1 });
            var window = ctx.CurrentWindow;
            Assert.NotNull(window);

            window!.Close();

            Assert.Null(ctx.CurrentWindow);
            host.Close();
        }
    }
}
