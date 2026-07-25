using System;
using System.Collections.ObjectModel;
using System.Linq;
using Router.WPF.Sample.Common;
using Router.Wpf.Abstractions;

namespace Router.WPF.Sample.ViewModels
{
    public sealed class OpenRouteItem : ObservableObject
    {
        private string _path;
        private string _title;
        private object? _parameters;

        public OpenRouteItem(string id, string path, string title, object? parameters)
        {
            Id = id;
            _path = path;
            _title = title;
            _parameters = parameters;
        }

        public string Id { get; }

        public string Path
        {
            get => _path;
            private set => Set(ref _path, value);
        }

        public string Title
        {
            get => _title;
            private set => Set(ref _title, value);
        }

        public object? Parameters
        {
            get => _parameters;
            private set => Set(ref _parameters, value);
        }

        public void Update(string path, string title, object? parameters)
        {
            Path = path;
            Title = title;
            Parameters = parameters;
        }
    }

    public sealed class OpenRouteStore : ObservableObject
    {
        private OpenRouteItem? _activeItem;

        public ObservableCollection<OpenRouteItem> Items { get; } = new();
        public event Action? Changed;

        public OpenRouteItem? ActiveItem
        {
            get => _activeItem;
            private set
            {
                if (!Set(ref _activeItem, value)) return;
                Changed?.Invoke();
            }
        }

        public OpenRouteItem OpenOrUpdate(string path, object? extraData = null)
        {
            var metadata = extraData as RouteInstanceMetadata;
            var id = metadata?.InstanceId ?? path;
            var title = metadata?.Title ?? CreateTitle(path);
            var item = Ensure(id, path, title, metadata?.Payload ?? extraData);

            ActiveItem = item;
            Changed?.Invoke();
            return item;
        }

        public OpenRouteItem OpenOrUpdate(NavigationEventArgs navigation)
        {
            var metadata = navigation.RouteInstance;
            var id = metadata?.InstanceId ?? navigation.Target;
            var title = metadata?.Title ?? CreateTitle(navigation.Target);
            var parameters = navigation.Parameters.Count > 0
                ? navigation.Parameters
                : metadata?.Payload;
            var item = Ensure(id, navigation.Target, title, parameters);

            ActiveItem = item;
            Changed?.Invoke();
            return item;
        }

        public OpenRouteItem OpenOrUpdate(
            string id,
            string path,
            string title,
            object? parameters = null)
        {
            var item = Ensure(id, path, title, parameters);
            ActiveItem = item;
            Changed?.Invoke();
            return item;
        }

        public OpenRouteItem Ensure(
            string id,
            string path,
            string title,
            object? parameters = null)
        {
            var item = Items.FirstOrDefault(candidate => candidate.Id == id);
            if (item is null)
            {
                item = new OpenRouteItem(id, path, title, parameters);
                Items.Add(item);
                Changed?.Invoke();
            }
            else
            {
                item.Update(path, title, parameters);
            }

            return item;
        }

        public void Activate(OpenRouteItem item)
        {
            if (Items.Contains(item))
                ActiveItem = item;
        }

        public OpenRouteItem? Close(OpenRouteItem item)
        {
            var index = Items.IndexOf(item);
            if (index < 0) return ActiveItem;

            var wasActive = ReferenceEquals(item, ActiveItem);
            Items.RemoveAt(index);
            if (wasActive)
            {
                ActiveItem = Items.Count == 0
                    ? null
                    : Items[Math.Min(index, Items.Count - 1)];
            }

            Changed?.Invoke();
            return ActiveItem;
        }

        private static string CreateTitle(string path)
        {
            var segment = path.Trim('/').Split('/').LastOrDefault();
            if (string.IsNullOrWhiteSpace(segment)) return "Home";

            return string.Join(
                " ",
                segment.Split('-', StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        }
    }
}
