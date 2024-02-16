using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UITesting;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.AI.MachineLearning;
using Windows.Devices.Display;
using Windows.Devices.Enumeration;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.UI.Core;
using WinRT.Interop;

namespace Voltorb_Flip.Calculator
{
    partial class ProbabilityCalculator
    {
        readonly MainWindow window;

        readonly ColorDifference tolerance = new(15, 15, 15, 15);
        double imageScale = 1;
        const double IMAGE_MARGIN_FRACTION = (double)3 / 28;

        public ProbabilityCalculator(MainWindow window)
        {
            this.window = window;

            // Initialize top rows of selected and unselected top-left squares
            Rectangle selectedBounds = new(0, 0, topLeftSelected.Width, 1);
            topRowSelected = topLeftSelected.Clone(selectedBounds, topLeftSelected.PixelFormat);
            Rectangle unselectedBounds = new(0, 0, topLeftUnselected.Width, 1);
            topRowUnselected = topLeftUnselected.Clone(unselectedBounds, topLeftUnselected.PixelFormat);
        
            // Initialize all cards as hidden
            for (int i = 0; i < 5; i++)
            {
                GameBoard[i] = new ushort[5];
                for (int j = 0; j < 5; j++) GameBoard[i][j] = 0;
            }
        }

        public bool CheckForGameOpen(Bitmap screenBitmap)
        {
            // Compare screenshot with Top-Left square of reference images
            Color topLeftSelectedPixel = topLeftSelected.GetPixel(0, 0);
            Color topLeftUnselectedPixel = topLeftUnselected.GetPixel(0, 0);

            for (int i = 0; i < screenBitmap.Height; i++)
            {
                for (int j = 0; j < screenBitmap.Width; j++)
                {
                    // Search pixel by pixel for top left pixel of reference image
                    Color pixel = screenBitmap.GetPixel(j, i);
                    if (pixel.Similar(topLeftSelectedPixel, tolerance) ||
                        pixel.Similar(topLeftUnselectedPixel, tolerance)) {
                        // Verify that we have found the right spot
                        if (!VerifySurroundings(screenBitmap, j, i))
                        {
                            DebugLog("Surroundings Verified: " + j + ", " + i);
                            continue;
                        }
                        if (!VerifyTopRow(screenBitmap, j, i))
                        {
                            DebugLog("Top Row Verified: " + j + ", " + i);
                            continue;
                        }

                        // Final Verification
                        if (!VerifyWholeThing(screenBitmap, j, i))
                        {
                            DebugLog("Whole Thing Verified: " + j + ", " + i);
                            continue;
                        }

                        // Scan the board and update our virtual board
                        ScanBoard(screenBitmap, j, i);

                        DebugLog("Done: Found!");
                        return true;
                    }
                }
            }
            DebugLog("Done: Not Found");
            return false;
        }

        bool VerifySurroundings(Bitmap screen, int startX, int startY)
        {
            // Just check a few more pixels around the top-left corner
            //       GREEN 
            // GREEN CORNER WHITE/RED
            //       WHITE/RED
            // Corner should look like this ^
            // Only works if there is space around corner
            if (startX == 0 || startX == screen.Width - 1
                || startY == 0 || startY == screen.Height - 1) return true; // Skip Verification

            // Top and Left Pixels
            return screen.GetPixel(startX, startY - 1).Similar(topLeftSelected.GetPixel(0, 0), tolerance) &&
                screen.GetPixel(startX - 1, startY).Similar(topLeftSelected.GetPixel(0, 0), tolerance) &&
                // Bottom Pixel
                (screen.GetPixel(startX, startY + 1).Similar(topLeftSelected.GetPixel(1, 0), tolerance) ||
                screen.GetPixel(startX, startY + 1).Similar(topLeftUnselected.GetPixel(1, 0), tolerance)) &&
                // Right Pixel
                (screen.GetPixel(startX + 1, startY).Similar(topLeftSelected.GetPixel(1, 0), tolerance) ||
                screen.GetPixel(startX + 1, startY).Similar(topLeftUnselected.GetPixel(1, 0), tolerance));
        }

