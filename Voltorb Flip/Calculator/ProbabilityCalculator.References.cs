using Microsoft.Win32.SafeHandles;
using System;
using System.Drawing;

namespace Voltorb_Flip.Calculator
{
    partial class ProbabilityCalculator
    {
        // Reference Images
        static readonly Bitmap topLeftSelected = Image.FromFile(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\top-left-selected.png") as Bitmap;
        static readonly Bitmap topLeftUnselected = Image.FromFile(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\top-left-unselected.png") as Bitmap;
        readonly Bitmap topRowSelected;
        readonly Bitmap topRowUnselected;
        static readonly Bitmap zero = Image.FromFile(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\0.png") as Bitmap;
        static readonly Bitmap one = Image.FromFile(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\1.png") as Bitmap;
        static readonly Bitmap two = Image.FromFile(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\2.png") as Bitmap;
        static readonly Bitmap three = Image.FromFile(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\3.png") as Bitmap;
        static readonly Bitmap four = Image.FromFile(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\4.png") as Bitmap;
        static readonly Bitmap five = Image.FromFile(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\5.png") as Bitmap;
        static readonly Bitmap six = Image.FromFile(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\6.png") as Bitmap;
        static readonly Bitmap seven = Image.FromFile(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\7.png") as Bitmap;
        static readonly Bitmap eight = Image.FromFile(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\8.png") as Bitmap;
        static readonly Bitmap nine = Image.FromFile(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\9.png") as Bitmap;
        static readonly Bitmap[] numberBitmaps = { zero, one, two, three, four, five, six, seven, eight, nine };
        static readonly Bitmap flippedOne = Image.FromFile(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\flipped-1.png") as Bitmap;
        static readonly Bitmap flippedTwo = Image.FromFile(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\flipped-2.png") as Bitmap;
        static readonly Bitmap flippedThree = Image.FromFile(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\flipped-3.png") as Bitmap;
        static readonly Bitmap cardHidden = Image.FromFile(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\card-hidden-lowres.png") as Bitmap;
        readonly Bitmap[] voltorbBitmaps;

        // Reference Quantities
        readonly int topLeftSelectedWidth;
        readonly int topLeftSelectedHeight;
        readonly Color topLeftSurroundingColor;
        readonly Color topLeftSelectedBorderColor;
        readonly Color topLeftUnselectedBorderColor;

        // Reference Boards - (x2s, x3s, xVs)
        readonly (int, int, int)[][] PossibleBoards = new (int, int, int)[][]
        {
            new (int, int, int)[] { (3, 1, 6), (0, 3, 6), (5, 0, 6), (2, 2, 6), (4, 1, 6) }, // Level 1
            new (int, int, int)[] { (1, 3, 7), (6, 0, 7), (3, 2, 7), (0, 4, 7), (5, 1, 7) }, // Level 2
            new (int, int, int)[] { (2, 3, 8), (7, 0, 8), (4, 2, 8), (1, 4, 8), (6, 1, 8) }, // ...etc
            new (int, int, int)[] { (3, 3, 8), (0, 5, 8), (8, 0, 10), (5, 2, 10), (2, 4, 10) },
            new (int, int, int)[] { (7, 1, 10), (4, 3, 10), (1, 5, 10), (9, 0, 10), (6, 2, 10) },
            new (int, int, int)[] { (3, 4, 10), (0, 6, 10), (8, 1, 10), (5, 3, 10), (2, 5, 10) },
            new (int, int, int)[] { (7, 2, 10), (4, 4, 10), (1, 6, 13), (9, 1, 13), (6, 3, 10) },
            new (int, int, int)[] { (0, 7, 10), (8, 2, 10), (5, 4, 10), (2, 6, 10), (7, 3, 10) }
        };
    }
}
