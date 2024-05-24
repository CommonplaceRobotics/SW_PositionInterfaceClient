using Microsoft.Win32;
using PositionInterfaceClient.MotionGenerator;
using PositionInterfaceClient.Network;
using System.Globalization;
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
        private readonly PositionClient m_positionClient = new();

        /// <summary>
        /// Communicates via CRI for control commands
        /// </summary>
        private readonly CRIClient m_criClient = new();

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

            m_positionClient.ConnectionChanged += OnPositionItfConnectionChanged;
            m_criClient.ConnectionChanged += OnCRIClientConnectionChanged;

            tbJogVelocity.Text = m_jogMotion.Velocity.ToString("F2");

            m_positionTimer.Elapsed += OnPositionUpdate;
            m_positionTimer.Interval = 100;
            m_positionTimer.AutoReset = true;
            m_positionTimer.Start();

            SelectJogSource();
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
                string status;
                if (!m_criClient.IsRunning && !m_positionClient.IsRunning)
                {
                    status = "Not connected";
                }
                else if (!m_criClient.IsRunning)
                {
                    status = "CRI not connected";
                }
                else if (!m_positionClient.IsRunning)
                {
                    status = m_criClient.ErrorCode + ", position not connected";
                }
                else
                {
                    status = m_criClient.ErrorCode;
                }
                tbStatus.Text = status;

                if (m_positionClient.IsRunning)
                {
                    var currentPosition = m_positionClient.CurrentPosition;
                    tbCurrentPosition.Text = string.Format("Joints: {0:F2} {1:F2} {2:F2} {3:F2} {4:F2} {5:F2} Ext: {6:F2} {7:F2} {8:F2} X={9:F2} Y={10:F2} Z={11:F2} A={12:F2} B={13:F2} C={14:F2}", currentPosition.Joints[0], currentPosition.Joints[1], currentPosition.Joints[2], currentPosition.Joints[3], currentPosition.Joints[4], currentPosition.Joints[5], currentPosition.Joints[6], currentPosition.Joints[7], currentPosition.Joints[8], currentPosition.CartesianPosition.X, currentPosition.CartesianPosition.Y, currentPosition.CartesianPosition.Z, currentPosition.CartesianOrientation.X, currentPosition.CartesianOrientation.Y, currentPosition.CartesianOrientation.Z);
                    tbCurrentPositionFrequency.Text = string.Format("{0:F1} ms", m_positionClient.CurrentPositionUpdateFrequencyMS);
                    var targetPosition = m_positionClient.LastTargetPosition;
                    tbTargetPosition.Text = string.Format("Joints: {0:F2} {1:F2} {2:F2} {3:F2} {4:F2} {5:F2} Ext: {6:F2} {7:F2} {8:F2} X={9:F2} Y={10:F2} Z={11:F2} A={12:F2} B={13:F2} C={14:F2}", targetPosition.Joints[0], targetPosition.Joints[1], targetPosition.Joints[2], targetPosition.Joints[3], targetPosition.Joints[4], targetPosition.Joints[5], targetPosition.Joints[6], targetPosition.Joints[7], targetPosition.Joints[8], targetPosition.CartesianPosition.X, targetPosition.CartesianPosition.Y, targetPosition.CartesianPosition.Z, targetPosition.CartesianOrientation.X, targetPosition.CartesianOrientation.Y, targetPosition.CartesianOrientation.Z);
                    tbTargetPositionFrequency.Text = string.Format("{0:F1} ms", m_positionClient.TargetPositionUpdateFrequencyMS);

                    tbJogA1Pos.Text = m_positionClient.CurrentPosition.Joints[0].ToString("F2", CultureInfo.InvariantCulture);
                    tbJogA2Pos.Text = m_positionClient.CurrentPosition.Joints[1].ToString("F2", CultureInfo.InvariantCulture);
                    tbJogA3Pos.Text = m_positionClient.CurrentPosition.Joints[2].ToString("F2", CultureInfo.InvariantCulture);
                    tbJogA4Pos.Text = m_positionClient.CurrentPosition.Joints[3].ToString("F2", CultureInfo.InvariantCulture);
                    tbJogA5Pos.Text = m_positionClient.CurrentPosition.Joints[4].ToString("F2", CultureInfo.InvariantCulture);
                    tbJogA6Pos.Text = m_positionClient.CurrentPosition.Joints[5].ToString("F2", CultureInfo.InvariantCulture);
                    tbJogE1Pos.Text = m_positionClient.CurrentPosition.Joints[6].ToString("F2", CultureInfo.InvariantCulture);
                    tbJogE2Pos.Text = m_positionClient.CurrentPosition.Joints[7].ToString("F2", CultureInfo.InvariantCulture);
                    tbJogE3Pos.Text = m_positionClient.CurrentPosition.Joints[8].ToString("F2", CultureInfo.InvariantCulture);
                }
                else
                {
                    tbCurrentPosition.Text = "n/a";
                    tbCurrentPositionFrequency.Text = "n/a";
                    tbTargetPosition.Text = "n/a";
                    tbTargetPositionFrequency.Text = "n/a";

                    tbJogA1Pos.Text = "0";
                    tbJogA2Pos.Text = "0";
                    tbJogA3Pos.Text = "0";
                    tbJogA4Pos.Text = "0";
                    tbJogA5Pos.Text = "0";
                    tbJogA6Pos.Text = "0";
                    tbJogE1Pos.Text = "0";
                    tbJogE2Pos.Text = "0";
                    tbJogE3Pos.Text = "0";
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
                ResetJog(true);
                if (isConnected)
                {
                    bConnect.Visibility = Visibility.Collapsed;
                    bDisconnect.Visibility = Visibility.Visible;
                }
                else
                {
                    bConnect.Visibility = Visibility.Visible;
                    bDisconnect.Visibility = Visibility.Collapsed;
                    tbStatus.Text = "Not Connected";
                    if (m_criClient.IsRunning)
                    {
                        log.Info("Position interface disconnected, stopping CRI");
                        m_criClient.Stop();
                    }
                }
            }));
        }

        /// <summary>
        /// Is called when the CRI interface connection changed
        /// </summary>
        /// <param name="connected"></param>
        private void OnCRIClientConnectionChanged(bool isConnected)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ResetJog(true);
                if (isConnected)
                {
                    bConnect.Visibility = Visibility.Collapsed;
                    bDisconnect.Visibility = Visibility.Visible;
                    bReset.IsEnabled = true;
                    bEnable.IsEnabled = true;
                }
                else
                {
                    bConnect.Visibility = Visibility.Visible;
                    bDisconnect.Visibility = Visibility.Collapsed;
                    tbStatus.Text = "Not Connected";
                    if (m_positionClient.IsRunning)
                    {
                        log.Info("CRI disconnected, stopping position interface");
                        m_criClient.Stop();
                    }
                    bReset.IsEnabled = false;
                    bEnable.IsEnabled = false;
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
            if (!int.TryParse(tbInterval.Text, out int interval))
            {
                interval = 100;
                log.ErrorFormat("Could not parse interval, using default {0} ms", interval);
            }
            interval = Math.Max(interval, 1);
            if (!int.TryParse(tbCRIPort.Text, out int criPort))
            {
                criPort = CRIClient.CRIDefaultPort;
                log.ErrorFormat("Could not parse CRI port, using default {0}", criPort);
            }

            // Start CRI
            m_criClient.Start(address, criPort);

            // Wait until CRI is up, then connect the position interface
            Task.Factory.StartNew((() => {
                // wait till connected
                DateTime timestamp = DateTime.Now;
                while (!m_criClient.IsRunning)
                {
                    if (DateTime.Now - timestamp > TimeSpan.FromSeconds(2))
                    {
                        log.Info("CRI did not connect in time, aborting");
                        m_criClient.Stop();
                        return;
                    }
                    Task.Delay(100);
                }

                // wait till position interface is started
                timestamp = DateTime.Now;
                if (!m_criClient.IsPositionInterfaceRunning)
                {
                    m_criClient.SendConfigurePositionInterface(true);
                }
                while(!m_criClient.IsPositionInterfaceRunning)
                {
                    m_criClient.RequestGetPositionInterface();
                    if (DateTime.Now - timestamp > TimeSpan.FromSeconds(2))
                    {
                        log.Info("CRI did not start the position interface in time, aborting");
                        m_criClient.Stop();
                        return;
                    }
                    Task.Delay(100);
                }

                // wait till position interface is in use
                timestamp = DateTime.Now;
                if (!m_criClient.IsPositionInterfaceRunning)
                {
                    m_criClient.SendUsePositionInterface(true);
                }
                while (!m_criClient.IsPositionInterfaceActive)
                {
                    m_criClient.RequestGetPositionInterface();
                    if (DateTime.Now - timestamp > TimeSpan.FromSeconds(1))
                    {
                        log.Info("CRI did not activate the position interface in time, aborting");
                        m_criClient.Stop();
                        return;
                    }
                    Task.Delay(100);
                }

                // Start position client
                m_positionClient.Start(interval, address, m_criClient.PositionInterfacePort);
            }));
        }

        /// <summary>
        /// Button handler for disconnecting from a robot control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bDisconnect_Click(object sender, RoutedEventArgs e)
        {
            m_positionClient.Stop();
            m_criClient.Stop();
        }

        /// <summary>
        /// Button handler for resetting hardware errors
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bReset_Click(object sender, RoutedEventArgs e)
        {
            ResetJog(true);
            m_criClient.SendResetErrors();
        }

        /// <summary>
        /// Button handler for enabling the motors
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bEnable_Click(object sender, RoutedEventArgs e)
        {
            ResetJog(true);
            m_criClient.SendEnableMotors();
        }

        /// <summary>
        /// Selects jog as position source
        /// </summary>
        private void SelectJogSource()
        {
            log.Info("Using jog motion");
            ResetJog(true);
            m_csvMotion.Stop();
            m_positionClient.PositionSource = m_jogMotion;
        }

        /// <summary>
        /// Selects CSV as position source
        /// </summary>
        private void SelectCSVSource()
        {
            log.Info("Using CSV motion");
            ResetJog(true);
            m_csvMotion.Stop();
            m_positionClient.PositionSource = m_csvMotion;
        }

        /// <summary>
        /// Deselects the position source
        /// </summary>
        private void SelectNoSource()
        {
            log.Info("Disabling motion");
            ResetJog(true);
            m_csvMotion.Stop();
            m_positionClient.PositionSource = null;
        }

        /// <summary>
        /// Button handler for resetting the jog values to 0
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bJogReset_Click(object sender, RoutedEventArgs e)
        {
            ResetJog(false);
        }

        /// <summary>
        /// Resets all jog values to 0
        /// </summary>
        /// <param name="resetPosition">Set to true to set the current position. When in motion this may lead to a short backwards motion</param>
        private void ResetJog(bool resetPosition)
        {
            for (int i = 0; i < m_jogValues.Length; i++)
            {
                m_jogValues[i] = 0;
            }
            m_jogMotion.SetJog(m_jogValues);
            m_jogMotion.SetJog(m_jogValues);
            if (resetPosition)
            {
                m_jogMotion.SetJoints(m_positionClient.CurrentPosition.Joints);
            }
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
        /// Handles changes of the CSV file name text box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbCSVFileName_TextChanged(object sender, TextChangedEventArgs e)
        {
            m_csvMotion.Filename = tbCSVFileName.Text;
        }

        /// <summary>
        /// Button handler for starting motion from CSV
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bCSVStart_Click(object sender, RoutedEventArgs e)
        {
            m_csvMotion.Start();
        }

        /// <summary>
        /// Button handler for stopping motion from CSV
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bCSVStop_Click(object sender, RoutedEventArgs e)
        {
            m_csvMotion.Stop();
        }

        /// <summary>
        /// Handles the CSV repeat checkbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cbRepeat_Checked(object sender, RoutedEventArgs e)
        {
            m_csvMotion.Repeat = cbRepeat.IsChecked.GetValueOrDefault(false);
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
