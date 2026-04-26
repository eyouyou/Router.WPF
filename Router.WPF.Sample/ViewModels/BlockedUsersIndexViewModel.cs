using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Router.WPF.Sample.Common;
using Router.Wpf;

namespace Router.WPF.Sample.ViewModels
{
    public sealed record BlockedUserSummary(string Id, string DisplayName, string Reason);

    public sealed class BlockedUsersIndexViewModel : ObservableObject
    {
        public BlockedUsersIndexViewModel()
        {
            Users = new ObservableCollection<BlockedUserSummary>
            {
                new("alice",   "Alice Johnson",   "Spam"),
                new("bob",     "Bob Stein",       "Harassment"),
                new("charlie", "Charlie Mendez",  "Impersonation"),
                new("delta",   "Delta Kim",       "Fraud"),
                new("echo",    "Echo Walker",     "Hate speech"),
            };

            OpenUserCommand = new RelayCommand(p =>
            {
                if (p is string id)
                    Application.Current.Router().Navigate($"/moderation/blocked-users/{id}", null);
            });
        }

        public ObservableCollection<BlockedUserSummary> Users { get; }
        public ICommand OpenUserCommand { get; }
    }
}
