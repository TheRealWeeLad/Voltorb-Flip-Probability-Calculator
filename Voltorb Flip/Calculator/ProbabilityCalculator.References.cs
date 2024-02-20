using Microsoft.Win32.SafeHandles;
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

        // Reference Quantities
        readonly int topLeftSelectedWidth;
        readonly int topLeftSelectedHeight;
        readonly Color topLeftSurroundingColor;
        readonly Color topLeftSelectedBorderColor;
        readonly Color topLeftUnselectedBorderColor;
    }
}
