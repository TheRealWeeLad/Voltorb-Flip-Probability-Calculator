using Microsoft.VisualStudio.TestTools.UITesting;
using System;
using System.Drawing.Imaging;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Media.Imaging;
using System.IO;

namespace Voltorb_Flip.Calculator
{
    internal static class HelperExtensions
    {
        /// <summary>
        /// Determines whether two <see cref="Color"/> structures are
        /// similar within a certain tolerance
        /// </summary>
        /// <param name="color">This <see cref="Color"/></param>
        /// <param name="other">Other <see cref="Color"/></param>
        /// <param name="tolerance">How far apart each RGB value can be</param>
        /// <returns>True if similar, False if not</returns>
        public static bool Similar(this Color color, Color other, ColorDifference tolerance,
            byte brightnessTolerance)
        {
            byte alpha = (byte)Math.Abs(color.A - other.A);
            byte red = (byte)Math.Abs(color.R - other.R);
            byte green = (byte)Math.Abs(color.G - other.G);
            byte blue = (byte)Math.Abs(color.B - other.B);

            if (alpha > tolerance.Alpha || red > tolerance.Red ||
                green > tolerance.Green || blue > tolerance.Blue)
                {
                // Color values outside of tolerance
                // Check if it's just a brightness discrepancy
                return Math.Abs(red - green) < brightnessTolerance && Math.Abs(red - blue)
                    < brightnessTolerance && Math.Abs(green - blue) < brightnessTolerance;
                }

            // Colors within tolerance
            return true;
        }

        /// <summary>
        /// Resizes a <see cref="Bitmap"/> to the specified width and height
        /// </summary>
        /// <param name="image">The <see cref="Bitmap"/> to resize</param>
        /// <param name="scale">The scale of the new image relative to the old image</param>
        /// <returns>The new, resized <see cref="Bitmap"/></returns>
        public static Bitmap Resize(this Bitmap image, double scale)
        {
            int newWidth = (int)(image.Width * scale + 0.5);
            int newHeight = (int)(image.Height * scale + 0.5);
            Bitmap result = new(newWidth, newHeight);

            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
            }

            return result;
        }

        /// <summary>
        /// Resizes a <see cref="Bitmap"/> to the specified width, maintaining height
        /// </summary>
        /// <param name="image">The <see cref="Bitmap"/> to resize</param>
        /// <param name="scale">The scale of the new image relative to the old image</param>
        /// <returns>The new, resized <see cref="Bitmap"/></returns>
        public static Bitmap ResizeWidth(this Bitmap image, double scale)
        {
            int newWidth = (int)(image.Width * scale + 0.5);
            Bitmap result = new(newWidth, 1);

            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.DrawImage(image, 0, 0, newWidth, 1);
            }
            
            return result;
        }

        /// <summary>
        /// Converts a <see cref="Bitmap"/> into a <see cref="BitmapImage"/>
        /// for use in WinUI3 rendering (Must be run on the UI thread)
        /// </summary>
        /// <param name="image">The <see cref="Bitmap"/> to convert</param>
        /// <param name="dispose">Should the <see cref="Bitmap"/> be disposed after
        /// conversion</param>
        /// <returns>The resulting BitmapImage</returns>
        public static BitmapImage ConvertToBitmapImage(this Bitmap image, bool dispose)
        {
            BitmapImage img = new();
            using (MemoryStream stream = new())
            {
                image.Save(stream, ImageFormat.Png);
                stream.Position = 0;
                img.SetSource(stream.AsRandomAccessStream());
            }

            if (dispose) image.Dispose();
            return img;
        }

        /// <summary>
        /// Gets the ARGB values of each pixel in a <see cref="Bitmap"/> in a single-dimensional
        /// byte array. For <see cref="PixelFormat.Format32bppArgb">32BPP</see>, 
        /// every 4 entries in the array describes a single pixel
        /// (order: BGRA)
        /// </summary>
        /// <param name="bitmap">The <see cref="Bitmap"/> to get the argb values from</param>
        /// <returns>
        /// A single-dimensional byte array containing pixel <see cref="Color"/>
        /// information in BGRA order
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

        /// <summary>
        /// Multiplies a <see cref="Rectangle"/> by a <paramref name="scalar"/> double
        /// </summary>
        /// <param name="r">The <see cref="Rectangle"/> to multiply</param>
        /// <param name="scalar">A double value to multiply each parameter of
        /// <paramref name="r"/> by</param>
        /// <returns>The updated <see cref="Rectangle"/></returns>
        public static Rectangle Multiply(this Rectangle r, double scalar)
        {
            return new Rectangle((int)(r.X * scalar + 0.5), (int)(r.Y * scalar + 0.5), 
                (int)(r.Width * scalar + 0.5), (int)(r.Height * scalar + 0.5));
        }
    }
}
