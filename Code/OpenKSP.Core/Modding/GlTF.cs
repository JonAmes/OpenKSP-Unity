using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenKSP.Core.Modding
{
    // ---- GlTF result model (minimal surface needed to build a Mesh) --------

    /// <summary>glTF buffer: byteLength plus optional external uri. External
    /// URIs are NOT fetched (seam); a .glb internal BIN chunk is exposed on
    /// GlTFScene.BinChunk and covers the common buffer 0 case.</summary>
    public sealed class GlTFBuffer
    {
        public int ByteLength;
        public string Uri = "";
    }

    public sealed class GlTFBufferView
    {
        public int Buffer;
        public int ByteOffset;
        public int ByteLength;
    }

    /// <summary>componentType per spec: 5120..5126 (BYTE..FLOAT). type:
    /// "SCALAR", "VEC2", "VEC3", "VEC4", "MAT4".</summary>
    public sealed class GlTFAccessor
    {
        public int BufferView = -1;
        public int ByteOffset;
        public int ComponentType;
        public int Count;
        public string Type = "";
    }

    /// <summary>Attribute indices reference accessors; -1 = absent.
    /// Mode 4 = TRIANGLES (spec default).</summary>
    public sealed class GlTFPrimitive
    {
        public int Position = -1;
        public int Normal = -1;
        public int Tex0 = -1;
        public int Indices = -1;
        public int Material = -1;
        public int Mode = 4;
    }

    public sealed class GlTFMesh
    {
        public string Name = "";
        public List<GlTFPrimitive> Primitives = new List<GlTFPrimitive>();
    }

    public sealed class GlTFMaterial
    {
        public string Name = "";
    }

    /// <summary>translation (3), rotation (4, quaternion x,y,z,w), scale (3)
    /// as raw document order; null when absent (identity).</summary>
    public sealed class GlTFNode
    {
        public string Name = "";
        public int Mesh = -1;
        public double[]? Translation;
        public double[]? Rotation;
        public double[]? Scale;
        public List<int> Children = new List<int>();
    }

    /// <summary>Parsed glTF document: the scene surface a Unity bridge maps
    /// onto Mesh + Material. RootNodes are node indices for the active
    /// scene (or computed no-parent roots when no scene is declared).</summary>
    public sealed class GlTFScene
    {
        public List<GlTFNode> Nodes = new List<GlTFNode>();
        public List<GlTFMesh> Meshes = new List<GlTFMesh>();
        public List<GlTFMaterial> Materials = new List<GlTFMaterial>();
        public List<GlTFBuffer> Buffers = new List<GlTFBuffer>();
        public List<GlTFBufferView> BufferViews = new List<GlTFBufferView>();
        public List<GlTFAccessor> Accessors = new List<GlTFAccessor>();
        public List<int> RootNodes = new List<int>();
        /// <summary>Internal .glb BIN chunk bytes, or null when absent.</summary>
        public byte[]? BinChunk;
    }

    /// <summary>
    /// Minimal hand-written glTF 2.0 reader (step-03-plan.md Task 5, seam per
    /// step-03-research.md §6): parses the JSON document of a text `.gltf`
    /// or the JSON chunk of a binary `.glb` into a GlTFScene. Pure C# 9 /
    /// netstandard2.0: no UnityEngine, no LINQ, no System.Text.Json (the
    /// JSON is read by JsonMini below so the DLL stays free of v8 typerefs).
    /// The Unity-side import is NOT part of this phase (seam).
    ///
    /// Deterministic FormatException failures: bad .glb magic/version,
    /// chunks exceeding the file, missing/unsupported asset.version, bad JSON,
    /// out-of-range index references.
    /// </summary>
    public static class GlTF
    {
        public const uint GlbMagic = 0x46546C67;   // 'glTF'
        private const uint ChunkJson = 0x4E4F534A; // 'JSON'
        private const uint ChunkBin = 0x004E4942;  // 'BIN\0'

        public static GlTFScene Decode(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            byte[] jsonBytes;
            byte[]? binChunk = null;

            if (bytes.Length >= 4 && bytes[0] == 'g' && bytes[1] == 'l' &&
                bytes[2] == 'T' && bytes[3] == 'F')
            {
                // .glb container: header + chunks (spec: JSON first, BIN optional).
                var r = new BinLE(bytes, "GlTF");
                uint magic = r.ReadUInt();            // 0 (ASCII 'glTF' already matched)
                if (magic != GlbMagic)
                    throw new FormatException(
                        "GlTF: bad glb magic 0x" + magic.ToString("X8") +
                        " (expected 0x46546C67 'glTF') at offset 0");
                uint version = r.ReadUInt();          // 4
                if (version != 2)
                    throw new FormatException(
                        "GlTF: unsupported glb version " + version + " (expected 2) at offset 4");
                r.ReadUInt();                         // 8 totalLength (bounds enforced by reads)
                bool jsonSeen = false;
                int jsonStart = -1, jsonLen = 0;
                while (r.Position + 8 <= bytes.Length)
                {
                    int chunkLen = r.ReadInt();       // 12, 20, ...
                    uint chunkType = r.ReadUInt();
                    r.Need(chunkLen);
                    if (chunkType == ChunkJson && !jsonSeen)
                    {
                        jsonStart = r.Position;
                        jsonLen = chunkLen;
                        jsonSeen = true;
                    }
                    else if (chunkType == ChunkBin && binChunk == null)
                    {
                        binChunk = new byte[chunkLen];
                        for (int i = 0; i < chunkLen; i++)
                            binChunk[i] = (byte)r.ReadByte();
                        continue;   // already advanced
                    }
                    r.Position += chunkLen;   // JSON (2nd+) and unknown chunks: skip
                }
                if (!jsonSeen)
                    throw new FormatException("GlTF: glb has no JSON chunk");
                jsonBytes = new byte[jsonLen];
                Array.Copy(bytes, jsonStart, jsonBytes, 0, jsonLen);
            }
            else
            {
                jsonBytes = bytes;
            }

            string json = Encoding.UTF8.GetString(jsonBytes);
            var doc = JsonMini.Parse(json);
            if (doc.JKind != JsonMini.Kind.Object)
                throw new FormatException("GlTF: JSON root is not an object");

            var scene = new GlTFScene();
            scene.BinChunk = binChunk;

            var asset = doc.Get("asset");
            if (asset == null || asset.JKind != JsonMini.Kind.Object)
                throw new FormatException("GlTF: missing required 'asset' object");
            var versionVal = asset.Get("version");
            if (versionVal == null || versionVal.JKind != JsonMini.Kind.String)
                throw new FormatException("GlTF: 'asset.version' is not a string");
            if (versionVal.Str != "2.0")
                throw new FormatException(
                    "GlTF: unsupported asset.version '" + versionVal.Str +
                    "' (expected '2.0')");

            // buffers
            foreach (var b in doc.ArrOf("buffers"))
            {
                var buf = new GlTFBuffer();
                buf.ByteLength = b.Int("byteLength", -1);
                if (buf.ByteLength < 0)
                    throw new FormatException("GlTF: buffer missing 'byteLength'");
                var uri = b.Get("uri");
                if (uri != null && uri.JKind == JsonMini.Kind.String)
                    buf.Uri = uri.Str;
                scene.Buffers.Add(buf);
            }
            // bufferViews
            foreach (var v in doc.ArrOf("bufferViews"))
            {
                var bv = new GlTFBufferView();
                bv.Buffer = v.Int("buffer", -1);
                if (bv.Buffer < 0 || bv.Buffer >= scene.Buffers.Count)
                    throw new FormatException(
                        "GlTF: bufferView references buffer " + bv.Buffer +
                        " out of range (count " + scene.Buffers.Count + ")");
                bv.ByteOffset = v.Int("byteOffset", 0);
                bv.ByteLength = v.Int("byteLength", -1);
                if (bv.ByteLength < 0)
                    throw new FormatException("GlTF: bufferView missing 'byteLength'");
                scene.BufferViews.Add(bv);
            }
            // accessors
            foreach (var a in doc.ArrOf("accessors"))
            {
                var ac = new GlTFAccessor();
                ac.BufferView = a.Int("bufferView", -1);
                if (ac.BufferView >= scene.BufferViews.Count)
                    throw new FormatException(
                        "GlTF: accessor references bufferView " + ac.BufferView +
                        " out of range (count " + scene.BufferViews.Count + ")");
                ac.ByteOffset = a.Int("byteOffset", 0);
                ac.ComponentType = a.Int("componentType", 0);
                ac.Count = a.Int("count", 0);
                var t = a.Get("type");
                if (t != null && t.JKind == JsonMini.Kind.String)
                    ac.Type = t.Str;
                scene.Accessors.Add(ac);
            }
            // materials
            foreach (var m in doc.ArrOf("materials"))
            {
                var mat = new GlTFMaterial();
                var name = m.Get("name");
                if (name != null && name.JKind == JsonMini.Kind.String)
                    mat.Name = name.Str;
                scene.Materials.Add(mat);
            }
            // meshes
            foreach (var m in doc.ArrOf("meshes"))
            {
                var mesh = new GlTFMesh();
                var name = m.Get("name");
                if (name != null && name.JKind == JsonMini.Kind.String)
                    mesh.Name = name.Str;
                foreach (var p in m.ArrOf("primitives"))
                {
                    var prim = new GlTFPrimitive();
                    var attrs = p.Get("attributes");
                    if (attrs != null && attrs.JKind == JsonMini.Kind.Object)
                    {
                        prim.Position = attrs.Int("POSITION", -1);
                        prim.Normal = attrs.Int("NORMAL", -1);
                        prim.Tex0 = attrs.Int("TEXCOORD_0", -1);
                    }
                    prim.Indices = p.Int("indices", -1);
                    prim.Material = p.Int("material", -1);
                    prim.Mode = p.Int("mode", 4);
                    if (prim.Material >= scene.Materials.Count)
                        throw new FormatException(
                            "GlTF: primitive references material " + prim.Material +
                            " out of range (count " + scene.Materials.Count + ")");
                    if (prim.Indices >= scene.Accessors.Count)
                        throw new FormatException(
                            "GlTF: primitive references accessor " + prim.Indices +
                            " out of range (count " + scene.Accessors.Count + ")");
                    mesh.Primitives.Add(prim);
                }
                scene.Meshes.Add(mesh);
            }
            // nodes
            foreach (var n in doc.ArrOf("nodes"))
            {
                var node = new GlTFNode();
                var name = n.Get("name");
                if (name != null && name.JKind == JsonMini.Kind.String)
                    node.Name = name.Str;
                node.Mesh = n.Int("mesh", -1);
                if (node.Mesh >= scene.Meshes.Count)
                    throw new FormatException(
                        "GlTF: node references mesh " + node.Mesh +
                        " out of range (count " + scene.Meshes.Count + ")");
                node.Translation = ReadVec(n.Get("translation"));
                node.Rotation = ReadVec(n.Get("rotation"));
                node.Scale = ReadVec(n.Get("scale"));
                var children = n.Get("children");
                if (children != null && children.JKind == JsonMini.Kind.Array)
                    foreach (var c in children.List)
                        node.Children.Add((int)c.Num);
                scene.Nodes.Add(node);
            }
            // scene roots: declared scenes win; otherwise no-parent nodes.
            var scenes = doc.Get("scenes");
            if (scenes != null && scenes.JKind == JsonMini.Kind.Array && scenes.List.Count > 0)
            {
                int idx = doc.Int("scene", 0);
                if (idx < 0 || idx >= scenes.List.Count)
                    throw new FormatException(
                        "GlTF: scene index " + idx + " out of range (count " +
                        scenes.List.Count + ")");
                var nodesArr = scenes.List[idx].Get("nodes");
                if (nodesArr != null && nodesArr.JKind == JsonMini.Kind.Array)
                    foreach (var v in nodesArr.List)
                        scene.RootNodes.Add((int)v.Num);
            }
            else
            {
                bool[] child = new bool[scene.Nodes.Count];
                foreach (var nd in scene.Nodes)
                    foreach (int c in nd.Children)
                        if (c >= 0 && c < child.Length)
                            child[c] = true;
                for (int i = 0; i < scene.Nodes.Count; i++)
                    if (!child[i])
                        scene.RootNodes.Add(i);
            }
            return scene;
        }

        private static double[]? ReadVec(JsonMini.Val? v)
        {
            if (v == null || v.JKind != JsonMini.Kind.Array)
                return null;
            var elems = v.List;
            double[] outv = new double[elems.Count];
            for (int i = 0; i < elems.Count; i++)
                outv[i] = elems[i].Num;
            return outv;
        }
    }

    /// <summary>
    /// Minimal recursive-descent JSON parser (objects, arrays, strings with
    /// full escape set incl. \uXXXX, numbers, true/false/null). No LINQ, no
    /// BCL JSON types. Errors are deterministic FormatExceptions with the
    /// exact character offset.
    /// </summary>
    internal sealed class JsonMini
    {
        public enum Kind { Object, Array, String, Number, Bool, Null }

        public sealed class Val
        {
            public Kind JKind;
            public Dictionary<string, Val>? Obj;  // object members
            public List<Val>? Arr;                // array elements
            public string Str = "";
            public double Num;
            public bool B;

            /// <summary>Array elements, never null (empty when not an array).</summary>
            public List<Val> List
            {
                get { return Arr != null ? Arr : new List<Val>(); }
            }

            public Val? Get(string key)
            {
                if (Obj == null)
                    return null;
                Val v;
                return Obj.TryGetValue(key, out v) ? v : null;
            }

            public int Int(string key, int dflt)
            {
                Val? v = Get(key);
                return v != null && v.JKind == Kind.Number ? (int)v.Num : dflt;
            }

            public List<Val> ArrOf(string key)
            {
                Val? v = Get(key);
                return v != null && v.Arr != null ? v.Arr : new List<Val>();
            }
        }

        private readonly string _s;
        private int _p;

        private JsonMini(string s)
        {
            _s = s;
            _p = 0;
        }

        public static Val Parse(string s)
        {
            var p = new JsonMini(s);
            p.SkipWs();
            Val v = p.ParseValue();
            p.SkipWs();
            if (p._p != s.Length)
                throw p.Err("trailing data");
            return v;
        }

        private FormatException Err(string what)
        {
            return new FormatException("GlTF: JSON parse error at offset " + _p + ": " + what);
        }

        private void SkipWs()
        {
            while (_p < _s.Length)
            {
                char c = _s[_p];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                    _p++;
                else
                    break;
            }
        }

        private Val ParseValue()
        {
            if (_p >= _s.Length)
                throw Err("unexpected end of JSON");
            char c = _s[_p];
            if (c == '{') return ParseObject();
            if (c == '[') return ParseArray();
            if (c == '"') return ParseString();
            if (c == 't' || c == 'f') return ParseLiteral();
            if (c == 'n') return ParseLiteral();
            if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
            throw Err("unexpected token '" + c + "'");
        }

        private Val ParseObject()
        {
            var members = new Dictionary<string, Val>();
            _p++;   // '{'
            SkipWs();
            if (_p < _s.Length && _s[_p] == '}')
            {
                _p++;
                return new Val { JKind = Kind.Object, Obj = members };
            }
            while (true)
            {
                SkipWs();
                if (_p >= _s.Length || _s[_p] != '"')
                    throw Err("expected string key");
                Val key = ParseString();
                SkipWs();
                if (_p >= _s.Length || _s[_p] != ':')
                    throw Err("expected ':'");
                _p++;
                SkipWs();
                members[key.Str] = ParseValue();
                SkipWs();
                if (_p < _s.Length && _s[_p] == ',')
                {
                    _p++;
                    continue;
                }
                if (_p < _s.Length && _s[_p] == '}')
                {
                    _p++;
                    return new Val { JKind = Kind.Object, Obj = members };
                }
                throw Err("expected ',' or '}'");
            }
        }

        private Val ParseArray()
        {
            var elems = new List<Val>();
            _p++;   // '['
            SkipWs();
            if (_p < _s.Length && _s[_p] == ']')
            {
                _p++;
                return new Val { JKind = Kind.Array, Arr = elems };
            }
            while (true)
            {
                SkipWs();
                elems.Add(ParseValue());
                SkipWs();
                if (_p < _s.Length && _s[_p] == ',')
                {
                    _p++;
                    continue;
                }
                if (_p < _s.Length && _s[_p] == ']')
                {
                    _p++;
                    return new Val { JKind = Kind.Array, Arr = elems };
                }
                throw Err("expected ',' or ']'");
            }
        }

        private Val ParseString()
        {
            var sb = new StringBuilder();
            _p++;   // opening quote
            while (true)
            {
                if (_p >= _s.Length)
                    throw Err("unterminated string");
                char c = _s[_p++];
                if (c == '"')
                    break;
                if (c == '\\')
                {
                    if (_p >= _s.Length)
                        throw Err("unterminated escape");
                    char e = _s[_p++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_p + 4 > _s.Length)
                                throw Err("truncated \\u escape");
                            string hex = _s.Substring(_p, 4);
                            int code;
                            if (!int.TryParse(hex, NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture, out code))
                                throw Err("invalid \\u escape '" + hex + "'");
                            _p += 4;
                            sb.Append((char)code);
                            break;
                        default:
                            throw Err("invalid escape '\\" + e + "'");
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return new Val { JKind = Kind.String, Str = sb.ToString() };
        }

        private Val ParseNumber()
        {
            int start = _p;
            if (_p < _s.Length && _s[_p] == '-')
                _p++;
            while (_p < _s.Length)
            {
                char c = _s[_p];
                if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' ||
                    c == '+' || c == '-')
                    _p++;
                else
                    break;
            }
            double d;
            if (!double.TryParse(_s.Substring(start, _p - start), NumberStyles.Float,
                CultureInfo.InvariantCulture, out d))
                throw Err("invalid number '" + _s.Substring(start, _p - start) + "'");
            return new Val { JKind = Kind.Number, Num = d };
        }

        private Val ParseLiteral()
        {
            if (Match("true")) return new Val { JKind = Kind.Bool, B = true };
            if (Match("false")) return new Val { JKind = Kind.Bool, B = false };
            if (Match("null")) return new Val { JKind = Kind.Null };
            throw Err("invalid literal");
        }

        private bool Match(string word)
        {
            if (_p + word.Length > _s.Length)
                return false;
            for (int i = 0; i < word.Length; i++)
                if (_s[_p + i] != word[i])
                    return false;
            _p += word.Length;
            return true;
        }
    }
}
