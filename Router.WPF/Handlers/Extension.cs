using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Router.Wpf.Abstractions.Routing;
using Router.Wpf.Abstractions;
using Router.Wpf.Contexts;
using System.Windows;

namespace Router.Wpf.Handlers
{
    public static class HanlderExtension
    {
        public static IRouteContext CreateComponentContext(this IComponentHandler handler, RouteMatch match, IRouteContext? outletRouteContext)
        {
            return new FrameRouteContext(match, Router.CreateHost(), outletRouteContext);
        }
    }
}
