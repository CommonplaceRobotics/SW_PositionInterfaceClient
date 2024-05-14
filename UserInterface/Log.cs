using log4net.Core;

namespace PositionInterfaceClient.UserInterface
{
    public class LogAppender : log4net.Appender.IAppender
    {
        public string Name { get; set; } = "";

        public void Close()
        {
        }

        public void DoAppend(LoggingEvent loggingEvent)
        {
            Log.Instance.AddEvent(loggingEvent);
        }
    }

    public class Log
    {
        public delegate void EventAddedHandler(LoggingEvent loggingEvent);
        private List<LoggingEvent> m_Events = [];
        private static Log m_Instance = new();

        public static Log Instance
        {
            get
            {
                return m_Instance;
            }
        }

        private Log()
        {

        }

        public event EventAddedHandler? EventAdded = null;

        public void AddEvent(LoggingEvent loggingEvent)
        {
            lock (this)
            {
                m_Events.Add(loggingEvent);
            }
            EventAdded?.Invoke(loggingEvent);
        }

        public IEnumerable<LoggingEvent> LoggedEvents
        {
            get
            {
                lock (this)
                {
                    return m_Events;
                }
            }
        }
    }
}
