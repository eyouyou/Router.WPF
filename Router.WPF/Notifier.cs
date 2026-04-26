using System;

namespace Router.Wpf
{
    /// <summary>
    /// 极简的事件式 Notifier，拥有和 <c>Subject&lt;T&gt;</c> 一致的订阅语义
    /// （<c>Subscribe</c> 返回 <see cref="IDisposable"/>）。仅服务于框架内三处单向广播
    /// 点（<c>NavigationRequested</c> / <c>OnMatchChanged</c> / <c>OnOutletChanged</c>），
    /// 这些位置都没用到 Rx 的任何操作符，所以不再依赖 System.Reactive。
    /// </summary>
    /// <remarks>
    /// 非线程安全、不重入安全 —— <see cref="OnNext"/> 在调用线程上同步派发，与现有
    /// 调用点（全部跑在 WPF Dispatcher 线程上）的语义一致。
    /// </remarks>
    public sealed class Notifier<T> : IDisposable
    {
        private event Action<T>? _handlers;
        private event Action? _completionHandlers;
        private bool _completed;

        public IDisposable Subscribe(Action<T> handler) => Subscribe(handler, null);

        public IDisposable Subscribe(Action<T> handler, Action? onCompleted)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_completed)
            {
                onCompleted?.Invoke();
                return EmptyDisposable.Instance;
            }
            _handlers += handler;
            if (onCompleted != null) _completionHandlers += onCompleted;
            return new Unsubscriber(this, handler, onCompleted);
        }

        public void OnNext(T value)
        {
            if (_completed) return;
            _handlers?.Invoke(value);
        }

        /// <summary>
        /// 标记终结。后续 OnNext / Subscribe 将变为无操作；存量订阅者在自身的
        /// onCompleted 回调被调用后也会被释放。
        /// </summary>
        public void OnCompleted()
        {
            if (_completed) return;
            _completed = true;
            var completion = _completionHandlers;
            _handlers = null;
            _completionHandlers = null;
            completion?.Invoke();
        }

        public void Dispose() => OnCompleted();

        private sealed class Unsubscriber : IDisposable
        {
            private readonly Notifier<T> _owner;
            private Action<T>? _handler;
            private Action? _completionHandler;

            public Unsubscriber(Notifier<T> owner, Action<T> handler, Action? completionHandler)
            {
                _owner = owner;
                _handler = handler;
                _completionHandler = completionHandler;
            }

            public void Dispose()
            {
                if (_handler == null) return;
                _owner._handlers -= _handler;
                if (_completionHandler != null) _owner._completionHandlers -= _completionHandler;
                _handler = null;
                _completionHandler = null;
            }
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
