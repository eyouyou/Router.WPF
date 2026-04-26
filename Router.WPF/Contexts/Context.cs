using System;
using System.Collections.Generic;
using System.Linq;

using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using Router.Wpf.Abstractions.Routing;
using Router.Wpf.Abstractions;
using Router.Wpf.Handlers;

namespace Router.Wpf.Contexts
{
    public class FrameRouteContext : UserControl, IParentRouteContext
    {
        public Notifier<IRouteContext> OnMatchChanged { get; } = new();
        public Notifier<IRouteContext> OnOutletChanged { get; } = new();

        public RouteMatch Match { get; set; }
        /// <summary>
        /// Outlet对应的上下文
        /// </summary>
        public IRouteContext? OutletRouteContext { get; set; }
        /// <summary>
        /// 匹配到的元素的宿主容器。当 context 包裹的是非渲染型 handler（比如未来
        /// 的 TaskHandler）时可能为 null。最初是 <c>Frame</c>，现在改用普通
        /// <see cref="ContentControl"/>，目的是避免 Frame 自带的 journal。
        /// </summary>
        public ContentControl? Host { get; set; }
        public IRouteContext? RefContext { get; set; }

        public FrameRouteContext(RouteMatch match, ContentControl? host = null, IRouteContext? outletRouteContext = null)
        {
            Match = match;
            OutletRouteContext = outletRouteContext;
            Loaded += RouteContext_Loaded;
            Unloaded += RouteContext_Unloaded;
            OnMatchChanged.Subscribe(OnChanged);
            Host = host;
            Content = Host;
        }


        private void RouteContext_Unloaded(object sender, RoutedEventArgs e)
        {
            // 提前断掉 host 的子内容，防止它带着旧的元素再次进入视觉树时
            // 又触发一次 Loaded（context 复用、参数变化的场景下尤其容易踩到）。
            if (Host != null)
            {
                Host.Content = null;
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
                throw new InvalidOperationException(
                    $"FrameRouteContext expected a component handler on route '{match.Route.Key}', " +
                    $"got {match.Route.Handler?.GetType().Name ?? "null"}.");
            }

            var content = handler.GetResult(match);
            if (content is not null)
            {
                Load(content);
            }
        }

        public void Load(FrameworkElement element)
        {
            if (Host != null) Host.Content = element;
        }

        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 释放所有订阅者并标记 Notifier 终结。Dispose 之后再 OnNext 进来，
            // Notifier 会安静地丢弃事件（不会派发也不会抛异常）。
            OnMatchChanged.OnCompleted();
            OnMatchChanged.Dispose();
            OnOutletChanged.OnCompleted();
            OnOutletChanged.Dispose();

            // 切断所有视觉树引用，让 GC 能尽快回收匹配到的元素。
            if (Host != null) Host.Content = null;
            Content = null;
            OutletRouteContext = null;
            RefContext = null;
        }
    }
}
