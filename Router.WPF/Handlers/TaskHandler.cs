using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.UI.Core.Abstractions.Routing;
using Unity.UI.Core.Abstractions;
using Unity.UI.WPF.Contexts;

namespace Unity.UI.WPF.Handlers
{
    public abstract class TaskHandler : IRouteHandler<bool>
    {
        public IRouteContext CreateContext(RouteMatch match, IRouteContext? outletRouteContext)
        {
            return new FrameRouteContext(match, null, outletRouteContext);
        }

        public abstract void Do(RouteMatch match);
        public bool GetResult(RouteMatch match)
        {
            Do(match);
            return true;
        }
    }

    public class ActionHandler : TaskHandler
    {
        public ActionHandler(Action<RouteMatch> action) : base()
        {
            Action = action;
        }
        public Action<RouteMatch> Action { get; set; }

        public override void Do(RouteMatch match)
        {
            Action(match);
        }
    }
}
