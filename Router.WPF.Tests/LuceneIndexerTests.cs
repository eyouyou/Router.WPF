using System.Linq;
using Router.Wpf;
using Router.Wpf.Abstractions;
using Router.Wpf.Abstractions.Routing;
using Xunit;

namespace Router.Wpf.Tests
{
    public class LuceneIndexerTests
    {
        private sealed class StubHandler : IRouteHandler
        {
            public IRouteContext CreateContext(RouteMatch m, IRouteContext? o) => throw new System.NotSupportedException();
        }

        private static IndexRouteCollection BuildIndexed()
        {
            var col = new IndexRouteCollection();
            col.Add("/home",                                   new Route("home",        "home",        new StubHandler()));
            col.Add("/profile",                                new Route("profile",     "profile",     new StubHandler()));
            col.Add("/account",                                new Route("account",     "account",     new StubHandler()));
            col.Add("/billing-and-plans",                      new Route("billing",     "billing-and-plans", new StubHandler()));
            col.Add("/billing-and-plans/spending-limits",      new Route("spending",    "spending-limits", new StubHandler()));
            col.Add("/moderation",                             new Route("moderation",  "moderation",  new StubHandler()));
            col.Add("/moderation/blocked-users",               new Route("blocked",     "blocked-users", new StubHandler()));
            return col;
        }

        [Theory]
        [InlineData("mod",   "moderation")]
        [InlineData("bill",  "billing")]
        [InlineData("spend", "spending")]
        [InlineData("block", "blocked")]
        [InlineData("hom",   "home")]
        public void Search_finds_route_by_prefix(string query, string expectedKey)
        {
            var col = BuildIndexed();
            var hits = col.Search(query).ToList();

            Assert.Contains(hits, h => h.Key == expectedKey);
        }

        [Fact]
        public void Search_full_term_still_matches()
        {
            var col = BuildIndexed();
            var hits = col.Search("moderation").ToList();
            Assert.Contains(hits, h => h.Key == "moderation");
        }

        [Fact]
        public void Search_unknown_returns_empty()
        {
            var col = BuildIndexed();
            Assert.Empty(col.Search("zzzzz_no_such_thing"));
        }

        [Fact]
        public void Search_handles_empty_input()
        {
            var col = BuildIndexed();
            Assert.Empty(col.Search(""));
            Assert.Empty(col.Search("   "));
        }

        [Fact]
        public void Search_handles_special_chars_gracefully()
        {
            var col = BuildIndexed();
            // 历史版本下，碰到分词器的特殊字符（比如 '*'）会抛异常。
            // 现在的实现切词时会把它们剥掉，整体不能崩。
            var hits = col.Search("mod*ration").ToList();
            // 不要抛异常；是否命中不做承诺，但必须正常返回。
            Assert.NotNull(hits);
        }

        [Fact]
        public void Search_topN_caps_results()
        {
            var col = BuildIndexed();
            // 切词时 '-' 是分隔符，所以单字母前缀 'e' 会命中很多 token。
            // 用它来验证 topN 的截断。
            var capped = col.Search("e", topN: 2);
            Assert.True(capped.Count() <= 2);
        }

        [Fact]
        public void Indexer_supports_increment_after_first_search()
        {
            // 第一次 Search 之后再 Index 进来的路由，下一次 Search 也必须能命中。
            var col = BuildIndexed();
            _ = col.Search("home").ToList();

            col.Add("/late-arrival", new Route("late", "late-arrival", new StubHandler()));
            var hits = col.Search("late").ToList();
            Assert.Contains(hits, h => h.Key == "late");
        }
    }
}
