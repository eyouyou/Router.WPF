using System.Windows;
using System.Windows.Controls;
using Router.WPF.Sample.ViewModels;
using Router.Wpf.Routers;

namespace Router.WPF.Sample.Views
{
    public partial class PlanView : UserControl
    {
        public PlanView() => InitializeComponent();

        private void ShowMatchesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PlanViewModel vm) return;

            vm.RouteMatches.Clear();
            foreach (var m in this.GetRouteMatches())
                vm.RouteMatches.Add($"{m.Route.Key,-22} →  {m.PathName}");
        }
    }
}
