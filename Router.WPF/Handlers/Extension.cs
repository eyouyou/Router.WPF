using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.UI.Core.Abstractions.Routing;
using Unity.UI.Core.Abstractions;
using Unity.UI.WPF.Contexts;
using System.Windows;

namespace Unity.UI.WPF.Handlers
{
    public static class HanlderExtension
    {
        public static IRouteContext CreateComponentContext(this IComponentHandler handler, RouteMatch match, IRouteContext? outletRouteContext)
        {
            var router = Application.Current.Router();
            return new FrameRouteContext(match, Router.CreateFrame(), outletRouteContext);
        }
    }
}
