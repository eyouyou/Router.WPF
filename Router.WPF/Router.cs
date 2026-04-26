using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Router.Wpf.Core;
using Router.Wpf.Abstractions;
using Router.Wpf.Abstractions.Routing;
using Router.Wpf.Routers;

namespace Router.Wpf
{
    /// 包含uri的解析逻辑，所有路由信息
    /// </summary>
    public abstract partial class Router : UserControl, IRouter
    {

        internal Router()
        {
        }

        /// <summary>
        /// 创建一个供路由 context 使用的内容宿主。这里用普通的 <see cref="ContentControl"/> 而不是
        /// <c>Frame</c>，是为了避免 Frame 自带的 journal（每个 Frame 内部都维护一份独立的前进/后退栈）。
        /// 我们的 Router 自己维护了 <c>_pathRecord</c>，再多一份只会引入双重历史。
        /// </summary>
        internal static ContentControl CreateHost() => new ContentControl();

        public string CurrentTarget { get; internal set; } = string.Empty;

        protected List<string> _pathRecord = new();

        protected int _pathIndex = -1;
        public IEnumerable<string> PathRecord => _pathRecord;

        public abstract RouteCollection RouteCollection { get; }

        public Notifier<NavigationEventArgs> NavigationRequested { get; } = new();

        public IEnumerable<string> BackStack => _pathRecord.Take(_pathIndex);

        public IEnumerable<string> ForwardStack => _pathRecord.Skip(_pathIndex + 1);

        public bool CanGoBack => _pathIndex > 0;

        public bool CanGoForward => _pathIndex < _pathRecord.Count - 1;

        public virtual void Refresh()
        {
            // 用当前 target 重新跳转一次但不入历史。
            // 子类可以在 base 调用前清掉自己的缓存以强制完整重建。
            Navigate(CurrentTarget, false, null);
        }

        public void GoBack()
        {
            if (!CanGoBack) return;

            // 只有当 Navigate 真的成功时才下移索引。
            // 否则一条已失效的旧路径会让 _pathIndex 无声地腐化。
            var newIndex = _pathIndex - 1;
            var path = _pathRecord[newIndex];
            if (Navigate(path, false, null))
                _pathIndex = newIndex;
        }

        public void GoForwad()
        {
            if (!CanGoForward) return;

            var newIndex = _pathIndex + 1;
            var path = _pathRecord[newIndex];
            if (Navigate(path, false, null))
                _pathIndex = newIndex;
        }

        protected void PushRecord(string target)
        {
            // 截断前进栈 —— 从某个回退位置再次前进式跳转之后，
            // 原先那些"未来"路径不再可达。
            if (_pathIndex >= 0 && _pathIndex < _pathRecord.Count - 1)
                _pathRecord.RemoveRange(_pathIndex + 1, _pathRecord.Count - _pathIndex - 1);

            _pathRecord.Add(target);
            _pathIndex = _pathRecord.Count - 1;
        }

        public bool Navigate(string target, object? extraData)
        {
            return Navigate(target, true, extraData);
        }

        /// <summary>
        /// 异步跳转入口。当前是同步 <see cref="Navigate(string, object?)"/> 的薄包装，
        /// 仅消费一次 <paramref name="ct"/>。形状提前留出，方便调用方写
        /// <c>await router.NavigateAsync(...)</c>，也给以后的路由守卫
        /// （<c>CanActivate</c> / <c>CanDeactivate</c>）和异步加载留好接缝。
        /// </summary>
        public virtual Task<bool> NavigateAsync(string target, object? extraData = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Navigate(target, extraData));
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
                throw new InvalidOperationException("Router already exists, please call `Register` only once.");
            }
            Application.Current.Properties.Add(APPROUTER, router);
        }

        public static Router CurrentRouter(this FrameworkElement element)
        {
            // 优先查找视觉树上最近的 Router 实例（为日后的多 Router scope 留口子），
            // 找不到再回退到应用级注册的全局 Router。
            return element?.FindVisualParent<Router>() ?? Application.Current.Router();
        }

        public static Router Router(this Application application)
        {
            if (application.Properties[APPROUTER] is Router router)
            {
                return router;
            }
            throw new InvalidOperationException("Router not found, please call `Register` first.");
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
