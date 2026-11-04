using System;
using System.Text;
using Xunit;
using OpenKSP.Core.Modding;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T18: DDS/DXT + KSP .mbm decoders (step-03-plan.md Task 4).
    /// Hand-built fixtures; every expected RGBA byte is computed by hand.
    /// </summary>
    public class T18
    {
        // ---- fixture builders ----------------------------------------------

        private static void PutU32(byte[] b, int at, uint v)
        {
            b[at] = (byte)(v & 0xFF);
            b[at + 1] = (byte)((v >> 8) & 0xFF);
            b[at + 2] = (byte)((v >> 16) & 0xFF);
            b[at + 3] = (byte)((v >> 24) & 0xFF);
        }

        private static byte[] DdsFile(int w, int h, int mips, string fourcc,
            int bitCount, uint flags, uint caps, byte[] payload)
        {
            var b = new byte[128 + payload.Length];
            b[0] = (byte)'D'; b[1] = (byte)'D'; b[2] = (byte)'S'; b[3] = (byte)' ';
            PutU32(b, 4, 124);
            PutU32(b, 8, flags);
            PutU32(b, 12, (uint)h);
            PutU32(b, 16, (uint)w);
            PutU32(b, 28, (uint)mips);
            PutU32(b, 76, 32);      // DDS_PIXELFORMAT.dwSize
            PutU32(b, 80, 0x40);    // DDPF_FOURCC
            b[84] = (byte)fourcc[0];
            b[85] = (byte)fourcc[1];
            b[86] = (byte)fourcc[2];
            b[87] = (byte)fourcc[3];
            PutU32(b, 88, (uint)bitCount);
            PutU32(b, 108, caps);
            Array.Copy(payload, 0, b, 128, payload.Length);
            return b;
        }

        private static byte[] MbmFile(int w, int h, int type, int bits, byte[] payload,
            uint magic = 0x50534B03)
        {
            var b = new byte[20 + payload.Length];
            PutU32(b, 0, magic);
            PutU32(b, 4, (uint)w);
            PutU32(b, 8, (uint)h);
            PutU32(b, 12, (uint)type);
            PutU32(b, 16, (uint)bits);
            Array.Copy(payload, 0, b, 20, payload.Length);
            return b;
        }

        private static void AssertPixel(byte[] rgba, int i, int r, int g, int b, int a)
        {
            int o = i * 4;
            Assert.Equal(r, rgba[o]);
            Assert.Equal(g, rgba[o + 1]);
            Assert.Equal(b, rgba[o + 2]);
            Assert.Equal(a, rgba[o + 3]);
        }

        // ---- DXT1 -----------------------------------------------------------

        [Fact]
        public void Dxt1_Decodes_FourByFour_To_Exact_Rgba()
        {
            // c0=0xF800 red (255,0,0), c1=0x07E0 green (0,255,0), c0>c1:
            // c2=(2c0+c1)/3=(170,85,0), c3=(c0+2c1)/3=(85,170,0).
            // Code bytes E4 00 00 00: row0 codes 0,1,2,3; rows1-3 code 0.
            var block = new byte[]
            {
                0x00, 0xF8, 0xE0, 0x07,
                0xE4, 0x00, 0x00, 0x00,
            };
            var img = Dds.Decode(DdsFile(4, 4, 1, "DXT1", 0, 0x1007, 0x1000, block));
            Assert.Equal(64, img.Rgba.Length);
            AssertPixel(img.Rgba, 0, 255, 0, 0, 255);
            AssertPixel(img.Rgba, 1, 0, 255, 0, 255);
            AssertPixel(img.Rgba, 2, 170, 85, 0, 255);
            AssertPixel(img.Rgba, 3, 85, 170, 0, 255);
            for (int i = 4; i < 16; i++)
                AssertPixel(img.Rgba, i, 255, 0, 0, 255);
        }

        [Fact]
        public void Dxt1_Color0LeColor1_Mode3_Is_Transparent()
        {
            // c0=green <= c1=red: c2=c0/2=(0,127,0), c3=c1/2=(127,0,0),
            // code 3 = (0,0,0,0). Code byte 4E: row0 codes 2,3,0,1.
            var block = new byte[]
            {
                0xE0, 0x07, 0x00, 0xF8,
                0x4E, 0x00, 0x00, 0x00,
            };
            var img = Dds.Decode(DdsFile(4, 4, 1, "DXT1", 0, 0x1007, 0x1000, block));
            AssertPixel(img.Rgba, 0, 0, 127, 0, 255);
            AssertPixel(img.Rgba, 1, 0, 0, 0, 0);
            AssertPixel(img.Rgba, 2, 0, 255, 0, 255);
            AssertPixel(img.Rgba, 3, 255, 0, 0, 255);
            for (int i = 4; i < 16; i++)
                AssertPixel(img.Rgba, i, 0, 255, 0, 255);
        }

        // ---- DXT5 -----------------------------------------------------------

        [Fact]
        public void Dxt5_Decodes_FourByFour_To_Exact_Rgba()
        {
            // Alpha: a0=0xFF > a1=0x00 -> table [255,0,218,182,...]
            // (218=(6*255)/7 int-div, 182=(5*255)/7 int-div).
            // Alpha codes 88 06 00...: pixel codes 0,1,2,3 then all 0.
            // Color: c0=0xF800 red > c1=0 -> every code-0 pixel = (255,0,0).
            var block = new byte[]
            {
                0xFF, 0x00, 0x88, 0x06, 0x00, 0x00, 0x00, 0x00,
                0x00, 0xF8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            };
            var img = Dds.Decode(DdsFile(4, 4, 1, "DXT5", 0, 0x1007, 0x1000, block));
            Assert.Equal(64, img.Rgba.Length);
            AssertPixel(img.Rgba, 0, 255, 0, 0, 255);
            AssertPixel(img.Rgba, 1, 255, 0, 0, 0);
            AssertPixel(img.Rgba, 2, 255, 0, 0, 218);
            AssertPixel(img.Rgba, 3, 255, 0, 0, 182);
            for (int i = 4; i < 16; i++)
                AssertPixel(img.Rgba, i, 255, 0, 0, 255);
        }

        // ---- raw DIB ---------------------------------------------------------

        [Fact]
        public void Dib_Rgb24_Expands_To_Opaque_Rgba()
        {
            var payload = new byte[]
            {
                10, 20, 30, 40, 50, 60,
                70, 80, 90, 100, 110, 120,
            };
            var img = Dds.Decode(DdsFile(2, 2, 1, "RGB ", 24, 0x40, 0x1000, payload));
            Assert.Equal(16, img.Rgba.Length);
            AssertPixel(img.Rgba, 0, 10, 20, 30, 255);
            AssertPixel(img.Rgba, 1, 40, 50, 60, 255);
            AssertPixel(img.Rgba, 2, 70, 80, 90, 255);
            AssertPixel(img.Rgba, 3, 100, 110, 120, 255);
        }

        [Fact]
        public void Dib_Argb32_Reorders_To_Rgba()
        {
            // Memory order A,R,G,B.
            var payload = new byte[]
            {
                40, 10, 20, 30, 80, 50, 60, 70,
                120, 90, 100, 110, 160, 130, 140, 150,
            };
            var img = Dds.Decode(DdsFile(2, 2, 1, "ARGB", 32, 0x40, 0x1000, payload));
            AssertPixel(img.Rgba, 0, 10, 20, 30, 40);
            AssertPixel(img.Rgba, 1, 50, 60, 70, 80);
            AssertPixel(img.Rgba, 2, 90, 100, 110, 120);
            AssertPixel(img.Rgba, 3, 130, 140, 150, 160);
        }

        [Fact]
        public void Header_Fields_Are_Parsed()
        {
            var payload = new byte[16];   // 8x4 DXT1 = 2x1 blocks x 8 bytes
            var img = Dds.Decode(DdsFile(8, 4, 3, "DXT1", 0, 0x1007, 0x1000, payload));
            Assert.Equal(8, img.Width);
            Assert.Equal(4, img.Height);
            Assert.Equal(3, img.MipCount);
            Assert.Equal("DXT1", img.FourCc);
            Assert.Equal(0x1007u, img.Flags);
            Assert.Equal(0x1000u, img.Caps);
        }

        // ---- deterministic errors (exact strings) ----------------------------

        [Fact]
        public void Dds_BadMagic_Messages_Are_Deterministic()
        {
            var file = DdsFile(4, 4, 1, "DXT1", 0, 0x1007, 0x1000, new byte[8]);
            file[0] = 0x2A; file[1] = 0; file[2] = 0; file[3] = 0;
            var err = Assert.Throws<FormatException>(() => Dds.Decode(file));
            Assert.Equal(
                "Dds: bad magic 0x0000002A (expected 0x20534444 'DDS ') at offset 0",
                err.Message);
        }

        [Fact]
        public void Dds_Truncated_Block_Fails()
        {
            var file = DdsFile(4, 4, 1, "DXT1", 0, 0x1007, 0x1000, new byte[3]);
            var err = Assert.Throws<FormatException>(() => Dds.Decode(file));
            Assert.Equal("Dds: unexpected end of data at offset 130", err.Message);
        }

        // ---- .mbm ------------------------------------------------------------

        [Fact]
        public void Mbm_Raw32bpp_2x2_Decodes_Exactly()
        {
            var payload = new byte[]
            {
                10, 20, 30, 40, 50, 60, 70, 80,
                90, 100, 110, 120, 130, 140, 150, 160,
            };
            var file = MbmFile(2, 2, 3, 32, payload);
            // Verified raw byte order of the magic (fixture bytes 03 4B 53 50).
            Assert.Equal(0x03, file[0]);
            Assert.Equal(0x4B, file[1]);
            Assert.Equal(0x53, file[2]);
            Assert.Equal(0x50, file[3]);
            var img = Mbm.Decode(file);
            Assert.Equal(2, img.Width);
            Assert.Equal(2, img.Height);
            Assert.Equal(3, img.Type);
            Assert.Equal(32, img.Bits);
            Assert.Equal(16, img.Rgba.Length);       // payload is exactly w*h*4
            AssertPixel(img.Rgba, 0, 10, 20, 30, 40);
            AssertPixel(img.Rgba, 1, 50, 60, 70, 80);
            AssertPixel(img.Rgba, 2, 90, 100, 110, 120);
            AssertPixel(img.Rgba, 3, 130, 140, 150, 160);
        }

        [Fact]
        public void Mbm_Raw24bpp_Expands_To_Opaque_Rgba()
        {
            var payload = new byte[]
            {
                1, 2, 3, 4, 5, 6,
                7, 8, 9, 10, 11, 12,
            };
            var img = Mbm.Decode(MbmFile(2, 2, 0, 24, payload));
            Assert.Equal(20, Mbm.HeaderSize);       // 0x14 == 20
            AssertPixel(img.Rgba, 0, 1, 2, 3, 255);
            AssertPixel(img.Rgba, 1, 4, 5, 6, 255);
            AssertPixel(img.Rgba, 2, 7, 8, 9, 255);
            AssertPixel(img.Rgba, 3, 10, 11, 12, 255);
        }

        [Fact]
        public void Mbm_Trailing_Bytes_Are_Ignored_No_Json_Tail()
        {
            // Real fixture (kethane_mbm.bin) is exactly 20 + w*h*4 bytes: any
            // trailing bytes (e.g. a fabricated "JSON tail") are not parsed.
            var payload = new byte[16];
            var file = MbmFile(2, 2, 0, 32, payload);
            var grown = new byte[file.Length + 8];
            Array.Copy(file, grown, file.Length);
            var img = Mbm.Decode(grown);
            Assert.Equal(16, img.Rgba.Length);
        }

        [Fact]
        public void Mbm_Error_Messages_Are_Deterministic()
        {
            var bad = MbmFile(2, 2, 0, 32, new byte[16], 0x2A);
            var err = Assert.Throws<FormatException>(() => Mbm.Decode(bad));
            Assert.Equal(
                "Mbm: bad magic 0x0000002A (expected 0x50534B03) at offset 0",
                err.Message);

            var bits = MbmFile(2, 2, 0, 16, new byte[8]);
            var err2 = Assert.Throws<FormatException>(() => Mbm.Decode(bits));
            Assert.Equal(
                "Mbm: unsupported bits 16 (expected 24 or 32) at offset 16",
                err2.Message);

            var trunc = MbmFile(2, 2, 0, 32, new byte[15]);
            var err3 = Assert.Throws<FormatException>(() => Mbm.Decode(trunc));
            Assert.Equal(
                "Mbm: unexpected end of data at offset 20 (payload of 16 bytes needed)",
                err3.Message);
        }

        // ---- GlTF seam smoke tests (Task 5; folded into T18 per plan) ------
        // Reader design notes: hand-rolled JSON (JsonMini in GlTF.cs) keeps the
        // DLL free of System.Text.Json typerefs; external buffer URIs are not
        // fetched (seam), .glb BIN chunk is exposed via BinChunk.

        private const string GltfDoc =
            "{\"asset\":{\"version\":\"2.0\"},\"scene\":0," +
            "\"scenes\":[{\"nodes\":[0]}]," +
            "\"nodes\":[{\"name\":\"root\",\"children\":[1]}," +
            "{\"name\":\"hull\",\"mesh\":0,\"translation\":[1.5,2,-3]}]," +
            "\"meshes\":[{\"name\":\"hullMesh\",\"primitives\":[{\"attributes\":" +
            "{\"POSITION\":0,\"NORMAL\":1},\"indices\":2,\"material\":0}]}]," +
            "\"materials\":[{\"name\":\"mat-hull\"}]," +
            "\"buffers\":[{\"byteLength\":108}]," +
            "\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":72}," +
            "{\"buffer\":0,\"byteOffset\":72,\"byteLength\":36}]," +
            "\"accessors\":[{\"bufferView\":0,\"byteOffset\":0,\"componentType\":5126," +
            "\"count\":6,\"type\":\"VEC3\"},{\"bufferView\":0,\"byteOffset\":72," +
            "\"componentType\":5126,\"count\":3,\"type\":\"VEC3\"}," +
            "{\"bufferView\":1,\"byteOffset\":0,\"componentType\":5125," +
            "\"count\":3,\"type\":\"SCALAR\"}]}";

        [Fact]
        public void Gltf_minimal_text_document_parses()
        {
            // Plan fixture: 2-node scene, 1 mesh (POSITION/NORMAL/indices),
            // 1 material, 1 buffer, 2 bufferViews, 3 accessors.
            var s = GlTF.Decode(Encoding.UTF8.GetBytes(GltfDoc));

            Assert.Equal(2, s.Nodes.Count);
            Assert.Single(s.RootNodes);
            Assert.Equal(0, s.RootNodes[0]);
            Assert.Equal("root", s.Nodes[0].Name);
            Assert.Single(s.Nodes[0].Children);
            Assert.Equal(1, s.Nodes[0].Children[0]);
            Assert.Equal("hull", s.Nodes[1].Name);
            Assert.Equal(0, s.Nodes[1].Mesh);
            Assert.Equal(3, s.Nodes[1].Translation!.Length);
            Assert.Equal(1.5, s.Nodes[1].Translation![0]);
            Assert.Equal(-3.0, s.Nodes[1].Translation![2]);
            Assert.Null(s.Nodes[0].Rotation);

            Assert.Single(s.Meshes);
            Assert.Equal("hullMesh", s.Meshes[0].Name);
            Assert.Single(s.Meshes[0].Primitives);
            var prim = s.Meshes[0].Primitives[0];
            Assert.Equal(0, prim.Position);
            Assert.Equal(1, prim.Normal);
            Assert.Equal(-1, prim.Tex0);
            Assert.Equal(2, prim.Indices);
            Assert.Equal(0, prim.Material);
            Assert.Equal(4, prim.Mode);

            Assert.Single(s.Materials);
            Assert.Equal("mat-hull", s.Materials[0].Name);
            Assert.Single(s.Buffers);
            Assert.Equal(108, s.Buffers[0].ByteLength);
            Assert.Equal(2, s.BufferViews.Count);
            Assert.Equal(3, s.Accessors.Count);
            Assert.Equal(5125, s.Accessors[2].ComponentType);
            Assert.Equal("SCALAR", s.Accessors[2].Type);
            Assert.Null(s.BinChunk);
        }

        [Fact]
        public void Glb_minimal_container_parses_and_exposes_bin_chunk()
        {
            string json = "{\"asset\":{\"version\":\"2.0\"}," +
                "\"nodes\":[{\"name\":\"a\"},{\"name\":\"b\"}]," +
                "\"materials\":[{\"name\":\"m\"}]," +
                "\"buffers\":[{\"byteLength\":8}]}";
            byte[] jb = Encoding.UTF8.GetBytes(json);
            int pad = (4 - jb.Length % 4) % 4;
            const int binLen = 8;
            int total = 12 + 8 + jb.Length + pad + 8 + binLen;
            var g = new byte[total];
            PutU32(g, 0, 0x46546C67);   // 'glTF'
            PutU32(g, 4, 2);            // version
            PutU32(g, 8, (uint)total);
            PutU32(g, 12, (uint)(jb.Length + pad));
            PutU32(g, 16, 0x4E4F534A);  // 'JSON'
            for (int i = 0; i < jb.Length; i++)
                g[20 + i] = jb[i];
            for (int i = 0; i < pad; i++)
                g[20 + jb.Length + i] = 0x20;   // JSON chunks pad with spaces
            int p2 = 20 + jb.Length + pad;
            PutU32(g, p2, binLen);
            PutU32(g, p2 + 4, 0x004E4942);      // 'BIN\0'
            for (int i = 0; i < binLen; i++)
                g[p2 + 8 + i] = (byte)(i + 1);

            var s = GlTF.Decode(g);
            Assert.Equal(2, s.Nodes.Count);
            Assert.Single(s.Materials);
            Assert.Single(s.Buffers);
            // No declared scene: roots = nodes without parents.
            Assert.Equal(2, s.RootNodes.Count);
            Assert.Equal(0, s.RootNodes[0]);
            Assert.Equal(1, s.RootNodes[1]);
            Assert.NotNull(s.BinChunk);
            Assert.Equal(binLen, s.BinChunk!.Length);
            Assert.Equal(1, s.BinChunk![0]);
            Assert.Equal(8, s.BinChunk![7]);
        }

        [Fact]
        public void Gltf_errors_are_deterministic()
        {
            var err = Assert.Throws<FormatException>(() =>
                GlTF.Decode(Encoding.UTF8.GetBytes("{\"asset\":{\"version\":\"1.0\"}}")));
            Assert.Equal(
                "GlTF: unsupported asset.version '1.0' (expected '2.0')",
                err.Message);

            var err2 = Assert.Throws<FormatException>(() =>
                GlTF.Decode(Encoding.UTF8.GetBytes("{bad")));
            Assert.Equal(
                "GlTF: JSON parse error at offset 1: expected string key",
                err2.Message);

            // glb header promising a 100-byte JSON chunk, file only 24 bytes.
            var t = new byte[24];
            PutU32(t, 0, 0x46546C67);
            PutU32(t, 4, 2);
            PutU32(t, 8, 112);
            PutU32(t, 12, 100);
            PutU32(t, 16, 0x4E4F534A);
            var err3 = Assert.Throws<FormatException>(() => GlTF.Decode(t));
            Assert.Equal(
                "GlTF: unexpected end of data at offset 20",
                err3.Message);
        }
    }
}
