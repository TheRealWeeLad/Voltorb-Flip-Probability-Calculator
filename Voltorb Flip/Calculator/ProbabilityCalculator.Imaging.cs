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
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.AI.MachineLearning;
using Windows.ApplicationModel.LockScreen;
using Windows.ApplicationModel.VoiceCommands;
using Windows.Devices.Display;
using Windows.Devices.Enumeration;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Services.TargetedContent;

namespace Voltorb_Flip.Calculator
{
    partial class ProbabilityCalculator
    {
        readonly MainWindow window;

        const byte COLOR_TOLERANCE = 30;
        const byte BRIGHTNESS_TOLERANCE = 20;
        readonly ColorDifference tolerance = new(COLOR_TOLERANCE, COLOR_TOLERANCE, COLOR_TOLERANCE, COLOR_TOLERANCE);
        const int SIZE_TOLERANCE = 2;
        const int STRICT_SIZE_TOLERANCE = 1;
        const double IMAGE_MARGIN_FRACTION = (double)3 / 28;
        const double VOLTORB_MARGIN_FRACTION = IMAGE_MARGIN_FRACTION * 2 / 3;
        const int MAX_SURROUNDING_DISTANCE = 30;
        readonly Point POINTS_POS1 = new(9, 0);
        readonly Point POINTS_POS2 = new(17, 0);
        readonly Point VOLTORB_POS = new(17, 13);
        readonly Size NUMBER_SIZE = new(6, 8);
        // All number have black in this position
        readonly Point BLACK_POS = new(3, 7);

        PixelFormat screenPixelFormat;
        int screenWidth;
        int screenHeight;
        double imageScale = 1;

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
            topLeftSurroundingColor = topLeftSelected.GetPixel(0, 0);
            topLeftSelectedBorderColor = topLeftSelected.GetPixel(1, 0);
            topLeftUnselectedBorderColor = topLeftUnselected.GetPixel(1, 0);

            // Initialize all cards as hidden
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 5; j++) GameBoard[i, j] = 0;
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

                        //DebugLog("Done: Found!");
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
            if (startX == 0 || startX >= screenWidth - MAX_SURROUNDING_DISTANCE
                || startY == 0 || startY >= screenHeight - MAX_SURROUNDING_DISTANCE) return false;

            // Get rgb values of screen pixels
            int dim = MAX_SURROUNDING_DISTANCE + 1;
            Rectangle cornerBounds = new(startX - 1, startY - 1, dim, dim);
            using Bitmap corner = screen.Clone(cornerBounds, screen.PixelFormat);
            byte[] rgbValues = corner.GetBGRAValues();

