using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Unity.UI.Core;
using Unity.UI.Core.Abstractions;
using Unity.UI.Core.Abstractions.Routing;
using Unity.UI.WPF.Routers;

namespace Unity.UI.WPF
{
    /// 包含uri的解析逻辑，所有路由信息
    /// </summary>
    public abstract partial class Router : UserControl, IRouter
    {

        internal Router()
        {
        }

        internal static Frame CreateFrame()
        {
            return new() { NavigationUIVisibility = System.Windows.Navigation.NavigationUIVisibility.Hidden };
        }

        public string CurrentTarget { get; internal set; } = string.Empty;

        protected List<string> _pathRecord = new();

        protected int _pathIndex = -1;
        public IEnumerable<string> PathRecord => _pathRecord;

        public abstract RouteCollection RouteCollection { get; }

        public Subject<NavigationEventArgs> NavigationRequested { get; } = new();

        public IEnumerable<string> BackStack => _pathRecord.Take(_pathIndex - 1);

        public IEnumerable<string> ForwardStack => _pathRecord.Take(_pathIndex + 1);

        public bool CanGoBack => _pathIndex > 0;

        public bool CanGoForward => _pathIndex < _pathRecord.Count - 1;

        public void Refresh()
        {
            // Implement the refresh logic here
            throw new NotImplementedException("暂时没有刷新逻辑，没想好要怎么刷");
        }

        public void GoBack()
        {
            if (!CanGoBack)
            {
                return;
            }
            var path = _pathRecord[--_pathIndex];
            // 丢弃extradata
            Navigate(path, false, null);
        }

        public void GoForwad()
        {
            if (!CanGoForward)
            {
                return;
            }
            var path = _pathRecord[++_pathIndex];
            // 丢弃extradata
            Navigate(path, false, null);
        }

        protected void PushRecord(string target)
        {
            _pathRecord.Add(target);
            _pathIndex = _pathRecord.Count - 1;
        }

        public bool Navigate(string target, object? extraData)
        {
            return Navigate(target, true, extraData);
        }

        protected abstract bool Navigate(string target, bool addHistory, object? extraData);
    }

    partial class Router
    {
        /// <summary>
        /// 后续可以支持各种类型路由形式
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        public static void InitRouter(string? target = null, params Route[] routes)
        {
            var router = new GenericRouter(target, routes);
            RouterExtension.Register(router);
        }
    }


    public static class RouterExtension
    {
        const string APPROUTER = "__router__";
        internal static void Register(Router router)
        {
            // 重新加载暂时不支持
            if (Application.Current.Properties.Contains(APPROUTER))
            {
                throw new Exception("Router already exists, please call `Register` only once");
            }
            Application.Current.Properties.Add(APPROUTER, router);
        }

        public static Router CurrentRouter(this FrameworkElement element)
        {
            return Application.Current.Router();
        }

        public static Router Router(this Application application)
        {
            if (application.Properties[APPROUTER] is Router router)
            {
                return router;
            }
            throw new Exception("Router not found, please call `Register` first");
        }
        public static INavigation? CurrentNavigation(this FrameworkElement element)
        {
            return element.FindVisualParent<INavigation>();
        }

        public static bool Navigate(this FrameworkElement element, string target, object? extraData)
        {
            return element.CurrentRouter().Navigate(target, extraData);
        }
    }
}
