using System;
using System.Windows;
using System.Windows.Input;
using Router.WPF.Sample.Common;
using Router.Wpf;

namespace Router.WPF.Sample.ViewModels
{
    public sealed class BillingViewModel : ObservableObject
    {
        private string _userIdInput = "1001";

        public BillingViewModel()
        {
            OpenPlansCommand = new RelayCommand(_ =>
            {
                if (!int.TryParse(UserIdInput, out var uid)) return;
                Application.Current.Router().Navigate(
                    $"/billing-and-plans/plans-and-usage/{uid}",
                    new { OpenedAt = DateTime.Now, Source = "BillingView" });
            }, p => int.TryParse(UserIdInput, out _));

            GoToSpendingLimitsCommand = new RelayCommand(_ =>
                Application.Current.Router().Navigate("/billing-and-plans/spending-limits", null));
        }

        public string UserIdInput
        {
            get => _userIdInput;
            set
            {
                if (Set(ref _userIdInput, value))
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        public ICommand OpenPlansCommand { get; }
        public ICommand GoToSpendingLimitsCommand { get; }
    }
}
