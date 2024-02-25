using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Voltorb_Flip.Calculator
{
    partial class ProbabilityCalculator
    {
        public byte[,] GameBoard { get; } = new byte[5,5];
        // X-value of Point represents point values per column, y-value is voltorb numbers
        Point[,] VoltorbBoard { get; } = new Point[2, 5]; // Row 1 is Vertical, 2 is Horizontal
        public List<byte>[,] PossibleValues { get; } = new List<byte>[5, 5];

        readonly byte[] allPossible = { 0, 1, 2, 3 };

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

            // Intialize Reference Voltorb Card Images
            voltorbBitmaps = new Bitmap[10];
            for (int i = 0; i < 10; i++)
            {
                voltorbBitmaps[i] = Image.FromFile(string.Format(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\voltorb{0}.png",
                    i + 1)) as Bitmap;
            }

            // Initialize all cards as hidden
            // Initialize empty lists in possible values
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 5; j++)
                {
                    GameBoard[i, j] = 0;
                    PossibleValues[i, j] = new();
                }

        }
        
        public void CalculateUnknowns()
        {
            // Fill in known quantities from game board
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 5; j++)
                {
                    byte val = GameBoard[i, j];
                    if (val == 0)
                        PossibleValues[i, j].AddRange(allPossible);
                    else PossibleValues[i, j].Add(val);
                }
            
            // Loop through VoltorbBoard list to find row/column information
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 5; j++)
                {
                    Point vals = VoltorbBoard[i, j];

                    // Check if row/col has 0 voltorbs
                    if (!Check0Voltorb(i, j, vals))
                        // If not, check for 5 voltorbs
                        Check5Voltorb(i, j, vals);
                }
        }

        /// <summary>
        /// Checks the provided row/column for if it has 0 voltorbs and removes 
        /// possibilities accordingly
        /// </summary>
        /// <param name="i">The row of the VoltorbBoard we are in</param>
        /// <param name="j">The index of the current card within the row/column</param>
        /// <param name="vals">The point value and voltorb value of the provided
        /// row/column</param>
        /// <returns>True if there are 0 voltorbs in the provided row/column, False
        /// otherwise</returns>
        bool Check0Voltorb(int i, int j, Point vals)
        {
            // Check for row/col with 0 voltorbs
            if (vals.Y == 0)
            {
                // Every card in this row/col is safe
                for (int n = 0; n < 5; n++)
                {
                    int row = i * n + (1 - i) * j;
                    int col = (1 - i) * n + i * j;
                    // Get rid of voltorb as a possible option
                    PossibleValues[row, col].Remove(0);
                }
                return true;
            }
            return false;
        }
        void Check5Voltorb(int i, int j, Point vals)
        {

        }
    }
}
