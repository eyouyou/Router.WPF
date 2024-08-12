using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.UI.Core.Abstractions.Routing;
using Unity.UI.Core.Abstractions;
using Unity.UI.Core;
using System.IO;
using Unity.UI.WPF;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using System.Windows;
using System.Xml.Linq;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using Pidgin;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.UI.WPF.Handlers;

namespace Unity.UI.WPF.Routers
{
    enum NotifyType
    {
        Self,
        Child
    }
    class GenericRouter : Router
    {
        private readonly List<Route> _routes;
        private readonly Dictionary<Route, IRouteContext> _contexts = new();
        public override RouteCollection RouteCollection { get; } = new IndexRouteCollection();
        readonly Frame frame;
        internal GenericRouter(string? target = null, params Route[] routes)
        {
            CurrentTarget = target ?? "/";
            _routes = new(routes);
            Loaded += GenericRouter_Loaded;
            frame = CreateFrame();
            Content = frame;
        }

        private static void FrameNavigated(List<(IRouteContext, NotifyType)> contexts)
        {
            try
            {
                foreach (var context in contexts)
                {
                    switch (context.Item2)
                    {
                        case NotifyType.Self:
                            context.Item1.OnMatchChanged.OnNext(context.Item1);
                            break;
                        case NotifyType.Child:
                            if (context.Item1 is IParentRouteContext parent)
                            {
                                parent.OnOutletChanged.OnNext(context.Item1);
                            }
                            else
                            {
                                throw new Exception("to notify rerender outlet, parent must to implement [`IParentRouteContext`]");
                            }
                            break;
                    }
                    //TODO: 是不是就刷一个顶级的就够了
                    break;
                }
            }
            finally
            {
            }
        }

        private void GenericRouter_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // 通过当前路径生成路由上下文
            var collection = RouteCollection;
            foreach (var item in _routes)
            {
                RecursiveGenerateRoute(item, "/", ref collection);
            }

            Navigate(CurrentTarget, null);
        }



        static void RecursiveGenerateRoute(Route route, string parentPath, ref RouteCollection collection)
        {
            var pathPattern = Path.Join(parentPath, route.Path);
            pathPattern = pathPattern.Replace("\\", "/");
            collection.Add(pathPattern, route);
            if (route.Children != null)
            {
                foreach (var item in route.Children)
                {
                    RecursiveGenerateRoute(item, pathPattern, ref collection);
                }
            }
        }

        protected override bool Navigate(string pathName, bool addHistory, object? extraData)
        {
            if (addHistory)
            {
                PushRecord(pathName);
            }

            var target = pathName;

            var args = new NavigationEventArgs(target, extraData);

            CurrentTarget = pathName;

            var collection = RouteCollection;

            var matches = collection.Match(target);

            //TODO: 需要排除的是 从上到下 IsMultiple true 到 IsMultiple false的场景 但是这种场景暂时不支持 或者可以通过另外的路由实现
            var index = matches.FindIndex(it => it.Route.Handler is WindowHandler handler && handler.IsMultiple);

            IRouteContext? outletContext = null;
            List<(IRouteContext, NotifyType)> contexts = new();
            var isUnLoaded = false;
            for (var i = matches.Count - 1; i >= 0; i--)
            {
                var isChanged = false;
                var match = matches.ElementAt(i);
                if (i + 1 >= matches.Count - index)
                {
                    outletContext = match.Route.Handler.CreateContext(match, outletContext);
                }
                else if (!_contexts.TryGetValue(match.Route, out var context))
                {
                    // 叶子没有outlet
                    outletContext = match.Route.Handler.CreateContext(match, outletContext);
                    _contexts[match.Route] = outletContext;
                }
                else
                {
                    context.OutletRouteContext = outletContext;
                    outletContext = context;

                    if (!outletContext.Match.Equals(match))
                    {
                        outletContext.Match = match;
                        isChanged = true;
                    }
                }
                // 如果未加载 只需要重新绘制父Outlet(需要通过父上下文去通知)
                if (outletContext is FrameworkElement fe && !fe.IsLoaded)
                {
                    isUnLoaded = true;
                }
                else if (isUnLoaded && contexts.Count
                    == 0)
                {
                    contexts.Insert(0, (outletContext, NotifyType.Child));
                }
                else if (isChanged)
                {
                    contexts.Insert(0, (outletContext, NotifyType.Self));
                }

            }

            // root如果未加载 则走navigate逻辑
            if (contexts.Count == 0)
            {
                return frame.Navigate(outletContext);
            }
            else
            {
                FrameNavigated(contexts);
            }

            NavigationRequested.OnNext(args);
            return true;
        }
    }
}
