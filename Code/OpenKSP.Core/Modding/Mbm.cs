using System;

namespace OpenKSP.Core.Modding
{
    /// <summary>Decoded KSP .mbm texture: header fields plus a flat row-major RGBA buffer.</summary>
    public sealed class MbmImage
    {
        public int Width;
        public int Height;
        public int Type;
        public int Bits;
        /// <summary>Row-major RGBA, w*h*4 bytes (R,G,B,A at offsets 0,1,2,3).</summary>
        public byte[] Rgba = new byte[0];
    }

    /// <summary>
    /// KSP part-texture (.mbm) reader (step-03-research.md §3, verified
    /// against the real fixture and mbm2tga.c). Layout, all little-endian:
    ///   0x00 uint32 magic   = 0x50534B03 (bytes 03 4B 53 50)
    ///   0x04 uint32 width
    ///   0x08 uint32 height
    ///   0x0C uint32 type
    ///   0x10 uint32 bits     (24 or 32)
    ///   0x14 raw pixels, width*height*(bits/8) bytes, R,G,B,A order
    /// There is no DDS DIB and no trailing JSON — the orchestrator's "split
    /// DIB from JSON tail" phrasing is superseded by this verified layout
    /// (recorded in step-03-research.md §3; see plan Open questions).
    /// Output is always flat row-major RGBA (w*h*4); 24bpp input gets A=255.
    /// No UnityEngine, no LINQ.
    /// </summary>
    public static class Mbm
    {
        public const uint Magic = 0x50534B03;
        public const int HeaderSize = 20;

        public static MbmImage Decode(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            var r = new BinLE(bytes, "Mbm");

            uint magic = r.ReadUInt();
            if (magic != Magic)
                throw new FormatException(
                    "Mbm: bad magic 0x" + magic.ToString("X8") +
                    " (expected 0x50534B03) at offset 0");
            int width = (int)r.ReadUInt();
            int height = (int)r.ReadUInt();
            int type = (int)r.ReadUInt();
            int bits = (int)r.ReadUInt();
            if (width <= 0 || height <= 0)
                throw new FormatException("Mbm: zero width or height at offset 4");
            if (bits != 24 && bits != 32)
                throw new FormatException(
                    "Mbm: unsupported bits " + bits + " (expected 24 or 32) at offset 16");

            long need = (long)width * height * (bits / 8);
            if (r.Length - r.Position < need)
                throw new FormatException(
                    "Mbm: unexpected end of data at offset " + r.Position +
                    " (payload of " + need + " bytes needed)");

            var img = new MbmImage();
            img.Width = width;
            img.Height = height;
            img.Type = type;
            img.Bits = bits;
            int n = width * height;
            img.Rgba = new byte[n * 4];
            if (bits == 32)
            {
                for (int i = 0; i < n * 4; i++)
                    img.Rgba[i] = (byte)r.ReadByte();
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    img.Rgba[i * 4] = (byte)r.ReadByte();
                    img.Rgba[i * 4 + 1] = (byte)r.ReadByte();
                    img.Rgba[i * 4 + 2] = (byte)r.ReadByte();
                    img.Rgba[i * 4 + 3] = 255;
                }
            }
            // Extra trailing bytes (if any) are ignored; the verified real
            // fixture's payload is exactly w*h*bits/8, and no JSON tail exists.
            return img;
        }
    }
}
