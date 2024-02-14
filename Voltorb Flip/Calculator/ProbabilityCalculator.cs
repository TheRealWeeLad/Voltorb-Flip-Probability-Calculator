using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Voltorb_Flip.Calculator
{
    class ProbabilityCalculator
    {
        readonly MainWindow window;

        public ProbabilityCalculator(MainWindow window)
        {
            this.window = window;
        }

        public void Calibrate()
        {
            Thread.Sleep(2000);

            window.EndCalibration();
        }
    }
}
