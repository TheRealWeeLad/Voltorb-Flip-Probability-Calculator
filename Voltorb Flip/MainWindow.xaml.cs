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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.PointOfService.Provider;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.Foundation.Collections;
using System.Threading.Tasks;
using Voltorb_Flip.Calculator;
using Windows.ApplicationModel.Core;

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

            // Initialize Empty Board with 5 rows and 5 columns
            for (int r = 1; r <= 6; r++)
            {
                // Find Correct Row Element
                Grid row = GridObj.FindName("Row" + r) as Grid ?? throw new Exception(string.Format("Row{0} not found", r));

                for (int c = 0; c <= 5; c++)
                {
                    if (c == 5 && r == 6) continue; // Leave bottom-right corner blank

                    // Fill 5x5 square with unflipped cards
                    // Fill last row and column with voltorb indicators
                    Uri sourceUri;
                    if (c < 5 && r < 6) sourceUri = new Uri("ms-appx:///Assets/card-hidden.png");
                    else
                    {
                        // Index voltorb images by the row/column
                        int voltorbIdx = c == 5 ? r : c + 6;
                        sourceUri = new Uri(string.Format("ms-appx:///Assets/voltorb{0}.png", voltorbIdx));
                    }

                    Canvas canvas = new();
                    Image hiddenCardImg = new()
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

        void Calibrate(object sender, RoutedEventArgs e)
        {
            // Disable button until calibration is done
            CalibrateButton.IsEnabled = false;

            // Animate Text Box while Calibrating
            AnimateCanceler.Reset();
            Task.Factory.StartNew(() => { CalibrateText(AnimateCanceler); } );

            Task.Factory.StartNew(calculator.Calibrate);
        }

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

        void CalibrateText(TaskCanceler Canceler)
        {
            string dots = "";
            while (!Canceler.canceled)
            {
                int numDots = (dots.Length + 1) % 4;
                dots = "";
                for (int i = 0; i < numDots; i++) dots += ".";
                DispatcherQueue.TryEnqueue(() => { CalibrateButton.Content = "Calibrating" + dots; });

                Thread.Sleep(200);
            }
        }
    }
}
