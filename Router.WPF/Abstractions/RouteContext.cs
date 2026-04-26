using System;
using System.Collections.Generic;
using System.Text;

namespace Router.Wpf.Abstractions.Routing
{
    /// <summary>
    /// 匹配链上的一个节点。实现通常是 WPF UserControl（用于宿主匹配到的组件），
    /// 同时持有几个 <see cref="Notifier{T}"/> 订阅源 —— 当它从活跃链上摘下时，需要
    /// 主动释放这些资源，所以接口继承 <see cref="IDisposable"/>。
    /// </summary>
    public interface IRouteContext : IDisposable
    {
        /// <summary>
        /// 当前 context 的 <see cref="Match"/> 变化时触发（典型场景：路由 key 不变但
        /// 动态参数变了，比如 <c>{userId}</c> 从 1001 变到 2222）。
        /// </summary>
        Notifier<IRouteContext> OnMatchChanged { get; }

        RouteMatch Match { get; set; }

        IRouteContext? OutletRouteContext { get; set; }
    }

    public interface IParentRouteContext : IRouteContext
    {
        /// <summary>
        /// 当本 context 之下的 outlet 内容需要重新渲染时触发
        /// （例如在它下面挂了一条新的子路由链）。
        /// </summary>
        Notifier<IRouteContext> OnOutletChanged { get; }

        /// <summary>
        /// 关联上下文
        /// </summary>
        public IRouteContext? RefContext { get; set; }

    }
}
