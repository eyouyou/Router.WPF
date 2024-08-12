using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using Unity.UI.Core.Abstractions.Routing;
using Unity.UI.Core.Abstractions;
using Unity.UI.WPF.Handlers;

namespace Unity.UI.WPF.Contexts
{
    public class FrameRouteContext : UserControl, IParentRouteContext
    {
        public Subject<IRouteContext> OnMatchChanged { get; } = new();
        public Subject<IRouteContext> OnOutletChanged { get; } = new();

        public RouteMatch Match { get; set; }
        /// <summary>
        /// Outlet对应的上下文
        /// </summary>
        public IRouteContext? OutletRouteContext { get; set; }
        /// <summary>
        /// 保存真正的outlet
        /// 自己的frame 可能为空 因为
        /// </summary>
        public Frame? Frame { get; set; }
        public IRouteContext? RefContext { get; set; }

        public FrameRouteContext(RouteMatch match, Frame? frame = null, IRouteContext? outletRouteContext = null)
        {
            Match = match;
            OutletRouteContext = outletRouteContext;
            Loaded += RouteContext_Loaded;
            Unloaded += RouteContext_Unloaded;
            OnMatchChanged.Subscribe(OnChanged);
            Frame = frame;
            Content = Frame;
        }


        private void RouteContext_Unloaded(object sender, RoutedEventArgs e)
        {
            // 提前断开frame和孩子的绑定防止多次触发卸载和加载的情况
            // 因为frame设置之后 会统一触发加载 而当时发生的加载 非当前frame的实体
            if (Frame != null)
            {
                Frame.Content = null;
            }
        }

        private void RouteContext_Loaded(object sender, RoutedEventArgs e)
        {
            OnChanged(this);
        }

        private void OnChanged(IRouteContext context)
        {
            var match = context.Match;

            if (match.Route.Handler is not IComponentHandler handler)
            {
                throw new Exception("");
            }

            var content = handler.GetResult(match);
            if (content is not null)
            {
                Load(content);
            }
        }

        // 加载
        public void Load(FrameworkElement element)
        {
            Frame?.Navigate(element);
        }
    }
}
