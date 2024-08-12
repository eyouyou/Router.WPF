using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Text;
using Unity.UI.Core.Abstractions.Routing;

namespace Unity.UI.Core.Abstractions
{
    public class NavigationEventArgs
    {
        public NavigationEventArgs(string target, object? extraData)
        {
            Target = target;
            ExtraData = extraData;
        }
        public string Target { get; set; }
        public object? ExtraData { get; set; }
    }

    public interface INavigation
    {
        /// <summary>
        /// 跳转请求
        /// </summary>
        Subject<NavigationEventArgs> NavigationRequested { get; }
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
