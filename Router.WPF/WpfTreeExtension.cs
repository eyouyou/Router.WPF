using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;

namespace Router.Wpf
{
    public static class WpfTreeExtension
    {
        public static T? FindVisualParent<T>(this DependencyObject child) where T : class
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null)
                return null;

            if (parentObject is T parent)
            {
                return parent;
            }
            else
            {
                return FindVisualParent<T>(parentObject);
            }
        }

        public static T? FindVisualParentUntil<T, U>(this DependencyObject child) where T : class
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null)
                return null;

            if (parentObject is T parent)
            {
                return parent;
            }
            else if (parentObject is U)
            {
                return null;
            }
            else
            {
                return FindVisualParentUntil<T, U>(parentObject);
            }
        }


        public static T? FindLogicalParent<T>(this DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = LogicalTreeHelper.GetParent(child);

            if (parentObject == null)
                return null;

            if (parentObject is T parent)
            {
                return parent;
            }
            else
            {
                return FindLogicalParent<T>(parentObject);
            }
        }
    }
}
