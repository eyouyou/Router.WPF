using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Router.WPF.Sample.Common;
using Router.Wpf;
using Router.Wpf.Abstractions;
using Router.Wpf.Abstractions.Routing;

namespace Router.WPF.Sample.ViewModels
{
    public sealed record HistoryEntry(string Path, bool IsCurrent);
    public sealed record SideContentItem(string Title, string Description, string Value);
    public enum SideContentSource { LeftRoute, TopOpenRoute }
    public sealed record SideContentTab(
        string Id,
        string Title,
        SideContentSource Source,
        IReadOnlyList<SideContentItem> Items);

    public sealed class MainViewModel : ObservableObject
    {
        private readonly Router.Wpf.Router _router;
        private readonly IDisposable _subscription;
        private IReadOnlyList<SideContentTab> _aggregatedTabs = Array.Empty<SideContentTab>();
        private LeftNavigationItem _activeLeftNavigation;
        private SideContentTab _activeSideTab = null!;
        private string _currentPath = AppRoutes.StartupPath;
        private string _addressBarPath = AppRoutes.StartupPath;
        private string _searchQuery = string.Empty;
        private string _statusMessage = "Ready";
        private bool _isLeftNavigationOpen = true;
        private bool _isFeaturePanelOpen = true;

        public MainViewModel()
        {
            _router = Application.Current.Router();
            LeftNavigationItems = AppRoutes.MatrixNavigation;
            _activeLeftNavigation = LeftNavigationItems[0];
            OpenRoutes = new OpenRouteStore();
            OpenRoutes.Changed += RebuildAggregatedTabs;
            OpenRoutes.Ensure("workspace", "/home", "Workspace");
            OpenRoutes.Ensure("account", "/profile", "Account");
            OpenRoutes.Ensure("administration", "/settings/general", "Administration");
            OpenRoutes.Ensure(
                "commerce",
                "/billing-and-plans/spending-limits",
                "Commerce");

            NavigateCommand = new RelayCommand(p =>
            {
                if (p is string path && !string.IsNullOrWhiteSpace(path))
                    _router.Navigate(path, null);
            });
            ActivateOpenRouteCommand = new RelayCommand(p =>
            {
                if (p is not OpenRouteItem item) return;
                OpenRoutes.Activate(item);
                _router.Navigate(
                    item.Path,
                    new RouteInstanceMetadata(item.Id, item.Title, item.Parameters));
            });
            OpenUserRouteCommand = new RelayCommand(p =>
            {
                if (p is not string userId || string.IsNullOrWhiteSpace(userId)) return;
                var title = char.ToUpperInvariant(userId[0]) + userId[1..];
                _router.Navigate(
                    $"/moderation/blocked-users/{userId}",
                    new RouteInstanceMetadata("user-details", title, userId));
            });
            CloseOpenRouteCommand = new RelayCommand(p =>
            {
                if (p is not OpenRouteItem item) return;
                var next = OpenRoutes.Close(item);
                if (next is not null)
                    _router.Navigate(next.Path, next.Parameters);
            });
            SelectLeftNavigationCommand = new RelayCommand(p =>
            {
                if (p is not LeftNavigationItem item) return;
                ActiveLeftNavigation = item;
                RebuildAggregatedTabs();
                ActiveSideTab = AggregatedTabs[0];
            });
            ToggleLeftNavigationCommand = new RelayCommand(_ =>
                IsLeftNavigationOpen = !IsLeftNavigationOpen);
            ToggleFeaturePanelCommand = new RelayCommand(_ =>
                IsFeaturePanelOpen = !IsFeaturePanelOpen);
            NavigateToAddressCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrWhiteSpace(AddressBarPath))
                    _router.Navigate(AddressBarPath.Trim(), null);
            });
            BackCommand = new RelayCommand(_ => _router.GoBack(), _ => _router.CanGoBack);
            ForwardCommand = new RelayCommand(_ => _router.GoForwad(), _ => _router.CanGoForward);
            RefreshCommand = new RelayCommand(_ => _router.Refresh());
            ClearSearchCommand = new RelayCommand(_ =>
            {
                SearchQuery = string.Empty;
                SearchResults.Clear();
            });

            _subscription = _router.NavigationRequested.Subscribe(OnNavigated);
            CurrentPath = _router.CurrentTarget;
            AddressBarPath = _router.CurrentTarget;
            OpenTopRouteForPath(_router.CurrentTarget, null);
            UpdateHistory();
        }

        public IReadOnlyList<LeftNavigationItem> LeftNavigationItems { get; }
        public OpenRouteStore OpenRoutes { get; }
        public IReadOnlyList<SideContentTab> AggregatedTabs => _aggregatedTabs;
        public ObservableCollection<HistoryEntry> History { get; } = new();
        public ObservableCollection<RouteSearchInfo> SearchResults { get; } = new();
        public int TotalHistoryCount => _router.PathRecord.Count();

        public LeftNavigationItem ActiveLeftNavigation
        {
            get => _activeLeftNavigation;
            private set => Set(ref _activeLeftNavigation, value);
        }

        public SideContentTab ActiveSideTab
        {
            get => _activeSideTab;
            set => Set(ref _activeSideTab, value);
        }

        public bool IsLeftNavigationOpen
        {
            get => _isLeftNavigationOpen;
            private set => Set(ref _isLeftNavigationOpen, value);
        }

        public bool IsFeaturePanelOpen
        {
            get => _isFeaturePanelOpen;
            private set => Set(ref _isFeaturePanelOpen, value);
        }

        public string CurrentPath
        {
            get => _currentPath;
            private set => Set(ref _currentPath, value);
        }

        public string AddressBarPath
        {
            get => _addressBarPath;
            set => Set(ref _addressBarPath, value);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (Set(ref _searchQuery, value))
                    RefreshSearch();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => Set(ref _statusMessage, value);
        }

        public ICommand NavigateCommand { get; }
        public ICommand ActivateOpenRouteCommand { get; }
        public ICommand OpenUserRouteCommand { get; }
        public ICommand CloseOpenRouteCommand { get; }
        public ICommand SelectLeftNavigationCommand { get; }
        public ICommand ToggleLeftNavigationCommand { get; }
        public ICommand ToggleFeaturePanelCommand { get; }
        public ICommand NavigateToAddressCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand ForwardCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearSearchCommand { get; }

        private void OnNavigated(NavigationEventArgs e)
        {
            CurrentPath = e.Target;
            AddressBarPath = e.Target;
            StatusMessage = $"navigated to {e.Target}";
            OpenTopRouteForPath(e.Target, e);
            UpdateHistory();
            CommandManager.InvalidateRequerySuggested();
        }

        private void RebuildAggregatedTabs()
        {
            var selectedId = ActiveSideTab?.Id;
            var localTab = CreateLeftRouteTab();
            var secondaryTabs = CreateSecondaryTabs(OpenRoutes.ActiveItem);
            _aggregatedTabs = new[] { localTab }.Concat(secondaryTabs).ToArray();
            OnPropertyChanged(nameof(AggregatedTabs));

            ActiveSideTab = _aggregatedTabs.FirstOrDefault(tab => tab.Id == selectedId)
                ?? _aggregatedTabs[0];
        }

        private SideContentTab CreateLeftRouteTab()
        {
            var items = ActiveLeftNavigation.Key switch
            {
                "personal" => new[]
                {
                    Item("Signed-in user", "Left-route personal context", "Alice"),
                    Item("Drafts", "Unsaved local changes", "3"),
                },
                "team" => new[]
                {
                    Item("Members online", "Left-route team context", "8"),
                    Item("Pending reviews", "Waiting for teammates", "2"),
                },
                _ => new[]
                {
                    Item("Runtime", "Left-route system context", ".NET 8"),
                    Item("Health", "Router service status", "Healthy"),
                },
            };
            return new SideContentTab(
                "left-route",
                ActiveLeftNavigation.Title,
                SideContentSource.LeftRoute,
                items);
        }

        private static IReadOnlyList<SideContentTab> CreateSecondaryTabs(
            OpenRouteItem? route)
        {
            if (route is null) return Array.Empty<SideContentTab>();

            return route.Id switch
            {
                "workspace" => new[]
                {
                    Secondary("overview", "Overview",
                        Item("Route", "Workspace landing route", route.Path),
                        Item("History", "Visited routes this session", "Live")),
                    Secondary("activity", "Activity",
                        Item("Recent activity", "Workspace events", "12")),
                },
                "account" => new[]
                {
                    Secondary("profile", "Profile",
                        Item("Identity", "Current account identity", "Ready")),
                    Secondary("preferences", "Preferences",
                        Item("Theme", "Current appearance preference", "System")),
                },
                "administration" => new[]
                {
                    Secondary("settings", "Settings",
                        Item("Policies", "Configured system policies", "6")),
                    Secondary("moderation", "Moderation",
                        Item("Pending review", "Moderation queue", "2")),
                },
                "commerce" => new[]
                {
                    Secondary("billing", "Billing",
                        Item("Usage", "Current billing period", "68%")),
                    Secondary("limits", "Limits",
                        Item("Spending limit", "Configured monthly limit", "$500")),
                },
                _ => new[]
                {
                    Secondary("details", "Details",
                        Item("Route", "Dynamic route instance", route.Path)),
                    Secondary("parameters", "Parameters",
                        Item("Instance", "Stable route instance id", route.Id),
                        Item("Payload", "Instance-specific data",
                            route.Parameters is null ? "None" : "Attached")),
                },
            };
        }

        private static SideContentTab Secondary(
            string key,
            string title,
            params SideContentItem[] items)
            => new(
                $"secondary:{key}",
                title,
                SideContentSource.TopOpenRoute,
                items);

        private void OpenTopRouteForPath(string path, NavigationEventArgs? navigation)
        {
            if (navigation?.RouteInstance is { } instance)
            {
                OpenRoutes.OpenOrUpdate(navigation);
                return;
            }

            var (id, title) = path switch
            {
                var value when value.StartsWith("/billing-and-plans", StringComparison.OrdinalIgnoreCase)
                    => ("commerce", "Commerce"),
                var value when value.StartsWith("/settings", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("/moderation", StringComparison.OrdinalIgnoreCase)
                    => ("administration", "Administration"),
                var value when value.StartsWith("/profile", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("/account", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("/appearance", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("/preferences", StringComparison.OrdinalIgnoreCase)
                    => ("account", "Account"),
                _ => ("workspace", "Workspace"),
            };

            OpenRoutes.OpenOrUpdate(
                id,
                path,
                title,
                navigation?.Parameters);
        }

        private static SideContentItem Item(string title, string description, string value)
            => new(title, description, value);

        private void UpdateHistory()
        {
            var paths = _router.PathRecord.ToList();
            History.Clear();
            foreach (var path in paths.TakeLast(8).Reverse())
                History.Add(new HistoryEntry(path, path == _router.CurrentTarget));
            OnPropertyChanged(nameof(TotalHistoryCount));
        }

        private void RefreshSearch()
        {
            SearchResults.Clear();
            var query = SearchQuery.Trim();
            if (string.IsNullOrEmpty(query)) return;
            try
            {
                foreach (var hit in _router.RouteCollection.Search(query).Take(20))
                    SearchResults.Add(hit);
            }
            catch
            {
                // Partial search input may be invalid for the index parser.
            }
        }
    }
}
