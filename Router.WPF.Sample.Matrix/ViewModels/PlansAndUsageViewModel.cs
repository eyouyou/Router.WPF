using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Router.WPF.Sample.Common;
using Router.Wpf;

namespace Router.WPF.Sample.ViewModels
{
    public sealed record PlanItem(int Id, string Name, string Tier, string MonthlyPrice);

    public sealed class PlansAndUsageViewModel : ObservableObject
    {
        public PlansAndUsageViewModel(IDictionary<string, object> parameters)
        {
            UserId = (int)parameters["userId"];

            Plans = new ObservableCollection<PlanItem>
            {
                new(101, "Starter",     "Free",       "$0"),
                new(102, "Pro Monthly", "Individual", "$19"),
                new(103, "Team Annual", "Team",       "$148"),
                new(104, "Enterprise",  "Org",        "$499"),
            };

            ViewPlanCommand = new RelayCommand(p =>
            {
                if (p is PlanItem plan)
                    Application.Current.Router().Navigate(
                        $"/billing-and-plans/plans-and-usage/{UserId}/plan/{plan.Id}",
                        null);
            });
        }

        public int UserId { get; }
        public ObservableCollection<PlanItem> Plans { get; }
        public ICommand ViewPlanCommand { get; }
    }
}
