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

    public sealed class CurrentRouteConverter : IMultiValueConverter
    {
        public static readonly CurrentRouteConverter Instance = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not string route || values[1] is not string current)
                return false;

            var normalizedRoute = route.TrimEnd('/');
            return string.Equals(current, normalizedRoute, StringComparison.OrdinalIgnoreCase)
                || current.StartsWith(normalizedRoute + "/", StringComparison.OrdinalIgnoreCase);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public sealed class EqualityConverter : IMultiValueConverter
    {
        public static readonly EqualityConverter Instance = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            => values.Length >= 2 && Equals(values[0], values[1]);

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public sealed class BooleanToGridLengthConverter : IValueConverter
    {
        public static readonly BooleanToGridLengthConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var width = parameter is string text
                && double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : 0d;

            return value is true ? new GridLength(width) : new GridLength(0);
        }

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
