using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Router.Wpf.Abstractions;
using Router.Wpf.Abstractions.Routing;
using Router.Wpf.Handlers;
using Router.Wpf.Routers;
using Xunit;

namespace Router.Wpf.Tests
{
    /// <summary>
    /// 验证 GenericRouter 在 C3-d 引入的 LCA 差分跳转算法 ——
    /// 当新的匹配链与活跃链不同时，哪些 context 被复用、哪些被原地 Update、
    /// 哪些被 Replace（即 Dispose+重建）。
    /// </summary>
    public class LcaDiffTests
    {
        private static Route Func(string key, string path) =>
            new(key, path, new FuncComponentHandler(_ => new TextBlock()));

        private static Route Win(string key, string path, bool multiple = false) =>
            new(key, path, new WindowHandler(_ => new WindowResult(key, new TextBlock()),
                multiple: multiple,
                refRoute: false));

        private static GenericRouter NewRouter(string startup, params Route[] routes)
        {
            var router = new GenericRouter(startup);
            router.RegisterRoutesForTests(routes);
            return router;
        }

        [WpfFact]
        public void Initial_navigation_builds_full_chain()
        {
            var billing = Func("billing", "billing");
            billing.Children = new List<Route> { Func("spending", "spending-limits") };
            var router = NewRouter("/billing/spending-limits", billing);

            Assert.True(router.Navigate("/billing/spending-limits", null));
            Assert.Equal(2, router.ActiveChainForTests.Count);
            Assert.Equal("billing",  router.ActiveChainForTests[0].Match.Route.Key);
            Assert.Equal("spending", router.ActiveChainForTests[1].Match.Route.Key);
        }

        [WpfFact]
        public void Same_route_param_change_reuses_leaf_context()
        {
            // billing / plans/{userId:int} / plan/{planId:int} —— 三层链。
            var billing = Func("billing", "billing");
            var plans = Func("plans", "plans/{userId:int}");
            var plan = Func("plan", "plan/{planId:int}");
            plans.Children = new List<Route> { plan };
            billing.Children = new List<Route> { plans };
            var router = NewRouter("/billing", billing);

            router.Navigate("/billing/plans/1001/plan/123", null);
            var leafBefore   = router.ActiveChainForTests[2];
            var middleBefore = router.ActiveChainForTests[1];
            var rootBefore   = router.ActiveChainForTests[0];

            // 只有 planId 变化 —— 参数差异在叶子层，Route 本身相同。
            router.Navigate("/billing/plans/1001/plan/456", null);

            // 叶子 context 被复用（同一对象），Match 已替换为新参数。
            Assert.Same(leafBefore,   router.ActiveChainForTests[2]);
            Assert.Same(middleBefore, router.ActiveChainForTests[1]);
            Assert.Same(rootBefore,   router.ActiveChainForTests[0]);
            Assert.Equal(456, (int)router.ActiveChainForTests[2].Match.Parameters["planId"]);
        }

        [WpfFact]
        public void Different_route_replaces_and_disposes_old_tail()
        {
            var moderation = Func("moderation", "moderation");
            moderation.Children = new List<Route>
            {
                Func("blocked-users", "blocked-users"),
                Func("reports",       "reports"),
            };
            var router = NewRouter("/moderation", moderation);

            router.Navigate("/moderation/blocked-users", null);
            var blockedCtx = router.ActiveChainForTests[1];
            var rootCtx    = router.ActiveChainForTests[0];

            router.Navigate("/moderation/reports", null);

            // 旧叶子已 Dispose，新叶子是全新的 context。
            Assert.NotSame(blockedCtx, router.ActiveChainForTests[1]);
            Assert.Same(rootCtx, router.ActiveChainForTests[0]);
            Assert.Equal("reports", router.ActiveChainForTests[1].Match.Route.Key);
        }

        [WpfFact]
        public void Truncating_chain_disposes_old_tail()
        {
            var moderation = Func("moderation", "moderation");
            moderation.Children = new List<Route> { Func("blocked-users", "blocked-users") };
            var router = NewRouter("/moderation", moderation);

            router.Navigate("/moderation/blocked-users", null);
            var leaf = router.ActiveChainForTests[1];

            router.Navigate("/moderation", null);

            Assert.Single(router.ActiveChainForTests);
            // 复用的根此时不应再挂任何子 outlet。
            Assert.Null(router.ActiveChainForTests[0].OutletRouteContext);
        }

        [WpfFact]
        public void Same_path_navigated_twice_does_not_mutate_chain()
        {
            var route = Func("home", "home");
            var router = NewRouter("/home", route);

            router.Navigate("/home", null);
            var rootCtx = router.ActiveChainForTests[0];
            var rootMatch = rootCtx.Match;

            router.Navigate("/home", null);

            Assert.Same(rootCtx, router.ActiveChainForTests[0]);
            Assert.Equal(rootMatch, router.ActiveChainForTests[0].Match);
        }

        [WpfFact]
        public void IsMultiple_window_route_is_force_replace_anchor()
        {
            // IsMultiple=true 的窗体路由每次进入都重建 context —— 即使路径和参数完全相同，
            // 也按"每次新开一个窗体"的语义处理。
            var billing = Func("billing", "billing");
            billing.Children = new List<Route> { Win("plans", "plans/{userId:int}", multiple: true) };
            var router = NewRouter("/billing", billing);

            router.Navigate("/billing/plans/1001", null);
            var firstWindowCtx = router.ActiveChainForTests[1];

            router.Navigate("/billing/plans/1001", null);

            Assert.NotSame(firstWindowCtx, router.ActiveChainForTests[1]);
        }

        [WpfFact]
        public void No_match_returns_false_and_does_not_touch_chain()
        {
            var route = Func("home", "home");
            var router = NewRouter("/home", route);
            router.Navigate("/home", null);
            var snapshotCount = router.ActiveChainForTests.Count;

            var ok = router.Navigate("/this/does/not/exist", null);

            Assert.False(ok);
            Assert.Equal(snapshotCount, router.ActiveChainForTests.Count);
            Assert.Equal("/home", router.CurrentTarget);
        }

        [WpfFact]
        public void Refresh_disposes_chain_and_rebuilds()
        {
            var route = Func("home", "home");
            var router = NewRouter("/home", route);
            router.Navigate("/home", null);
            var firstRoot = router.ActiveChainForTests[0];

            router.Refresh();

            Assert.Single(router.ActiveChainForTests);
            Assert.NotSame(firstRoot, router.ActiveChainForTests[0]);
        }
    }
}
