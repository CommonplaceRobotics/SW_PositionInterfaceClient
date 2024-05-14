using log4net.Core;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace PositionInterfaceClient.UserInterface
{
    public class WidthConverter : IValueConverter
    {
        public object Convert(object o, Type type, object parameter, CultureInfo culture)
        {
            ListView l = o as ListView;
            GridView g = l.View as GridView;
            double total = 0;
            for (int i = 0; i < g.Columns.Count - 1; i++)
            {
                total += g.Columns[i].Width;
            }
            return (l.ActualWidth - total);
        }

        public object ConvertBack(object o, Type type, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Interaktionslogik für LogControl.xaml
    /// </summary>
    public partial class LogControl : UserControl
    {
        private readonly ObservableCollection<LoggingEvent> m_Items = new();

        // maximal so viele Einträge werden angezeigt, danach werden alte verworfen
        private readonly int MaxEntryCount = 2000;

        // Always show the newest entry. This is disabled when scrolling up and enabled when scrolling to the end
        private bool m_IsLockedToEnd = true;

        private void InvokeAddEvent(LoggingEvent loggingEvent)
        {
            m_Items.Add(loggingEvent);
            if (m_Items.Count > MaxEntryCount) m_Items.RemoveAt(0);

            MyList.Items.Refresh();
            (MyList.View as GridView).Columns[1].Width = 60;

            if (m_IsLockedToEnd)
            {
                MyList.ScrollIntoView(loggingEvent);
            }

            ListViewBehaviors.UpdateColumnsWidth(MyList);
        }

        private delegate void AddEventDelegate(LoggingEvent loggingEvent);
        private readonly AddEventDelegate m_AddEventDelegate;

        private void AddEvent(LoggingEvent loggingEvent)
        {
            if (!MyList.Dispatcher.CheckAccess())
            {
                MyList.Dispatcher.BeginInvoke(m_AddEventDelegate, loggingEvent);
            }
            else
            {
                InvokeAddEvent(loggingEvent);
            }
        }

        private static ScrollViewer GetScrollViewer(ListView lv)
        {
            // GetChild schlägt fehl wenn kein ScrollViewer vorhanden ist
            try
            {
                var border = VisualTreeHelper.GetChild(lv, 0);
                return (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
            } catch { return null; }
        }

        public LogControl()
        {
            m_AddEventDelegate = new AddEventDelegate(InvokeAddEvent);
            InitializeComponent();
            MyList.ItemsSource = m_Items;
            foreach (LoggingEvent loggingEvent in Log.Instance.LoggedEvents)
                AddEvent(loggingEvent);
            Log.Instance.EventAdded += Instance_EventAdded;
            Loaded += LogControl_Loaded;
            Unloaded += LogControl_Unloaded;
            IsVisibleChanged += LogControl_IsVisibleChanged;
        }

        private void LogControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                // Ohne das hier scrollt es weg vom Ende wenn Nachrichten kommen während das Log ausgeblendet ist
                if (m_IsLockedToEnd && m_Items.Count > 0)
                {
                    MyList.ScrollIntoView(m_Items[m_Items.Count - 1]);
                }
            }
        }

        private void LogControl_Loaded(object sender, RoutedEventArgs e)
        {
            ScrollViewer sv = GetScrollViewer(MyList);
            if (sv != null) sv.ScrollChanged += Sv_ScrollChanged;
        }

        private void LogControl_Unloaded(object sender, RoutedEventArgs e)
        {
            ScrollViewer sv = GetScrollViewer(MyList);
            if (sv != null) sv.ScrollChanged -= Sv_ScrollChanged;
        }

        private void Sv_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            ListViewBehaviors.UpdateColumnsWidth(MyList);
        }

        private void Instance_EventAdded(LoggingEvent loggingEvent)
        {
            AddEvent(loggingEvent);
        }

        private void MyList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ListViewBehaviors.UpdateColumnsWidth(MyList);
        }

        /// <summary>
        /// This locks the view to the newest entry when scrolled all the way down or unlocks when scrolling up
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListView_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if(e.VerticalChange > 0 && e.VerticalOffset + e.ViewportHeight == e.ExtentHeight)
            {
                m_IsLockedToEnd = true;
            }
            else if(e.VerticalChange < 0)
            {
                m_IsLockedToEnd = false;
            }
        }
    }
}
