using System.Windows.Controls;
using System.Windows.Input;
using Router.WPF.Sample.Common;
using Router.Wpf;

namespace Router.WPF.Sample.Views
{
    public partial class SettingsShellView : UserControl
    {
        public SettingsShellView()
        {
            InitializeComponent();
            DataContext = this;
        }

        public ICommand NavigateCommand { get; } = new RelayCommand(p =>
        {
            if (p is string path)
                System.Windows.Application.Current.Router().Navigate(path, null);
        });
    }
}