        bool VerifyTopRow(Bitmap screen, int startX, int startY)
        {
            for (int i = topLeftSelected.Width; i < screen.Width - startX; i++)
            {
                // Cut out top row of screen image
                Rectangle bounds = new(startX, startY, i, 1);
                using Bitmap top = screen.Clone(bounds, screen.PixelFormat);
                // Resize to reference size
                double scale = (double)i / topLeftSelected.Width;
                using Bitmap resizedTop = top.ResizeWidth(1.0 / scale); // Invert Scale b/c its backwards
                try
                {
                    // Compare to reference top row
                    if (ImageComparer.Compare(resizedTop, topRowSelected, tolerance) ||
                        ImageComparer.Compare(resizedTop, topRowUnselected, tolerance))
                    {
                        // Set Image Scale based on width of screen image
                        imageScale = scale;
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                    DebugLog("Invalid Size: " + resizedTop.Size);
                }
            }
            return false;
        }

        bool VerifyWholeThing(Bitmap screen, int startX, int startY)
        {
            // Take snapshot of entire top-left corner and compare it with reference
            int width = (int)(topLeftSelected.Width * imageScale + 0.5);
            int height = (int)(topLeftSelected.Height * imageScale + 0.5);

            // Take corner area
            Rectangle bounds = new(startX, startY, width, height);
            using Bitmap corner = screen.Clone(bounds, screen.PixelFormat);

            // Resize Corner to reference size
            using Bitmap resizedCorner = corner.Resize(1.0 / imageScale);

            // Compare to reference images
            return ImageComparer.Compare(resizedCorner, topLeftSelected, tolerance) ||
                ImageComparer.Compare(resizedCorner, topLeftUnselected, tolerance);
        }

        void ScanBoard(Bitmap screen, int startX, int startY)
        {
            // Iterate over 5x5 board of cards
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    int referenceWidth = cardHidden.Width;
                    int referenceHeight = cardHidden.Height;
                    int width = (int)(referenceWidth * imageScale + 0.5); // Image Scale will always scale to an integer
                    int height = (int)(referenceHeight * imageScale + 0.5); // Round properly just in case
                    // Distance between cards is width of Top Left Reference Image
                    int x = (startX + c * width) + (int)(IMAGE_MARGIN_FRACTION * width + 0.5); // Skip Margins
                    int y = (startY + r * height) + (int)(IMAGE_MARGIN_FRACTION * height + 0.5);
                    int cardWidth = (int)(width - IMAGE_MARGIN_FRACTION * width * 2 + 0.5); // Remove Close and Far Margins
                    int cardHeight = (int)(height - IMAGE_MARGIN_FRACTION * height * 2 + 0.5); // Remove Close and Far Margins
                    Rectangle cardBounds = new(x, y, cardWidth, cardHeight);
                    using Bitmap card = screen.Clone(cardBounds, screen.PixelFormat);

                    // Resize card bitmap to reference size
                    using Bitmap resizedCard = card.Resize(1.0 / imageScale); // Invert Scale b/c its backwards

                    try
                    {
                        // Compare Resized Card to 1, 2, 3 reference images and Update Game Board
                        if (ImageComparer.Compare(resizedCard, flippedOne, tolerance)) GameBoard[r][c] = 1;
                        else if (ImageComparer.Compare(resizedCard, flippedTwo, tolerance)) GameBoard[r][c] = 2;
                        else if (ImageComparer.Compare(resizedCard, flippedThree, tolerance)) GameBoard[r][c] = 3;
                        else GameBoard[r][c] = 0;
                    }
                    catch (InvalidOperationException)
                    {
                        DebugLog("Invalid Size: " + resizedCard.Size + "\nShould be: " + flippedOne.Size);
                    }
                }
            }

            // Update the board in the window on the UI thread
            window.DispatcherQueue.TryEnqueue(() => window.UpdateBoard());
        }

        void DebugLog(object msg)
        {
            window.DispatcherQueue.TryEnqueue(() => window.DebugLog(msg));
        }
    }

    static class Extensions
    {
        /// <summary>
        /// Determines whether two colors similar within a certain tolerance
        /// </summary>
        /// <param name="color">This Color</param>
        /// <param name="other">Other Color</param>
        /// <param name="tolerance">How far apart each RGB value can be</param>
        /// <returns>True if similar, False if not</returns>
        public static bool Similar(this Color color, Color other, ColorDifference tolerance)
        {
            byte alpha = (byte)Math.Abs(color.A - other.A);
            byte red = (byte)Math.Abs(color.R - other.R);
            byte green = (byte)Math.Abs(color.G - other.G);
            byte blue = (byte)Math.Abs(color.B - other.B);
            return alpha <= tolerance.Alpha && red <= tolerance.Red &&
                green <= tolerance.Green && blue <= tolerance.Blue;
        }

        /// <summary>
        /// Resizes a Bitmap to the specified width and height
        /// </summary>
        /// <param name="image">The bitmap to resize</param>
        /// <param name="scale">The scale of the new image relative to the old image</param>
        /// <returns>The new, resized bitmap</returns>
        public static Bitmap Resize(this Bitmap image, double scale)
        {
            int width = (int)(image.Width * scale + 0.5);
            int height = (int)(image.Height * scale + 0.5);
            /*Bitmap destBitmap = new(width, height);

            using (Graphics graphics = Graphics.FromImage(destBitmap))
            {
                graphics.DrawImage(image, 0, 0, width, height);
            }*/

            Bitmap destBitmap = new(image, width, height);

            return destBitmap;
        }

        /// <summary>
        /// Resizes a Bitmap to the specified width, maintaining height
        /// </summary>
        /// <param name="image">The bitmap to resize</param>
        /// <param name="scale">The scale of the new image relative to the old image</param>
        /// <returns>The new, resized bitmap</returns>
        public static Bitmap ResizeWidth(this Bitmap image, double scale)
        {
            int width = (int)(image.Width * scale + 0.5);
            /*Bitmap destBitmap = new(width, height);

            using (Graphics graphics = Graphics.FromImage(destBitmap))
            {
                graphics.DrawImage(image, 0, 0, width, height);
            }*/

            Bitmap destBitmap = new(image, width, image.Height);

            return destBitmap;
        }
    }
}
