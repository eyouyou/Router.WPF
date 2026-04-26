using System;
using System.Collections.Generic;
using System.Linq;

using System.Windows;
using System.Windows.Controls;
using Router.Wpf.Abstractions;
using Router.Wpf.Abstractions.Routing;
using Router.Wpf.Handlers;

namespace Router.Wpf.Contexts
{
    public class WindowRouteContext : UserControl, IParentRouteContext
    {
        public Notifier<IRouteContext> OnMatchChanged { get; } = new();

        /// <summary>
        /// outlet 刷新信号。WindowRouteContext 自身不直接托管 Outlet —— 匹配到的视图
        /// 是通过一个内部的 <see cref="FrameRouteContext"/> 装在弹出 Window 里，
        /// Window 内部那个 Outlet 才是真正绑定订阅的对象（它通过
        /// <c>FindVisualParentUntil</c> 找到内部 context）。所以链上 router 给我们发
        /// OnOutletChanged 时，我们必须把信号转发给内部 context，它的 Subject 才能
        /// 让 Window 内的 Outlet 感知到新的子链。OutletRouteContext 的 setter 也会
        /// 同步把值写到内部 context 上。
        /// </summary>
        public Notifier<IRouteContext> OnOutletChanged { get; } = new();

        public RouteMatch Match { get; set; }
        public IRouteContext? RefContext { get; set; }

        private IRouteContext? _outletRouteContext;
        public IRouteContext? OutletRouteContext
        {
            get => _outletRouteContext;
            set
            {
                _outletRouteContext = value;
                if (_innerContext != null)
                    _innerContext.OutletRouteContext = value;
            }
        }

        /// <summary>
        /// 弹出 Window 内部那个 FrameRouteContext 用的宿主容器。
        /// 同样从 <c>Frame</c> 改为 <see cref="ContentControl"/>，原因见
        /// <see cref="Router.CreateHost"/>。
        /// </summary>
        public ContentControl? Host { get; set; }

        public WindowHandler Handler { get; set; }

        /// <summary>
        /// 当前挂在弹出 Window 里的内部 FrameRouteContext。我们跟踪它是为了：
        /// 一是把自己的 OutletRouteContext 同步过去，二是把 OnOutletChanged 的
        /// 信号代理给它。
        /// </summary>
        private FrameRouteContext? _innerContext;

        public WindowRouteContext(RouteMatch match, ContentControl? host = null, IRouteContext? outletRouteContext = null)
        {
            if (match.Route.Handler is not WindowHandler handler)
            {
                throw new InvalidOperationException(
                    $"WindowRouteContext requires a WindowHandler on route '{match.Route.Key}', " +
                    $"got {match.Route.Handler?.GetType().Name ?? "null"}.");
            }
            Match = match;
            Host = host;
            _outletRouteContext = outletRouteContext;
            Handler = handler;
            OnMatchChanged.Subscribe(OnChanged);
            OnOutletChanged.Subscribe(OnOutletForward);
            Loaded += WindowRouteContext_Loaded;
            Unloaded += WindowRouteContext_Unloaded;
        }

        private void WindowRouteContext_Unloaded(object sender, RoutedEventArgs e)
        {
            foreach (var window in RefWindows.ToList())
            {
                window.Close();
            }
        }

        public Window? CurrentWindow { get; set; }

        /// <summary>
        /// IsRouteRef=true 时每次 Load 创建出的内部 FrameRouteContext 集合，
        /// 用于在 context Dispose 时统一释放。
        /// </summary>
        public List<FrameRouteContext> RefContexts { get; } = new();

        /// <summary>
        /// IsRouteRef=true 时跟踪开出来的 Window 集合，context Dispose 时一起关闭。
        /// </summary>
        public List<Window> RefWindows { get; } = new();

        private void WindowRouteContext_Loaded(object sender, RoutedEventArgs e)
        {
            Load(Match, this);
        }

        /// <summary>
        /// outlet 刷新转发：链上 router 给我们发了 OnOutletChanged，但弹出 Window 里
        /// 真正的 Outlet 是绑在内部 FrameRouteContext 上的，所以信号要再转一手。
        /// </summary>
        private void OnOutletForward(IRouteContext _)
        {
            _innerContext?.OnOutletChanged.OnNext(_innerContext);
        }

