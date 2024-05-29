namespace PositionInterfaceClient.Tools
{
    /// <summary>
    /// Die Klasse implementiert einen Mittelwert-Filter. Werte werden hinzugefügt,
    /// intern werden die letzten x Werte gespeichert. Dann bekommt man den Mittelwert zurück.
    /// </summary>
    public class FilterAverage
    {
        private readonly double[] memArray;
        private readonly int filterSize = 0;
        private int cnt = 0;
        private double currentAverage = 0.0;
        // Number of elements in the buffer
        private int m_level = 0;

        /// <summary>
        /// initialisiert den Filter mit der Anzahl der zu Mittelnden Zahlen
        /// </summary>
        /// <param name="nrOfValues"></param>
        public FilterAverage(int nrOfValues)
        {
            filterSize = nrOfValues;
            memArray = new double[filterSize];
            currentAverage = 0.0;
        }

        /// <summary>
        /// setzt den Filter auf 0
        /// </summary>
        public void ResetFilter()
        {
            for (int i = 0; i < filterSize; i++)
                memArray[i] = 0.0;
            cnt = 0;
            currentAverage = 0.0;
            m_level = 0;
        }

        /// <summary>
        /// Schiebt einen neuen Wert hoch und berechnet die neuen Mittelwert
        /// </summary>
        public double GetNewAverage(double newValue)
        {
            memArray[cnt++] = newValue;
            if (cnt >= filterSize) cnt = 0;
            if (m_level < filterSize) m_level++;

            double res = 0.0;
            for (int i = 0; i < m_level; i++) res += memArray[i];

            res /= m_level;
            currentAverage = res;
            return currentAverage;
        }

        /// <summary>
        /// Gibt den Mittelwert zurück
        /// </summary>
        public double GetAverage()
        {
            if (filterSize == 0)
                return 0.0;

            return currentAverage;
        }

    }
}
