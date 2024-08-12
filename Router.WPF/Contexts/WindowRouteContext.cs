using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using Unity.UI.Core.Abstractions.Routing;
using Unity.UI.Core.Abstractions;
using Unity.UI.WPF.Handlers;

namespace Unity.UI.WPF.Contexts
{
    public class WindowRouteContext : UserControl, IRouteContext
    {
        public Subject<IRouteContext> OnMatchChanged { get; } = new();

        public RouteMatch Match { get; set; }
        /// <summary>
        /// Outlet对应的上下文
        /// </summary>
        public IRouteContext? OutletRouteContext { get; set; }
        /// <summary>
        /// 给window使用的frame
        /// </summary>
        public Frame? Frame { get; set; }

        public WindowHandler Handler { get; set; }
        public WindowRouteContext(RouteMatch match, Frame? frame = null, IRouteContext? outletRouteContext = null)
        {
            if (match.Route.Handler is not WindowHandler handler)
            {
                throw new Exception("");
            }
            Match = match;
            Frame = frame;
            OutletRouteContext = outletRouteContext;
            Handler = handler;
            OnMatchChanged.Subscribe(OnChanged);
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
        /// <summary>
        /// 当前窗体
        /// 单窗体的时候会有
        /// </summary>
        public Window? CurrentWindow { get; set; }
        /// <summary>
        /// 通知windows关联上下文
        /// 跟随路由
        /// </summary>
        public List<FrameRouteContext> RefContexts { get; } = new();
        /// <summary>
        /// 卸载的时候关闭当前路由
        /// 跟随路由
        /// </summary>
        public List<Window> RefWindows { get; } = new();

        private void WindowRouteContext_Loaded(object sender, RoutedEventArgs e)
        {
            Load(Match, this);
        }

        private FrameRouteContext Load(RouteMatch match, IRouteContext context)
        {
            if (match.Route.Handler is not WindowHandler handler)
            {
                throw new Exception("");
            }
            // 如果是单个窗体的情况
            if (!handler.IsMultiple && CurrentWindow is Window window)
            {
                // TODO:
            }
            else
            {
                var result = handler.GetResult(match);
                window = new Window
                {
                    Title = result.Title,
                };
                if (handler.IsRouteRef)
                {
                    RefWindows.Add(window);
                }
                if (!handler.IsMultiple)
                {
                    CurrentWindow = window;
                }
                window.Closed += Window_Closed;
            }
            var windowContext = CreateWindowRouteContext(context);
            if (handler.IsRouteRef)
            {
                RefContexts.Add(windowContext);
            }
            window.Content = windowContext;
            window.Show();
            return windowContext;
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            if (sender is Window window)
            {
                if (!Handler.IsMultiple)
                {
                    RefWindows.Remove(window);
                    CurrentWindow = null;
                }

                if (window.Content is FrameRouteContext context)
                {
                    RefContexts.Remove(context);
                    context.RefContext = null;
                }
            }

        }

        private static FrameRouteContext CreateWindowRouteContext(IRouteContext context)
        {
            if (context is not WindowRouteContext windowRouteContext || windowRouteContext.Match.Route.Handler is not WindowHandler handler)
            {
                throw new Exception("");
            }
            var match = windowRouteContext.Match.CopyWith(windowRouteContext.Match.Route.CopyWith(handler.CopyToComponent()));
            var parentContext = windowRouteContext.FindVisualParentUntil<IRouteContext, Router>();
            return new FrameRouteContext(match, windowRouteContext.Frame, windowRouteContext.OutletRouteContext) { RefContext = parentContext };
        }

        private void OnChanged(IRouteContext context)
        {
            var refContext = Load(context.Match, context);
            RefContexts.ForEach(it => it.OnMatchChanged.OnNext(refContext));
        }
    }
}
