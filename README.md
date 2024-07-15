# Position Interface Demo Client

This is the demonstration and test client for the fast position interface of robots based on the igus Robot Control (iRC) or Commonplace Robotics' RobotControl Core. This interface is supported in V14-003 and newer.

The fast position interface is intended for streaming current and target positions via a text based TCP/IP interface. It is intended as an extension of the CRI interface which is needed for control commands like enabling the motors and selecting the position interface as position source. The CRI interface provides commands ("CMD Move" and "PROG") for point-to-point motion.

This client is intended as an example. You may use it as a base for your own applications but be aware that functions like error checking or code quality may not be sufficient for production applications.

Please read the [summary article](https://wiki.cpr-robots.com/index.php/Position_Interface) and the [user guide and protocol definition](https://wiki.cpr-robots.com/images/7/70/CPR_PositionInterface_V1.pdf) on how to implement your own client.

## Using the Position Interface Client
1. Download the client binary from the Releases section on Github.
2. Start the Position Interface Client
3. Enter the IP address and CRI port number of your robot (standard IP of a real robot: 192.168.3.11, CRI port of a real robot: 3920, simulation: 3921-3931).
4. You may change the interval, this is the frequency at which the client sends position updates. We recommend a value between 10-50ms. Most robots run at 20ms, some (e.g. igus Rebel, delta) at 10ms, the client may be slower, there is no benefit in being faster.
5. Click "Connect". The client automatically requests CRI active state, meaning all other connected clients (e.g. the iRC PC software) will become passive observers.
6. Click "Enable" to enable the motors
7. Check the Status box, confirm the client is connected. "Current Position" and "Target Position" should show values.
8. You may now use the buttons in the Jog and CSV tabs.
9. Click "Reset" to quickly disable the motors and reset the motion generation

## Jog motion
The jog motion takes the received current position, adds a delta value (click the buttons to increase velocity) and sends it as target position.

## CSV motion
Be careful with this mode. It is intended for testing simple automatic paths. The client does not calculate a motion from the robot's current position to the starting position of the CSV path so the robot may move very quickly or stop with a velocity limit exceeded error.

Take a look at the example XSLX file in CSVExamples. A row starts with C for cartesian motion or J for joint motion. The following columns are 6 robot joints, 3 external joints, XYZ and ABC. All values in mm/s or degrees/s. Further columns are ignored, the example file uses these for calculating a sine motion for an igus Rebel robot.

## Custom motion generator
To add your own motion source into the Position Interface Client implement IPositionSource and integrate it in MainWindow.xaml.cs.
