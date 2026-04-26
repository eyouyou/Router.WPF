using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Router.Wpf;

namespace Router.WPF.Sample
{
    public sealed class GreaterThanZeroConverter : IValueConverter
    {
        public static readonly GreaterThanZeroConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int i && i > 0;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            RouterHost.Content = this.CurrentRouter();
        }
    }
}
