using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Router.Wpf.Abstractions;
using Router.Wpf.Abstractions.Routing;

namespace Router.Wpf.Core
{
    public abstract class TypePathRouteCollection : RouteCollection
    {
        public TypePathRouteCollection()
        {
        }

        public override void Add(string pattern, Route route)
        {
            PathMatcher matcher = new TypePathParser(pattern);
            if (!Collections.TryGetValue(pattern, out var list))
            {
                list = new();
                Collections.Add(pattern, list);
            }

            list.Add(new RouteMatcher(pattern, route, matcher));
        }
    }
}
