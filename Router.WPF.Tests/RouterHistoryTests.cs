using System.Linq;
using Router.Wpf;
using Router.Wpf.Abstractions;
using Router.Wpf.Abstractions.Routing;
using Xunit;

namespace Router.Wpf.Tests
{
    /// <summary>
    /// 在一个 Router 的 stub 子类上验证后退/前进历史栈机制。Router 继承自
    /// UserControl，构造时需要 STA 线程，所以这些用例都要走 [WpfFact]。
    /// </summary>
    public class RouterHistoryTests
    {
        private sealed class StubRouter : Router
        {
            public override RouteCollection RouteCollection { get; } = new IndexRouteCollection();
            public bool NextNavigateResult { get; set; } = true;
            public int PathIndex => _pathIndex;

            protected override bool Navigate(string target, bool addHistory, object? extraData)
            {
                if (!NextNavigateResult) return false;
                if (addHistory) PushRecord(target);
                CurrentTarget = target;
                return true;
            }
        }

        [WpfFact]
        public void Navigate_appends_to_PathRecord()
        {
            var r = new StubRouter();
            r.Navigate("/a", null);
            r.Navigate("/b", null);
            r.Navigate("/c", null);

            Assert.Equal(new[] { "/a", "/b", "/c" }, r.PathRecord);
            Assert.Equal("/c", r.CurrentTarget);
        }

        [WpfFact]
        public void BackStack_excludes_current_ForwardStack_is_empty_at_tip()
        {
            var r = new StubRouter();
            r.Navigate("/a", null);
            r.Navigate("/b", null);
            r.Navigate("/c", null);

            Assert.Equal(new[] { "/a", "/b" }, r.BackStack);
            Assert.Empty(r.ForwardStack);
            Assert.True(r.CanGoBack);
            Assert.False(r.CanGoForward);
        }

        [WpfFact]
        public void GoBack_then_GoForwad_round_trips()
        {
            var r = new StubRouter();
            r.Navigate("/a", null);
            r.Navigate("/b", null);
            r.Navigate("/c", null);

            r.GoBack();
            Assert.Equal("/b", r.CurrentTarget);
            Assert.Equal(new[] { "/a" }, r.BackStack);
            Assert.Equal(new[] { "/c" }, r.ForwardStack);

            r.GoForwad();
            Assert.Equal("/c", r.CurrentTarget);
            Assert.Empty(r.ForwardStack);
        }

        [WpfFact]
        public void Navigate_after_GoBack_truncates_forward_stack()
        {
            var r = new StubRouter();
            r.Navigate("/a", null);
            r.Navigate("/b", null);
            r.Navigate("/c", null);

            r.GoBack();           // 此时停在 /b，前进栈是 [/c]
            r.Navigate("/d", null);

            // /c 应当被驱逐 —— 从一个回退位置再次前进式跳转后，原先的"未来"路径不可达了。
            Assert.Equal(new[] { "/a", "/b", "/d" }, r.PathRecord);
            Assert.False(r.CanGoForward);
        }

        [WpfFact]
        public void GoBack_does_not_corrupt_index_when_Navigate_fails()
        {
            var r = new StubRouter();
            r.Navigate("/a", null);
            r.Navigate("/b", null);

            var indexBefore = r.PathIndex;
            r.NextNavigateResult = false;
            r.GoBack();

            // Navigate 拒绝执行时不能让 _pathIndex 也跟着回退（否则历史指针就坏了）。
            Assert.Equal(indexBefore, r.PathIndex);
            Assert.Equal("/b", r.CurrentTarget);
        }

        [WpfFact]
        public void GoForwad_does_not_corrupt_index_when_Navigate_fails()
        {
            var r = new StubRouter();
            r.Navigate("/a", null);
            r.Navigate("/b", null);
            r.GoBack();
            Assert.True(r.CanGoForward);

            var indexBefore = r.PathIndex;
            r.NextNavigateResult = false;
            r.GoForwad();

            Assert.Equal(indexBefore, r.PathIndex);
        }

        [WpfFact]
        public void GoBack_at_root_is_a_noop()
        {
            var r = new StubRouter();
            r.Navigate("/a", null);
            Assert.False(r.CanGoBack);
            r.GoBack();
            Assert.Equal("/a", r.CurrentTarget);
            Assert.Equal(new[] { "/a" }, r.PathRecord);
        }

        [WpfFact]
        public async System.Threading.Tasks.Task NavigateAsync_delegates_to_sync_navigate()
        {
            var r = new StubRouter();
            var ok = await r.NavigateAsync("/a");
            Assert.True(ok);
            Assert.Equal("/a", r.CurrentTarget);

            r.NextNavigateResult = false;
            var failed = await r.NavigateAsync("/b");
            Assert.False(failed);
        }

        [WpfFact]
        public async System.Threading.Tasks.Task NavigateAsync_honors_cancellation()
        {
            var r = new StubRouter();
            using var cts = new System.Threading.CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAsync<System.OperationCanceledException>(
                () => r.NavigateAsync("/a", null, cts.Token));
        }
    }
}
