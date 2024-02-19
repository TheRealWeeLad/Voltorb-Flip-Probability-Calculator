using Microsoft.VisualStudio.TestTools.UITesting;
using System;
using System.Drawing.Imaging;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Voltorb_Flip.Calculator
{
    internal static class HelperExtensions
    {
        /// <summary>
        /// Determines whether two colors similar within a certain tolerance
        /// </summary>
        /// <param name="color">This Color</param>
        /// <param name="other">Other Color</param>
        /// <param name="tolerance">How far apart each RGB value can be</param>
        /// <returns>True if similar, False if not</returns>
        public static bool Similar(this Color color, Color other, ColorDifference tolerance)
        {
            byte alpha = (byte)Math.Abs(color.A - other.A);
            byte red = (byte)Math.Abs(color.R - other.R);
            byte green = (byte)Math.Abs(color.G - other.G);
            byte blue = (byte)Math.Abs(color.B - other.B);
            return alpha <= tolerance.Alpha && red <= tolerance.Red &&
                green <= tolerance.Green && blue <= tolerance.Blue;
        }

        /// <summary>
        /// Resizes a Bitmap to the specified width and height
        /// </summary>
        /// <param name="image">The bitmap to resize</param>
        /// <param name="scale">The scale of the new image relative to the old image</param>
        /// <returns>The new, resized bitmap</returns>
        public static Bitmap Resize(this Bitmap image, double scale)
        {
            int newWidth = (int)(image.Width * scale + 0.5);
            int newHeight = (int)(image.Height * scale + 0.5);
            Bitmap result = new(newWidth, newHeight);

            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
            }

            return result;
        }

        /// <summary>
        /// Resizes a Bitmap to the specified width, maintaining height
        /// </summary>
        /// <param name="image">The bitmap to resize</param>
        /// <param name="scale">The scale of the new image relative to the old image</param>
        /// <returns>The new, resized bitmap</returns>
        public static Bitmap ResizeWidth(this Bitmap image, double scale)
        {
            int newWidth = (int)(image.Width * scale + 0.5);
            Bitmap result = new(newWidth, 1);

            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                graphics.DrawImage(image, 0, 0, newWidth, 1);
            }
            
            return result;
        }

        /// <summary>
        /// Gets the ARGB values of each pixel in a bitmap in a single-dimensional
        /// byte array. For 32BPP, every 4 entries in the array describes a single pixel
        /// (order: BGRA)
        /// </summary>
        /// <param name="bitmap">The bitmap to get the argb values from</param>
        /// <returns>
        /// A single-dimensional byte array containing pixel color information in BGRA order
        /// </returns>
        public static byte[] GetBGRAValues(this Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            // Get data object
            BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly, bitmap.PixelFormat);

            // Address of first pixel in memory
            IntPtr scan0 = bitmapData.Scan0;
            // Make new array of rgb values
            int bytes = Math.Abs(bitmapData.Stride) * height;
            byte[] rgbValues = new byte[bytes];
            // Copy Colors from diffData into rgbValues
            Marshal.Copy(scan0, rgbValues, 0, bytes);

            // Free memory
            bitmap.UnlockBits(bitmapData);

            return rgbValues;
        }
    }
}
