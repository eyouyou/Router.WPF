using System.Windows;
using System.Windows.Controls;
using Router.WPF.Sample.ViewModels;
using Router.Wpf.Routers;

namespace Router.WPF.Sample.Views
{
    public partial class BlockedUserView : UserControl
    {
        public BlockedUserView() => InitializeComponent();

        private void ShowMatches_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not BlockedUserViewModel vm) return;

            vm.RouteMatches.Clear();
            foreach (var m in this.GetRouteMatches())
                vm.RouteMatches.Add($"{m.Route.Key,-22} →  {m.PathName}");
        }
    }
}
