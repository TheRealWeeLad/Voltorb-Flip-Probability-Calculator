using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Threading;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.PointOfService.Provider;
using Windows.UI.Core;
using Windows.Foundation.Collections;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Capture;
using Windows.Devices.Enumeration;
using Windows.Devices.Display;

using Voltorb_Flip.Calculator;
using System.Drawing.Imaging;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Voltorb_Flip
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        // Constants
        const int CARD_SIZE = 88;
        readonly Uri HIDDEN_URI = new("ms-appx:///Assets/card-hidden.png");

        readonly ProbabilityCalculator calculator;

        class TaskCanceler
        {
            public bool canceled = false;
            public void Cancel() => canceled = true;
            public void Reset() => canceled = false;
        }
        readonly TaskCanceler AnimateCanceler = new();

        public MainWindow()
        {
            InitializeComponent();
            // Initialize Title Bar
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            AppTitleBarText.Text = "Voltorb Flip Probability Calculator";

            // Initialize Calculator
            calculator = new(this);

            // Initialize an Empty 5x5 Board
            UpdateBoard();

            // Disable Calibrate Button if Screen Capture not supported
            if (!GraphicsCaptureSession.IsSupported())
            {
                CalibrateButton.IsEnabled = false;
                CalibrateButton.Content = "Screen Capture Not Supported";
            }
        }

        public void UpdateBoard()
        {
            ushort[][] board = calculator.GameBoard;

            // Initialize Board with 5 rows and 5 columns
            for (int r = 1; r <= 6; r++)
            {
                // Find Correct Row Element
                Grid row = GridObj.FindName("Row" + r) as Grid ?? throw new Exception(string.Format("Row{0} not found", r));
                // Reset children if there are any
                if (row.Children.Count > 0) row.Children.Clear();

                for (int c = 0; c <= 5; c++)
                {
                    if (c == 5 && r == 6) continue; // Leave bottom-right corner blank

                    // Fill 5x5 square with unflipped cards
                    // Fill last row and column with voltorb indicators
                    Uri sourceUri;
                    if (c < 5 && r < 6)
                    {
                        if (board[r - 1][c] == 0) sourceUri = HIDDEN_URI;
                        else sourceUri = new(string.Format("ms-appx:///Assets/flipped-{0}-highres", board[r - 1][c]));
                    } 
                    else
                    {
                        // Index voltorb images by the row/column
                        int voltorbIdx = c == 5 ? r : c + 6;
                        sourceUri = new Uri(string.Format("ms-appx:///Assets/voltorb{0}.png", voltorbIdx));
                    }

                    Canvas canvas = new();
                    Microsoft.UI.Xaml.Controls.Image hiddenCardImg = new()
                    {
                        Margin = new Thickness(CARD_SIZE / 2, 0, 0, CARD_SIZE / 2),
                        Width = CARD_SIZE,
                        Height = CARD_SIZE,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Source = new BitmapImage(sourceUri)
                    };
                    canvas.Children.Add(hiddenCardImg);
                    Grid.SetColumn(canvas, c);

                    row.Children.Add(canvas);
                }
            }
        }

        // UI Thread
        void Calibrate(object sender, RoutedEventArgs e)
        {
            // Disable button until calibration is done
            CalibrateButton.IsEnabled = false;

            // Animate Text Box while Calibrating
            AnimateCanceler.Reset();
            Task.Run(() => { CalibrateText(AnimateCanceler); } );

            Task.Run(CalibrationLoop);
        }

        // Background Thread
        async Task CalibrationLoop()
        {
            uint time = 0;
            Task<bool> checkingTask = null;
            bool taskCompleted = false;
            bool found = false;
            while (time < 10000 && !taskCompleted && !found) // Give up after 10 seconds
            {
                if (checkingTask == null || checkingTask.IsCompleted)
                {
                    // Run Screen Capture on UI Thread
                    Bitmap screenBitmap = await CaptureScreen();

                    // Check screen capture to determine whether game is open
                    checkingTask = Task.Run(() => calculator.CheckForGameOpen(screenBitmap));
                }

                taskCompleted = checkingTask.IsCompleted;
                found = checkingTask.Result;
                Thread.Sleep(500); // Only try to capture screen every 0.5 seconds for performace
                time += 500;
            }

            EndCalibration();
        }

        // Background Thread
        async Task<Bitmap> CaptureScreen()
        {
            // Find info about main monitor
            DeviceInformationCollection displayList = await DeviceInformation
                .FindAllAsync(DisplayMonitor.GetDeviceSelector());
            DisplayMonitor monitorInfo = await DisplayMonitor.FromInterfaceIdAsync(displayList[0].Id);
            // Get Height and Width of Monitor
            int screenHeight = monitorInfo.NativeResolutionInRawPixels.Height;
            int screenWidth = monitorInfo.NativeResolutionInRawPixels.Width;

            // Copy Bitmap From Screen using System.Drawing.Graphics
            Bitmap bitmap = new(screenWidth, screenHeight);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(Point.Empty, Point.Empty, bitmap.Size, CopyPixelOperation.SourceCopy);

            return bitmap;
        }

        // Background Thread
        public void EndCalibration()
        {
            // Cancel Text Animation
            AnimateCanceler.Cancel();
            // Reenable Button and Revert Text
            DispatcherQueue.TryEnqueue(() =>
            {
                CalibrateButton.Content = "Calibrate";
                CalibrateButton.IsEnabled = true;
            });
        }

        // Background Thread
        void CalibrateText(TaskCanceler Canceler)
        {
            string dots = "";
            while (!Canceler.canceled)
            {
                int numDots = (dots.Length + 1) % 4;
                dots = "";
                for (int i = 0; i < numDots; i++) dots += ".";
                DispatcherQueue?.TryEnqueue(() => { CalibrateButton.Content = "Calibrating" + dots; });

                Thread.Sleep(200);
            }
        }

        // DEBUG
        public void DebugLog(object msg)
        {
            DebugText.Text = msg.ToString();
        }
    }
}
