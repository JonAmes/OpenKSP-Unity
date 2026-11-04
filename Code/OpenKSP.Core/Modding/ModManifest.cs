using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenKSP.Core.Modding
{
    /// <summary>
    /// One file inside a mod directory: path relative to the mod directory
    /// (always '/' separated) and its SHA-256 as 64 lowercase hex chars.
    /// </summary>
    public sealed class ModFileEntry
    {
        public string RelativePath = "";
        public string Sha256Hex = "";
    }

    /// <summary>
    /// One mod: its directory name and its full file list, sorted by
    /// relative path with StringComparer.Ordinal.
    /// </summary>
    public sealed class ModManifestEntry
    {
        public string Name = "";
        public List<ModFileEntry> Files = new List<ModFileEntry>();
    }

    /// <summary>
    /// Scan of the Mods tree (step-03 Task 7a). Each immediate subdirectory
    /// of the Mods root is one mod; files are enumerated recursively, mods
    /// sorted by directory name and files by relative path, both with
    /// StringComparer.Ordinal. Only the allow-list extensions
    /// (.cfg .mu .dds .mbm .gltf — case-insensitive) are permitted; any
    /// other extension is rejected with a deterministic FormatException
    /// naming the file path and the extension. Files sitting directly in
    /// the Mods root (outside any mod directory) are ignored.
    /// SHA-256 is hand-rolled per the phase constraint (FIPS 180-4, no
    /// System.Security.Cryptography type references in the shipped DLL).
    /// </summary>
    public static class ModManifest
    {
        // Allowed extensions, lowercase; compared case-insensitively.
        private static readonly string[] Supported =
            { ".cfg", ".mu", ".dds", ".mbm", ".gltf" };

        public static bool IsSupportedExtension(string ext)
        {
            for (int i = 0; i < Supported.Length; i++)
                if (string.Equals(ext, Supported[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public static List<ModManifestEntry> Scan(string modsRootPath)
        {
            var mods = new List<ModManifestEntry>();
            if (string.IsNullOrEmpty(modsRootPath) || !Directory.Exists(modsRootPath))
                return mods;

            string[] dirs = Directory.GetDirectories(modsRootPath);
            Array.Sort(dirs, StringComparer.Ordinal);

            foreach (string dir in dirs)
            {
                string name = Path.GetFileName(dir);
                var entry = new ModManifestEntry { Name = name };

                string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                // Relative paths first so the Ordinal sort matches T19's list.
                var rels = new List<string>(files.Length);
                foreach (string f in files)
                {
                    string rel = f.Substring(dir.Length + 1).Replace('\\', '/');
                    rels.Add(rel);
                }
                rels.Sort(StringComparer.Ordinal);

                foreach (string rel in rels)
                {
                    string f = Path.Combine(dir, rel.Replace('/', Path.DirectorySeparatorChar));
                    string ext = GetExtension(rel);
                    if (!IsSupportedExtension(ext))
                    {
                        throw new FormatException(
                            "ModManifest: unsupported extension '" + ext +
                            "' in mod '" + name + "' file '" + rel + "'");
                    }
                    byte[] bytes = File.ReadAllBytes(f);
                    entry.Files.Add(new ModFileEntry
                    {
                        RelativePath = rel,
                        Sha256Hex = Sha256Hex(bytes),
                    });
                }

                mods.Add(entry);
            }
            return mods;
        }

        private static string GetExtension(string relPath)
        {
            int dot = relPath.LastIndexOf('.');
            if (dot < 0 || dot == relPath.Length - 1) return "";
            return relPath.Substring(dot);
        }

        // -----------------------------------------------------------------
        // SHA-256 (FIPS 180-4), hand-rolled: big-endian word schedule,
        // no allocations beyond the message copy-free streaming over blocks.
        // -----------------------------------------------------------------

        private static readonly uint[] K =
        {
            0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5,
            0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
            0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3,
            0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
            0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc,
            0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
            0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7,
            0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
            0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13,
            0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
            0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3,
            0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
            0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5,
            0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
            0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208,
            0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
        };

        public static string Sha256Hex(byte[] message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            uint h0 = 0x6a09e667, h1 = 0xbb67ae85, h2 = 0x3c6ef372, h3 = 0xa54ff53a,
                 h4 = 0x510e527f, h5 = 0x9b05688c, h6 = 0x1f83d9ab, h7 = 0x5be0cd19;

            // Padding: message || 0x80 || 0... || u64 big-endian bit length.
            long bitLen = (long)message.Length * 8L;
            int padZeros = (56 - ((message.Length + 1) % 64) + 64) % 64;
            int total = message.Length + 1 + padZeros + 8;

            var block = new byte[64];
            var w = new uint[64];

            for (int off = 0; off < total; off += 64)
            {
                Array.Clear(block, 0, 64);
                for (int i = 0; i < 64; i++)
                {
                    int j = off + i;
                    if (j < message.Length) block[i] = message[j];
                    else if (j == message.Length) block[i] = 0x80;
                    else if (j >= total - 8)
                    {
                        int shift = (7 - (j - (total - 8))) * 8;
                        block[i] = (byte)((bitLen >> shift) & 0xFF);
                    }
                }

                for (int i = 0; i < 16; i++)
                    w[i] = ((uint)block[i * 4] << 24) | ((uint)block[i * 4 + 1] << 16)
                         | ((uint)block[i * 4 + 2] << 8) | block[i * 4 + 3];
                for (int i = 16; i < 64; i++)
                {
                    uint s0 = Rotr(w[i - 15], 7) ^ Rotr(w[i - 15], 18) ^ (w[i - 15] >> 3);
                    uint s1 = Rotr(w[i - 2], 17) ^ Rotr(w[i - 2], 19) ^ (w[i - 2] >> 10);
                    w[i] = w[i - 16] + s0 + w[i - 7] + s1;
                }

                uint a = h0, b = h1, c = h2, d = h3, e = h4, f = h5, g = h6, h = h7;
                for (int i = 0; i < 64; i++)
                {
                    uint S1 = Rotr(e, 6) ^ Rotr(e, 11) ^ Rotr(e, 25);
                    uint ch = (e & f) ^ (~e & g);
                    uint temp1 = h + S1 + ch + K[i] + w[i];
                    uint S0 = Rotr(a, 2) ^ Rotr(a, 13) ^ Rotr(a, 22);
                    uint maj = (a & b) ^ (a & c) ^ (b & c);
                    uint temp2 = S0 + maj;
                    h = g; g = f; f = e; e = d + temp1;
                    d = c; c = b; b = a; a = temp1 + temp2;
                }

                h0 += a; h1 += b; h2 += c; h3 += d;
                h4 += e; h5 += f; h6 += g; h7 += h;
            }

            var sb = new StringBuilder(64);
            AppendHex(sb, h0); AppendHex(sb, h1); AppendHex(sb, h2); AppendHex(sb, h3);
            AppendHex(sb, h4); AppendHex(sb, h5); AppendHex(sb, h6); AppendHex(sb, h7);
            return sb.ToString();
        }

        private static uint Rotr(uint x, int n) => (x >> n) | (x << (32 - n));

        private static void AppendHex(StringBuilder sb, uint v)
        {
            for (int shift = 28; shift >= 0; shift -= 4)
                sb.Append("0123456789abcdef"[(int)((v >> shift) & 0xF)]);
        }
    }
}
