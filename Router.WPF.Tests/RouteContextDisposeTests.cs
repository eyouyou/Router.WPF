using System;
using System.Collections.Generic;
using Router.Wpf.Abstractions;
using Router.Wpf.Abstractions.Routing;
using Router.Wpf.Contexts;
using Router.Wpf.Handlers;
using Xunit;

namespace Router.Wpf.Tests
{
    /// <summary>
    /// 给 C3-c 引入的 IRouteContext.Dispose 链做基本健全性检查 ——
    /// Dispose 必须把订阅者断开 / Notifier 终结，这样 C3-d 的 LCA diff 在替换
    /// 旧 context 时才能安心地 Dispose 它们。
    /// </summary>
    public class RouteContextDisposeTests
    {
        private static FrameRouteContext MakeFrameContext()
        {
            // Dispose 不走 handler 路径，FuncComponentHandler 用来占位即可。
            var handler = new FuncComponentHandler(_ => new System.Windows.Controls.TextBlock());
            var route = new Route("k", "k", handler);
            var match = new RouteMatch(route, new PathMatch("/k", new Dictionary<string, object>()));
            return new FrameRouteContext(match);
        }

        [WpfFact]
        public void Dispose_completes_match_and_outlet_subjects()
        {
            var ctx = MakeFrameContext();
            var matchSeen = false;
            var outletSeen = false;
            var matchCompleted = false;
            var outletCompleted = false;

            ctx.OnMatchChanged.Subscribe(_ => matchSeen = true, () => matchCompleted = true);
            ctx.OnOutletChanged.Subscribe(_ => outletSeen = true, () => outletCompleted = true);

            ctx.Dispose();

            Assert.True(matchCompleted);
            Assert.True(outletCompleted);
            // Dispose 期间不应该有"幽灵 OnNext"派发到任何一个流。
            Assert.False(matchSeen);
            Assert.False(outletSeen);
        }

        [WpfFact]
        public void Dispose_is_idempotent()
        {
            var ctx = MakeFrameContext();
            ctx.Dispose();
            // 第二次 Dispose 不能抛 —— 否则被 Dispose 过的 Notifier 在再次进入时就炸了。
            ctx.Dispose();
        }

        [WpfFact]
        public void Dispose_clears_references()
        {
            var ctx = MakeFrameContext();
            var child = MakeFrameContext();
            ctx.OutletRouteContext = child;
            ctx.RefContext = child;

            ctx.Dispose();

            Assert.Null(ctx.OutletRouteContext);
            Assert.Null(ctx.RefContext);
        }
    }
}

namespace Router.Wpf.Tests
{
    // RouteMatch 唯一的构造器是 internal —— 通过 InternalsVisibleTo 暴露给测试程序集。
    // 这里在编译时验证我们仍然能构造它（一旦访问路径被破坏，本程序集就会编译失败）。
    internal static class _AccessSentinel
    {
        public static RouteMatch Make() =>
            new(new Route("k", "/", new ActionHandler(_ => { })),
                new PathMatch("/", new System.Collections.Generic.Dictionary<string, object>()));
    }
}
