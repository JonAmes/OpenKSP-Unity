using OpenKSP.Core.Modding;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T16: Task 3 of Phase 3 — the .mu binary mesh reader (Mu.cs).
    ///
    /// Builds a version-2 .mu file byte-for-byte in memory (one transform, one
    /// child transform, one submesh, one v2 "material3" specular material, one
    /// texture, renderer + tag/layer) and asserts exact counts and values for
    /// verts / uvs / normals / triangles / materials. The LHS quaternion
    /// convention (stored (x,y,z,w) -> emitted (W,X,Y,Z) = (q3,-q0,-q2,-q1),
    /// mu.py's read_quaternion) is pinned on identity — reads (1, -0, -0, -0),
    /// with the negative zeros asserted via their sign bits — and on a known
    /// 45° half-angle rotation about X — reads (0.7071, 0.7071, -0, -0).
    /// A wrong magic and a truncated payload raise the exact deterministic
    /// FormatException strings.
    /// </summary>
    public class T16
    {
        private const float HalfRoot2 = 0.7071068f;   // sin(45°) = cos(45°), float

        // --- byte builder ---------------------------------------------------

        private sealed class Builder
        {
            public List<byte> Bytes = new List<byte>();

            public void I(int v)
            {
                Bytes.Add((byte)(v & 0xFF));
                Bytes.Add((byte)((v >> 8) & 0xFF));
                Bytes.Add((byte)((v >> 16) & 0xFF));
                Bytes.Add((byte)((v >> 24) & 0xFF));
            }

            public void F(float v)
            {
                byte[] b = BitConverter.GetBytes(v);
                for (int i = 0; i < 4; i++)
                    Bytes.Add(b[i]);
            }

            /// <summary>7-bit varint length + raw bytes (names stay ASCII here).</summary>
            public void S(string s)
            {
                Bytes.Add((byte)s.Length);   // all fixture names fit one 7-bit group
                for (int i = 0; i < s.Length; i++)
                    Bytes.Add((byte)s[i]);
            }
        }

        private static byte[] BuildFixtureV2()
        {
            var b = new Builder();
            b.I(76543);        // magic 0x012AFF
            b.I(2);            // version 2
            b.S("t16-model");  // file-level name

            // root transform; identity quat stored (x,y,z,w) = (+0,+0,+0,1)
            b.S("root");
            b.F(0f); b.F(0f); b.F(0f);
            b.F(0f); b.F(0f); b.F(0f); b.F(1f);
            b.F(1f); b.F(1f); b.F(1f);

            // ET_TAG_AND_LAYER
            b.I(MuEntry.TagAndLayer);
            b.S("Untagged");
            b.I(0);

            // ET_MESH_FILTER — full mesh block
            b.I(MuEntry.MeshFilter);
            b.I(MuEntry.MeshStart);
            b.I(3);            // numVerts
            b.I(1);            // numSubmeshes
            b.I(MuEntry.MeshVerts);
            b.F(0f); b.F(0f); b.F(0f);
            b.F(1f); b.F(0f); b.F(0f);
            b.F(0f); b.F(1f); b.F(0f);
            b.I(MuEntry.MeshUv);
            b.F(0f); b.F(0f);
            b.F(1f); b.F(0f);
            b.F(0f); b.F(1f);
            b.I(MuEntry.MeshNormals);
            b.F(0f); b.F(0f); b.F(1f);
            b.F(0f); b.F(0f); b.F(1f);
            b.F(0f); b.F(0f); b.F(1f);
            b.I(MuEntry.MeshTriangles);
            b.I(3);            // int count, then triplets
            b.I(0); b.I(1); b.I(2);
            b.I(MuEntry.MeshEnd);

            // ET_MESH_RENDERER — version 2 carries shadow bytes
            b.I(MuEntry.MeshRenderer);
            b.Bytes.Add(1);    // castShadows
            b.Bytes.Add(1);    // receiveShadows
            b.I(1);            // numMaterials
            b.I(0);

            // ET_TEXTURES (file-level)
            b.I(MuEntry.Textures);
            b.I(1);
            b.S("tex_d");
            b.I(0);            // TT_TEXTURE

            // ET_MATERIALS (file-level) — v2 material3, ST_SPECULAR
            b.I(MuEntry.Materials);
            b.I(1);
            b.S("mat_a");
            b.I(MuShaderType.Specular);
            b.I(0);            // _MainTex -> texture 0
            b.F(1f); b.F(1f);  // scale
            b.F(0f); b.F(0f);  // offset
            b.F(0.25f); b.F(0.5f); b.F(0.75f); b.F(1f); // _SpecColor
            b.F(0.375f);       // _Shininess

            // ET_CHILD_TRANSFORM_START — quat stored (x,y,z,w) =
            // (-0.7071068, +0, +0, 0.7071068); reads (W,X,Y,Z)=(.7071,.7071,-0,-0)
            b.I(MuEntry.ChildTransformStart);
            b.S("child");
            b.F(0f); b.F(1f); b.F(2f);
            b.F(-HalfRoot2); b.F(0f); b.F(0f); b.F(HalfRoot2);
            b.F(2f); b.F(2f); b.F(2f);
            b.I(MuEntry.ChildTransformEnd);

            // EOF terminates the root object loop (mu.py break on EOFError)
            return b.Bytes.ToArray();
        }

        // --- parsed fixture -------------------------------------------------

        [Fact]
        public void ParsesHandBuiltV2File()
        {
            MuFile f = Mu.Read(BuildFixtureV2());

            Assert.Equal(2, f.Version);
            Assert.Equal("t16-model", f.Name);

            // root transform
            Assert.Equal("root", f.Root.Transform.Name);
            Assert.Equal(0f, f.Root.Transform.LocalPosition.X);
            Assert.Equal(1f, f.Root.Transform.LocalScale.X);
            Assert.Equal(1f, f.Root.Transform.LocalScale.Y);
            Assert.Equal(1f, f.Root.Transform.LocalScale.Z);

            // tag/layer
            Assert.NotNull(f.Root.TagLayer);
            Assert.Equal("Untagged", f.Root.TagLayer!.Tag);
            Assert.Equal(0, f.Root.TagLayer.Layer);

            // mesh: exact counts and values
            MuMesh m = f.Root.SharedMesh!;
            Assert.NotNull(m);
            Assert.Equal(3, m.NumVertices);
            Assert.Equal(1, m.NumSubmeshes);
            Assert.Equal(3, m.Verts.Length);
            Assert.Equal(1f, m.Verts[1].X);
            Assert.Equal(1f, m.Verts[2].Y);
            Assert.Equal(3, m.Uvs.Length);
            Assert.Equal(1f, m.Uvs[1].X);
            Assert.Equal(1f, m.Uvs[2].Y);
            Assert.Empty(m.Uv2s);
            Assert.Equal(3, m.Normals.Length);
            Assert.Equal(1f, m.Normals[0].Z);
            Assert.Single(m.Submeshes);
            Assert.Equal(new int[] { 0, 1, 2 }, m.Submeshes[0]);
            Assert.Equal(0, m.Submeshes[0].Length % 3);

            // renderer
            Assert.NotNull(f.Root.Renderer);
            Assert.Equal(1, f.Root.Renderer!.CastShadows);
            Assert.Equal(1, f.Root.Renderer.ReceiveShadows);
            Assert.Equal(new int[] { 0 }, f.Root.Renderer.MaterialIndices);

            // textures (file-level)
            Assert.Single(f.Textures);
            Assert.Equal("tex_d", f.Textures[0].Name);
            Assert.Equal(0, f.Textures[0].Type);

            // materials (file-level, v2 -> material3 layout)
            Assert.Single(f.Materials);
            MuMaterial mat = f.Materials[0];
            Assert.Equal("mat_a", mat.Name);
            Assert.Equal(MuShaderType.Specular, mat.ShaderType);
            Assert.Equal("KSP/Specular", mat.ShaderName);
            Assert.True(mat.TextureProperties.ContainsKey("_MainTex"));
            Assert.Equal(0, mat.TextureProperties["_MainTex"].Index);
            Assert.Equal(1f, mat.TextureProperties["_MainTex"].Scale.X);
            Assert.Equal(0.25f, mat.ColorProperties["_SpecColor"].X);
            Assert.Equal(0.5f, mat.ColorProperties["_SpecColor"].Y);
            Assert.Equal(0.75f, mat.ColorProperties["_SpecColor"].Z);
            Assert.Equal(1f, mat.ColorProperties["_SpecColor"].W);
            Assert.Equal(0.375f, mat.FloatProperties3["_Shininess"]);

            // child transform
            Assert.Single(f.Root.Children);
            MuObject child = f.Root.Children[0];
            Assert.Equal("child", child.Transform.Name);
            Assert.Equal(1f, child.Transform.LocalPosition.Y);
            Assert.Equal(2f, child.Transform.LocalPosition.Z);
            Assert.Equal(2f, child.Transform.LocalScale.X);
        }

        // --- quaternion convention -------------------------------------------

        [Fact]
        public void IdentityQuaternionReadsWFirstWithNegatedZeroComponents()
        {
            MuFile f = Mu.Read(BuildFixtureV2());
            MuQuat q = f.Root.Transform.LocalRotation;

            // (x,y,z,w) = (+0,+0,+0,1) -> (W,X,Y,Z) = (1, -0, -0, -0)
            Assert.Equal(1f, q.W);
            Assert.Equal(0f, q.X);
            Assert.Equal(0f, q.Y);
            Assert.Equal(0f, q.Z);
            // the negations are real: assert the sign bits are set
            Assert.Equal(unchecked((int)0x80000000), BitConverter.SingleToInt32Bits(q.X));
            Assert.Equal(unchecked((int)0x80000000), BitConverter.SingleToInt32Bits(q.Y));
            Assert.Equal(unchecked((int)0x80000000), BitConverter.SingleToInt32Bits(q.Z));
        }

        [Fact]
        public void KnownXRotationReadsAsHalfRoot2WAndX()
        {
            MuFile f = Mu.Read(BuildFixtureV2());
            MuQuat q = f.Root.Children[0].Transform.LocalRotation;

            // stored (x,y,z,w) = (-s,+0,+0,+s) -> (W,X,Y,Z) = (s, s, -0, -0)
            Assert.Equal(HalfRoot2, q.W);
            Assert.Equal(HalfRoot2, q.X);
            Assert.Equal(0f, q.Y);
            Assert.Equal(0f, q.Z);
            Assert.Equal(unchecked((int)0x80000000), BitConverter.SingleToInt32Bits(q.Y));
            Assert.Equal(unchecked((int)0x80000000), BitConverter.SingleToInt32Bits(q.Z));
        }

        // --- deterministic errors --------------------------------------------

        [Fact]
        public void WrongMagicThrowsDeterministicError()
        {
            var b = new Builder();
            b.I(42);
            b.I(2);
            b.S("bad");
            var ex = Assert.Throws<FormatException>(() => Mu.Read(b.Bytes.ToArray()));
            Assert.Equal("Mu: bad magic 42 (expected 76543) at offset 0", ex.Message);
        }

        [Fact]
        public void UnsupportedVersionThrowsDeterministicError()
        {
            var b = new Builder();
            b.I(76543);
            b.I(6);
            var ex = Assert.Throws<FormatException>(() => Mu.Read(b.Bytes.ToArray()));
            Assert.Equal("Mu: unsupported version 6 (expected 0..5) at offset 4", ex.Message);
        }

        [Fact]
        public void TruncatedPayloadThrowsDeterministicError()
        {
            byte[] full = BuildFixtureV2();
            byte[] cut = new byte[35];   // ends exactly before the quat floats
            Array.Copy(full, cut, 35);
            var ex = Assert.Throws<FormatException>(() => Mu.Read(cut));
            Assert.Equal("Mu: unexpected end of data at offset 35", ex.Message);
        }

        // Review hardening: hostile child nesting must fail with a
        // deterministic FormatException, never a process-killing
        // StackOverflow (Mu.MaxTreeDepth = 256).
        [Fact]
        public void DeepChildNestingFailsDeterministically()
        {
            var b = new Builder();
            b.I(76543);
            b.I(2);
            b.S("deep");
            b.S("root");
            b.F(0f); b.F(0f); b.F(0f);
            b.F(0f); b.F(0f); b.F(0f); b.F(1f);
            b.F(1f); b.F(1f); b.F(1f);
            for (int level = 0; level < 300; level++)   // > MaxTreeDepth
            {
                b.I(MuEntry.ChildTransformStart);
                b.S("c");
                b.F(0f); b.F(0f); b.F(0f);
                b.F(0f); b.F(0f); b.F(0f); b.F(1f);
                b.F(1f); b.F(1f); b.F(1f);
            }
            var ex = Assert.Throws<FormatException>(() => Mu.Read(b.Bytes.ToArray()));
            Assert.Equal("Mu: transform nesting deeper than 256", ex.Message);
        }
    }
}