        private FrameRouteContext Load(RouteMatch match, IRouteContext context)
        {
            if (match.Route.Handler is not WindowHandler handler)
            {
                throw new InvalidOperationException(
                    $"WindowRouteContext.Load expected a WindowHandler on route '{match.Route.Key}', " +
                    $"got {match.Route.Handler?.GetType().Name ?? "null"}.");
            }

            Window window;
            if (!handler.IsMultiple && CurrentWindow != null)
            {
                // 单窗体模式（IsMultiple=false）：复用已有 Window，让窗体的位置 /
                // 焦点 / 大小都跨路由切换保留下来。
                window = CurrentWindow;
                var result = handler.GetResult(match);
                if (window.Title != result.Title) window.Title = result.Title;
            }
            else
            {
                var result = handler.GetResult(match);
                window = new Window { Title = result.Title };
                if (handler.IsRouteRef) RefWindows.Add(window);
                if (!handler.IsMultiple) CurrentWindow = window;
                window.Closed += Window_Closed;
            }

            var newInner = CreateWindowRouteContext(context);

            var oldInner = window.Content as FrameRouteContext;
            window.Content = newInner;
            if (oldInner != null && !ReferenceEquals(oldInner, newInner))
            {
                if (handler.IsRouteRef) RefContexts.Remove(oldInner);
                oldInner.Dispose();
            }

            if (handler.IsRouteRef) RefContexts.Add(newInner);
            _innerContext = newInner;

            if (!window.IsVisible) window.Show();
            return newInner;
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            if (sender is not Window window) return;

            // 无论 IsMultiple 真假，先把这扇 Window 从我们持有的引用集合里清掉。
            // 此前的实现只在 !IsMultiple 时清，导致 IsMultiple=true 的窗体永远泄漏。
            RefWindows.Remove(window);
            if (!Handler.IsMultiple && CurrentWindow == window)
                CurrentWindow = null;

            if (window.Content is FrameRouteContext context)
            {
                RefContexts.Remove(context);
                context.RefContext = null;
                if (ReferenceEquals(_innerContext, context)) _innerContext = null;
                // 这扇 Window 本来持有 inner context；窗体关闭后 inner 也没有去处了，
                // 直接 Dispose 把它的 Subject 终结，比放任它等 GC 更利落。
                context.Dispose();
            }
        }

        private static FrameRouteContext CreateWindowRouteContext(IRouteContext context)
        {
            if (context is not WindowRouteContext windowRouteContext ||
                windowRouteContext.Match.Route.Handler is not WindowHandler handler)
            {
                throw new InvalidOperationException(
                    "CreateWindowRouteContext expected a WindowRouteContext with a WindowHandler-backed route, " +
                    $"got context={context?.GetType().Name ?? "null"}.");
            }
            var match = windowRouteContext.Match.CopyWith(windowRouteContext.Match.Route.CopyWith(handler.CopyToComponent()));
            var parentContext = windowRouteContext.FindVisualParentUntil<IRouteContext, Router>();
            return new FrameRouteContext(match, windowRouteContext.Host, windowRouteContext.OutletRouteContext) { RefContext = parentContext };
        }

        private void OnChanged(IRouteContext context)
        {
            var refContext = Load(context.Match, context);
            RefContexts.ForEach(it => it.OnMatchChanged.OnNext(refContext));
        }

        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // IsRouteRef = true  -> 窗体的生命周期跟随路由，关闭它。
            // IsRouteRef = false -> 独立生命周期，让窗体自己活着；
            //                       由用户或宿主应用决定何时关闭。我们仍然清掉持有引用，
            //                       避免被这层 context 钉住不能 GC。
            if (Handler.IsRouteRef)
            {
                foreach (var window in RefWindows.ToList()) window.Close();
                CurrentWindow?.Close();
            }
            RefWindows.Clear();

            foreach (var ctx in RefContexts.ToList()) ctx.Dispose();
            RefContexts.Clear();
            _innerContext = null;

            OnMatchChanged.OnCompleted();
            OnMatchChanged.Dispose();
            OnOutletChanged.OnCompleted();
            OnOutletChanged.Dispose();

            _outletRouteContext = null;
            Content = null;
        }
    }
}
