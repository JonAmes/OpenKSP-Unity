using System;
using System.Collections.Generic;
using System.IO;
using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;

namespace OpenKSP.Core.Celestial
{
    // Loads celestial bodies from JSON per Design Doc §6.
    // Schema:
    //   { "units": {...},
    //     "bodies": [ { "id", "name", "mass", "radius", "mu", "parent" } ] }
    // Required: id, name, mass, radius, mu (GM, m³/s²).
    // Optional: parent — id of the body this one revolves about;
    // must reference a body in the same file, may not be itself (Phase 2).
    //
    // Hand-rolled minimal JSON parser (no System.Text.Json): Unity's Mono runtime
    // does not ship System.Text.Json, and the netstandard2.0 target must stay free
    // of any assembly Unity cannot resolve at JIT time. Only fixed mscorlib
    // facades (System.IO.File, System.Text.*) are used here.
    public static class CBodyLoader
    {
        private const string PlanetsJsonRelativePath =
            "Assets/StreamingAssets/Planets/planets.json";

        // Load bodies from the canonical data file.
        // <param name="path">Absolute path, or null for the default
        // <c>Assets/StreamingAssets/Planets/planets.json</c> (resolved relative to the repo root).</param>
        // <returns>Immutable view of bodies. Throws <see cref="InvalidDataException"/> for schema violations.</returns>
        public static IReadOnlyList<CBody> Load(string? path = null)
        {
            var resolvedPath = path ?? ResolveDefaultPath();
            var json = File.ReadAllText(resolvedPath);
            return Parse(json);
        }

        // Default path relative to the repo root (AppDomain base dir walked up 5 levels).
        public static string ResolveDefaultPath()
        {
            // AppDomain.BaseDirectory is the build output dir
            // (…/Code/OpenKSP.Core(.Tests)/bin/Debug/<tfm>); five parents reach the repo root.
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (var i = 0; i < 5 && dir != null; i++)
            {
                dir = dir.Parent;
            }
            return Path.Combine(dir?.FullName ?? "", PlanetsJsonRelativePath);
        }

        // Parse a §6-schema JSON body string.
        // Position/velocity are left at origin; world state (task 3) initializes them.
        public static IReadOnlyList<CBody> Parse(string json)
        {
            var i = 0;
            var root = Json.ParseValue(json, ref i);
            Json.SkipWs(json, ref i);
            if (root.Kind != Json.Kind.Object)
                throw new InvalidDataException("planets.json: root document must be a JSON object.");

            if (!root.TryGetMember("bodies", out var bodies))
                throw new InvalidDataException("planets.json: 'bodies' member is required.");
            if (bodies.Kind != Json.Kind.Array)
                throw new InvalidDataException("planets.json: 'bodies' must be a JSON array.");

            var list = new List<CBody>(bodies.ItemCount);
            var seenIds = new HashSet<string>();
            for (var s = 0; s < bodies.ItemCount; s++)
            {
                var el = bodies.Element(s);
                if (el.Kind != Json.Kind.Object)
                    throw new InvalidDataException("planets.json: each body must be a JSON object.");
                var id = ReadRequiredString(el, "id");
                if (!seenIds.Add(id))
                    throw new InvalidDataException($"planets.json: duplicate body id '{id}'.");
                var name = ReadRequiredString(el, "name");
                ReadRequiredDouble(el, "mass");
                var radius = ReadRequiredDouble(el, "radius");
                var mu = ReadRequiredDouble(el, "mu");

                string? parentId = null;
                if (el.TryGetMember("parent", out var parentEl) && parentEl.Kind != Json.Kind.Null)
                {
                    if (parentEl.Kind != Json.Kind.String)
                        throw new InvalidDataException($"planets.json: 'parent' must be a string or null for body '{id}'.");
                    parentId = parentEl.Text!;
                }

                list.Add(new CBody(id, name, D3.Zero, DVector.Zero, radius, mu, parentId));
                // Hierarchy validation (unknown parent, self-parent) is done
                // against the full set in WorldState.ResolveParentIndices.
            }
            return list;
        }

        private static string ReadRequiredString(Json.Value obj, string prop)
        {
            if (!obj.TryGetMember(prop, out var el) || el.Kind != Json.Kind.String)
                throw new InvalidDataException($"planets.json: body property '{prop}' (string) is required.");
            return el.Text!;
        }

        private static double ReadRequiredDouble(Json.Value obj, string prop)
        {
            if (!obj.TryGetMember(prop, out var el) || el.Kind != Json.Kind.Number)
                throw new InvalidDataException($"planets.json: body property '{prop}' (number) is required.");
            return el.Num;
        }

        // Find a body by ID in a loaded collection.
        public static CBody? GetById(IReadOnlyList<CBody> bodies, string id)
        {
            foreach (var b in bodies)
            {
                if (b.Id == id) return b;
            }
            return null;
        }

        // =====================================================================
        // Minimal strict JSON value model + recursive-descent parser.
        // Uses only mscorlib facades resolvable by Unity's Mono runtime.
        // =====================================================================
        internal static class Json
        {
            internal enum Kind { Object, Array, String, Number, Bool, Null }

            internal sealed class Pair
            {
                internal string Name = "";
                internal Value Value = null!;
            }

            internal sealed class Value
            {
                internal Kind Kind;
                internal string? Text;        // String payload
                internal double Num;          // Number payload
                internal bool Bool;           // Bool payload
                internal List<Pair>? Members; // Object members (ordered)
                internal List<Value>? Items;  // Array elements

