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

        public MainViewModel()
        {
            _router = Application.Current.Router();

            SidebarItems = AppRoutes.Sidebar;

            NavigateCommand = new RelayCommand(p =>
            {
                if (p is string path && !string.IsNullOrWhiteSpace(path))
                    _router.Navigate(path, null);
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
            UpdateHistory();
        }

        private const int HistoryDisplayCap = 8;

        public IReadOnlyList<SidebarItem> SidebarItems { get; }
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

        public ICommand NavigateCommand { get; }
        public ICommand NavigateToAddressCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand ForwardCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearSearchCommand { get; }

        private void OnNavigated(NavigationEventArgs e)
        {
            CurrentPath = e.Target;
            AddressBarPath = e.Target;

            StatusMessage = e.ExtraData is null
                ? $"navigated to {e.Target}"
                : $"navigated to {e.Target}  ·  extraData: {e.ExtraData}";

            UpdateHistory();
            CommandManager.InvalidateRequerySuggested();
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
