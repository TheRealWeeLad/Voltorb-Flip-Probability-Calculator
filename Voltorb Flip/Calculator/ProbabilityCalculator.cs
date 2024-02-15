using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.AI.MachineLearning;
using Windows.Devices.Display;
using Windows.Devices.Enumeration;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.UI.Core;
using WinRT.Interop;

namespace Voltorb_Flip.Calculator
{
    partial class ProbabilityCalculator
    {
        readonly MainWindow window;

        public ProbabilityCalculator(MainWindow window)
        {
            this.window = window;
        }

        public bool CheckForGameOpen(Bitmap screenBitmap)
        {
            // Compare screenshot with Top-Left square of reference images

            return false;
        }

        void DebugLog(object msg)
        {
            window.DispatcherQueue.TryEnqueue(() => window.DebugLog(msg));
        }
    }
}
