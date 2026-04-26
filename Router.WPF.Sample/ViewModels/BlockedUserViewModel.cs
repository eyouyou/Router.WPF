using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Router.WPF.Sample.Common;
using Router.Wpf;

namespace Router.WPF.Sample.ViewModels
{
    public sealed class BlockedUserViewModel : ObservableObject
    {
        public BlockedUserViewModel(IDictionary<string, object> parameters)
        {
            BlockedUserId = parameters.TryGetValue("blockedUserId", out var v)
                ? v?.ToString() ?? ""
                : "";

            BackToIndexCommand = new RelayCommand(_ =>
                Application.Current.Router().Navigate("/moderation/blocked-users", null));
        }

        public string BlockedUserId { get; }

        public ObservableCollection<string> RouteMatches { get; } = new();

        public ICommand BackToIndexCommand { get; }
    }
}
