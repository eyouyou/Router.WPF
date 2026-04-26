using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Router.Wpf.Abstractions.Routing;

namespace Router.Wpf.Abstractions
{
    public class PathMatch
    {
        public PathMatch(string pathName) : this(pathName, new Dictionary<string, object>())
        {
        }

        public PathMatch(string pathName, Dictionary<string, object> parameters)
        {
            PathName = pathName;
            Parameters = parameters;
        }

        public string PathName { get; set; }
        public Dictionary<string, object> Parameters { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is not PathMatch other) return false;
            if (PathName != other.PathName) return false;
            if (Parameters.Count != other.Parameters.Count) return false;
            foreach (var kvp in Parameters)
            {
                if (!other.Parameters.TryGetValue(kvp.Key, out var v)) return false;
                if (!Equals(kvp.Value, v)) return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PathName, Parameters.Count);
        }
    }

    public static class RouteMatchExtensions
    {
        public static RouteMatch ToRouteMatch(this PathMatch pathMatch, Route route)
        {
            return new RouteMatch(route, pathMatch);
        }
    }
    public class RouteMatch : PathMatch
    {
        internal RouteMatch(Route route, PathMatch pathMatch) : base(pathMatch.PathName, pathMatch.Parameters)
        {
            Route = route;
        }
        public Route Route { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is not RouteMatch routeMatch) return false;
            return Route.Equals(routeMatch.Route) && base.Equals(routeMatch);
        }

        public RouteMatch CopyWith(Route? route = null)
        {
            RouteMatch clone = (RouteMatch)MemberwiseClone();
            if (route != null)
            {
                clone.Route = route;
            }
            return clone;
        }


        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), Route);
        }
    }

    public abstract class PathMatcher
    {
        /// <summary>
        /// 如果存在部分动态 则不能对其可见
        /// 搜索以及全量查询都会被忽略
        /// </summary>
        public abstract bool IsPartial { get; }
        public abstract IList<PathMatch> Match(string path, string? currentPath = null);
    }
}
