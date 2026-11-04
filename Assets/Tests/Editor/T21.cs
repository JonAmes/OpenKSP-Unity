using System;
using System.Collections.Generic;
using NUnit.Framework;
using OpenKSP.Core.Modding;
using UnityEngine;

namespace OpenKSP.UnityTests
{
    /// <summary>
    /// T21 (step-03 Task 8): Unity-side loader bridges, run headless in
    /// EditMode. Fixtures are byte-for-byte the same hand-built .mu (T16)
    /// and .mbm (T18) blobs used in the Core suite, so the Unity bridge is
    /// pinned against the exact same inputs. Asserts exact vertex/triangle
    /// counts through MuMeshLoader and an exact first-pixel RGBA match
    /// through DdsTextureLoader. Quaternion bridge (q4) asserted on the
    /// 45-degree-X child transform: Core reads (W,X,Y,Z)=(c,c,-0,-0), the
    /// Unity ctor takes (x,y,z,w) — bridge maps X,Y,Z,W verbatim.
    /// </summary>
    public class T21
    {
        private const float HalfRoot2 = 0.7071068f;

        // --- .mu byte builder (identical to T16's) ---------------------------

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

            public void S(string s)
            {
                Bytes.Add((byte)s.Length);
                for (int i = 0; i < s.Length; i++)
                    Bytes.Add((byte)s[i]);
            }
        }

        private static byte[] BuildMuV2()
        {
            var b = new Builder();
            b.I(76543);
            b.I(2);
            b.S("t21-model");

            b.S("root");
            b.F(0f); b.F(0f); b.F(0f);
            b.F(0f); b.F(0f); b.F(0f); b.F(1f);
            b.F(1f); b.F(1f); b.F(1f);

            b.I(MuEntry.TagAndLayer);
            b.S("Untagged");
            b.I(0);

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
            b.I(3);
            b.I(0); b.I(1); b.I(2);
            b.I(MuEntry.MeshEnd);

            b.I(MuEntry.MeshRenderer);
            b.Bytes.Add(1);
            b.Bytes.Add(1);
            b.I(1);
            b.I(0);

            b.I(MuEntry.Textures);
            b.I(1);
            b.S("tex_d");
            b.I(0);

            b.I(MuEntry.Materials);
            b.I(1);
            b.S("mat_a");
            b.I(MuShaderType.Specular);
            b.I(0);
            b.F(1f); b.F(1f);
            b.F(0f); b.F(0f);
            b.F(0.25f); b.F(0.5f); b.F(0.75f); b.F(1f);
            b.F(0.375f);

            b.I(MuEntry.ChildTransformStart);
            b.S("child");
            b.F(0f); b.F(1f); b.F(2f);
            b.F(-HalfRoot2); b.F(0f); b.F(0f); b.F(HalfRoot2);
            b.F(2f); b.F(2f); b.F(2f);
            b.I(MuEntry.ChildTransformEnd);
            return b.Bytes.ToArray();
        }

        // --- .mbm byte builder (same layout as T18's) ------------------------
        // 2x2, 32bpp RGBA: pixel0=(10,20,30,40) pixel1=(50,60,70,80)
        // pixel2=(90,100,110,120) pixel3=(130,140,150,160)

        private static byte[] BuildMbm32()
        {
            var b = new Builder();
            b.I(unchecked((int)0x50534B03));   // magic
            b.I(2);                            // width
            b.I(2);                            // height
            b.I(0);                            // type
            b.I(32);                           // bits
            int[] px = { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160 };
            for (int i = 0; i < px.Length; i++)
                b.Bytes.Add((byte)px[i]);
            return b.Bytes.ToArray();
        }

        // --- tests ------------------------------------------------------------

        [Test]
        public void MuMeshLoaderProducesExactCounts()
        {
            Mesh[] meshes = MuMeshLoader.LoadMeshes(BuildMuV2());
            Assert.AreEqual(1, meshes.Length);
            Mesh m = meshes[0];
            Assert.AreEqual(3, m.vertexCount);
            Assert.AreEqual(3, m.triangles.Length);
            Assert.AreEqual(1, m.subMeshCount >= 1 ? 1 : 0);
            Assert.AreEqual(1f, m.vertices[1].x, 1e-6f);
            Assert.AreEqual(1f, m.vertices[2].y, 1e-6f);
        }

        [Test]
        public void MuQuaternionBridgeMapsXyzw()
        {
            MuFile f = MuMeshLoader.LoadFile(BuildMuV2());
            MuQuat childQuat = f.Root.Children[0].Transform.LocalRotation;
            // Core emitted (W,X,Y,Z) = (c, c, -0, -0) for stored (-c,0,0,c).
            Quaternion uq = MuMeshLoader.ToUnityQuat(childQuat);
            Assert.AreEqual(childQuat.X, uq.x, 1e-6f);
            Assert.AreEqual(childQuat.Y, uq.y, 1e-6f);
            Assert.AreEqual(childQuat.Z, uq.z, 1e-6f);
            Assert.AreEqual(childQuat.W, uq.w, 1e-6f);
            // Unity semantics: a 90-degree rotation about X.
            Assert.AreEqual(HalfRoot2, uq.x, 1e-5f);
        }

        [Test]
        public void DdsTextureLoaderMatchesFirstPixel()
        {
            Texture2D tex = DdsTextureLoader.LoadTexture(BuildMbm32());
            Assert.AreEqual(2, tex.width);
            Assert.AreEqual(2, tex.height);
            Color32[] px = tex.GetPixels32();
            Assert.AreEqual(4, px.Length);
            // First stored pixel must match byte-exactly (RGBA32, raw copy).
            Assert.AreEqual((byte)10, px[0].r);
            Assert.AreEqual((byte)20, px[0].g);
            Assert.AreEqual((byte)30, px[0].b);
            Assert.AreEqual((byte)40, px[0].a);
            UnityEngine.Object.DestroyImmediate(tex);
        }

        [Test]
        public void ModDirectoryServiceRejectsPathEscape()
        {
            var svc = new ModDirectoryService();
            byte[] bytes;
            string error;
            bool ok = svc.TryReadFile("nope", "../secret.cfg", out bytes, out error);
            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }
    }
}
