using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Unity.UI.Core.Abstractions.Routing;
using Unity.UI.Core.Abstractions;

namespace Unity.UI.WPF.Handlers
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
