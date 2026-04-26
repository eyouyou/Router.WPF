using Router.Wpf;
using Xunit;

namespace Router.Wpf.Tests
{
    public class PathUtilTests
    {
        [Theory]
        [InlineData("/",          "foo",      "/foo")]
        [InlineData("/foo",       "bar",      "/foo/bar")]
        [InlineData("/foo/",      "bar",      "/foo/bar")]
        [InlineData("/foo",       "/bar",     "/foo/bar")]
        [InlineData("/foo/",      "/bar",     "/foo/bar")]
        [InlineData("/foo/bar",   "../baz",   "/foo/baz")]
        [InlineData("/foo/bar",   "./baz",    "/foo/bar/baz")]
        [InlineData("/foo/bar",   "../../baz","/baz")]
        [InlineData("/",          "../foo",   "/foo")] // 不能越过根
        [InlineData("/foo",       "",         "/foo")]
        [InlineData("/a//b",      "c",        "/a/b/c")]
        public void Combine_joins_and_collapses(string parent, string child, string expected)
        {
            Assert.Equal(expected, PathUtil.Combine(parent, child));
        }

        [Theory]
        [InlineData("/",            "/")]
        [InlineData("/foo",         "/foo")]
        [InlineData("/foo/",        "/foo")]
        [InlineData("/foo//bar",    "/foo/bar")]
        [InlineData("/foo/./bar",   "/foo/bar")]
        [InlineData("/foo/bar/..",  "/foo")]
        [InlineData("/../foo",      "/foo")]
        [InlineData("",             "/")]
        public void Normalize_collapses_segments(string input, string expected)
        {
            Assert.Equal(expected, PathUtil.Normalize(input));
        }
    }
}
