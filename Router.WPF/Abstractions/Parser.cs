using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.UI.Core.Abstractions.Routing;

namespace Unity.UI.Core.Abstractions
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

        public override bool Equals(object obj)
        {
            if (obj is PathMatch pathMatch)
            {
                return PathName == pathMatch.PathName && Parameters.SequenceEqual(pathMatch.Parameters);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return PathName.GetHashCode() << 16 & Parameters.GetHashCode();
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

        public override bool Equals(object obj)
        {
            if (obj is RouteMatch routeMatch)
            {
                return Route.Equals(routeMatch.Route) && base.Equals(routeMatch);
            }

            return false;
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
            return base.GetHashCode() << 12 & Route.GetHashCode();
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
