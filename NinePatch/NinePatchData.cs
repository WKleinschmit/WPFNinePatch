using System.Drawing;
using System.IO;

namespace NinePatch
{
    class NinePatchData
    {
        public int OutlineInsetsLeft { get; set; }
        public int OutlineInsetsTop { get; set; }
        public int OutlineInsetsRight { get; set; }
        public int OutlineInsetsBottom { get; set; }
        public float Radius { get; set; }
        public uint Alpha { get; set; }

        public bool HasOutline { get; private set; }
        public bool HasLayoutBounds { get; private set; }
        public bool HasPatches { get; private set; }

        public void Read_npOl(BinaryReader reader)
        {
            OutlineInsetsLeft = reader.ReadInt32();
            OutlineInsetsTop = reader.ReadInt32();
            OutlineInsetsRight = reader.ReadInt32();
            OutlineInsetsBottom = reader.ReadInt32();
            Radius = reader.ReadSingle();
            Alpha = reader.ReadUInt32();
            HasOutline = true;
        }

        public int LayoutBoundsLeft { get; set; }
        public int LayoutBoundsTop { get; set; }
        public int LayoutBoundsRight { get; set; }
        public int LayoutBoundsBottom { get; set; }

        public void Read_npLb(BinaryReader reader)
        {
            LayoutBoundsLeft = reader.ReadInt32();
            LayoutBoundsTop = reader.ReadInt32();
            LayoutBoundsRight = reader.ReadInt32();
            LayoutBoundsBottom = reader.ReadInt32();
            HasLayoutBounds = true;
        }

        public byte WasDeserialized { get; set; }
        public byte NumXDivs { get; set; }
        public byte NumYDivs { get; set; }
        public byte NumColors { get; set; }
        public uint XDivsOffset { get; set; }
        public uint YDivsOffset { get; set; }
        public int PaddingLeft { get; set; }
        public int PaddingRight { get; set; }
        public int PaddingTop { get; set; }
        public int PaddingBottom { get; set; }
        public uint ColorsOffset { get; set; }
        //BigEndian();
        public uint[] XDivs { get; set; }
        public uint[] YDivs { get; set; }
        public uint[] Colors { get; set; }

        public void Read_npTc(BinaryReader reader)
        {
            WasDeserialized = reader.ReadByte();
            NumXDivs = reader.ReadByte();
            NumYDivs = reader.ReadByte();
            NumColors = reader.ReadByte();
            XDivsOffset = reader.ReadUInt32();
            YDivsOffset = reader.ReadUInt32();
            PaddingLeft = reader.ReadBigEndianInt32();
            PaddingRight = reader.ReadBigEndianInt32();
            PaddingTop = reader.ReadBigEndianInt32();
            PaddingBottom = reader.ReadBigEndianInt32();
            ColorsOffset = reader.ReadUInt32();

            XDivs = new uint[NumXDivs];
            for (int i = 0; i < NumXDivs; i++)
                XDivs[i] = reader.ReadBigEndianUInt32();

            YDivs = new uint[NumYDivs];
            for (int i = 0; i < NumYDivs; i++)
                YDivs[i] = reader.ReadBigEndianUInt32();

            Colors = new uint[NumColors];
            for (int i = 0; i < NumColors; i++)
                Colors[i] = reader.ReadBigEndianUInt32();

            HasPatches = true;
        }
    }
}
