using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Unity.UI.Core.Abstractions;
using Unity.UI.Core.Abstractions.Routing;

namespace Unity.UI.WPF
{
    /// <summary>
    /// 因为outlet每次都是new的 所以会被重复的加载和卸载
    /// </summary>
    public class Outlet : UserControl
    {
        public Outlet()
        {
            Loaded += Outlet_Loaded;
            Unloaded += Outlet_Unloaded;
        }
        IDisposable? subscription;
        private void Outlet_Unloaded(object sender, RoutedEventArgs e)
        {
            subscription?.Dispose();
            Content = null;
        }
        private void Outlet_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            var context = this.FindVisualParentUntil<IParentRouteContext, Router>();
            if (context != null)
            {
                // 会导致路由上下文重复加载 所以增加一个上下文的标记 防止多次加载
                // 如果是window 则直接弹出window 并且把[OutletRouteContext]付给window
                // 如果非window 则直接付给自己 并且跳转
                Content = context.OutletRouteContext;

                subscription = context?.OnOutletChanged
                    .Subscribe(OnChanged);
            }
            // 获取[`RouteContext`]的匹配路由 并且frame到特定路由
        }

        private void OnChanged(IRouteContext context)
        {
            Content = context.OutletRouteContext;
        }
    }
}
