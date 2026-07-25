using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Router.WPF.Sample.Common;
using Router.Wpf.Abstractions;
using Router.Wpf.Abstractions.Routing;
using Router.Wpf;

namespace Router.WPF.Sample.ViewModels
{
    public sealed record HistoryEntry(string Path, bool IsCurrent);

    public sealed class MainViewModel : ObservableObject
    {
        private readonly Router.Wpf.Router _router;
        private readonly IDisposable _subscription;

        private string _currentPath = AppRoutes.StartupPath;
        private string _addressBarPath = AppRoutes.StartupPath;
        private string _searchQuery = string.Empty;
        private string _statusMessage = "Ready";
        private NavigationSection _activeSection;
        private FeatureTab _activeFeatureTab;
        private bool _isLeftNavigationOpen = true;
        private bool _isFeaturePanelOpen = true;

        public MainViewModel()
        {
            _router = Application.Current.Router();

            Sections = AppRoutes.Sections;
            _activeSection = Sections[0];
            _activeFeatureTab = _activeSection.FeatureTabs[0];

            NavigateCommand = new RelayCommand(p =>
            {
                if (p is string path && !string.IsNullOrWhiteSpace(path))
                    _router.Navigate(path, null);
            });

            SelectSectionCommand = new RelayCommand(p =>
            {
                if (p is not NavigationSection section)
                    return;

                ActiveSection = section;
                var firstRoute = section.Items.FirstOrDefault()?.Path;
                if (!string.IsNullOrWhiteSpace(firstRoute))
                    _router.Navigate(firstRoute, null);
            });

            ToggleFeaturePanelCommand = new RelayCommand(_ =>
                IsFeaturePanelOpen = !IsFeaturePanelOpen);

            ToggleLeftNavigationCommand = new RelayCommand(_ =>
                IsLeftNavigationOpen = !IsLeftNavigationOpen);

            SelectFeatureTabCommand = new RelayCommand(p =>
            {
                if (p is not FeatureTab featureTab)
                    return;

                ActiveFeatureTab = featureTab;
                var firstRoute = featureTab.Items.FirstOrDefault()?.Path;
                if (!string.IsNullOrWhiteSpace(firstRoute))
                    _router.Navigate(firstRoute, null);
            });

            NavigateToAddressCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrWhiteSpace(AddressBarPath))
                    _router.Navigate(AddressBarPath.Trim(), null);
            });

            BackCommand    = new RelayCommand(_ => _router.GoBack(),    _ => _router.CanGoBack);
            ForwardCommand = new RelayCommand(_ => _router.GoForwad(), _ => _router.CanGoForward);
            RefreshCommand = new RelayCommand(_ => _router.Refresh());

            ClearSearchCommand = new RelayCommand(_ =>
            {
                SearchQuery = string.Empty;
                SearchResults.Clear();
            });

            _subscription = _router.NavigationRequested.Subscribe(OnNavigated);

            CurrentPath    = _router.CurrentTarget;
            AddressBarPath = _router.CurrentTarget;
            SyncActiveSection(_router.CurrentTarget);
            UpdateHistory();
        }

        private const int HistoryDisplayCap = 8;

        public IReadOnlyList<NavigationSection> Sections { get; }
        public IReadOnlyList<FeatureTab> VisibleFeatureTabs => ActiveSection.FeatureTabs;
        public ObservableCollection<HistoryEntry> History { get; } = new();
        public ObservableCollection<RouteSearchInfo> SearchResults { get; } = new();
        public int TotalHistoryCount => _router.PathRecord.Count();

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

        public NavigationSection ActiveSection
        {
            get => _activeSection;
            private set
            {
                if (!Set(ref _activeSection, value))
                    return;

                OnPropertyChanged(nameof(VisibleFeatureTabs));
                ActiveFeatureTab = value.FeatureTabs[0];
            }
        }

        public FeatureTab ActiveFeatureTab
        {
            get => _activeFeatureTab;
            set => Set(ref _activeFeatureTab, value);
        }

        public bool IsFeaturePanelOpen
        {
            get => _isFeaturePanelOpen;
            private set => Set(ref _isFeaturePanelOpen, value);
        }

        public bool IsLeftNavigationOpen
        {
            get => _isLeftNavigationOpen;
            private set => Set(ref _isLeftNavigationOpen, value);
        }

        public ICommand NavigateCommand { get; }
        public ICommand SelectSectionCommand { get; }
        public ICommand ToggleFeaturePanelCommand { get; }
        public ICommand ToggleLeftNavigationCommand { get; }
        public ICommand SelectFeatureTabCommand { get; }
        public ICommand NavigateToAddressCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand ForwardCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearSearchCommand { get; }

        private void OnNavigated(NavigationEventArgs e)
        {
            CurrentPath = e.Target;
            AddressBarPath = e.Target;
            SyncActiveSection(e.Target);

            StatusMessage = e.ExtraData is null
                ? $"navigated to {e.Target}"
                : $"navigated to {e.Target}  ·  extraData: {e.ExtraData}";

            UpdateHistory();
            CommandManager.InvalidateRequerySuggested();
        }

        private void SyncActiveSection(string path)
        {
            var section = Sections.FirstOrDefault(candidate =>
                candidate.RoutePrefixes.Any(prefix =>
                    string.Equals(path, prefix, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)));

            if (section is not null)
                ActiveSection = section;

            var featureTab = ActiveSection.FeatureTabs.FirstOrDefault(tab =>
                tab.Items.Any(item =>
                    string.Equals(path, item.Path, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(item.Path.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)));

            if (featureTab is not null)
                ActiveFeatureTab = featureTab;
        }

        private void UpdateHistory()
        {
            var paths = _router.PathRecord.ToList();
            var skip = Math.Max(0, paths.Count - HistoryDisplayCap);
            // 最新的放最上。
            var slice = paths.Skip(skip).Reverse().ToList();

            History.Clear();
            var matchedCurrent = false;
            foreach (var p in slice)
            {
                var isCurrent = !matchedCurrent && p == _router.CurrentTarget;
                if (isCurrent) matchedCurrent = true;
                History.Add(new HistoryEntry(p, isCurrent));
            }

            OnPropertyChanged(nameof(TotalHistoryCount));
        }

        private void RefreshSearch()
        {
            SearchResults.Clear();
            var q = SearchQuery?.Trim();
            if (string.IsNullOrEmpty(q)) return;

            try
            {
                foreach (var hit in _router.RouteCollection.Search(q).Take(20))
                    SearchResults.Add(hit);
            }
            catch
            {
                // 搜索器对特殊字符容忍度有限 —— 半成品输入静默吞掉，避免敲键盘途中弹错。
            }
        }
    }
}
