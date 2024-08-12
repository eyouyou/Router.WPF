using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Unity.UI.Core.Abstractions.Routing
{
    public class RouteMatcher
    {
        public RouteMatcher(string path, Route route, PathMatcher pathMatcher)
        {
            Path = path;
            Route = route;
            PathMatcher = pathMatcher;
        }
        public string Path { get; set; }
        public Route Route { get; set; }

        public PathMatcher PathMatcher { get; set; }
    }

    public class RouteSearchInfo
    {
        public string Key { get; set; }
        public string Path { get; set; }

        public RouteSearchInfo(string key, string path)
        {
            Key = key;
            Path = path;
        }
    }


    public abstract class RouteCollection
    {
        /// <summary>
        /// 完整的path 静态可达的路由
        /// </summary>
        public IEnumerable<RouteMatcher> CompleteMarchItems
        {
            get
            {
                return Collections.SelectMany(x =>
                {
                    var result = x.Value.Where(it => !it.PathMatcher.IsPartial);
                    if (result.Any(it => it.Route is IndexRoute))
                    {
                        return result.Where(it => it.Route is IndexRoute);
                    }

                    return result;
                });
            }
        }

        /// <summary>
        /// 需要支持查询路由的功能
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public abstract IEnumerable<RouteSearchInfo> Search(string pattern);
        /// <summary>
        /// pattern => route
        /// </summary>
        protected Dictionary<string, List<RouteMatcher>> Collections { get; set; } = new Dictionary<string, List<RouteMatcher>>();

        public abstract void Add(string pattern, Route route);

        /// <summary>
        /// 通过url 获得path的路由路径
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public List<RouteMatch> Match(string url)
        {
            var path = new List<RouteMatch>();
            foreach (var item in Collections)
            {
                foreach (var matcher in item.Value)
                {
                    var match = matcher.PathMatcher.Match(url);
                    if (match.Any())
                    {
                        // 可能存在[`IndexRoute`] 他们的路径和其父路径一致
                        // 所以 如果还有叶子节点能被匹配 则去掉该[`IndexRoute`]
                        if (path.Count > 0 && path[^1].Route is IndexRoute)
                        {
                            path.RemoveAt(path.Count - 1);
                        }
                        path.Add(match.Last().ToRouteMatch(matcher.Route));
                    }
                }
            }

            return path;
        }
    }
}
