using System;

namespace OpenKSP.Core.Modding
{
    /// <summary>
    /// Little-endian cursor over a raw byte buffer, shared by the binary
    /// format readers in this assembly. Truncated reads throw a deterministic
    /// FormatException naming the owner format and the failing offset.
    /// </summary>
    internal sealed class BinLE
    {
        private readonly byte[] _bytes;
        private readonly string _who;
        public int Position;

        public BinLE(byte[] bytes, string who)
        {
            _bytes = bytes;
            _who = who;
            Position = 0;
        }

        public int Length { get { return _bytes.Length; } }

        public void Need(int n)
        {
            if (Position + n > _bytes.Length)
                throw new FormatException(_who + ": unexpected end of data at offset " + Position);
        }

        public int ReadByte()
        {
            Need(1);
            return _bytes[Position++];
        }

        public int ReadUInt16()
        {
            Need(2);
            int v = _bytes[Position] | (_bytes[Position + 1] << 8);
            Position += 2;
            return v;
        }

        public int ReadInt()
        {
            Need(4);
            int v = _bytes[Position] | (_bytes[Position + 1] << 8) |
                    (_bytes[Position + 2] << 16) | (_bytes[Position + 3] << 24);
            Position += 4;
            return v;
        }

        public uint ReadUInt()
        {
            return (uint)ReadInt();
        }

        /// <summary>Four raw bytes as a 4-char string (may contain NUL).</summary>
        public string ReadFourCc()
        {
            Need(4);
            string s = "" + (char)_bytes[Position] + (char)_bytes[Position + 1] +
                       (char)_bytes[Position + 2] + (char)_bytes[Position + 3];
            Position += 4;
            return s;
        }
    }

    /// <summary>Decoded DDS image: header fields plus a flat row-major RGBA buffer.</summary>
    public sealed class DdsImage
    {
        public int Width;
        public int Height;
        public int MipCount;
        public uint Flags;
        public uint Caps;
        public string FourCc = "";
        /// <summary>Row-major RGBA, w*h*4 bytes (R,G,B,A at offsets 0,1,2,3).</summary>
        public byte[] Rgba = new byte[0];
    }

    /// <summary>
    /// Generic DirectDraw Surface reader (step-03-research.md §4): parses the
    /// 128-byte DDS header and decodes the base mip level to flat row-major
    /// RGBA. Supported fourcc: "DXT1", "DXT5" (4x4 macroblocks), "RGB "
    /// (24bpp raw), "ARGB" / "RGBA" (32bpp raw; "ARGB" bytes are ordered
    /// A,R,G,B in memory, "RGBA" as named). Higher mips are present in the
    /// file but not decoded. No UnityEngine, no LINQ.
    ///
    /// Codeword convention (matching the DXT spec / DirectXTex): DXT1 2-bit
    /// color codes are read LSB-first from the 4 code bytes; DXT5 3-bit
    /// alpha codes are a 48-bit little-endian stream starting at the first
    /// code byte (pixel i takes bits 3*i..3*i+2). Pixels inside each 4x4
    /// block are row-major (x = i % 4, y = i / 4) and clipped to image size.
    /// </summary>
    public static class Dds
    {
        public const uint Magic = 0x20534444;   // 'DDS '
        public const int HeaderSize = 128;

        public static DdsImage Decode(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            var r = new BinLE(bytes, "Dds");

            uint magic = r.ReadUInt();
            if (magic != Magic)
                throw new FormatException(
                    "Dds: bad magic 0x" + magic.ToString("X8") +
                    " (expected 0x20534444 'DDS ') at offset 0");
            int headerSize = r.ReadInt();
            if (headerSize != 124)
                throw new FormatException(
                    "Dds: bad header size " + headerSize + " (expected 124) at offset 4");

            var img = new DdsImage();
            img.Flags = r.ReadUInt();                       // 8
            int height = (int)r.ReadUInt();                 // 12
            int width = (int)r.ReadUInt();                  // 16
            r.ReadUInt();                                   // 20 pitchOrLinearSize
            r.ReadUInt();                                   // 24 depth
            img.MipCount = (int)r.ReadUInt();               // 28
            for (int i = 0; i < 11; i++)
                r.ReadUInt();                               // 32..75 reserved1
            int pfSize = (int)r.ReadUInt();                 // 76
            if (pfSize != 32)
                throw new FormatException(
                    "Dds: bad pixel-format size " + pfSize + " (expected 32) at offset 76");
            r.ReadUInt();                                   // 80 pf flags
            img.FourCc = r.ReadFourCc();                    // 84
            int bitCount = (int)r.ReadUInt();               // 88
            r.ReadUInt(); r.ReadUInt(); r.ReadUInt(); r.ReadUInt(); // 92..104 masks
            img.Caps = r.ReadUInt();                        // 108
            r.ReadUInt(); r.ReadUInt(); r.ReadUInt(); r.ReadUInt(); // 112..124

            if (width <= 0 || height <= 0)
                throw new FormatException("Dds: zero width or height at offset 12");
            img.Width = width;
            img.Height = height;

            // Payload starts at offset 128 (base mip only).
            if (img.FourCc == "DXT1")
                DecodeDxt1(r, img);
            else if (img.FourCc == "DXT5")
                DecodeDxt5(r, img);
            else if (img.FourCc == "RGB " || img.FourCc == "RGB\0")
                DecodeDib24(r, img, bitCount);
            else if (img.FourCc == "ARGB" || img.FourCc == "RGBA")
                DecodeDib32(r, img, bitCount, img.FourCc == "ARGB");
            else
                throw new FormatException(
                    "Dds: unsupported fourcc '" + img.FourCc + "' at offset 84");
            return img;
        }

