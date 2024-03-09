using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Threading;
using System.Drawing;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Graphics.Capture;
using Windows.Devices.Enumeration;
using Windows.Devices.Display;

using Voltorb_Flip.Calculator;
using Microsoft.UI;
using Windows.ApplicationModel;

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
        const int CARD_BORDER_THICKNESS = 5;
        readonly BitmapImage HIDDEN_IMAGE = new(new Uri("ms-appx:///Assets/card-hidden.png"));
        readonly BitmapImage VOLTORB_IMAGE = new(new Uri("ms-appx:///Assets/voltorb.png"));
        readonly Thickness HUNDRED_MARGIN = new(6, 14, 0, 0);
        readonly Thickness TWO_DIGIT_MARGIN = new(15, 14, 0, 0);
        readonly Thickness ONE_DIGIT_MARGIN = new(25, 14, 0, 0);

        ProbabilityCalculator calculator;
        public readonly BitmapImage[] voltorbImages;
        readonly List<List<Canvas>> cardCanvases = new();

        bool _calibrated = false;

        enum Safety { Safe, Unsafe, HighestSafety };

        class TaskCanceler
        {
            public bool canceled = false;
            public void Cancel() => canceled = true;
            public void Reset() => canceled = false;
        }
        readonly TaskCanceler AnimateCanceler = new();
        readonly TaskCanceler GameLoopCanceler = new();

        public MainWindow()
        {
            InitializeComponent();
            // Initialize Title Bar
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            AppTitleBarText.Text = "Voltorb Flip Solver";
            AppWindow.SetIcon("Assets/voltorb.ico");
            AppWindow.Title = "Voltorb Flip Solver";

            // Initialize Calculator
            calculator = new(this);

            // Initialize Voltorb Images
            voltorbImages = new BitmapImage[10];
            for (int i = 1; i <= 10; i++)
                voltorbImages[i - 1] = new(new Uri(string.Format("ms-appx:///Assets/voltorb{0}.png", i)));

            // Initialize an Empty 5x5 Board
            UpdateBoard();

            // Disable Calibrate Button if Screen Capture not supported
            if (!GraphicsCaptureSession.IsSupported())
            {
                CalibrateButton.IsEnabled = false;
                CalibrateButton.Content = "Screen Capture Not Supported";
            }
        }

        // Update onscreen board with internal game state values
        public void UpdateBoard()
        {
            byte[,] board = calculator.GameBoard;

            // Reset list of card images
            if (cardCanvases.Count > 0) cardCanvases.Clear();

            // Initialize Board with 5 rows and 5 columns
            for (int r = 1; r <= 6; r++)
            {
                // Find Correct Row Element
                Grid row = GridObj.FindName("Row" + r) as Grid ?? throw new Exception(string.Format("Row{0} not found", r));
                // Reset children if there are any
                if (row.Children.Count > 0) row.Children.Clear();
                cardCanvases.Add(new());

                for (int c = 0; c <= 5; c++)
                {
                    if (c == 5 && r == 6) continue; // Leave bottom-right corner blank

                    // Fill 5x5 square with unflipped cards
                    // Fill last row and column with voltorb indicators
                    BitmapImage sourceImage;
                    if (c < 5 && r < 6)
                    {
                        if (board[r - 1, c] == 4 || board[r - 1, c] == 0)
                            sourceImage = HIDDEN_IMAGE;
                        else
                            sourceImage = new(new Uri(string.Format("ms-appx:///Assets/flipped-{0}-highres.png", board[r - 1, c])));
                    } 
                    else
                    {
                        // Index voltorb images by the row/column
                        int voltorbIdx = c == 5 ? r - 1 : c + 5;
                        sourceImage = voltorbImages[voltorbIdx];
                    }

                    Canvas canvas = new()
                    {
                        Margin = new Thickness(CARD_SIZE / 2, 0, 0, CARD_SIZE / 2)
                    };
                    Microsoft.UI.Xaml.Controls.Image hiddenCardImg = new()
                    {
                        Width = CARD_SIZE,
                        Height = CARD_SIZE,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Source = sourceImage
                    };
                    Border border = new()
                    {
                        Child = hiddenCardImg,
                        BorderThickness = new(CARD_BORDER_THICKNESS)
                    };
                    canvas.Children.Add(border);
                    if (c < 5 && r < 6)
                    {
                        // Add Darkening overlay
                        Microsoft.UI.Xaml.Shapes.Rectangle darkRect = new()
                        {
                            Fill = new SolidColorBrush(Colors.Black),
                            Opacity = 0.5,
                            Width = CARD_SIZE,
                            Height = CARD_SIZE,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new(CARD_BORDER_THICKNESS, CARD_BORDER_THICKNESS, 0, 0)
                        };
                        canvas.Children.Add(darkRect);
                        // Add numbers to corners for possibilities and center for probabilities
                        Canvas numberContainer = new();
                        TextBlock probText = new()
                        {
                            FontSize = 30,
                            Foreground = new SolidColorBrush(Colors.Lime)
                        };
                        Border probBorder = new()
                        {
                            Child = probText,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = HUNDRED_MARGIN
                        };
                        canvas.Children.Add(probBorder);

                        Thickness[] margins = { new(CARD_SIZE - 20, CARD_SIZE - 35, 0, 0), new(13, 5, 0, 0),
                            new(CARD_SIZE - 20, 5, 0, 0), new(13, CARD_SIZE - 35, 0, 0) };
                        for (int i = 0; i < 4; i++)
                        {
                            SolidColorBrush textColor = i == 0 ? new(Colors.Red)
                                : new(Colors.LightBlue);
                            TextBlock text = new()
                            {
                                FontSize = 25,
                                Foreground = textColor
                            };
                            Border textBorder = new()
                            {
                                Child = text,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Top,
                                Margin = margins[i]
                            };
                            numberContainer.Children.Add(textBorder);
                        }
                        canvas.Children.Add(numberContainer);
                    }
                    
                    Grid.SetColumn(canvas, c);
                    cardCanvases[r - 1].Add(canvas);
                    row.Children.Add(canvas);
                }
            }
        }

        // Update onscreen board with calculated values
        public void UpdateCalculations()
        {
            // Find highest probability and put a border around it
            float highestProb = 0;
            Canvas highestCanvas = cardCanvases[0][0];
            bool safeExists = false;
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    Canvas cardCanvas = cardCanvases[i][j];

                    if (calculator.GameBoard[i, j] != 4)
                    {
                        /*// If we already have this square flipped, get rid of
                        // borders, possibilities and probabilities
                        ((cardCanvas.Children[2] as Border).Child
                            as TextBlock).Text = "";

                        Canvas possibilityContainer = cardCanvas.Children[3] as Canvas;
                        for (int n = 0; n < 4; n++)
                        {
                            ((possibilityContainer.Children[n] as Border).Child as
                                TextBlock).Text = "";
                        }*/

                        RemoveBorder(cardCanvas);

                        continue;
                    }

                    List<byte> possibleVals = calculator.PossibleValues[i, j];

                    /*// Display Probability of Safety
                    float probability = calculator.Probabilities[i, j];
                    // Only consider cards with 2s or 3s worthwile to click
                    if (probability > highestProb && (possibleVals.Contains(2) || possibleVals.Contains(3)))
                    {
                        highestProb = probability;
                        highestCanvas = cardCanvas;
                    }
                    TextBlock probabilityText = (cardCanvas.Children[2]
                        as Border).Child as TextBlock;
                    byte roundedProbability = (byte)(probability * 100 + 0.5f);
                    // Never round to 100
                    if (roundedProbability == 100 && probability < 1)
                        roundedProbability -= 1;

                    // Set margins
                    Thickness probMargin = HUNDRED_MARGIN;
                    if (roundedProbability < 10) probMargin = ONE_DIGIT_MARGIN;
                    else if (roundedProbability < 100) probMargin = TWO_DIGIT_MARGIN;

                    // Make color gradient from 0 to 100
                    byte red = (byte)(0xff * (1 - MathF.Max(0, 2 * (probability - 0.5f))));
                    byte green = (byte)(0xff * MathF.Min(1, 2 * probability));
                    Windows.UI.Color color = Windows.UI.Color.FromArgb(255, red, green, 0);

                    probabilityText.Text = roundedProbability.ToString() + "%";
                    probabilityText.Margin = probMargin;
                    probabilityText.Foreground = new SolidColorBrush(color);*/

                    // Display all possibilities
                    if (!possibleVals.Contains(0))
                    {
                        // Add border to card image to signify that it is safe
                        AddBorder(cardCanvas, Safety.Safe);
                        safeExists = true;
                    }
                    else if (possibleVals.Count == 1 && possibleVals[0] == 0)
                    {
                        // Add border to signify that it is NOT safe
                        AddBorder(cardCanvas, Safety.Unsafe);
                        // Replace Card image with Voltorb
                        ((cardCanvas.Children[0] as Border).Child as
                            Microsoft.UI.Xaml.Controls.Image).Source = VOLTORB_IMAGE;
                        continue;
                    }

                    // Display Possible Values in Square
                    Canvas numContainer = cardCanvas.Children[3] as Canvas;
                    for (byte n = 0; n < 4; n++)
                    {
                        TextBlock numText = (numContainer.Children[n] as Border).Child as TextBlock;
                        if (possibleVals.Contains(n))
                            numText.Text = n == 0 ? "V" : n.ToString();
                    }
                }
            }

            /*// Add Border to highest probability if there is no completely safe option
            if (!safeExists) AddBorder(highestCanvas, Safety.HighestSafety);*/
        }

        // Adds a border to the specified card to signify that it is either safe or unsafe
        static void AddBorder(Canvas cardCanvas, Safety safety)
        {
            Border cardBorder = cardCanvas.Children[0] as Border;

            Windows.UI.Color borderColor = safety switch
            {
                Safety.Safe => Colors.LimeGreen,
                Safety.Unsafe => Colors.Red,
                Safety.HighestSafety => Colors.Yellow,
                _ => throw new ArgumentOutOfRangeException(nameof(safety), "Safety not recognized")
            };
            cardBorder.BorderBrush = new SolidColorBrush(borderColor);
        }
        // Removes a border from the specified card
        static void RemoveBorder(Canvas cardCanvas)
        {
            Border cardBorder = cardCanvas.Children[0] as Border;
            cardBorder.BorderBrush = null;
        }

        // UI Thread
        void Calibrate(object sender, RoutedEventArgs e)
        {
            // Disable button until calibration is done
            CalibrateButton.IsEnabled = false;

            // Cancel any currently running game
            GameLoopCanceler.Cancel();

            // Initialize New Calculator
            calculator = new(this);

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
            // Give up after 10 seconds or when game is found
            while ((time < 10000 || !taskCompleted) && !found)
            {
                if (checkingTask == null || taskCompleted)
                {
                    // Run Screen Capture on UI Thread
                    Bitmap screenBitmap = await CaptureScreen();

                    // Check screen capture to determine whether game is open
                    checkingTask = Task.Run(() => calculator.CheckForGameOpen(screenBitmap));
                }

                taskCompleted = checkingTask.IsCompleted;
                if (taskCompleted) found = checkingTask.Result;
                Thread.Sleep(500); // Only try to capture screen every 0.5 seconds for performace
                time += 500;
            }

            EndCalibration(found);
        }

        // Background Thread
        static async Task<Bitmap> CaptureScreen()
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
        void EndCalibration(bool foundGame)
        {
            // Cancel Text Animation
            AnimateCanceler.Cancel();
            DispatcherQueue.TryEnqueue(() =>
            {
                // Reenable Button and Revert Text
                CalibrateButton.Content = "Calibrate";
                CalibrateButton.IsEnabled = true;

                // Start Game Loop If Game Was Found
                if (foundGame)
                {
                    _calibrated = true;

                    // Enable Capture Button if Continuous Capture is Unchecked
                    if (!(bool)CaptureModeButton.IsChecked)
                        CaptureButton.IsEnabled = true;
                    else
                    {
                        // Otherwise Begin Game Loop
                        GameLoopCanceler.Reset();
                        Task.Run(() => GameLoop(GameLoopCanceler));
                    }
                }
            });
        }

        // Background Thread
        void GameLoop(TaskCanceler Canceler)
        {
            while (!Canceler.canceled)
            {
                CaptureAndScan();

                Thread.Sleep(1000); // Recheck screen every second
            }
        }

        // Take Screenshot and Scan Board
        async void CaptureAndScan()
        {
            Bitmap screenBitmap = await CaptureScreen();
            calculator.ResetBoard();
            calculator.ScanBoard(screenBitmap);
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

        // Button Handlers
        public void EnableCaptureButton(object sender, RoutedEventArgs e)
        {
            if (_calibrated)
                CaptureButton.IsEnabled = true;
            // Stop Game Loop Because Continuous Capture Has Been Disabled
            GameLoopCanceler.Cancel();
        } 
        public void DisableCaptureButton(object sender, RoutedEventArgs e)
        {
            if (_calibrated)
                CaptureButton.IsEnabled = false;
            // Begin Continuous Game Loop
            GameLoopCanceler.Reset();
            Task.Run(() => GameLoop(GameLoopCanceler));
        }
        public void TakeCapture(object sender, RoutedEventArgs e) => CaptureAndScan();
        public void UpdateLevel(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            if (item != null)
            {
                LevelDropdown.Content = item.Text;
                ProbabilityCalculator.CurrentLevel = int.Parse(item.Text);
            }
        }
        public void RaiseLevel(object sender, RoutedEventArgs e)
        {
            if (ProbabilityCalculator.CurrentLevel >= 8) return; // Level can't go past 8
            int newLevel = ++ProbabilityCalculator.CurrentLevel;
            LevelDropdown.Content = newLevel.ToString();
        }

        /*// DEBUG
        public void DebugLog(object msg)
        {
            DebugText.Text = msg.ToString();
        }
        public void DebugImage(BitmapImage image)
        {
            DebugImg.Source = image;
        }*/
    }
}
