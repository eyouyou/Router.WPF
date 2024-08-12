using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Text;

namespace Unity.UI.Core.Abstractions.Routing
{
    public interface IRouteContext
    {
        /// <summary>
        /// 通知自己变化
        /// </summary>
        Subject<IRouteContext> OnMatchChanged { get; }

        RouteMatch Match { get; set; }

        IRouteContext? OutletRouteContext { get; set; }
    }

    public interface IParentRouteContext : IRouteContext
    {
        /// <summary>
        /// 通知孩子变化
        /// </summary>
        Subject<IRouteContext> OnOutletChanged { get; }

        /// <summary>
        /// 关联上下文
        /// </summary>
        public IRouteContext? RefContext { get; set; }

    }
}
