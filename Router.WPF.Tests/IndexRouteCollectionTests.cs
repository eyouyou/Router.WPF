using System.Linq;
using Router.Wpf;
using Router.Wpf.Abstractions;
using Router.Wpf.Abstractions.Routing;
using Xunit;

namespace Router.Wpf.Tests
{
    public class IndexRouteCollectionTests
    {
        // 匹配过程不关心 handler，Match 只看 path pattern；这里用一个空实现占位。
        private sealed class StubHandler : IRouteHandler
        {
            public IRouteContext CreateContext(RouteMatch match, IRouteContext? outletRouteContext)
                => throw new System.NotSupportedException();
        }

        private static Route R(string key, string path) => new(key, path, new StubHandler());

        private static IndexRouteCollection BuildCollection()
        {
            var col = new IndexRouteCollection();
            // 仿照 GenericRouter.RecursiveGenerateRoute 的做法：传入完整的、带前导 '/' 的 pattern。
            col.Add("/billing-and-plans", R("billing", "billing-and-plans"));
            col.Add("/billing-and-plans/spending-limits", R("spending", "spending-limits"));
            col.Add("/billing-and-plans/plans-and-usage/{userId:int}", R("plans-and-usage", "plans-and-usage/{userId:int}"));
            col.Add("/moderation", R("moderation", "moderation"));
            col.Add("/moderation/blocked-users", R("blocked-users", "blocked-users"));
            col.Add("/moderation/blocked-users", new IndexRoute("blocked-users-index", new StubHandler()));
            col.Add("/moderation/blocked-users/{blockedUserId:string}", R("blocked-user", "{blockedUserId:string}"));
            return col;
        }

        [Fact]
        public void Match_walks_chain_for_nested_path()
        {
            var col = BuildCollection();
            var matches = col.Match("/billing-and-plans/spending-limits");

            // 两层链：billing 在前，spending-limits 在后。
            Assert.Equal(2, matches.Count);
            Assert.Equal("billing",  matches[0].Route.Key);
            Assert.Equal("spending", matches[1].Route.Key);
        }

        [Fact]
        public void Match_uses_IndexRoute_when_path_is_parent_only()
        {
            var col = BuildCollection();
            var matches = col.Match("/moderation/blocked-users");

            // moderation → blocked-users → blocked-users-index：
            // 末端用 IndexRoute 替代 blocked-users，因为它是该层级的默认子。
            Assert.Equal(3, matches.Count);
            Assert.Equal("moderation",          matches[0].Route.Key);
            Assert.Equal("blocked-users",       matches[1].Route.Key);
            Assert.Equal("blocked-users-index", matches[2].Route.Key);
        }

        [Fact]
        public void Match_picks_leaf_over_IndexRoute_when_deeper_segment_present()
        {
            var col = BuildCollection();
            var matches = col.Match("/moderation/blocked-users/alice");

            // 当能匹到更深的 leaf（{blockedUserId:string}）时，IndexRoute 应被它替换掉。
            Assert.Equal(3, matches.Count);
            Assert.Equal("moderation",    matches[0].Route.Key);
            Assert.Equal("blocked-users", matches[1].Route.Key);
            Assert.Equal("blocked-user",  matches[2].Route.Key);
            Assert.Equal("alice",         matches[2].Parameters["blockedUserId"]);
        }

        [Fact]
        public void Match_returns_empty_for_unknown_path()
        {
            var col = BuildCollection();
            Assert.Empty(col.Match("/this/does/not/exist"));
        }

        [Fact]
        public void Match_typed_param_converts_value()
        {
            var col = BuildCollection();
            var matches = col.Match("/billing-and-plans/plans-and-usage/1001");

            var leaf = matches.Last();
            Assert.Equal("plans-and-usage", leaf.Route.Key);
            Assert.IsType<int>(leaf.Parameters["userId"]);
            Assert.Equal(1001, (int)leaf.Parameters["userId"]);
        }
    }
}