            // Check pixels to the right and down to find correct colors
            for (int i = 1; i < MAX_SURROUNDING_DISTANCE; i++)
            {
                bool right = false, bottom = false;
                int rightIdx = 4 * (i + dim);
                int bottomIdx = 4 * (i * dim + 1);
                int upIdx = 4 * (i + 1);
                int leftIdx = 4 * i * dim;

                // Check to the right (order in rgb array is BGRA)
                Color rightColor = Color.FromArgb(rgbValues[rightIdx + 3], rgbValues[rightIdx + 2],
                    rgbValues[rightIdx + 1], rgbValues[rightIdx]);
                if (rightColor.Similar(topLeftSelectedBorderColor, tolerance) ||
                    rightColor.Similar(topLeftUnselectedBorderColor, tolerance))
                    right = true;
                // Check above right
                Color upColor = Color.FromArgb(rgbValues[upIdx + 3], rgbValues[upIdx + 2],
                    rgbValues[upIdx + 1], rgbValues[upIdx]);
                if (!upColor.Similar(topLeftSurroundingColor, tolerance))
                    return false; // All top pixels are the same, so don't waste time

                // Check down
                Color downColor = Color.FromArgb(rgbValues[bottomIdx + 3], rgbValues[bottomIdx + 2],
                    rgbValues[bottomIdx + 1], rgbValues[bottomIdx]);
                if (downColor.Similar(topLeftSelectedBorderColor, tolerance) ||
                    downColor.Similar(topLeftUnselectedBorderColor, tolerance))
                    bottom = true;
                // Check left of down
                Color leftColor = Color.FromArgb(rgbValues[leftIdx + 3], rgbValues[leftIdx + 2],
                    rgbValues[leftIdx + 1], rgbValues[leftIdx]);
                if (!leftColor.Similar(topLeftSurroundingColor, tolerance))
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
                using Bitmap resizedTop = top.ResizeWidth(1.0 / scale); // Invert Scale b/c its backwards
                Size size = resizedTop.Size;
                try
                {
                    // Compare to Reference Top Rows
                    bool selected = ImageComparer.Compare(resizedTop, topRowSelected, tolerance, out Image diffSelected);
                    bool unselected = ImageComparer.Compare(resizedTop, topRowUnselected, tolerance, out Image diffUnselected);
                    // Analyze Difference Images to determine whether they are within tolerance
                    bool withinTolerance = VerifyToleranceRow((Bitmap)diffSelected) ||
                        VerifyToleranceRow((Bitmap)diffUnselected);
                    try
                    {
                        if (!withinTolerance) DebugLog("Tolerance Row Failed:\n" + startX + ", " + startY);
                        if (selected || unselected || withinTolerance)
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

            // Check to see if whole board is on screen
            if (width * 6 > screenWidth || height * 6 > screenHeight) return false;

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
            bool withinTolerance = VerifyTolerance((Bitmap)diffSelected, false) ||
                VerifyTolerance((Bitmap)diffUnselected, false);
            try
            {
                if (!withinTolerance) DebugLog("Tolerance Failed:\n" + startX + ", " + startY);
                return selected || unselected || withinTolerance;
            }
            finally
            {
                diffSelected.Dispose();
                diffUnselected.Dispose();
            }
        }

        bool VerifyTolerance(Bitmap diff, bool strict)
        {
            // Activate strict size tolerance if needed
            int sizeTolerance = SIZE_TOLERANCE;
            if (strict) sizeTolerance = STRICT_SIZE_TOLERANCE;

            int width = diff.Width;
            int height = diff.Height;
            // Get rgb values of diff image
            byte[] rgbValues = diff.GetBGRAValues();

            bool[][] significantPixels = new bool[height][];
            // Find islands of non-tolerated pixels and determine their width/height
            int islandHeight = 0;
            // Look at every pixel to determine where inconsistensies are
            for (int y = 0; y < height; y++)
            {
                significantPixels[y] = new bool[width];
                
                int islandWidth = 0;
                for (int x = 0; x < width; x++)
                {
                    if (!IsTolerated(rgbValues, y, x, width, true))
                    {
                        /*DebugLog("X: " + x + "  Y: " + y + "  B: " + rgbValues[blueIdx] + "\nG: " + rgbValues[blueIdx + 1] +
                            "\nR: " + rgbValues[blueIdx + 2]);*/

                        significantPixels[y][x] = true;
                        // Make sure there is a pixel to the left
                        if (x > 0)
                        {
                            if (significantPixels[y][x - 1])
                            {
                                islandWidth++;
                                // Check if island is too large
                                if (islandWidth > sizeTolerance && islandHeight > sizeTolerance)
                                    return false;
                            }
                            else islandWidth = 1;
                        }
                        // Make sure there is a pixel above us
                        if (y > 0)
                        {
                            if (significantPixels[y - 1][x])
                            {
                                islandHeight++;
                                // Check if island is too large
                                if (islandHeight > sizeTolerance && islandWidth > sizeTolerance)
                                    return false;
                            }
                            else islandHeight = 1;
                        }
                    }
                }
            }

            // No too-large island was found
            return true;
        }
        bool VerifyToleranceRow(Bitmap diff)
        {
            int width = diff.Width;
            // Get rgb values of diff image
            byte[] rgbValues = diff.GetBGRAValues();

            // Look at every pixel to determine where inconsistensies are
            for (int x = 0; x < width; x++)
            {
                if (!IsTolerated(rgbValues, 0, x, width, true))
                {
                    /*DebugLog("X: " + x + "  B: " + rgbValues[blueIdx] + "\nG: " + rgbValues[blueIdx + 1] +
                            "\nR: " + rgbValues[blueIdx + 2]);*/
                    // No tolerance for any differences too large
                    return false;
                }
            }

            // Couldn't find any egregious discrepancies
            return true;
         }

        // More sophisticated island detection
        bool VerifyToleranceBetter(Bitmap diff, bool strict, bool tolerateBrightness)
        {
            // Activate strict size tolerance if needed
            int sizeTolerance = SIZE_TOLERANCE;
            if (strict) sizeTolerance = STRICT_SIZE_TOLERANCE;

            int width = diff.Width;
            int height = diff.Height;
            // Get rgb values of diff image
            byte[] rgbValues = diff.GetBGRAValues();

            bool[,] visited = new bool[height, width];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!visited[y, x] && !IsTolerated(rgbValues, y, x, width, tolerateBrightness))
                    {
                        DebugLog(x + ", " + y);
                        // New untolerated region found, check if it is too large
                        if (IslandTooBig(rgbValues, x, y, visited, width, height, sizeTolerance, tolerateBrightness))
                            return false;
                    }
                    visited[y, x] = true;
                }
            }
            // No islands found that exceed tolerance
            return true;
        }
        bool IslandTooBig(byte[] rgbValues, int x, int y, bool[,] visited,
            int arrayWidth, int arrayHeight, int tolerance, bool tolerateBrightness)
        {
            // DFS APPROACH BELOW
            /*// If island is already too big then give up
            if (size.Width > tolerance && size.Height > tolerance) return size;

            // Arrays to get correct neighboring indices
            int[] yIndices = { 1, 0, 0, -1 };
            int[] xIndices = { 0, 1, -1, 0 };

            visited[y, x] = true;

            // Check all four directions
            for (int i = 0; i < 4; i++)
            {
                int yDiff = yIndices[i];
                int xDiff = xIndices[i];
                int yIdx = y + yDiff;
                int xIdx = x + xDiff;

                // Make sure we aren't crossing the bounds of the array
                if (yIdx >= 0 && yIdx < arrayHeight && xIdx >= 0 && xIdx < arrayWidth
                    // Check tolerance of neighboring pixels
                    && !IsTolerated(rgbValues, yIdx, xIdx, arrayWidth) && !visited[yIdx, xIdx])
                {
                    size.Height += Math.Abs(yDiff);
                    size.Width += Math.Abs(xDiff);
                    size = SizeOfIsland(rgbValues, xIdx, yIdx, visited, size, arrayWidth, arrayHeight, tolerance);
                }
            }
            // No more island extensions detected
            DebugLog(size);
            return size;*/

            // BFS APPROACH BELOW
            int width = 0; // Initialize island size counter
            int height = 0;

            // Queue to perform breadth-first search (BFS)
            // Bool parameter is true if width should increase, false if height should
            Queue<(Point, bool)> queue = new();
            queue.Enqueue((new Point(x, y), true)); // Enqueue the starting point

            while (queue.Count > 0)
            {
                // If island is already too big, just quit
                if (width > tolerance && height > tolerance)
                    return true;

                (Point current, bool increaseWidth) = queue.Dequeue();
                int currentX = current.X;
                int currentY = current.Y;

                // Check if the current pixel is within the image bounds and unvisited
                if (currentX >= 0 && currentX < arrayWidth && currentY >= 0 && currentY < arrayHeight && !visited[currentY, currentX])
                {
                    visited[currentY, currentX] = true; // Mark the current pixel as visited

                    // Check if the current pixel is within the tolerance and update the island size
                    if (!IsTolerated(rgbValues, currentY, currentX, arrayWidth, tolerateBrightness))
                    {
                        DebugLog(x + ", " + y);
                        if (increaseWidth) width++;
                        else height++;

                        // Enqueue adjacent unvisited pixels
                        queue.Enqueue((new Point(currentX + 1, currentY), true));
                        queue.Enqueue((new Point(currentX - 1, currentY), true));
                        queue.Enqueue((new Point(currentX, currentY + 1), false));
                        queue.Enqueue((new Point(currentX, currentY - 1), false));
                    }
                }
            }

            // Check if the island size exceeds the tolerance
            return width > tolerance && height > tolerance;
        }
        bool IsTolerated(byte[] rgbValues, int y, int x, int width, bool tolerateBrightness)
        {
            // Blue index is followed by green, red, then alpha
            int blueIdx = 4 * (y * width + x);
            byte blue = rgbValues[blueIdx];
            byte green = rgbValues[blueIdx + 1];
            byte red = rgbValues[blueIdx + 2];

            if (blue > tolerance.Blue || green > tolerance.Green || red > tolerance.Red)
            {
                // Check if difference in all colors is basically the same
                // If it is, then it's just a difference in brightness
                if (tolerateBrightness && Math.Abs(blue - green) <= BRIGHTNESS_TOLERANCE &&
                    Math.Abs(blue - red) <= BRIGHTNESS_TOLERANCE &&
                    Math.Abs(red - green) <= BRIGHTNESS_TOLERANCE)
                    return true;
                // Otherwise we won't tolerate it
                return false;
            }
            // Color doesn't exceed tolerance
            return true;
        }

        void ScanBoard(Bitmap screen, int startX, int startY)
        {
            int cardTotalWidth = cardHidden.Width;
            int cardTotalHeight = cardHidden.Height;
            // Remove Close and Far Margins
            int cardWidth = (int)(cardTotalWidth - IMAGE_MARGIN_FRACTION * cardTotalWidth * 2 + 0.5);
            int cardHeight = (int)(cardTotalHeight - IMAGE_MARGIN_FRACTION * cardTotalHeight * 2 + 0.5);
            // Scale to proper size
            cardTotalWidth = (int)(cardTotalWidth * imageScale + 0.5);
            cardTotalHeight = (int)(cardTotalHeight * imageScale + 0.5);
            cardWidth = (int)(cardWidth * imageScale + 0.5);
            cardHeight = (int)(cardHeight * imageScale + 0.5);
            // Width and height between cards
            int width = (int)(topLeftSelectedWidth * imageScale + 0.5); // Image Scale will always scale to an integer
            int height = (int)(topLeftSelectedHeight * imageScale + 0.5); // Round properly just in case
            // Iterate over 5x5 board of cards
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    // Distance between cards is width of Top Left Reference Image
                    // Add 1 to width and height because it's 1 off ???
                    int x = (startX + c * (width + 1)) + (int)(IMAGE_MARGIN_FRACTION * cardTotalWidth + 0.5); // Skip Margins
                    int y = (startY + r * (height + 1)) + (int)(IMAGE_MARGIN_FRACTION * cardTotalHeight + 0.5);

                    Rectangle cardBounds = new(x, y, cardWidth, cardHeight);
                    using Bitmap card = screen.Clone(cardBounds, screenPixelFormat);

                    // Resize card bitmap to reference size
                    using Bitmap resizedCard = card.Resize(1.0 / imageScale); // Invert Scale b/c its backwards
                    try
                    {
                        // Compare Resized Card to 1, 2, 3 reference images and Update Game Board
                        // Use Stricter Comparison because the numbers are small
                        if (Compare(resizedCard, flippedOne, strict: true, tolerateBrightness: true)) 
                            GameBoard[r, c] = 1;
                        else if (Compare(resizedCard, flippedTwo, strict: true, tolerateBrightness: true)) 
                            GameBoard[r, c] = 2;
                        else if (Compare(resizedCard, flippedThree, strict: true, tolerateBrightness: true)) 
                            GameBoard[r, c] = 3;
                        else GameBoard[r, c] = 0;
                    }
                    catch (InvalidOperationException)
                    {
                        DebugLog("Invalid Size: " + resizedCard.Size + "\nShould be: " + flippedOne.Size);
                    }
                }
            }

            // Check voltorb cards
            int voltorbStartX = startX + 5 * (width + 1) + (int)(VOLTORB_MARGIN_FRACTION * cardTotalHeight + 0.5);
            int voltorbStartY = startY + 5 * (height + 1) + (int)(VOLTORB_MARGIN_FRACTION * cardTotalHeight + 0.5);
            int voltorbWidth = (int)(cardTotalWidth - VOLTORB_MARGIN_FRACTION * cardTotalWidth * 2 + 0.5);
            int voltorbHeight = (int)(cardTotalWidth - VOLTORB_MARGIN_FRACTION * cardTotalHeight * 2 + 0.5);
            for (ushort r = 0; r < 5; r++)
            {
                int y = startY + r * (height + 1) + (int)(VOLTORB_MARGIN_FRACTION * cardTotalHeight + 0.5);
                
                Rectangle cardBounds = new(voltorbStartX, y, voltorbWidth, voltorbHeight);
                using Bitmap card = screen.Clone(cardBounds, screenPixelFormat);

                using Bitmap resizedCard = card.Resize(1.0 / imageScale);

                FindNumbersInCard(resizedCard, 1, r);
            }
            for (ushort c = 0; c < 5; c++)
            {
                int x = (startX + c * (height + 1)) + (int)(VOLTORB_MARGIN_FRACTION * cardTotalHeight + 0.5);

                Rectangle cardBounds = new(x, voltorbStartY, voltorbWidth, voltorbHeight);
                using Bitmap card = screen.Clone(cardBounds, screenPixelFormat);

                using Bitmap resizedCard = card.Resize(1.0 / imageScale);

                FindNumbersInCard(resizedCard, 0, c);
            }

            // Update the board in the window on the UI thread
            window.DispatcherQueue.TryEnqueue(() => window.UpdateBoard());
        }

        // TODO: DISTINGUISH BETWEEN 5 AND 6
        public void FindNumbersInCard(Bitmap card, ushort row, ushort idx)
        {
            /*// First points number
            Rectangle num1Location = new(POINTS_POS1, NUMBER_SIZE);
            using Bitmap num1Bitmap = card.Clone(num1Location, card.PixelFormat);
            ushort points1 = CompareAllNumbers(num1Bitmap);*/
            // Second points number
            Rectangle num2Location = new(POINTS_POS2, NUMBER_SIZE);
            Bitmap num2Bitmap = card.Clone(num2Location, card.PixelFormat);
            ushort points2 = CompareAllNumbers(num2Bitmap);
            /*// Voltorb number
            Rectangle voltorbNumLocation = new(VOLTORB_POS, NUMBER_SIZE);
            using Bitmap voltorbNumBitmap = card.Clone(voltorbNumLocation, card.PixelFormat);
            ushort voltorbNum = CompareAllNumbers(voltorbNumBitmap);

            // Update Game Board Values
            VoltorbBoard[row, idx] = new Point(points1 * 10 + points2, voltorbNum);
            DebugLog(points1 + ", " + points2 + ", " + voltorbNum);
            DebugImage(num2Bitmap);*/
        }
        ushort CompareAllNumbers(Bitmap bitmap)
        {
            Color black = bitmap.GetPixel(BLACK_POS.X, BLACK_POS.Y);

            for (ushort i = 0; i < 10; i++)
            {
                if (CompareNumber(bitmap, numberBitmaps[i], black))
                { DebugLog(i); return i; }
            }
            // If not found, just return 1
            DebugLog("Number not found");
            return 1;
        }

        // Need a separate comparison for numbers because backgrounds
        // will be different colors
        bool CompareNumber(Bitmap numBmp, Bitmap referenceBmp, Color black)
        {
            if (numBmp.Size != referenceBmp.Size) return false;
            byte[] numRgb = numBmp.GetBGRAValues();
            int width = numBmp.Width;
            int height = numBmp.Height;

            // Loop through pixels in both bitmaps,
            // equating transparent with the background color
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int blueIdx = 4 * (y * width + x);
                    Color numColor = Color.FromArgb(numRgb[blueIdx + 3], numRgb[blueIdx + 2],
                        numRgb[blueIdx + 1], numRgb[blueIdx]);

                    // Set background pixels to transparent
                    if (!numColor.Similar(black, tolerance))
                        numBmp.SetPixel(x, y, Color.FromArgb(0));
                }
            }

            return Compare(numBmp, referenceBmp, strict: true, tolerateBrightness: false);
        }
        bool Compare(Bitmap bmp1, Bitmap bmp2, bool strict, bool tolerateBrightness)
        {
            // Compare to reference images
            bool same = ImageComparer.Compare(bmp1, bmp2,
                tolerance, out Image diff);
            if (same) return true;

            // Analyze Difference Images to determine whether they are within tolerance
            bool withinTolerance = VerifyToleranceBetter((Bitmap)diff, strict, tolerateBrightness);
            try
            {
                return withinTolerance;
            }
            finally
            {
                diff.Dispose();
            }
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
}
