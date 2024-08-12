using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Unity.UI.Core.Abstractions.Routing;
using Unity.UI.Core.Abstractions;
using Unity.UI.WPF.Contexts;

namespace Unity.UI.WPF.Handlers
{
    public class WindowResult
    {
        public string Title { get; set; }
        public FrameworkElement Content { get; set; }

        public WindowResult(string title, FrameworkElement content)
        {
            Title = title;
            Content = content;
        }
    }

    public class WindowRefHandler : IComponentHandler
    {
        private readonly Func<RouteMatch, WindowResult> _lazyComponent;
        public WindowRefHandler(Func<RouteMatch, WindowResult> lazyComponent)
        {
            _lazyComponent = lazyComponent;
        }
        public IRouteContext CreateContext(RouteMatch match, IRouteContext? outletRouteContext)
        {
            return this.CreateComponentContext(match, outletRouteContext);
        }

        public FrameworkElement GetResult(RouteMatch match)
        {
            var component = _lazyComponent(match);
            return component.Content;
        }
    }

    public class WindowHandler : FuncRouteHanlder<WindowResult>
    {
        /// <summary>
        /// 是否允许多个
        /// 是否复用
        /// </summary>
        public bool IsMultiple { get; set; }
        /// <summary>
        /// 是否关联全局路由 
        /// 如果是 则路由切换的掉之后 自动关闭窗体
        /// 不然 则是独立窗体
        /// </summary>
        public bool IsRouteRef { get; set; }
        public WindowHandler(Func<RouteMatch, WindowResult> lazyComponent, bool multiple = false, bool refRoute = false) : base(lazyComponent)
        {
            IsRouteRef = refRoute;
            IsMultiple = multiple;
        }

        public override IRouteContext CreateContext(RouteMatch match, IRouteContext? outletRouteContext)
        {
            var router = Application.Current.Router();
            return new WindowRouteContext(match, Router.CreateFrame(), outletRouteContext);
        }

        public IComponentHandler CopyToComponent()
        {
            return new WindowRefHandler(_lazyComponent);
        }
    }
}
