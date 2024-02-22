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
    }
}
