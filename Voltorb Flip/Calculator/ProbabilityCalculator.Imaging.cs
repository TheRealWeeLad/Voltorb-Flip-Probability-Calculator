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
using System.Security.Cryptography;
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
        readonly ColorDifference tolerance = new(255, COLOR_TOLERANCE, COLOR_TOLERANCE, COLOR_TOLERANCE);
        const int SIZE_TOLERANCE = 2;
        const int STRICT_SIZE_TOLERANCE = 1;
        const double IMAGE_MARGIN_FRACTION = (double)3 / 28;
        const int MAX_SURROUNDING_DISTANCE = 30;
        readonly Point POINTS_POS1 = new(11, 2);
        readonly Point POINTS_POS2 = new(19, 2);
        readonly Point VOLTORB_POS = new(19, 15);
        readonly Size NUMBER_SIZE = new(6, 8);
        // All numbers have black in this position
        readonly Point BLACK_POS = new(3, 7);

        PixelFormat screenPixelFormat;
        int screenWidth;
        int screenHeight;

        // Values to keep after screenshot
        double _imageScale = 1;
        int _startX = 0;
        int _startY = 0;

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

        /// <summary>
        /// Analyzes the screen and tries to find the game board
        /// </summary>
        /// <param name="screenBitmap">A <see cref="Bitmap"/> representing the entire screen</param>
        /// <returns>True if the game is found, false otherwise</returns>
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
                    // Store starting coordinates
                    _startX = j;
                    _startY = i;

                    // Search pixel by pixel for top left pixel of reference image
                    Color pixel = screenBitmap.GetPixel(j, i);
                    // Don't tolerate differences in shade
                    if (pixel.Similar(topLeftSelectedPixel, tolerance, 0) ||
                        pixel.Similar(topLeftUnselectedPixel, tolerance, 0)) {
                        // Verify that we have found the right spot
                        if (!VerifySurroundings(screenBitmap))
                        {
                            //DebugLog("Surroundings Failed:\n" + j + ", " + i);
                            continue;
                        }
                        if (!VerifyTopRow(screenBitmap))
                        {
                            //DebugLog("Top Row Failed:\n" + j + ", " + i);
                            continue;
                        }

                        // Final Verification
                        if (!VerifyWholeThing(screenBitmap))
                        {
                            //DebugLog("Whole Thing Failed:\n" + j + ", " + i);
                            continue;
                        }

                        // Scan the board and update our virtual board
                        ScanBoard(screenBitmap);

                        //DebugLog("Done: Found!");
                        return true;
                    }
                }
            }
            DebugLog("Done: Not Found");
            return false;
        }

        /// <summary>
        /// Checks a few pixels around the top-left pixel to initially verify that
        /// we have found the game board
        /// </summary>
        /// <param name="screen">A <see cref="Bitmap"/> representing the entire screen</param>
        /// <returns>True if the surrounding pixels match the reference image, false
        /// if not</returns>
        bool VerifySurroundings(Bitmap screen)
        {
            // Just check a few more pixels around the top-left corner
            //       GREEN 
            // GREEN CORNER WHITE/RED
            //       WHITE/RED
            // Corner should look like this ^
            // Only works if there is space around corner
            if (_startX == 0 || _startX >= screenWidth - MAX_SURROUNDING_DISTANCE
                || _startY == 0 || _startY >= screenHeight - MAX_SURROUNDING_DISTANCE) return false;

            // Get rgb values of screen pixels
            int dim = MAX_SURROUNDING_DISTANCE + 1;
            Rectangle cornerBounds = new(_startX - 1, _startY - 1, dim, dim);
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
                if (rightColor.Similar(topLeftSelectedBorderColor, tolerance, 0) ||
                    rightColor.Similar(topLeftUnselectedBorderColor, tolerance, 0))
                    right = true;
                // Check above right
                Color upColor = Color.FromArgb(rgbValues[upIdx + 3], rgbValues[upIdx + 2],
                    rgbValues[upIdx + 1], rgbValues[upIdx]);
                if (!upColor.Similar(topLeftSurroundingColor, tolerance, 0))
                    return false; // All top pixels are the same, so don't waste time

                // Check down
                Color downColor = Color.FromArgb(rgbValues[bottomIdx + 3], rgbValues[bottomIdx + 2],
                    rgbValues[bottomIdx + 1], rgbValues[bottomIdx]);
                if (downColor.Similar(topLeftSelectedBorderColor, tolerance, 0) ||
                    downColor.Similar(topLeftUnselectedBorderColor, tolerance, 0))
                    bottom = true;
                // Check left of down
                Color leftColor = Color.FromArgb(rgbValues[leftIdx + 3], rgbValues[leftIdx + 2],
                    rgbValues[leftIdx + 1], rgbValues[leftIdx]);
                if (!leftColor.Similar(topLeftSurroundingColor, tolerance, 0))
                    return false; // All left pixels are also the same, so don't waste time

                if (bottom && right) return true;
                else if (bottom || right) return false; // Both must be correct color
            }

            return false; // Not found
        }

        /// <summary>
        /// Procedurally Checks Rows of pixels beginning at (startX, startY), looking
        /// for a row that matches the reference image
        /// </summary>
        /// <param name="screen">A <see cref="Bitmap"/> representing the entire screen</param>
        /// <returns>True if a row matching the reference image is found, false otherwise</returns>
        bool VerifyTopRow(Bitmap screen)
        {
            for (int i = topLeftSelectedWidth; i < screenWidth - _startX; i++)
            {
                // Cut out top row of screen image
                Rectangle bounds = new(_startX, _startY, i, 1);
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
                        if (selected || unselected || withinTolerance)
                        {
                            // Set Image Scale based on width of screen image
                            _imageScale = scale;
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

        /// <summary>
        /// Compares a screenshot <see cref="Bitmap"/> of the top left corner of the
        /// game board with the reference <see cref="Bitmap"/> to determine their equality
        /// </summary>
        /// <param name="screen">A <see cref="Bitmap"/> representing the entire screen</param>
        /// <returns>True if the screenshot matches the reference image, false if not</returns>
        bool VerifyWholeThing(Bitmap screen)
        {
            // Take snapshot of entire top-left corner and compare it with reference
            int width = (int)(topLeftSelectedWidth * _imageScale + 0.5);
            int height = (int)(topLeftSelectedHeight * _imageScale + 0.5);

            // Check to see if whole board is on screen
            if (width * 6 > screenWidth || height * 6 > screenHeight) return false;

            // Take corner area
            Rectangle bounds = new(_startX, _startY, width, height);
            using Bitmap corner = screen.Clone(bounds, screen.PixelFormat);

            // Resize Corner to reference size
            using Bitmap resizedCorner = corner.Resize(1.0 / _imageScale);

            // Compare to reference images
            bool selected = ImageComparer.Compare(resizedCorner, topLeftSelected,
                tolerance, out Image diffSelected);
            bool unselected = ImageComparer.Compare(resizedCorner, topLeftUnselected,
                tolerance, out Image diffUnselected);
            // Analyze Difference Images to determine whether they are within tolerance
            bool withinTolerance = VerifyToleranceSimple((Bitmap)diffSelected, strict: false) ||
                VerifyToleranceSimple((Bitmap)diffUnselected, strict: false);
            try
            {
                if (!withinTolerance) DebugLog("Tolerance Failed:\n" + _startX + ", " + _startY);
                return selected || unselected || withinTolerance;
            }
            finally
            {
                diffSelected.Dispose();
                diffUnselected.Dispose();
            }
        }

        /// <summary>
        /// Looks through the differences between two <see cref="Bitmap"/>s to find
        /// islands of adjacent non-tolerated pixels and determine whether they
        /// should be tolerated (simple implementation)
        /// </summary>
        /// <param name="diff">A <see cref="Bitmap"/> representing the difference
        /// between two <see cref="Bitmap"/>s that are being compared</param>
        /// <param name="strict">Should we use a lower size tolerance for islands</param>
        /// <returns>True if the two original <see cref="Bitmap"/>s are determined to
        /// be close enough to be practically the same, False otherwise</returns>
        bool VerifyToleranceSimple(Bitmap diff, bool strict)
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
        /// <summary>
        /// Looks through every pixel in the <paramref name="diff"/> between the
        /// screenshot <see cref="Bitmap"/> of a row and the reference <see cref="Bitmap"/>
        /// of a row to determine if the difference is small enough to tolerate
        /// </summary>
        /// <param name="diff">A <see cref="Bitmap"/> representing the difference
        /// between two <see cref="Bitmap"/>s with a height of 1 pixel</param>
        /// <returns></returns>
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

        /// <summary>
        /// Looks through the differences between two <see cref="Bitmap"/>s to find
        /// islands of adjacent non-tolerated pixels and determine whether they
        /// should be tolerated
        /// </summary>
        /// <param name="diff">A <see cref="Bitmap"/> representing the difference
        /// between two <see cref="Bitmap"/>s that are being compared</param>
        /// <param name="strict">Should we use a lower size tolerance for islands</param>
        /// <param name="tolerateBrightness">Should we tolerate differences in brightness</param>
        /// <returns>True if the two original <see cref="Bitmap"/>s are determined to
        /// be close enough to be practically the same, False otherwise</returns>
        bool VerifyTolerance(Bitmap diff, bool strict, bool tolerateBrightness)
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
        /// <summary>
        /// Uses a Breadth-First Search (BFS) to find the size of clusters of the non-tolerated
        /// <see cref="Color"/> differences and determine if they are too big to be tolerated
        /// </summary>
        /// <param name="rgbValues">A byte array representing the difference between
        /// two <see cref="Bitmap"/>s that are being compared</param>
        /// <param name="x">The y position of the pixel in the array</param>
        /// <param name="y">The y position of the pixel in the array</param>
        /// <param name="visited">A 2-dimensional array containing information
        /// about which pixels have already been visited</param>
        /// <param name="arrayWidth">The width of the original difference <see cref="Bitmap"/></param>
        /// <param name="arrayHeight">The height of the original difference <see cref="Bitmap"/></param>
        /// <param name="tolerance">How large of an island should be tolerated,
        /// used for both width and height</param>
        /// <param name="tolerateBrightness">Should differences in brightness be tolerated</param>
        /// <returns></returns>
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
            int width = 1; // Initialize island size counter
            int height = 1; // Already found one so set them both to 1

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
        /// <summary>
        /// Determine whether a certain pixel <see cref="Color"/> at a certain position
        /// should be tolerated
        /// </summary>
        /// <param name="rgbValues">A byte array representing the difference between
        /// two <see cref="Bitmap"/>s that are being compared</param>
        /// <param name="y">The y position of the pixel in the array</param>
        /// <param name="x">The x position of the pixel in the array</param>
        /// <param name="width">The stride of the byte array, used to calculate
        /// positional information</param>
        /// <param name="tolerateBrightness">Should differences in brightness be tolerated</param>
        /// <returns>True if the <see cref="Color"/> difference should be tolerated,
        /// False otherwise</returns>
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

        /// <summary>
        /// Scan the whole onscreen board and update internal logic and gui accordingly
        /// </summary>
        /// <param name="screen">The <see cref="Bitmap"/> representing a screenshot</param>
        public void ScanBoard(Bitmap screen)
        {
            int cardTotalWidth = cardHidden.Width;
            int cardTotalHeight = cardHidden.Height;
            // Scale total width and height
            int totalWidth = (int)(cardTotalWidth * _imageScale + 0.5);
            int totalHeight = (int)(cardTotalHeight * _imageScale + 0.5);
            // Get cropped coordinates
            int croppedX = (int)(IMAGE_MARGIN_FRACTION * cardTotalWidth); // Skip Margins
            int croppedY = (int)(IMAGE_MARGIN_FRACTION * cardTotalHeight);
            // Remove Close and Far Margins
            int cardWidth = (int)(cardTotalWidth - IMAGE_MARGIN_FRACTION * cardTotalWidth * 2 + 0.5);
            int cardHeight = (int)(cardTotalHeight - IMAGE_MARGIN_FRACTION * cardTotalHeight * 2 + 0.5);
            // Width and height between cards
            int width = (int)(topLeftSelectedWidth * _imageScale + 0.5); // Image Scale will always scale to an integer
            int height = (int)(topLeftSelectedHeight * _imageScale + 0.5); // Round properly just in case
            // Iterate over 5x5 board of cards
            for (int r = 0; r < 6; r++)
            {
                for (int c = 0; c < 6; c++)
                {
                    // Distance between cards is width of Top Left Reference Image
                    // Add 1 to width and height because it's 1 off ???
                    int x = _startX + c * (width + 1);
                    int y = _startY + r * (height + 1);

                    Rectangle cardBounds = new(x, y, width, height);
                    using Bitmap card = screen.Clone(cardBounds, screenPixelFormat);
                    // Resize card bitmap to reference size
                    using Bitmap resizedCard = card.Resize(1.0 / _imageScale); // Invert Scale b/c its backwards

                    if (r < 5 && c < 5)
                    {
                        // Crop card bitmap to see only the card
                        Rectangle croppedBounds = new(croppedX, croppedY, cardWidth, cardHeight);
                        using Bitmap croppedCard = resizedCard.Clone(croppedBounds, resizedCard.PixelFormat);
                        try
                        {
                            // Compare Resized Card to 1, 2, 3 reference images and Update Game Board
                            // Use Stricter Comparison because the numbers are small
                            if (Compare(croppedCard, flippedOne, strict: true, tolerateBrightness: true))
                                GameBoard[r, c] = 1;
                            // Don't tolerate brightness in 2 because it's too similar to 3
                            else if (Compare(croppedCard, flippedTwo, strict: true, tolerateBrightness: false))
                                GameBoard[r, c] = 2;
                            else if (Compare(croppedCard, flippedThree, strict: true, tolerateBrightness: true))
                                GameBoard[r, c] = 3;
                            else GameBoard[r, c] = 0;
                        }
                        catch (InvalidOperationException)
                        {
                            DebugLog("Invalid Size: " + resizedCard.Size + "\nShould be: " + flippedOne.Size);
                        }
                    }
                    else
                    {
                        if (r == 5 && c == 5) continue; // No card there

                        int voltorbIdx;
                        if (c == 5) // Vertical Numbers
                        {
                            FindNumbersInCard(resizedCard, 0, r);
                            voltorbIdx = r;
                        }
                        else // Horizontal Numbers
                        {
                            FindNumbersInCard(resizedCard, 1, c);
                            voltorbIdx = c + 5;
                        }

                        // Crop original card bitmap to display only card
                        Rectangle bounds = new(0, 0, totalWidth, totalHeight);
                        Bitmap croppedCard = card.Clone(bounds, card.PixelFormat);
                        
                        // Update voltorb images in window
                        window.DispatcherQueue.TryEnqueue(() =>
                            window.voltorbImages[voltorbIdx] = croppedCard.ConvertToBitmapImage(true));   
                    }
                }
            }

            // Finally Dispose Screen Bitmap
            screen.Dispose();
            // Update the board in the window on the UI thread
            window.DispatcherQueue.TryEnqueue(() => window.UpdateBoard());
        }

        // TODO: DISTINGUISH BETWEEN 5 AND 6
        void FindNumbersInCard(Bitmap card, ushort row, int idx)
        {
            ushort points1, points2, voltorbNum;

            // First points number
            Rectangle num1Location = new(POINTS_POS1, NUMBER_SIZE);
            using (Bitmap num1Bitmap = card.Clone(num1Location, card.PixelFormat))
                points1 = CompareAllNumbers(num1Bitmap); 
            // Second points number
            Rectangle num2Location = new(POINTS_POS2, NUMBER_SIZE);
            using (Bitmap num2Bitmap = card.Clone(num2Location, card.PixelFormat))
                points2 = CompareAllNumbers(num2Bitmap);
            // Voltorb number
            Rectangle voltorbNumLocation = new(VOLTORB_POS, NUMBER_SIZE);
            using Bitmap voltorbNumBitmap = card.Clone(voltorbNumLocation, card.PixelFormat);
                voltorbNum = CompareAllNumbers(voltorbNumBitmap);

            // Update Game Board Values
            DebugLog(points1 + ", " + points2 + ", " + voltorbNum);
            Thread.Sleep(1000);
            VoltorbBoard[row, idx] = new Point(points1 * 10 + points2, voltorbNum);
        }
        ushort CompareAllNumbers(Bitmap bitmap)
        {
            Color black = bitmap.GetPixel(BLACK_POS.X, BLACK_POS.Y);

            for (ushort i = 0; i < 10; i++)
            {
                if (CompareNumber(bitmap, numberBitmaps[i], black))
                    return i;
            }
            // If not found, just return 5 cuz why not :D
            DebugLog("Number not found");
            return 5;
        }

        // Need to preprocess numbers because backgrounds will be different colors
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
                    if (!numColor.Similar(black, tolerance, BRIGHTNESS_TOLERANCE))
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
            bool withinTolerance = VerifyTolerance((Bitmap)diff, strict, tolerateBrightness);
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
            window.DispatcherQueue.TryEnqueue(() => 
                window.DebugImage(bitmap.ConvertToBitmapImage(false)));
        }
    }
}
