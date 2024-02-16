using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Voltorb_Flip.Calculator
{
    partial class ProbabilityCalculator
    {
        public ushort[][] GameBoard = new ushort[5][];
        ushort[][] voltorbBoard = new ushort[2][]; // Row 1 is Vertical, 2 is Horizontal
    }
}
