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
using System.Windows.Shapes;
using Tobii.EyeTracking.IO;

namespace GazeServer
{
    /// <summary>
    /// CalibrationWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class CalibrationWindow : Window
    {
        public CalibrationWindow()
        {
            InitializeComponent();
        }

        private Point2D cp;

        public Point2D CalibrationPoint
        {
            get { return cp;  }
            set {
                cp = value;
                if (calibationPoint.Dispatcher.CheckAccess())
                {
                    updateLocation();
                }
                else
                {
                    calibationPoint.Dispatcher.BeginInvoke(new Action(() => updateLocation()));
                }
            }
        }

        private void updateLocation()
        {
            double x = this.Width * cp.X - calibationPoint.Width / 2;
            double y = this.Height * cp.Y - calibationPoint.Height / 2;
            calibationPoint.Margin = new Thickness(x, y, 0, 0);
            InvalidateArrange();
        }
    }
}
