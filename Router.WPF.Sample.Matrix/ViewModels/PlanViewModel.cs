using System.Collections.Generic;
using System.Collections.ObjectModel;
using Router.WPF.Sample.Common;

namespace Router.WPF.Sample.ViewModels
{
    public sealed class PlanViewModel : ObservableObject
    {
        public PlanViewModel(IDictionary<string, object> parameters)
        {
            PlanId = (int)parameters["planId"];
        }

        public int PlanId { get; }

        public ObservableCollection<string> RouteMatches { get; } = new();
    }
}