                public bool TryGetMember(string name, out Value value)
                {
                    value = null!;
                    if (Members == null) return false;
                    foreach (var m in Members)
                    {
                        if (m.Name == name) { value = m.Value; return true; }
                    }
                    return false;
                }

                public Value Element(int index) => Items![index];
                public int ItemCount => Items!.Count;
            }

            public static Value ParseValue(string s, ref int i)
            {
                SkipWs(s, ref i);
                if (i >= s.Length)
                    throw new FormatException("JSON: unexpected end of input.");
                char c = s[i];
                if (c == '{') return ParseObject(s, ref i);
                if (c == '[') return ParseArray(s, ref i);
                if (c == '"') return ParseString(s, ref i);
                if (c == 't' || c == 'f') return ParseBool(s, ref i);
                if (c == 'n') return ParseNull(s, ref i);
                return ParseNumber(s, ref i);
            }

            public static void SkipWs(string s, ref int i)
            {
                while (i < s.Length)
                {
                    char c = s[i];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r') i++;
                    else break;
                }
            }

            private static Value ParseObject(string s, ref int i)
            {
                i++; // '{'
                var members = new List<Pair>();
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == '}')
                {
                    i++;
                    return new Value { Kind = Kind.Object, Members = members };
                }
                while (true)
                {
                    SkipWs(s, ref i);
                    var nameValue = ParseString(s, ref i);
                    SkipWs(s, ref i);
                    if (i >= s.Length || s[i] != ':')
                        throw new FormatException("JSON: expected ':' after a property name.");
                    i++; // ':'
                    members.Add(new Pair { Name = nameValue.Text!, Value = ParseValue(s, ref i) });
                    SkipWs(s, ref i);
                    if (i >= s.Length)
                        throw new FormatException("JSON: unexpected end of input in object.");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; break; }
                    throw new FormatException("JSON: expected ',' or '}' in object.");
                }
                return new Value { Kind = Kind.Object, Members = members };
            }

            private static Value ParseArray(string s, ref int i)
            {
                i++; // '['
                var items = new List<Value>();
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ']')
                {
                    i++;
                    return new Value { Kind = Kind.Array, Items = items };
                }
                while (true)
                {
                    items.Add(ParseValue(s, ref i));
                    SkipWs(s, ref i);
                    if (i >= s.Length)
                        throw new FormatException("JSON: unexpected end of input in array.");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; break; }
                    throw new FormatException("JSON: expected ',' or ']' in array.");
                }
                return new Value { Kind = Kind.Array, Items = items };
            }

            private static Value ParseString(string s, ref int i)
            {
                i++; // opening '"'
                var sb = new System.Text.StringBuilder();
                while (true)
                {
                    if (i >= s.Length)
                        throw new FormatException("JSON: unterminated string.");
                    char c = s[i++];
                    if (c == '"') break;
                    if (c != '\\') { sb.Append(c); continue; }
                    if (i >= s.Length)
                        throw new FormatException("JSON: unterminated escape in string.");
                    char e = s[i++];
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
                            if (i + 4 > s.Length)
                                throw new FormatException("JSON: bad \\u escape.");
                            int code = System.Convert.ToInt32(s.Substring(i, 4), 16);
                            i += 4;
                            sb.Append((char)code);
                            break;
                        default:
                            throw new FormatException("JSON: bad escape character '" + e + "'.");
                    }
                }
                return new Value { Kind = Kind.String, Text = sb.ToString() };
            }

            private static Value ParseNumber(string s, ref int i)
            {
                var start = i;
                if (i < s.Length && s[i] == '-') i++;
                bool intPart = false, fracPart = false, expPart = false;
                while (i < s.Length)
                {
                    char c = s[i];
                    if ((c >= '0' && c <= '9'))
                    {
                        intPart = true;
                        i++;
                    }
                    else if (c == '.' && intPart && !fracPart)
                    {
                        fracPart = true;
                        i++;
                    }
                    else if ((c == 'e' || c == 'E') && !expPart)
                    {
                        expPart = true;
                        i++;
                    }
                    else if ((c == '+' || c == '-') && expPart)
                    {
                        i++;
                    }
                    else
                    {
                        break;
                    }
                }
                string token = s.Substring(start, i - start);
                if (intPart == false)
                    throw new FormatException("JSON: invalid number token '" + token + "'.");
                double d;
                if (!double.TryParse(token, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out d))
                    throw new FormatException("JSON: invalid number token '" + token + "'.");
                return new Value { Kind = Kind.Number, Num = d };
            }

            private static Value ParseBool(string s, ref int i)
            {
                if (i + 4 <= s.Length && s.Substring(i, 4) == "true")
                {
                    i += 4;
                    return new Value { Kind = Kind.Bool, Bool = true };
                }
                if (i + 5 <= s.Length && s.Substring(i, 5) == "false")
                {
                    i += 5;
                    return new Value { Kind = Kind.Bool, Bool = false };
                }
                throw new FormatException("JSON: invalid literal.");
            }

            private static Value ParseNull(string s, ref int i)
            {
                if (i + 4 <= s.Length && s.Substring(i, 4) == "null")
                {
                    i += 4;
                    return new Value { Kind = Kind.Null };
                }
                throw new FormatException("JSON: invalid literal.");
            }
        }
    }
}
