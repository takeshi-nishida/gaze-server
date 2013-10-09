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

namespace GazeServer
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private ConcurrentBag<SuperWebSocket.WebSocketSession> sessions;
        private List<EyeTrackerInfo> eyeTrackers;
        private XConfiguration xconfig;

        public MainWindow()
        {
            InitializeComponent();
            InitializeEyeTracking();
            sessions = new ConcurrentBag<SuperWebSocket.WebSocketSession>();
        }

        #region Server

        private void startServerButton_Click(object sender, RoutedEventArgs e)
        {
            int port;
            if (!Int32.TryParse(portTextBox.Text, out port)) port = 10811;
            SuperWebSocket.WebSocketServer server = new SuperWebSocket.WebSocketServer();
            server.Setup(port);
            server.NewSessionConnected += new SuperSocket.SocketBase.SessionHandler<SuperWebSocket.WebSocketSession>(server_NewSessionConnected);
            server.Start();
        }

        void server_NewSessionConnected(SuperWebSocket.WebSocketSession session)
        {
            sessions.Add(session);
        }

        private void broadcast(string data)
        {
            Parallel.ForEach(sessions, s => s.Send(data));
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
        }

        private void startTrackingButton_Click(object sender, RoutedEventArgs e)
        {
            if (eyeTrackerSelect.SelectedItem != null)
            {
                EyeTrackerInfo info = eyeTrackerSelect.SelectedItem as EyeTrackerInfo;
                IEyeTracker tracker = info.Factory.CreateEyeTracker(EventThreadingOptions.BackgroundThread);
                xconfig = tracker.GetXConfiguration();
                tracker.RealTimeGazeData = true;
                tracker.GazeDataReceived += new EventHandler<GazeDataEventArgs>(tracker_GazeDataReceived);
                tracker.StartTracking();
            }
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

    }
}
