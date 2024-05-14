using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Controls;
using System.Windows;

namespace PositionInterfaceClient.UserInterface
{
    public static class ListViewBehaviors
    {
        // Technique for updating column widths of a ListView's GridView manually

        // Definition of the IsAutoUpdatingColumnWidthsProperty attached DependencyProperty
        public static readonly DependencyProperty IsAutoUpdatingColumnWidthsProperty =
            DependencyProperty.RegisterAttached(
                "IsAutoUpdatingColumnWidths",
                typeof(bool),
                typeof(ListViewBehaviors),
                new UIPropertyMetadata(false, OnIsAutoUpdatingColumnWidthsChanged));

        // Get/set methods for the attached DependencyProperty
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters",
            Justification = "Only applies to ListView instances.")]
        public static bool GetIsAutoUpdatingColumnWidths(ListView listView)
        {
            return (bool)listView.GetValue(IsAutoUpdatingColumnWidthsProperty);
        }
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters",
            Justification = "Only applies to ListView instances.")]
        public static void SetIsAutoUpdatingColumnWidths(ListView listView, bool value)
        {
            listView.SetValue(IsAutoUpdatingColumnWidthsProperty, value);
        }

        // Change handler for the attached DependencyProperty
        private static void OnIsAutoUpdatingColumnWidthsChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            // Get the ListView instance and new bool value
            var listView = o as ListView;
            if ((null != listView) && (e.NewValue is bool))
            {
                // Get a descriptor for the ListView's ItemsSource property
                var descriptor = DependencyPropertyDescriptor.FromProperty(ListView.ItemsSourceProperty, typeof(ListView));
                if ((bool)e.NewValue)
                {
                    // Enabling the feature, so add the change handler
                    descriptor.AddValueChanged(listView, OnListViewItemsSourceValueChanged);
                }
                else
                {
                    // Disabling the feature, so remove the change handler
                    descriptor.RemoveValueChanged(listView, OnListViewItemsSourceValueChanged);
                }
            }
        }
        public static void UpdateColumnsWidth(ListView listView)
        {
            GridView gridView = listView.View as GridView;

            int autoFillColumnIndex = gridView.Columns.Count - 1;
            if (listView.ActualWidth == Double.NaN)
            {
                listView.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
            }

            double remainingSpace = listView.ActualWidth - 32;
            for (int i = 0; i < gridView.Columns.Count; i++)
            {
                if (i != autoFillColumnIndex)
                {
                    remainingSpace -= gridView.Columns[i].ActualWidth;
                }
            }

            gridView.Columns[autoFillColumnIndex].Width = remainingSpace >= 0 ? remainingSpace : 0;
        }

        // Handler for changes to the ListView's ItemsSource updates the column widths
        private static void OnListViewItemsSourceValueChanged(object sender, EventArgs e)
        {
            // Get a reference to the ListView's GridView...
            ListView listView = sender as ListView;
            if (null != listView)
            {
                // And update its column widths
                UpdateColumnsWidth(listView);
            }
        }
    }
}
