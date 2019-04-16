using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace NinePatch
{
    public static class ExtensionMethods
    {
        public static ulong ReadBigEndianUInt64(this BinaryReader reader)
        {
            return BitConverter.ToUInt64(reader.ReadBytes(sizeof(ulong)).Reverse().ToArray(), 0);
        }

        public static long ReadBigEndianInt64(this BinaryReader reader)
        {
            return BitConverter.ToInt64(reader.ReadBytes(sizeof(long)).Reverse().ToArray(), 0);
        }

        public static uint ReadBigEndianUInt32(this BinaryReader reader)
        {
            return BitConverter.ToUInt32(reader.ReadBytes(sizeof(uint)).Reverse().ToArray(), 0);
        }

        public static int ReadBigEndianInt32(this BinaryReader reader)
        {
            return BitConverter.ToInt32(reader.ReadBytes(sizeof(int)).Reverse().ToArray(), 0);
        }

        public static float ReadBigEndianSingle(this BinaryReader reader)
        {
            return BitConverter.ToSingle(reader.ReadBytes(sizeof(float)).Reverse().ToArray(), 0);
        }

        public static double ReadBigEndianDouble(this BinaryReader reader)
        {
            return BitConverter.ToDouble(reader.ReadBytes(sizeof(double)).Reverse().ToArray(), 0);
        }

        public static void Skip(this BinaryReader reader, ulong count)
        {
            while (count > int.MaxValue)
            {
                reader.ReadBytes(int.MaxValue);
                count -= int.MaxValue;
            }
            reader.ReadBytes((int)count);
        }

        public static uint GetPixel(this BitmapData bmpData, uint x, uint y)
        {
            uint startIndex = x * 4u + y * (uint)bmpData.Stride;
            byte[] data = new byte[4];
            Marshal.Copy(bmpData.Scan0 + (int)startIndex, data, 0, 4);
            return BitConverter.ToUInt32(data, 0);
        }

        /// <summary>
        /// Converts a <see cref="System.Drawing.Image"/> into a WPF <see cref="BitmapSource"/>.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <returns>A BitmapSource</returns>
        public static BitmapSource ToBitmapSource(this System.Drawing.Image source)
        {
            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(source);
            try
            {
                return bitmap.ToBitmapSource();
            }
            finally
            {
                bitmap.Dispose();
            }
        }

        /// <summary>
        /// Converts a <see cref="System.Drawing.Bitmap"/> into a WPF <see cref="BitmapSource"/>.
        /// </summary>
        /// <remarks>Uses GDI to do the conversion. Hence the call to the marshalled DeleteObject.
        /// </remarks>
        /// <param name="source">The source bitmap.</param>
        /// <returns>A BitmapSource</returns>
        public static BitmapSource ToBitmapSource(this System.Drawing.Bitmap source)
        {
            var hBitmap = source.GetHbitmap();

            try
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                NativeMethods.DeleteObject(hBitmap);
            }
        }
    }
}
