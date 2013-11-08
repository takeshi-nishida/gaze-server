using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Tobii.EyeTracking.IO;

namespace GazeServer
{
    public class CalibrationRunner
    {
        private readonly CalibrationWindow _calibrationWindow;
        private readonly DispatcherTimer _sleepTimer;
        private IEyeTracker _tracker;
        private Queue<Point2D> _calibrationPoints;
        private Calibration _calibrationResult;

        public CalibrationRunner()
        {
            _sleepTimer = new DispatcherTimer();
            _sleepTimer.Interval = TimeSpan.FromMilliseconds(1000);
            _sleepTimer.Tick += HandleTimerTick;

            _calibrationWindow = new CalibrationWindow();
            _calibrationWindow.Loaded += CalibrationFormLoaded;
        }

        public Calibration RunCalibration(IEyeTracker tracker)
        {
            CreatePointList();

            try
            {
                _tracker = tracker;
                _tracker.ConnectionError += HandleConnectionError;
                _tracker.ComputeCalibrationCompleted += ComputeCompleted;
                _tracker.AddCalibrationPointCompleted += PointCompleted;

                _tracker.StartCalibration();
                _calibrationWindow.ShowDialog();
            }
            finally
            {
                _tracker.StopCalibration();
                _tracker.ConnectionError -= HandleConnectionError;
                _tracker.ComputeCalibrationCompleted -= ComputeCompleted;
                _tracker.AddCalibrationPointCompleted -= PointCompleted;
                _tracker = null;
            }

            return _calibrationResult;
        }

        private void StartNextOrFinish()
        {
            if (_calibrationPoints.Count > 0)
            {
                var point = _calibrationPoints.Dequeue();
                _calibrationWindow.CalibrationPoint = point;
                _sleepTimer.Start();
            }
            else
            {
                _tracker.ComputeCalibrationAsync();
            }
        }

        private void HandleTimerTick(object sender, EventArgs e)
        {
            _sleepTimer.Stop();
            var point = _calibrationWindow.CalibrationPoint;
            _tracker.AddCalibrationPointAsync(point);
        }

        private void PointCompleted(object sender, AsyncCompletedEventArgs e)
        {
            StartNextOrFinish();
        }

        private void ComputeCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (_calibrationWindow.Dispatcher.CheckAccess())
            {
                _calibrationWindow.Close();
            }
            else
            {
                _calibrationWindow.Dispatcher.BeginInvoke(new Action(() => _calibrationWindow.Close()));
            }

            if (e.Error != null)
            {
                _calibrationResult = null;
            }
            else
            {
                _calibrationResult = _tracker.GetCalibration();
            }
        }

        private void CreatePointList()
        {
            _calibrationPoints = new Queue<Point2D>();
            _calibrationPoints.Enqueue(new Point2D(0.1, 0.1));
            _calibrationPoints.Enqueue(new Point2D(0.5, 0.5));
            _calibrationPoints.Enqueue(new Point2D(0.9, 0.1));
            _calibrationPoints.Enqueue(new Point2D(0.9, 0.9));
            _calibrationPoints.Enqueue(new Point2D(0.1, 0.9));
        }

        private void CalibrationFormLoaded(object sender, EventArgs e)
        {
            StartNextOrFinish();
        }

        private void HandleConnectionError(object sender, ConnectionErrorEventArgs e)
        {
            AbortCalibration();
        }

        private void AbortCalibration()
        {
            _calibrationResult = null;
            _sleepTimer.Stop();
            _calibrationWindow.Close();
        }
    }
}
