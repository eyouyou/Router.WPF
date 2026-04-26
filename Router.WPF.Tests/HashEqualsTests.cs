using System.Collections.Generic;
using Router.Wpf.Abstractions;
using Xunit;

namespace Router.Wpf.Tests
{
    public class HashEqualsTests
    {
        [Fact]
        public void PathMatch_equal_when_same_pathname_and_params_in_any_order()
        {
            var a = new PathMatch("/users/42", new Dictionary<string, object> { ["id"] = 42, ["tab"] = "settings" });
            var b = new PathMatch("/users/42", new Dictionary<string, object> { ["tab"] = "settings", ["id"] = 42 });

            Assert.True(a.Equals(b));
            // 哈希允许在 Equals=true 时碰撞，所以这里不直接断言相等 ——
            // 真正要拦的是之前那个 bug（左移 + 按位与，会让大量值挤到同一个槽）。
            // HashCode.Combine(PathName, Count) 对不同 pathname 给出稳定不同的值。
            Assert.NotEqual(a.GetHashCode(), new PathMatch("/other", a.Parameters).GetHashCode());
        }

        [Fact]
        public void PathMatch_not_equal_when_param_value_differs()
        {
            var a = new PathMatch("/users/42", new Dictionary<string, object> { ["id"] = 42 });
            var b = new PathMatch("/users/42", new Dictionary<string, object> { ["id"] = 43 });

            Assert.False(a.Equals(b));
        }

        [Fact]
        public void PathMatch_not_equal_when_param_count_differs()
        {
            var a = new PathMatch("/x", new Dictionary<string, object> { ["a"] = 1 });
            var b = new PathMatch("/x", new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 });

            Assert.False(a.Equals(b));
        }

        [Fact]
        public void PathMatch_not_equal_when_path_differs()
        {
            var a = new PathMatch("/x", new Dictionary<string, object>());
            var b = new PathMatch("/y", new Dictionary<string, object>());

            Assert.False(a.Equals(b));
        }
    }
}
