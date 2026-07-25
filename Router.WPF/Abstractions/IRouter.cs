using System;
using System.Collections;
using System.Collections.Generic;

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Router.Wpf.Abstractions.Routing;

namespace Router.Wpf.Abstractions
{
    /// <summary>
    /// Optional presentation-neutral identity for one opened instance of a
    /// pre-registered route. UI shells may use it to keep a stable tab while
    /// the matched path and parameters change.
    /// </summary>
    public sealed record RouteInstanceMetadata(
        string InstanceId,
        string? Title = null,
        object? Payload = null);

    public class NavigationEventArgs
    {
        public NavigationEventArgs(
            string target,
            object? extraData,
            IReadOnlyList<RouteMatch>? matches = null,
            bool addedToHistory = true)
        {
            Target = target;
            ExtraData = extraData;
            Matches = matches ?? Array.Empty<RouteMatch>();
            AddedToHistory = addedToHistory;
        }
        public string Target { get; set; }
        public object? ExtraData { get; set; }
        public IReadOnlyList<RouteMatch> Matches { get; }
        public Route? Route => Matches.LastOrDefault()?.Route;
        public string? RouteKey => Route?.Key;
        public IReadOnlyDictionary<string, object> Parameters =>
            Matches.LastOrDefault()?.Parameters
            ?? (IReadOnlyDictionary<string, object>)new Dictionary<string, object>();
        public bool AddedToHistory { get; }
        public RouteInstanceMetadata? RouteInstance =>
            ExtraData as RouteInstanceMetadata;
    }

    public interface INavigation
    {
        /// <summary>
        /// 跳转请求
        /// </summary>
        Notifier<NavigationEventArgs> NavigationRequested { get; }
    }


    public interface IRouter : INavigation
    {
        /// <summary>
        /// 回退栈
        /// </summary>
        IEnumerable<string> BackStack { get; }
        /// <summary>
        /// 前进栈
        /// </summary>
        IEnumerable<string> ForwardStack { get; }

        RouteCollection RouteCollection { get; }
        string CurrentTarget { get; }
        IEnumerable<string> PathRecord { get; }
        bool Navigate(string target, object? extraData);

        /// <summary>
        /// 异步跳转入口。当前是同步 <see cref="Navigate"/> 的薄包装，调用方可以提前
        /// 写成 <c>await</c> 形式；未来版本会用它承载路由守卫
        /// （<c>CanActivate</c> / <c>CanDeactivate</c>）和延迟视图加载。
        /// </summary>
        Task<bool> NavigateAsync(string target, object? extraData = null, CancellationToken ct = default);
        /// <summary>
        /// 回退
        /// </summary>
        void GoBack();
        /// <summary>
        /// 前进
        /// </summary>
        void GoForwad();

        /// <summary>
        /// 是否可以回退
        /// </summary>
        bool CanGoBack { get; }
        /// <summary>
        /// 是否可以前进
        /// </summary>
        bool CanGoForward { get; }
        void Refresh();
    }
}
