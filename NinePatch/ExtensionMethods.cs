using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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
    }
}
