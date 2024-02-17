using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.Scenes;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UITesting;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        const int SIZE_TOLERANCE = 2;
        double imageScale = 1;
        const double IMAGE_MARGIN_FRACTION = (double)3 / 28;

        PixelFormat screenPixelFormat;
        int screenWidth;
        int screenHeight;

        public ProbabilityCalculator(MainWindow window)
        {
            this.window = window;

            // Initialize top rows of selected and unselected top-left squares
            Rectangle selectedBounds = new(0, 0, topLeftSelected.Width, 1);
            topRowSelected = topLeftSelected.Clone(selectedBounds, topLeftSelected.PixelFormat);
            Rectangle unselectedBounds = new(0, 0, topLeftUnselected.Width, 1);
            topRowUnselected = topLeftUnselected.Clone(unselectedBounds, topLeftUnselected.PixelFormat);

            // Initialize Reference Quantities
            topLeftSelectedHeight = topLeftSelected.Height;
            topLeftSelectedWidth = topLeftSelected.Width;
            topleftUnselectedHeight = topLeftUnselected.Height;
            topLeftUnselectedWidth = topLeftUnselected.Width;

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

            // Get reference to screen information
            screenPixelFormat = screenBitmap.PixelFormat;
            screenWidth = screenBitmap.Width;
            screenHeight = screenBitmap.Height;

            for (int i = 0; i < screenHeight; i++)
            {
                for (int j = 0; j < screenWidth; j++)
                {
                    // Search pixel by pixel for top left pixel of reference image
                    Color pixel = screenBitmap.GetPixel(j, i);
                    if (pixel.Similar(topLeftSelectedPixel, tolerance) ||
                        pixel.Similar(topLeftUnselectedPixel, tolerance)) {
                        // Verify that we have found the right spot
                        if (!VerifySurroundings(screenBitmap, j, i))
                        {
                            //DebugLog("Surroundings Failed:\n" + j + ", " + i);
                            continue;
                        }
                        if (!VerifyTopRow(screenBitmap, j, i))
                        {
                            //DebugLog("Top Row Failed:\n" + j + ", " + i);
                            continue;
                        }

                        // Final Verification
                        if (!VerifyWholeThing(screenBitmap, j, i))
                        {
                            //DebugLog("Whole Thing Failed:\n" + j + ", " + i);
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

        // TODO: Switch to screen.LockBits()
        bool VerifySurroundings(Bitmap screen, int startX, int startY)
        {
            // Just check a few more pixels around the top-left corner
            //       GREEN 
            // GREEN CORNER WHITE/RED
            //       WHITE/RED
            // Corner should look like this ^
            // Only works if there is space around corner
            if (startX == 0 || startX >= screenWidth - 30
                || startY == 0 || startY >= screenHeight - 30) return false; // Impossible

            // Check 30 pixels to the right and down to find correct colors
            for (int i = 1; i <= 30; i++)
            {
                bool right = false, bottom = false;

                // Check to the right
                if (screen.GetPixel(startX + i, startY).Similar(topLeftSelected.GetPixel(1, 0), tolerance) ||
                screen.GetPixel(startX + i, startY).Similar(topLeftUnselected.GetPixel(1, 0), tolerance))
                    right = true;
                // Check above right
                if (!screen.GetPixel(startX + i - 1, startY - 1).Similar(topLeftSelected.GetPixel(0, 0), tolerance))
                    return false; // All top pixels are the same, so don't waste time
                // Check down
                if (screen.GetPixel(startX, startY + i).Similar(topLeftSelected.GetPixel(0, 1), tolerance) ||
                screen.GetPixel(startX, startY + i).Similar(topLeftUnselected.GetPixel(0, 1), tolerance))
                    bottom = true;
                // Check left of down
                if (!screen.GetPixel(startX - 1, startY + i - 1).Similar(topLeftSelected.GetPixel(0, 0), tolerance))
                    return false; // All left pixels are also the same, so don't waste time

                if (bottom && right) return true;
                else if (bottom || right) return false; // Both must be correct color
            }

            return false; // Not found
        }

        bool VerifyTopRow(Bitmap screen, int startX, int startY)
        {
            for (int i = topLeftSelectedWidth; i < screenWidth - startX; i++)
            {
                // Cut out top row of screen image
                Rectangle bounds = new(startX, startY, i, 1);
                using Bitmap top = screen.Clone(bounds, screenPixelFormat);
                // Resize to reference size
                double scale = (double)i / topLeftSelectedWidth;
                Bitmap resizedTop = top.ResizeWidth(1.0 / scale); // Invert Scale b/c its backwards
                Size size = resizedTop.Size;
                try
                {
                    // Compare to Reference Top Rows
                    bool selected = ImageComparer.Compare(resizedTop, topRowSelected, tolerance, out Image diffSelected);
                    bool unselected = ImageComparer.Compare(resizedTop, topRowUnselected, tolerance, out Image diffUnselected);
                    // No tolerance for top row, must be exact
                    try
                    {
                        if (selected || unselected)
                        {
                            // Set Image Scale based on width of screen image
                            imageScale = scale;
                            return true;
                        }
                    }
                    finally
                    {
                        diffSelected.Dispose();
                        diffUnselected.Dispose();
                    }
                }
                catch (InvalidOperationException)
                {
                    DebugLog("Invalid Size: " + size);
                }
            }
            return false;
        }

        bool VerifyWholeThing(Bitmap screen, int startX, int startY)
        {
            // Take snapshot of entire top-left corner and compare it with reference
            int width = (int)(topLeftSelectedWidth * imageScale + 0.5);
            int height = (int)(topLeftSelectedHeight * imageScale + 0.5);

            // Take corner area
            Rectangle bounds = new(startX, startY, width, height);
            using Bitmap corner = screen.Clone(bounds, screen.PixelFormat);

            // Resize Corner to reference size
            using Bitmap resizedCorner = corner.Resize(1.0 / imageScale);

            // Compare to reference images
            bool selected = ImageComparer.Compare(resizedCorner, topLeftSelected,
                tolerance, out Image diffSelected);
            bool unselected = ImageComparer.Compare(resizedCorner, topLeftUnselected,
                tolerance, out Image diffUnselected);
            // Analyze Difference Images to determine whether they are within tolerance
            bool withinTolerance = VerifyTolerance((Bitmap)diffSelected) ||
                VerifyTolerance((Bitmap)diffUnselected);
            try
            {
                return selected || unselected || withinTolerance;
            }
            finally
            {
                diffSelected.Dispose();
                //diffUnselected.Dispose();
            }
        }

        bool VerifyTolerance(Bitmap diff)
        {
            int width = diff.Width;
            int height = diff.Height;
            // Get data object
            BitmapData diffData = diff.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly, diff.PixelFormat);

            // Address of first pixel in memory
            IntPtr scan0 = diffData.Scan0;
            // Make new array of rgb values
            int bytes = Math.Abs(diffData.Stride) * height;
            byte[] rgbValues = new byte[bytes];
            // Copy Colors from diffData into rgbValues
            Marshal.Copy(scan0, rgbValues, 0, bytes);

            bool[][] significantPixels = new bool[height][];
            // Look at every pixel to determine where inconsistensies are
            // Find islands of non-tolerated pixels and determine their width/height
            int islandHeight = 0;
            bool tooWide = false;
            bool tooHigh = false;
            for (int y = 0; y < height; y++)
            {
                significantPixels[y] = new bool[width];
                
                int islandWidth = 0;
                for (int x = 0; x < width; x++)
                {
                    // Blue index is followed by green, red, then alpha
                    int blueIdx = 4 * (y * width + x);

                    if (rgbValues[blueIdx] > tolerance.Blue || rgbValues[blueIdx + 1] > tolerance.Green
                        || rgbValues[blueIdx + 2] > tolerance.Red)
                    {
                        DebugLog("X: " + x + "  Y: " + y + "  B: " + rgbValues[blueIdx] + "\nG: " + rgbValues[blueIdx + 1] +
                            "\nR: " + rgbValues[blueIdx + 2]);
                        significantPixels[y][x] = true;
                        // Make sure there is a pixel to the left
                        if (x > 0) {
                            if (significantPixels[y][x - 1])
                            {
                                islandWidth++;
                                // Check if island is too large to tolerate
                                if (islandWidth > SIZE_TOLERANCE) tooWide = true;
                            }
                            else islandWidth = 1;
                        }
                        // Make sure there is a pixel above us
                        if (y > 0)
                        {
                            if (significantPixels[y - 1][x])
                            {
                                islandHeight++;
                                if (islandHeight > SIZE_TOLERANCE) tooHigh = true;
                            }
                            else islandHeight = 1;
                        }
                    }
                }
            }

            // Free Memory
            diff.UnlockBits(diffData);
            // Verification fails only if an island is too wide and too high
            return !(tooWide && tooHigh);
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

        // DEBUG
        void DebugLog(object msg)
        {
            window.DispatcherQueue.TryEnqueue(() => window.DebugLog(msg));
        }
        void DebugImage(Bitmap bitmap)
        {
            window.DispatcherQueue.TryEnqueue(() => {
                BitmapImage img = new();
                using (MemoryStream stream = new())
                {
                    bitmap.Save(stream, ImageFormat.Png);
                    stream.Position = 0;
                    img.SetSource(stream.AsRandomAccessStream());
                }

                window.DebugImage(img);
            });
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
