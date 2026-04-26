using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Router.Wpf;
using Router.Wpf.Abstractions;

namespace Router.Wpf.Core
{
    public class TypePathParser : PathMatcher
    {
        /// <summary>
        /// pattern → 编译后的正则。键是和 pattern 同步生成的"规范化名字"
        /// （动态段写成 <c>{name:type}</c> 形式），方便后续支持多形态展开 ——
        /// 当前语法下每个 parser 实例只产出一个 regex。
        /// </summary>
        private readonly Dictionary<string, Regex> _regexs = new();

        /// <summary>
        /// 命名捕获组 → 动态 token 信息；在出参取值时用来做类型转换。
        /// </summary>
        private readonly Dictionary<string, DynamicToken> _dynamicTokens = new();

        public string Pattern { get; set; }

        public override bool IsPartial => _dynamicTokens.Count > 0;

        public TypePathParser(string pattern)
        {
            Pattern = pattern;

            // 1. 切词。手写状态机走一遍 pattern；之前是 Pidgin 解析器组合子做这件事，
            //    一并把 50KB 多的依赖去掉了。
            var segments = TokenizePattern(pattern);

            // 2. 把 segments 降到一份 regex + 一份规范化 pathName。
            var regexPattern = new StringBuilder();
            var pathName = new StringBuilder();

            for (int i = 0; i < segments.Count; i++)
            {
                pathName.Append('/');
                var token = segments[i];

                switch (token)
                {
                    case StringToken s:
                        regexPattern.Append('/');
                        regexPattern.Append(s.Value);
                        pathName.Append(s.Value);
                        break;

                    case MixedToken mixed:
                        // 保留祖传怪规则：单个动态段的前导 '/' 被折进动态正则里写成
                        // "(/?)"（即整段变为可选）。多 token 段则照常带字面 '/'。
                        var single = mixed.Tokens.Count == 1;
                        if (!single) regexPattern.Append('/');

                        foreach (var item in mixed.Tokens)
                        {
                            switch (item)
                            {
                                case StringToken str:
                                    regexPattern.Append("(?:").Append(str.Value).Append(')');
                                    pathName.Append(str.Value);
                                    break;
                                case DynamicToken dyn:
                                    if (_dynamicTokens.ContainsKey(dyn.Name))
                                        throw new InvalidOperationException(
                                            $"Path pattern '{pattern}' contains duplicate parameter name '{dyn.Name}'. " +
                                            "Each dynamic segment must use a unique name.");
                                    var quantifier = dyn.Type == typeof(Nullable) ? '*' : '+';
                                    regexPattern.Append(single ? "(/?)" : string.Empty);
                                    regexPattern.Append("(?<").Append(dyn.Name).Append(">[\\w]")
                                                .Append(quantifier).Append(')');
                                    pathName.Append('{').Append(dyn.Name).Append(dyn.TypeTag).Append('}');
                                    _dynamicTokens[dyn.Name] = dyn;
                                    break;
                            }
                        }
                        break;
                }
            }

            var regex = new Regex(regexPattern.ToString(),
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            _regexs.Add(pathName.ToString(), regex);
        }

        /// <summary>
        /// 把 pattern 按 '/' 切成段，每段产出一个 <see cref="IToken"/>。每段可能是：
        ///   * <see cref="StringToken"/>           —— 纯字面文本（如 "billing-and-plans"）
        ///   * 空 <see cref="StringToken"/>         —— 空段（pattern 以 '/' 结尾的情形）
        ///   * 含 1 个 <see cref="DynamicToken"/> 的 <see cref="MixedToken"/> —— 纯动态段（<c>{id:int}</c> / <c>{theme?}</c>）
        ///   * 含多个 token 的 <see cref="MixedToken"/> —— 字面与动态混合（<c>{prefix}-{id:int}</c>）
        /// 输入非法时抛 <see cref="ArgumentException"/>，消息中带上原始 pattern。
        /// </summary>
        private static IList<IToken> TokenizePattern(string pattern)
        {
            if (string.IsNullOrEmpty(pattern) || pattern[0] != '/')
                throw new ArgumentException(
                    $"Path pattern must start with '/' (got '{pattern}').", nameof(pattern));

            var segments = new List<IToken>();
            var i = 1;
            var len = pattern.Length;

            while (true)
            {
                var perSegment = new List<IToken>();
                var literal = new StringBuilder();

                while (i < len && pattern[i] != '/')
                {
                    if (pattern[i] == '{')
                    {
                        if (literal.Length > 0)
                        {
                            perSegment.Add(new StringToken(literal.ToString()));
                            literal.Clear();
                        }
                        var close = pattern.IndexOf('}', i + 1);
                        if (close < 0)
                            throw new ArgumentException(
                                $"Unclosed '{{' in path pattern '{pattern}' at position {i}.", nameof(pattern));

                        var inner = pattern.Substring(i + 1, close - i - 1);
                        var sepIdx = IndexOfTypeSeparator(inner);
                        if (sepIdx < 0)
                            throw new ArgumentException(
                                $"Dynamic segment '{{{inner}}}' in pattern '{pattern}' is missing a type tag " +
                                "(use '{name:int}', '{name:string}', '{name?}', etc.).", nameof(pattern));

                        var name = inner.Substring(0, sepIdx);
                        var tag  = inner.Substring(sepIdx);
                        perSegment.Add(new DynamicToken(name, tag));
                        i = close + 1;
                    }
                    else
                    {
                        literal.Append(pattern[i]);
                        i++;
                    }
                }

                if (literal.Length > 0)
                    perSegment.Add(new StringToken(literal.ToString()));

                // 这里的合并规则与原 Pidgin 文法外层 Select 完全一致：
                if (perSegment.Count == 0)
                    segments.Add(new StringToken(string.Empty));
                else if (perSegment.Count == 1 && perSegment[0] is StringToken solo)
                    segments.Add(solo);
                else
                    segments.Add(new MixedToken(perSegment));

                if (i >= len) break;
                // 此时 pattern[i] == '/'
                i++;
            }

            return segments;
        }

        private static int IndexOfTypeSeparator(string inner)
        {
            for (int i = 0; i < inner.Length; i++)
            {
                if (inner[i] == ':' || inner[i] == '?') return i;
            }
            return -1;
        }

        public override IList<PathMatch> Match(string path, string? currentPath = null)
        {
            if (currentPath != null)
            {
                // URL 式拼接：纯字符串 + 折叠 '.' / '..'，
                // 不引入任何文件系统语义（不会出现盘符、反斜杠等）。
                path = PathUtil.Combine(currentPath, path);
            }

            var pathMatches = new List<PathMatch>();

            foreach (var regex in _regexs)
            {
                var parameters = new Dictionary<string, object>();
                var matches = regex.Value.Match(path);

                if (!matches.Success) continue;

                foreach (var match in matches.Groups)
                {
                    if (match is Group group && _dynamicTokens.TryGetValue(group.Name, out var dynamicToken))
                    {
                        parameters.Add(group.Name, dynamicToken.GetValue(group.Value));
                    }
                }

                pathMatches.Add(new PathMatch(matches.Groups[0].Value, parameters));
            }

            return pathMatches;
        }
    }
}
