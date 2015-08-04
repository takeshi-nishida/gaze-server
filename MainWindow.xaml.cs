using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Tobii.EyeTracking.IO;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GazeServer
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private SuperWebSocket.WebSocketServer server;
        private ConcurrentBag<SuperWebSocket.WebSocketSession> sessions;
        private List<EyeTrackerInfo> eyeTrackers;
        private CalibrationRunner calibrationRunner;
        private IEyeTracker tracker;

        public MainWindow()
        {
            InitializeComponent();
            InitializeEyeTracking();
            sessions = new ConcurrentBag<SuperWebSocket.WebSocketSession>();
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            finishEmulation();
            stopServer();
        }

        #region Server

        private void startServerButton_Click(object sender, RoutedEventArgs e)
        {
            int port;
            if (!Int32.TryParse(portTextBox.Text, out port)) port = 10811;
            server = new SuperWebSocket.WebSocketServer();
//            server.Setup(port);
            server.Setup(buildServerConfig(port));
            server.NewSessionConnected += new SuperSocket.SocketBase.SessionHandler<SuperWebSocket.WebSocketSession>(server_NewSessionConnected);
            server.Start();
        }

        private SuperSocket.SocketBase.Config.ServerConfig buildServerConfig(int port)
        {
            string pathToBaseDir = System.IO.Directory.GetParent(Environment.CurrentDirectory).Parent.FullName;
            var cert = new SuperSocket.SocketBase.Config.CertificateConfig()
            {
                FilePath = "localhost.pfx", //System.IO.Path.Combine(pathToBaseDir, "localhost.pfx"),
                Password = "localWS"
            };
            var config = new SuperSocket.SocketBase.Config.ServerConfig()
            {
                Port = port,
                Ip = "Any",
                MaxConnectionNumber = 100,
                Mode = SuperSocket.SocketBase.SocketMode.Tcp,
                Name = "GazeServer",
                Security = "tls",
                Certificate = cert
            };
            return config;
        }

        void server_NewSessionConnected(SuperWebSocket.WebSocketSession session)
        {
            sessions.Add(session);
        }

        private void broadcast(string data)
        {
            Parallel.ForEach(sessions, s => s.Send(data));
        }

        private void stopServer()
        {
            if(server != null) server.Stop();
        }

        #endregion

        #region Eye tracking

        private void InitializeEyeTracking()
        {
            Library.Init();
            eyeTrackers = new List<EyeTrackerInfo>();
            eyeTrackerSelect.ItemsSource = eyeTrackers;
            EyeTrackerBrowser browser = new EyeTrackerBrowser();
            browser.EyeTrackerFound += new EventHandler<EyeTrackerInfoEventArgs>(browser_EyeTrackerFound);
            browser.EyeTrackerRemoved += new EventHandler<EyeTrackerInfoEventArgs>(browser_EyeTrackerRemoved);
            browser.StartBrowsing();
            calibrationRunner = new CalibrationRunner();
        }

        private void connectButton_Click(object sender, RoutedEventArgs e)
        {
            if (eyeTrackerSelect.SelectedItem != null)
            {
                EyeTrackerInfo info = eyeTrackerSelect.SelectedItem as EyeTrackerInfo;
                tracker = info.Factory.CreateEyeTracker(EventThreadingOptions.BackgroundThread);
                tracker.RealTimeGazeData = true;
                tracker.GazeDataReceived += new EventHandler<GazeDataEventArgs>(tracker_GazeDataReceived);
                calibrateButton.IsEnabled = true;
                startButton.IsEnabled = true;
            }
        }

        private void calibrateButton_Click(object sender, RoutedEventArgs e)
        {
            if (tracker != null)
            {
                try
                {
                    var result = calibrationRunner.RunCalibration(tracker);
                    if (result != null)
                    {
                        // TODO: show results
                    }
                    else
                    {
                        MessageBox.Show("Failed to calibrate.", "Calibration Failed");
                    }
                }
                catch (EyeTrackerException ee)
                {
                    MessageBox.Show("Failed to calibrate. Got exception " + ee, "Calibration Failed");
                }
            }
        }

        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            if (tracker != null)
            {
                tracker.StartTracking();
                calibrateButton.IsEnabled = false;
                startEmulationButton.IsEnabled = false;
            }
        }

        private void startEmulationButton_Click(object sender, RoutedEventArgs e)
        {
            startEmulation();
            startButton.IsEnabled = false;
            startEmulationButton.IsEnabled = false;
        }

        void browser_EyeTrackerFound(object sender, EyeTrackerInfoEventArgs e)
        {
            eyeTrackers.Add(e.EyeTrackerInfo);
            eyeTrackerSelect.SelectedItem = e.EyeTrackerInfo;
        }

        void browser_EyeTrackerRemoved(object sender, EyeTrackerInfoEventArgs e)
        {
            eyeTrackers.Remove(e.EyeTrackerInfo);
        }

        void tracker_GazeDataReceived(object sender, GazeDataEventArgs e)
        {
            Dispatcher.Invoke(new Action(() => updateTrackingStatus(e)));
            //            Point2D p = e.GazeDataItem.LeftGazePoint2D;
            broadcast(JsonConvert.SerializeObject(e.GazeDataItem));
        }

        void updateTrackingStatus(GazeDataEventArgs e)
        {
            statusLeft.Fill = new SolidColorBrush(e.GazeDataItem.LeftValidity < 2 ? Colors.LightGreen : Colors.Red);
            statusRight.Fill = new SolidColorBrush(e.GazeDataItem.RightValidity < 2 ? Colors.LightGreen : Colors.Red);
        }
        #endregion

        #region Mouse Hook
        static MouseHookAPI.HookProcedureDelegate mouse_proc;

        public IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                MouseHookInfo.MouseHookStruct data = (MouseHookInfo.MouseHookStruct)Marshal.PtrToStructure(lParam, typeof(MouseHookInfo.MouseHookStruct));
                switch ((int)wParam)
                {
                    case MouseHookInfo.WM_LBUTTONDOWN:
                        {
                            break;
                        }
                    case MouseHookInfo.WM_LBUTTONUP:
                        {
                            break;
                        }
                }
            }
            return MouseHookAPI.CallNextHookEx(MouseHookInfo.hHook, nCode, wParam, lParam);
        }

        void startEmulation()
        {
            using (Process process = Process.GetCurrentProcess())
            using (ProcessModule module = process.MainModule)
            {
                MouseHookInfo.hHook = MouseHookAPI.SetWindowsHookEx(
                MouseHookInfo.WH_MOUSE_LL,
                mouse_proc = new MouseHookAPI.HookProcedureDelegate(MouseHookProc),
                MouseHookAPI.GetModuleHandle(module.ModuleName), 0);
            }
            if (MouseHookInfo.hHook == IntPtr.Zero)
                MessageBox.Show("SetWindowsHookEx Failed.");
        }

        void finishEmulation()
        {
            if (MouseHookAPI.UnhookWindowsHookEx(MouseHookInfo.hHook) == false)
                MessageBox.Show("UnhookWindowsHookEx Failed.");
        }
        #endregion
    }
}
