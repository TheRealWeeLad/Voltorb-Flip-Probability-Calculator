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
        public ushort[,] GameBoard { get; } = new ushort[5,5];
        // X-value of Point represents point values per column, y-value is voltorb numbers
        Point[,] VoltorbBoard { get; } = new Point[2, 5]; // Row 1 is Vertical, 2 is Horizontal

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
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 5; j++) GameBoard[i, j] = 0;
        }

    }
}
