using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Router.WPF.Sample.Common;
using Router.Wpf;

namespace Router.WPF.Sample.ViewModels
{
    public sealed class PreferencesViewModel : ObservableObject
    {
        public PreferencesViewModel(IDictionary<string, object> parameters)
        {
            Theme = parameters.TryGetValue("theme", out var v) && v is string s && !string.IsNullOrEmpty(s)
                ? s
                : "(none)";

            HasTheme = Theme != "(none)";

            SelectThemeCommand = new RelayCommand(p =>
            {
                if (p is string theme)
                    Application.Current.Router().Navigate($"/preferences/{theme}", null);
            });

            ClearThemeCommand = new RelayCommand(_ =>
                Application.Current.Router().Navigate("/preferences", null));
        }

        public string Theme { get; }
        public bool HasTheme { get; }

        public ICommand SelectThemeCommand { get; }
        public ICommand ClearThemeCommand { get; }
    }
}
