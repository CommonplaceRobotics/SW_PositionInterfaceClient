using log4net.Config;
using Microsoft.Win32;
using PositionInterfaceClient.MotionGenerator;
using PositionInterfaceClient.Network;
using System.Windows;
using System.Windows.Controls;

namespace PositionInterfaceClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        /// <summary>
        /// Does the network communication
        /// </summary>
        private readonly PositionClient m_client = new();

        /// <summary>
        /// Current jog values
        /// </summary>
        private readonly double[] m_jogValues = new double[9];

        /// <summary>
        /// Generates the jog motion
        /// </summary>
        private readonly JogMotionGenerator m_jogMotion = new();

        /// <summary>
        /// Generates a motion from CSV
        /// </summary>
        private readonly CSVMotionGenerator m_csvMotion = new();

        /// <summary>
        /// Timer for updating the position info box
        /// </summary>
        private readonly System.Timers.Timer m_positionTimer = new();

        /// <summary>
        /// Constructor
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            m_client.ConnectionChanged += OnPositionItfConnectionChanged;

            tbJogVelocity.Text = m_jogMotion.Velocity.ToString("F2");

            m_positionTimer.Elapsed += OnPositionUpdate;
            m_positionTimer.Interval = 100;
            m_positionTimer.AutoReset = true;
            m_positionTimer.Start();

            SelectJogSource();

            log.Debug("MainWindow loaded");
        }

        /// <summary>
        /// Updates the current position bar, is called by the timer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnPositionUpdate(object? sender, System.Timers.ElapsedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (m_client.IsRunning)
                {
                    var currentPosition = m_client.CurrentPosition;
                    tbCurrentPosition.Text = string.Format("Joints: {0:F2} {1:F2} {2:F2} {3:F2} {4:F2} {5:F2} Ext: {6:F2} {7:F2} {8:F2} X={9:F2} Y={10:F2} Z={11:F2} A={12:F2} B={13:F2} C={14:F2}", currentPosition.Joints[0], currentPosition.Joints[1], currentPosition.Joints[2], currentPosition.Joints[3], currentPosition.Joints[4], currentPosition.Joints[5], currentPosition.Joints[6], currentPosition.Joints[7], currentPosition.Joints[8], currentPosition.CartesianPosition.X, currentPosition.CartesianPosition.Y, currentPosition.CartesianPosition.Z, currentPosition.CartesianOrientation.X, currentPosition.CartesianOrientation.Y, currentPosition.CartesianOrientation.Z);
                    tbCurrentPositionFrequency.Text = string.Format("{0:F1} ms", m_client.CurrentPositionUpdateFrequencyMS);
                    var targetPosition = m_client.LastTargetPosition;
                    tbTargetPosition.Text = string.Format("Joints: {0:F2} {1:F2} {2:F2} {3:F2} {4:F2} {5:F2} Ext: {6:F2} {7:F2} {8:F2} X={9:F2} Y={10:F2} Z={11:F2} A={12:F2} B={13:F2} C={14:F2}", targetPosition.Joints[0], targetPosition.Joints[1], targetPosition.Joints[2], targetPosition.Joints[3], targetPosition.Joints[4], targetPosition.Joints[5], targetPosition.Joints[6], targetPosition.Joints[7], targetPosition.Joints[8], targetPosition.CartesianPosition.X, targetPosition.CartesianPosition.Y, targetPosition.CartesianPosition.Z, targetPosition.CartesianOrientation.X, targetPosition.CartesianOrientation.Y, targetPosition.CartesianOrientation.Z);
                    tbTargetPositionFrequency.Text = string.Format("{0:F1} ms", m_client.TargetPositionUpdateFrequencyMS);
                }
                else
                {
                    tbCurrentPosition.Text = "n/a";
                    tbCurrentPositionFrequency.Text = "n/a";
                    tbTargetPosition.Text = "n/a";
                    tbTargetPositionFrequency.Text = "n/a";
                }
            }));
        }

        /// <summary>
        /// Is called when the position interface connection changed
        /// </summary>
        /// <param name="isConnected"></param>
        private void OnPositionItfConnectionChanged(bool isConnected)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (isConnected)
                {
                    bConnect.Visibility = Visibility.Collapsed;
                    bDisconnect.Visibility = Visibility.Visible;
                    tbStatus.Text = "Connected";
                    ResetJog();
                }
                else
                {
                    bConnect.Visibility = Visibility.Visible;
                    bDisconnect.Visibility = Visibility.Collapsed;
                    tbStatus.Text = "Not Connected";
                    // TODO: disconnect CRI
                }
            }));
        }

        /// <summary>
        /// Button handler for connecting to a robot control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bConnect_Click(object sender, RoutedEventArgs e)
        {
            bConnect.Visibility = Visibility.Collapsed;
            bDisconnect.Visibility = Visibility.Visible;

            string address = tbAddress.Text;
            if (!int.TryParse(tbInterval.Text, out int interval)) interval = 100;
            interval = Math.Max(interval, 1);
            m_client.Start(interval, address, PositionClient.PositionDefaultPort);
            // TODO: connect CRI
        }

        /// <summary>
        /// Button handler for disconnecting from a robot control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bDisconnect_Click(object sender, RoutedEventArgs e)
        {
            m_client.Stop();
            // TODO: disconnect CRI
        }

        /// <summary>
        /// Button handler for resetting hardware errors
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bReset_Click(object sender, RoutedEventArgs e)
        {
            // TODO: reset via CRI
        }

        /// <summary>
        /// Button handler for enabling the motors
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bEnable_Click(object sender, RoutedEventArgs e)
        {
            // TODO: enable via CRI
        }

        /// <summary>
        /// Selects jog as position source
        /// </summary>
        private void SelectJogSource()
        {
            ResetJog();
            log.Info("Using jog motion");
            m_client.PositionSource = m_jogMotion;
        }

        /// <summary>
        /// Selects CSV as position source
        /// </summary>
        private void SelectCSVSource()
        {
            ResetJog();
            log.Info("Using CSV motion");
            m_client.PositionSource = m_csvMotion;
        }

        /// <summary>
        /// Deselects the position source
        /// </summary>
        private void SelectNoSource()
        {
            ResetJog();
            log.Info("Disabling motion");
            m_client.PositionSource = null;
        }

        /// <summary>
        /// Button handler for resetting the jog values to 0
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bJogReset_Click(object sender, RoutedEventArgs e)
        {
            ResetJog();
        }

        /// <summary>
        /// Resets all jog values to 0
        /// </summary>
        private void ResetJog()
        {
            for (int i = 0; i < m_jogValues.Length; i++)
            {
                m_jogValues[i] = 0;
            }
            m_jogMotion.SetJog(m_jogValues);
            m_jogMotion.SetJog(m_jogValues);
            m_jogMotion.SetJoints(m_client.CurrentPosition.Joints);
        }

        /// <summary>
        /// Changes the jog value of the given axis
        /// </summary>
        /// <param name="axis">Axis index 0-8</param>
        /// <param name="diff">Difference in percent (-1.0 .. 1.0)</param>
        private void ChangeJog(int axis, double diff)
        {
            if (axis < 0 || axis > m_jogValues.Length) return;
            m_jogValues[axis] = Math.Clamp(m_jogValues[axis] + diff, -1, 1);
            m_jogMotion.SetJog(m_jogValues);
        }

        private void bJogA1Neg_Click(object sender, RoutedEventArgs e)
        {
            ChangeJog(0, -0.1);
        }

        private void bJogA1Pos_Click(object sender, RoutedEventArgs e)
        {
            ChangeJog(0, +0.1);
        }

        private void bJogA2Neg_Click(object sender, RoutedEventArgs e)
        {
            ChangeJog(1, -0.1);
        }

        private void bJogA2Pos_Click(object sender, RoutedEventArgs e)
        {
            ChangeJog(1, 0.1);
        }

        private void bJogA3Neg_Click(object sender, RoutedEventArgs e)
        {
            ChangeJog(2, -0.1);
        }

        private void bJogA3Pos_Click(object sender, RoutedEventArgs e)
        {
            ChangeJog(2, 0.1);
        }

        private void bJogA4Neg_Click(object sender, RoutedEventArgs e)
        {
            ChangeJog(3, -0.1);
        }

        private void bJogA4Pos_Click(object sender, RoutedEventArgs e)
        {
            ChangeJog(3, 0.1);
        }

        private void bJogA5Neg_Click(object sender, RoutedEventArgs e)
        {
            ChangeJog(4, -0.1);
        }

        private void bJogA5Pos_Click(object sender, RoutedEventArgs e)
        {
            ChangeJog(4, 0.1);
        }

        private void bJogA6Neg_Click(object sender, RoutedEventArgs e)
        {
            ChangeJog(5, -0.1);
        }

        private void bJogA6Pos_Click(object sender, RoutedEventArgs e)
        {
            ChangeJog(5, 0.1);
        }

        private void bJogE1Neg_Click(object sender, RoutedEventArgs e)
        {
            ChangeJog(6, -0.1);
        }

        private void bJogE1Pos_Click(object sender, RoutedEventArgs e)
        {
            ChangeJog(6, 0.1);
        }

        private void bJogE2Neg_Click(object sender, RoutedEventArgs e)
        {
            ChangeJog(7, -0.1);
        }

        private void bJogE2Pos_Click(object sender, RoutedEventArgs e)
        {
            ChangeJog(7, 0.1);
        }

        private void bJogE3Neg_Click(object sender, RoutedEventArgs e)
        {
            ChangeJog(8, -0.1);
        }

        private void bJogE3Pos_Click(object sender, RoutedEventArgs e)
        {
            ChangeJog(8, 0.1);
        }

        /// <summary>
        /// Handler for jog velocity changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbJogVelocity_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(tbJogVelocity.Text, out double vel))
            {
                m_jogMotion.Velocity = vel;
            }
        }

        /// <summary>
        /// Button handler for CSV file selction
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bCSVSelectFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                tbCSVFileName.Text = openFileDialog.FileName;
            }
        }

        /// <summary>
        /// Button handler for starting motion from CSV
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bCSVStart_Click(object sender, RoutedEventArgs e)
        {
            // TODO
        }

        /// <summary>
        /// Button handler for stopping motion from CSV
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bCSVStop_Click(object sender, RoutedEventArgs e)
        {
            // TODO
        }

        /// <summary>
        /// Changes the position source when the tab is changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (tcPositionSource.SelectedItem == tJog)
            {
                SelectJogSource();
            }
            else if (tcPositionSource.SelectedItem == tCSV)
            {
                SelectCSVSource();
            }
            else
            {
                SelectNoSource();
            }
        }
    }
}
