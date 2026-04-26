using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Router.Wpf.Abstractions.Routing;
using Router.Wpf.Abstractions;

namespace Router.Wpf.Handlers
{
    public interface IComponentHandler : IRouteHandler<FrameworkElement>
    {
    }

    public class FuncComponentHandler : FuncRouteHanlder<FrameworkElement>, IComponentHandler
    {
        public FuncComponentHandler(Func<RouteMatch, FrameworkElement> lazyComponent) : base(lazyComponent)
        {
        }

        public override IRouteContext CreateContext(RouteMatch match, IRouteContext? outletRouteContext)
        {
            return this.CreateComponentContext(match, outletRouteContext);
        }
    }


}
