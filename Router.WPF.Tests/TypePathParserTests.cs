using System;
using Router.Wpf.Core;
using Xunit;

namespace Router.Wpf.Tests
{
    public class TypePathParserTests
    {
        [Fact]
        public void Static_path_matches_exact()
        {
            var p = new TypePathParser("/profile");
            var matches = p.Match("/profile");
            Assert.Single(matches);
            Assert.Empty(matches[0].Parameters);
        }

        [Fact]
        public void Static_path_misses_when_different()
        {
            var p = new TypePathParser("/profile");
            Assert.Empty(p.Match("/account"));
        }

        [Fact]
        public void Int_param_is_typed()
        {
            var p = new TypePathParser("/users/{id:int}");
            var matches = p.Match("/users/42");
            Assert.Single(matches);
            Assert.True(matches[0].Parameters.TryGetValue("id", out var v));
            Assert.IsType<int>(v);
            Assert.Equal(42, (int)v!);
        }

        [Fact]
        public void String_param_keeps_string()
        {
            var p = new TypePathParser("/users/{name:string}");
            var matches = p.Match("/users/alice");
            Assert.Single(matches);
            Assert.Equal("alice", matches[0].Parameters["name"]);
        }

        [Fact]
        public void Nullable_param_matches_with_or_without_segment()
        {
            var p = new TypePathParser("/preferences/{theme?}");

            var withSeg = p.Match("/preferences/dark");
            Assert.Single(withSeg);
            Assert.Equal("dark", withSeg[0].Parameters["theme"]);

            var withoutSeg = p.Match("/preferences");
            Assert.Single(withoutSeg);
            Assert.True(string.IsNullOrEmpty(withoutSeg[0].Parameters["theme"]?.ToString()));
        }

        [Fact]
        public void Multiple_dynamic_segments()
        {
            var p = new TypePathParser("/billing/users/{userId:int}/plan/{planId:int}");
            var matches = p.Match("/billing/users/1001/plan/42");
            Assert.Single(matches);
            Assert.Equal(1001, matches[0].Parameters["userId"]);
            Assert.Equal(42,   matches[0].Parameters["planId"]);
        }

        [Fact]
        public void Duplicate_param_name_throws_descriptive()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => new TypePathParser("/a/{id:int}/b/{id:int}"));
            Assert.Contains("duplicate parameter name", ex.Message);
            Assert.Contains("id", ex.Message);
        }

        [Fact]
        public void Unknown_type_tag_throws_arg_exception()
        {
            // 类型 tag 必须是已知集合里的一员；像 ":number" 这种笔误应当显式抛错。
            Assert.Throws<ArgumentException>(() => new TypePathParser("/users/{id:number}"));
        }

        [Fact]
        public void Bad_pattern_throws_arg_exception()
        {
            // 缺前导 '/' —— pattern 解析器不接受这种输入。
            Assert.Throws<ArgumentException>(() => new TypePathParser("users/{id:int}"));
        }
    }
}
