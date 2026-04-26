using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Router.Wpf.Abstractions;
using Router.Wpf.Abstractions.Routing;

namespace Router.Wpf.Routers
{
    public static class Extension
    {
        public static List<RouteMatch> GetRouteMatches(this FrameworkElement element)
        {
            var list = new List<RouteMatch>();

            FrameworkElement? routeContext = element;

            do
            {
                var context = routeContext?.FindVisualParentUntil<IParentRouteContext, Router>();
                if (context is not null)
                {
                    list.Insert(0, context.Match);
                }
                else
                {
                    if (routeContext is IParentRouteContext parent && parent.RefContext is FrameworkElement parentFe)
                    {
                        list.Insert(0, parent.RefContext.Match);
                        routeContext = parentFe;
                        continue;
                    }
                    routeContext = null;
                }

                if (context is FrameworkElement fe)
                {
                    routeContext = fe;
                }
            }
            while (routeContext != null);


            return list;
        }
    }
}
