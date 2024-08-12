using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Unity.UI.Core.Abstractions.Routing
{
    /// <summary>
    /// 包含各种各样的路由形式 比如弹窗以及内嵌
    /// </summary>
    public interface IRouteHandler
    {
        IRouteContext CreateContext(RouteMatch match, IRouteContext? outletRouteContext);
    }

    public interface IRouteHandler<T> : IRouteHandler
    {
        T GetResult(RouteMatch match);
    }

    public abstract class FuncRouteHanlder<T> : IRouteHandler<T>
    {
        protected readonly Func<RouteMatch, T> _lazyComponent;

        protected FuncRouteHanlder(Func<RouteMatch, T> component)
        {
            _lazyComponent = component;
        }

        public abstract IRouteContext CreateContext(RouteMatch match, IRouteContext? outletRouteContext);

        public T GetResult(RouteMatch match)
        {
            return _lazyComponent.Invoke(match);
        }
    }
}
