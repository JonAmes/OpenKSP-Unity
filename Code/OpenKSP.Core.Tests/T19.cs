using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using OpenKSP.Core.Modding;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T19: Mod manifest scan + hand-rolled SHA-256 (step-03-plan.md Task 7a).
    /// The mod tree is built at test time under the OS temp dir (never
    /// shipped); expected hashes are pinned from sha256sum (FIPS 180-4
    /// reference vectors plus the exact fixture bytes).
    /// </summary>
    public class T19
    {
        private const string CfgText = "PART\n{\nname = testPart\n}\n";
        private const string CfgHash =
            "818852cd15c711f7c7dca280abc0170aa12529e3d6735488f2734495e652318d";
        private const string EmptyHash =
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        private const string FourByteHash =
            "054edec1d0211f624fed0cbca9d4f9400b0e491c43742af2c5b0abebf0c990d8";

        private static string MakeTempRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(), "openksp-t19-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void WriteFile(string dir, string rel, byte[] bytes)
        {
            string full = Path.Combine(dir, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? dir);
            File.WriteAllBytes(full, bytes);
        }

        // ---- SHA-256 correctness: FIPS vectors checked against sha256sum ----

        [Fact]
        public void Sha256_KnownVectors()
        {
            Assert.Equal(EmptyHash, ModManifest.Sha256Hex(new byte[0]));
            Assert.Equal(
                "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
                ModManifest.Sha256Hex(Encoding.ASCII.GetBytes("abc")));
            Assert.Equal(
                FourByteHash,
                ModManifest.Sha256Hex(new byte[] { 0x00, 0x01, 0x02, 0x03 }));
        }

        [Fact]
        public void Sha256_MultiBlockBoundary()
        {
            // 55/56/65-byte messages hit every padding-length case (pad 0, pad overflow, extra block).
            Assert.Equal(
                "9f4390f8d30c2dd92ec9f095b65e2b9ae9b0a925a5258e241c9f1e910f734318",
                ModManifest.Sha256Hex(Repeat((byte)'a', 55)));
            Assert.Equal(
                "b35439a4ac6f0948b6d6f9e3c6af0f5f590ce20f1bde7090ef7970686ec6738a",
                ModManifest.Sha256Hex(Repeat((byte)'a', 56)));
            Assert.Equal(
                "635361c48bb9eab14198e76ea8ab7f1a41685d6ad62aa9146d301d4f17eb0ae0",
                ModManifest.Sha256Hex(Repeat((byte)'a', 65)));
        }

        private static byte[] Repeat(byte b, int n)
        {
            var v = new byte[n];
            for (int i = 0; i < n; i++) v[i] = b;
            return v;
        }

        // ---- Scan: exact sorted file list + exact hashes ----

        [Fact]
        public void Scan_ExactModFileListAndHashes()
        {
            string root = MakeTempRoot();
            try
            {
                string alpha = Path.Combine(root, "alpha");
                string zzz = Path.Combine(root, "zzz");
                WriteFile(alpha, "a.cfg", Encoding.ASCII.GetBytes(CfgText));
                WriteFile(alpha, "sub/b.mu", new byte[] { 0x00, 0x01, 0x02, 0x03 });
                WriteFile(alpha, "z.mbm", new byte[0]);
                WriteFile(zzz, "model.cfg", Encoding.ASCII.GetBytes(CfgText));
                // Files at the Mods root itself are ignored, not mod files.
                WriteFile(root, "README.cfg", Encoding.ASCII.GetBytes("ignore me"));

                List<ModManifestEntry> mods = ModManifest.Scan(root);

                Assert.Equal(2, mods.Count);
                Assert.Equal("alpha", mods[0].Name);   // StringComparer.Ordinal
                Assert.Equal("zzz", mods[1].Name);

                Assert.Equal(3, mods[0].Files.Count);
                Assert.Equal("a.cfg", mods[0].Files[0].RelativePath);
                Assert.Equal(CfgHash, mods[0].Files[0].Sha256Hex);
                Assert.Equal("sub/b.mu", mods[0].Files[1].RelativePath);
                Assert.Equal(FourByteHash, mods[0].Files[1].Sha256Hex);
                Assert.Equal("z.mbm", mods[0].Files[2].RelativePath);
                Assert.Equal(EmptyHash, mods[0].Files[2].Sha256Hex);

                Assert.Single(mods[1].Files);
                Assert.Equal("model.cfg", mods[1].Files[0].RelativePath);
                Assert.Equal(CfgHash, mods[1].Files[0].Sha256Hex);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void Scan_AcceptsUppercaseExtensions()
        {
            string root = MakeTempRoot();
            try
            {
                string m = Path.Combine(root, "m1");
                WriteFile(m, "MODEL.CFG", Encoding.ASCII.GetBytes(CfgText));
                WriteFile(m, "tex.MBM", new byte[0]);
                List<ModManifestEntry> mods = ModManifest.Scan(root);
                Assert.Single(mods);
                Assert.Equal(2, mods[0].Files.Count);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void Scan_RejectsUnknownExtension_ExactMessage()
        {
            string root = MakeTempRoot();
            try
            {
                string bar = Path.Combine(root, "bar");
                WriteFile(bar, "notes.txt", Encoding.ASCII.GetBytes("hello"));

                var ex = Assert.Throws<FormatException>(() => ModManifest.Scan(root));
                Assert.Equal(
                    "ModManifest: unsupported extension '.txt' in mod 'bar' file 'notes.txt'",
                    ex.Message);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void Scan_MissingRoot_ReturnsEmpty()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "openksp-t19-absent-" + Guid.NewGuid().ToString("N"));
            Assert.Empty(ModManifest.Scan(root));
        }
    }
}