        // ---- block formats -------------------------------------------------

        /// <summary>Expand a 5-6-5 color to 8-bit channels (replicated low bits).</summary>
        private static void From565(int c, out int r8, out int g8, out int b8)
        {
            int r5 = (c >> 11) & 31;
            int g6 = (c >> 5) & 63;
            int b5 = c & 31;
            r8 = (r5 << 3) | (r5 >> 2);
            g8 = (g6 << 2) | (g6 >> 4);
            b8 = (b5 << 3) | (b5 >> 2);
        }

        /// <summary>
        /// Read the 4-byte color pair of a DXT1/DXT5 block into a 4-entry
        /// RGBA table (table[e*4+0..2]; alpha is 255 except DXT1's
        /// one-bit-alpha mode, where entry 3 is fully transparent).
        /// </summary>
        private static void ReadDxtColors(BinLE r, bool dxt5, int[] table)
        {
            int c0 = r.ReadUInt16();
            int c1 = r.ReadUInt16();
            int r0, g0, b0, r1, g1, b1;
            From565(c0, out r0, out g0, out b0);
            From565(c1, out r1, out g1, out b1);

            int r2, g2, b2, r3, g3, b3, a3 = 255;
            if (c0 > c1)
            {
                r2 = (2 * r0 + r1) / 3;
                g2 = (2 * g0 + g1) / 3;
                b2 = (2 * b0 + b1) / 3;
                r3 = (r0 + 2 * r1) / 3;
                g3 = (g0 + 2 * g1) / 3;
                b3 = (b0 + 2 * b1) / 3;
            }
            else
            {
                r2 = r0 >> 1; g2 = g0 >> 1; b2 = b0 >> 1;
                if (dxt5)
                {
                    r3 = r1 >> 1; g3 = g1 >> 1; b3 = b1 >> 1;
                }
                else
                {
                    // DXT1 one-bit-alpha mode: code 3 is fully transparent black.
                    r3 = 0; g3 = 0; b3 = 0; a3 = 0;
                }
            }

            table[0] = r0; table[1] = g0; table[2] = b0; table[3] = 255;
            table[4] = r1; table[5] = g1; table[6] = b1; table[7] = 255;
            table[8] = r2; table[9] = g2; table[10] = b2; table[11] = 255;
            table[12] = r3; table[13] = g3; table[14] = b3; table[15] = a3;
        }

