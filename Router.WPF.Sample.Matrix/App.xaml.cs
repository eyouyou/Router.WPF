using System.Windows;

namespace Router.WPF.Sample
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppRoutes.Register();
            base.OnStartup(e);
        }
    }
}
