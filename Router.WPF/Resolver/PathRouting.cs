using Pidgin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Unity.UI.Core.Abstractions;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;

namespace Unity.UI.Core
{

    public class TypePathParser : PathMatcher
    {
        private static readonly Parser<char, IToken> Content =
            from name in Token(c => c != '?' && c != ':').ManyString().Select(it => it)
            from type in Token(c => c != '}').ManyString().Labelled("parameter type")
            select new DynamicToken(name, type) as IToken;

        private static readonly Parser<char, IToken> DynamicSegment =
            from open in Char('{')
            from content in Content
            from close in Char('}')
            select content;

        private static readonly Parser<char, string> StaticSegment =
            Token(c => c != '{' && c != '/').AtLeastOnceString();

        private static readonly Parser<char, IEnumerable<IToken>> TokenParser =
            DynamicSegment
            .Or(StaticSegment.Select(x => new StringToken(x) as IToken)).Many();

        private static readonly Parser<char, IEnumerable<IToken>> PathParser =
            from leadingSlash in Char('/')
            from segments in TokenParser.Separated(Char('/'))
            select segments.Select(it =>
            {
                var list = it.ToList();
                if (list.Count > 1 || list.Any(it => it is DynamicToken))
                {
                    return new MixedToken(list);
                }
                if (list.Count == 0)
                {
                    return new StringToken(string.Empty);
                }
                return list[0];
            });

        /// <summary>
        /// path name => regex
        /// </summary>
        private readonly Dictionary<string, Regex> _regexs = new();
        /// <summary>
        /// group name => dynamic token
        /// </summary>
        private readonly Dictionary<string, DynamicToken> _dynamicTokens = new();




        public string Pattern { get; set; }

        public override bool IsPartial => _dynamicTokens.Count > 0;

        public TypePathParser(string pattern)
        {
            var result = PathParser.Parse(pattern);

            if (!result.Success)
            {
                throw new Exception($"pattern resolve error: {result.Error?.Message}");
            }

            StringBuilder regexPattern = new();
            StringBuilder pathName = new();
            var index = 0;
            var total = result.Value.Count();
            foreach (var token in result.Value)
            {
                pathName.Append('/');

                index++;
                switch (token)
                {
                    case StringToken stringToken:
                        regexPattern.Append('/');

                        regexPattern.Append($"{stringToken.Value}");
                        pathName.Append($"{stringToken.Value}");
                        break;
                    case MixedToken mixedToken:
                        var nullable = mixedToken.Tokens.Count == 1;
                        if (!nullable)
                        {
                            regexPattern.Append('/');
                        }

                        foreach (var item in mixedToken.Tokens)
                        {
                            switch (item)
                            {
                                case StringToken stringToken:
                                    regexPattern.Append($"(?:{stringToken.Value})");
                                    pathName.Append(stringToken.Value);
                                    break;
                                case DynamicToken dynamicToken:
                                    regexPattern.Append(@$"{(nullable ? "(/?)" : "")}(?<{dynamicToken.Name}>[\w]{(dynamicToken.Type == typeof(Nullable) ? '*' : '+')})");
                                    pathName.Append($"{{{dynamicToken.Name}{dynamicToken.TypeTag}}}");
                                    _dynamicTokens.Add(dynamicToken.Name, dynamicToken);
                                    break;
                            }
                        }
                        break;
                }

                if (index == total)
                {
                    var regex = new Regex(regexPattern.ToString(), RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                    var pathNameStr = pathName.ToString();
                    _regexs.Add(pathNameStr, regex);
                }
            }


            Pattern = pattern;
        }
        static string ConvertToLinuxPath(string windowsPath)
        {
            // Replace backslashes with forward slashes
            string linuxPath = windowsPath.Replace('\\', '/');

            // Remove the drive letter and colon (e.g., "C:")
            if (Path.IsPathRooted(linuxPath))
            {
                int colonIndex = linuxPath.IndexOf(':');
                if (colonIndex >= 0)
                {
                    linuxPath = linuxPath.Substring(colonIndex + 1);
                }
            }

            return linuxPath;
        }
        public override IList<PathMatch> Match(string path, string? currentPath = null)
        {
            if (currentPath != null)
            {
                string combinedPath = Path.Combine(currentPath, path);

                path = ConvertToLinuxPath(Path.GetFullPath(combinedPath));
            }

            var pathMatches = new List<PathMatch>();

            foreach (var regex in _regexs)
            {
                Dictionary<string, object> parameters = new();
                var matches = regex.Value.Match(path);

                if (!matches.Success)
                {
                    continue;
                }
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
