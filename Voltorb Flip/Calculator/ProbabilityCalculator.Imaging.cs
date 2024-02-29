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
using System.Drawing.Drawing2D;
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
using Windows.Globalization.NumberFormatting;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Security.Isolation;
using Windows.Services.TargetedContent;
using System.Collections;

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
        const double IMAGE_MARGIN_FRACTION = 3.0 / 28;
        const int MAX_SURROUNDING_DISTANCE = 30;
        const double VOLTORB_IMAGE_SCALE = 158.0 / 28;
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
                using Bitmap resizedTop = top.ResizeWidth(1.0 / scale); // Invert Scale to resize down
                Size size = resizedTop.Size;
                try
                {
                    // Compare to Reference Top Rows
                    bool selected = ImageComparer.Compare(resizedTop, topRowSelected, tolerance, out Image diffSelected);
                    bool unselected = ImageComparer.Compare(resizedTop, topRowUnselected, tolerance, out Image diffUnselected);
                    try
                    {
                        if (selected || unselected)
                        {
                            // Set Image Scale based on width of screen image
                            _imageScale = scale;
                            return true;
                        }
                        // Analyze Difference Images to determine whether they are within tolerance
                        bool withinTolerance = VerifyToleranceRow((Bitmap)diffSelected) ||
                            VerifyToleranceRow((Bitmap)diffUnselected);
                        if (withinTolerance)
                        {
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
            if (_startX + width * 6 > screenWidth || _startY + height * 6 > screenHeight)
                return false;

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
        bool VerifyTolerance(Bitmap diff, bool tolerateSize, bool strict, bool tolerateBrightness)
        {
            // Activate strict size tolerance if needed
            int sizeTolerance = 0;
            if (tolerateSize)
            {
                sizeTolerance = SIZE_TOLERANCE;
                if (strict) sizeTolerance = STRICT_SIZE_TOLERANCE;
            }

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
            // There's a 0.5 pixel offset for each card for some reason ???
            int widthBetween = (int)((topLeftSelectedWidth + 0.5) * _imageScale + 0.5); // Image Scale will always scale to an integer
            int heightBetween = (int)((topLeftSelectedHeight + 0.5) * _imageScale + 0.5); // Round properly just in case
            // Width and Height of Cards
            int cardWidthScaled = (int)(cardTotalWidth * _imageScale + 0.5);
            int cardHeightScaled = (int)(cardTotalHeight * _imageScale + 0.5);
            // Iterate over 5x5 board of cards
            for (int r = 0; r < 6; r++)
            {
                for (int c = 0; c < 6; c++)
                {
                    // Distance between cards is width of Top Left Reference Image
                    int x = _startX + c * widthBetween;
                    int y = _startY + r * heightBetween;

                    Rectangle cardBounds = new(x, y, cardWidthScaled, cardHeightScaled);
                    using Bitmap card = screen.Clone(cardBounds, screenPixelFormat);
                    // Resize card bitmap to reference size
                    using Bitmap resizedCard = card.Resize(1.0 / _imageScale); // Invert Scale to resize down

                    if (r < 5 && c < 5)
                    {
                        // Crop card bitmap to see only the card
                        Rectangle croppedBounds = new(croppedX, croppedY, cardWidth, cardHeight);
                        using Bitmap croppedCard = resizedCard.Clone(croppedBounds, resizedCard.PixelFormat);
                        try
                        {
                            // Compare Resized Card to 1, 2, 3 reference images and Update Game Board
                            // Use Stricter Comparison because the numbers are small
                            if (Compare(croppedCard, flippedOne, tolerateSize: true, strict: true, tolerateBrightness: true))
                                GameBoard[r, c] = 1;
                            // Don't tolerate brightness in 2 because it's too similar to 3
                            else if (Compare(croppedCard, flippedTwo, tolerateSize: true, strict: true, tolerateBrightness: false))
                                GameBoard[r, c] = 2;
                            else if (Compare(croppedCard, flippedThree, tolerateSize: true, strict: true, tolerateBrightness: true))
                                GameBoard[r, c] = 3;
                            else GameBoard[r, c] = 4;
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
                        ushort[] numbers;
                        if (c == 5) // Vertical Numbers
                        {
                            numbers = FindNumbersInCard(resizedCard, 0, r);
                            voltorbIdx = r;
                        }
                        else // Horizontal Numbers
                        {
                            numbers = FindNumbersInCard(resizedCard, 1, c);
                            voltorbIdx = c + 5;
                        }

                        // Update voltorb image with detected numbers
                        UpdateVoltorbImage(numbers, voltorbIdx);
                    }
                }
            }

            // Finally Dispose Screen Bitmap
            screen.Dispose();
            // Update the board in the window on the UI thread
            window.DispatcherQueue.TryEnqueue(() => window.UpdateBoard());
            // Update Internal Boards for Calcuations
            InternalGameBoard = GameBoard.Clone() as byte[,];
            InternalVoltorbBoard = VoltorbBoard.Clone() as Triple[,];
            // Calculate what's behind the unkown cards
            FillInKnownValues();
            CalculateUnknowns();
            CalculateProbabilities();
            // Update board based on calculated values on UI thread
            window.DispatcherQueue.TryEnqueue(() => window.UpdateCalculations());
        }

        /// <summary>
        /// Update the window's voltorb images to correctly represent the game board
        /// </summary>
        /// <param name="numbers">An array of the numbers in the onscreen voltorb card</param>
        /// <param name="voltorbIdx">The index of which voltorb card to use</param>
        void UpdateVoltorbImage(ushort[] numbers, int voltorbIdx)
        {
            Bitmap originalImage = voltorbBitmaps[voltorbIdx].Clone() as Bitmap;
            Bitmap points1Bitmap = numberBitmaps[numbers[0]];
            Bitmap points2Bitmap = numberBitmaps[numbers[1]];
            Bitmap voltorbBitmap = numberBitmaps[numbers[2]];
            
            using (Graphics graphics = Graphics.FromImage(originalImage))
            {
                // Get Positions of All Numbers
                Rectangle points1Rect = new Rectangle(POINTS_POS1, NUMBER_SIZE)
                    .Multiply(VOLTORB_IMAGE_SCALE);
                Rectangle points2Rect = new Rectangle(POINTS_POS2, NUMBER_SIZE)
                    .Multiply(VOLTORB_IMAGE_SCALE);
                Rectangle voltorbRect = new Rectangle(VOLTORB_POS, NUMBER_SIZE)
                    .Multiply(VOLTORB_IMAGE_SCALE);

                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                // Draw numbers in their specified positions
                graphics.DrawImage(points1Bitmap, points1Rect);
                graphics.DrawImage(points2Bitmap, points2Rect);
                graphics.DrawImage(voltorbBitmap, voltorbRect);
            }

            // Update window voltorb images
            window.DispatcherQueue.TryEnqueue(() =>
                window.voltorbImages[voltorbIdx] = originalImage.ConvertToBitmapImage(true));
        }

        /// <summary>
        /// Looks at the numbers within a voltorb card and updates the internal
        /// game board with the detected numbers
        /// </summary>
        /// <param name="card"><see cref="Bitmap"/> of a voltorb card</param>
        /// <param name="row">Which row of the internal game board to update 
        /// (0 if card is in the right column, 1 if card is in the bottom row)</param>
        /// <param name="idx">The index of the card in its row/column</param>
        /// <returns>An array of the numbers in the card (length 3) (order: points 
        /// number 1, points number 2, voltorb number)</returns>
        ushort[] FindNumbersInCard(Bitmap card, ushort row, int idx)
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
            VoltorbBoard[row, idx] = new Triple(points1 * 10 + points2, voltorbNum, 5);

            return new ushort[] { points1, points2, voltorbNum };
        }
        /// <summary>
        /// Compares <paramref name="bitmap"/> with all reference numbers to find
        /// which number it is
        /// </summary>
        /// <param name="bitmap"><see cref="Bitmap"/> of the number to compare</param>
        /// <returns>The number within the bitmap</returns>
        ushort CompareAllNumbers(Bitmap bitmap)
        {
            Color black = bitmap.GetPixel(BLACK_POS.X, BLACK_POS.Y);

            for (ushort i = 0; i < 10; i++)
            {
                bool tolerateSize = true;
                // 5 and 6 too similar to tolerate size differences
                if (i == 5 || i == 6) tolerateSize = false;

                if (CompareNumber(bitmap, numberBitmaps[i], black, tolerateSize))
                    return i;
            }
            // If not found, just return 0 cuz why not :D
            DebugLog("Number not found");
            return 0;
        }

        /// <summary>
        /// Preprocess <paramref name="numBmp"/> to remove background colors to prepare
        /// for image comparison and then compare with <paramref name="referenceBmp"/>
        /// </summary>
        /// <param name="numBmp"><see cref="Bitmap"/> of a number</param>
        /// <param name="referenceBmp">reference <see cref="Bitmap"/> to compare to</param>
        /// <param name="black">The <see cref="Color"/> representing the color of the number
        /// within <paramref name="numBmp"/></param>
        /// <param name="tolerateSize">Should tolerate small differences in pixel color</param>
        /// <returns>True if <paramref name="numBmp"/> is the same number as
        /// <paramref name="referenceBmp"/>, false otherwise</returns>
        bool CompareNumber(Bitmap numBmp, Bitmap referenceBmp, Color black, bool tolerateSize)
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

            return Compare(numBmp, referenceBmp, tolerateSize, strict: true, tolerateBrightness: false);
        }
        /// <summary>
        /// Compare two <see cref="Bitmap"/>s to determine if they are the same
        /// </summary>
        /// <param name="bmp1">First <see cref="Bitmap"/></param>
        /// <param name="bmp2">Second <see cref="Bitmap"/></param>
        /// <param name="tolerateSize">Should tolerate small differences in pixel color</param>
        /// <param name="strict">Should use a stricter tolerance for color differences</param>
        /// <param name="tolerateBrightness">Should tolerate differences in brightness</param>
        /// <returns>True if the two <see cref="Bitmap"/>s are the same, false otherwise</returns>
        bool Compare(Bitmap bmp1, Bitmap bmp2, bool tolerateSize, bool strict, bool tolerateBrightness)
        {
            // Compare to reference images
            bool same = ImageComparer.Compare(bmp1, bmp2,
                tolerance, out Image diff);
            if (same) return true;

            // Analyze Difference Images to determine whether they are within tolerance
            bool withinTolerance = VerifyTolerance((Bitmap)diff, tolerateSize, strict, tolerateBrightness);
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
        void DebugList<T>(List<T> list)
        {
            string s = "";
            foreach (T item in list)
            {
                if (item.GetType().IsGenericType && 
                    item.GetType().GetGenericTypeDefinition() == typeof(List<>))
                {
                    IList iterable = (IList)item;
                    foreach(var item2 in iterable)
                    {
                        s += item2.ToString() + ", ";
                    }
                    s = s[0..^2] + "\n";
                }
                else s += item.ToString() + ", ";
            }
            DebugLog(s);
        }
        void DebugImage(Bitmap bitmap)
        {
            window.DispatcherQueue.TryEnqueue(() => 
                window.DebugImage(bitmap.ConvertToBitmapImage(false)));
        }
    }
}