        private static void DecodeDxt1(BinLE r, DdsImage img)
        {
            int blocksX = (img.Width + 3) / 4;
            int blocksY = (img.Height + 3) / 4;
            int[] table = new int[16];
            img.Rgba = new byte[img.Width * img.Height * 4];
            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    ReadDxtColors(r, false, table);
                    int codes = r.ReadInt();    // 4 code bytes, LSB-first
                    for (int i = 0; i < 16; i++)
                    {
                        int code = (codes >> (i * 2)) & 3;
                        WritePixel(img, bx, by, i,
                            table[code * 4], table[code * 4 + 1],
                            table[code * 4 + 2], table[code * 4 + 3]);
                    }
                }
            }
        }

        private static void DecodeDxt5(BinLE r, DdsImage img)
        {
            int blocksX = (img.Width + 3) / 4;
            int blocksY = (img.Height + 3) / 4;
            int[] table = new int[16];
            int[] alpha = new int[8];
            img.Rgba = new byte[img.Width * img.Height * 4];
            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    int a0 = r.ReadByte();
                    int a1 = r.ReadByte();
                    int[] codeBytes = new int[6];
                    for (int i = 0; i < 6; i++)
                        codeBytes[i] = r.ReadByte();
                    if (a0 > a1)
                    {
                        alpha[0] = a0;
                        alpha[1] = a1;
                        alpha[2] = (6 * a0 + a1) / 7;
                        alpha[3] = (5 * a0 + 2 * a1) / 7;
                        alpha[4] = (4 * a0 + 3 * a1) / 7;
                        alpha[5] = (3 * a0 + 4 * a1) / 7;
                        alpha[6] = (2 * a0 + 5 * a1) / 7;
                        alpha[7] = (a0 + 6 * a1) / 7;
                    }
                    else
                    {
                        alpha[0] = a0;
                        alpha[1] = a1;
                        alpha[2] = (4 * a0 + 1) / 5;
                        alpha[3] = (3 * a0 + 2) / 5;
                        alpha[4] = (2 * a0 + 3) / 5;
                        alpha[5] = (a0 + 4) / 5;
                        alpha[6] = 0;
                        alpha[7] = 255;
                    }
                    ReadDxtColors(r, true, table);
                    int colorCodes = r.ReadInt();   // 4 color code bytes, LSB-first
                    for (int i = 0; i < 16; i++)
                    {
                        int ccode = (colorCodes >> (i * 2)) & 3;
                        int which = i * 3;
                        int byteIdx = which >> 3;
                        int shift = which & 7;
                        int code = (codeBytes[byteIdx] >> shift) & 7;
                        if (shift > 5)
                            code |= (codeBytes[byteIdx + 1] << (8 - shift)) & 7;
                        WritePixel(img, bx, by, i,
                            table[ccode * 4], table[ccode * 4 + 1],
                            table[ccode * 4 + 2], alpha[code]);
                    }
                }
            }
        }

        private static void WritePixel(DdsImage img, int bx, int by, int i,
            int r8, int g8, int b8, int a8)
        {
            int px = bx * 4 + (i & 3);
            int py = by * 4 + (i >> 2);
            if (px >= img.Width || py >= img.Height)
                return;
            int o = (py * img.Width + px) * 4;
            img.Rgba[o] = (byte)r8;
            img.Rgba[o + 1] = (byte)g8;
            img.Rgba[o + 2] = (byte)b8;
            img.Rgba[o + 3] = (byte)a8;
        }

        // ---- raw DIB formats ------------------------------------------------

        private static void DecodeDib24(BinLE r, DdsImage img, int bitCount)
        {
            if (bitCount != 24)
                throw new FormatException(
                    "Dds: bad bit count " + bitCount + " (expected 24) at offset 88");
            int n = img.Width * img.Height;
            img.Rgba = new byte[n * 4];
            for (int i = 0; i < n; i++)
            {
                int rr = r.ReadByte();
                int gg = r.ReadByte();
                int bb = r.ReadByte();
                img.Rgba[i * 4] = (byte)rr;
                img.Rgba[i * 4 + 1] = (byte)gg;
                img.Rgba[i * 4 + 2] = (byte)bb;
                img.Rgba[i * 4 + 3] = 255;
            }
        }

        private static void DecodeDib32(BinLE r, DdsImage img, int bitCount, bool argb)
        {
            if (bitCount != 32)
                throw new FormatException(
                    "Dds: bad bit count " + bitCount + " (expected 32) at offset 88");
            int n = img.Width * img.Height;
            img.Rgba = new byte[n * 4];
            for (int i = 0; i < n; i++)
            {
                int b0 = r.ReadByte();
                int b1 = r.ReadByte();
                int b2 = r.ReadByte();
                int b3 = r.ReadByte();
                if (argb)   // memory order A,R,G,B
                {
                    img.Rgba[i * 4] = (byte)b1;
                    img.Rgba[i * 4 + 1] = (byte)b2;
                    img.Rgba[i * 4 + 2] = (byte)b3;
                    img.Rgba[i * 4 + 3] = (byte)b0;
                }
                else        // "RGBA": memory order R,G,B,A
                {
                    img.Rgba[i * 4] = (byte)b0;
                    img.Rgba[i * 4 + 1] = (byte)b1;
                    img.Rgba[i * 4 + 2] = (byte)b2;
                    img.Rgba[i * 4 + 3] = (byte)b3;
                }
            }
        }
    }
}
