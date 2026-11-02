using System;
using System.Globalization;

namespace OpenKSP.Core.Modding
{
    /// <summary>
    /// An immutable part definition extracted from a parsed KSP1 part <c>.cfg</c>
    /// tree (built by Task 1's <see cref="KspConfig"/> — no separate config
    /// path) or from a JSON part object (reusing the same hand-rolled JSON
    /// parser as <c>CBodyLoader.Load</c>; this is a thin mapping, not a new
    /// JSON parser).
    ///
    /// Fields (paths are relative under the containing mod's root):
    ///   id, name, category (strings)
    ///   mass (double, kg)
    ///   meshPath, texturePath, collisionPath (texture/collision: null when absent)
    ///   isStack, isLaunchable (display flags from the <c>.cfg</c>, if present)
    ///
    /// Deterministic errors (<see cref="ArgumentException"/>):
    ///   - no top-level "PART" node in the tree:
    ///       "PartDefinition: expected a top-level 'PART' node."
    ///   - missing required field (cfg or JSON — same message for both paths):
    ///       "PartDefinition: required field '{field}' is missing"
    ///   - non-numeric mass (cfg):
    ///       "PartDefinition: mass value '{token}' is not a number"
    ///   - JSON root not an object:
    ///       "PartDefinition: JSON part must be an object."
    ///
    /// Required fields (both paths): id/name, category, mass, meshPath.
    /// </summary>
    public sealed class PartDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Category { get; }
        public double Mass { get; }
        public string MeshPath { get; }
        public string? TexturePath { get; }
        public string? CollisionPath { get; }
        public bool IsStack { get; }
        public bool IsLaunchable { get; }

        private PartDefinition(
            string id, string name, string category, double mass,
            string meshPath, string? texturePath, string? collisionPath,
            bool isStack, bool isLaunchable)
        {
            Id = id; Name = name; Category = category; Mass = mass;
            MeshPath = meshPath; TexturePath = texturePath;
            CollisionPath = collisionPath; IsStack = isStack;
            IsLaunchable = isLaunchable;
        }

        /// <summary>
        /// Build a part definition from the parsed KSP1 part <c>.cfg</c> tree
        /// (Task 1 entry point is <see cref="KspConfig.Parse"/>; pass its root).
        ///
        /// Mapping:
        ///   id         = name (the "PART" node's "name" key)
        ///   meshPath   = "model".meshPath when a "model" node is present,
        ///                otherwise the top-level "mesh" key
        ///   texturePath  = "model".texturePath (else null)
        ///   collisionPath = "model".collisionPath (else null)
        ///   isStack    = any "node_stack*" key present on the "PART" node
        ///   isLaunchable = any MODULE container whose "name" == "ModuleEngines"
        ///
        /// Duplicate keys resolve last-wins per the KSP contract (the parser
        /// flags earlier duplicates; the last occurrence is used here).
        /// </summary>
        public static PartDefinition FromConfig(ConfigNode root)
        {
            var part = FindPartNode(root);

            string id = RequireString(part, "name");
            string name = id;
            string category = RequireString(part, "category");
            double mass = RequireMass(part);

            string meshPath;
            string? texturePath;
            string? collisionPath;
            var model = FindChildLast(part, "model");
            if (model != null && model.Kind == NodeKind.Brace)
            {
                meshPath = RequireString(model, "meshPath");
                texturePath = StrOrNull(model, "texturePath");
                collisionPath = StrOrNull(model, "collisionPath");
            }
            else
            {
                meshPath = RequireString(part, "mesh");
                texturePath = null;
                collisionPath = null;
            }

            bool isStack = HasNodeStack(part);
            bool isLaunchable = HasModuleEngines(part);

            return new PartDefinition(
                id, name, category, mass, meshPath, texturePath,
                collisionPath, isStack, isLaunchable);
        }

        /// <summary>
        /// Build a part definition from a JSON part document (same parser as
        /// <c>CBodyLoader.Parse</c>; no new JSON syntax). Object keys map onto
        /// the same field set: "id" (required; also reported as Name),
        /// "name" (required), "category" (required), "mass" (required, number),
        /// "meshPath" (required), "texturePath"/"collisionPath" (optional
        /// strings, else null), "isStack"/"isLaunchable" (optional booleans,
        /// default false).
        /// </summary>
        public static PartDefinition FromJson(string json)
        {
            int i = 0;
            var root = Celestial.CBodyLoader.Json.ParseValue(json, ref i);
            if (root.Kind != Celestial.CBodyLoader.Json.Kind.Object)
                throw new ArgumentException("PartDefinition: JSON part must be an object.");

            string id = RequireString(root, "id");
            string name = RequireString(root, "name");
            string category = RequireString(root, "category");
            double mass = RequireMass(root);
            string meshPath = RequireString(root, "meshPath");
            string? texturePath = StrOrNull(root, "texturePath");
            string? collisionPath = StrOrNull(root, "collisionPath");
            bool isStack = BoolOrFalse(root, "isStack");
            bool isLaunchable = BoolOrFalse(root, "isLaunchable");

            return new PartDefinition(
                id, name, category, mass, meshPath, texturePath,
                collisionPath, isStack, isLaunchable);
        }

        // ---- cfg tree helpers --------------------------------------------------

        private static ConfigNode FindPartNode(ConfigNode root)
        {
            for (int k = 0; k < root.SubNodes.Count; k++)
            {
                var n = root.SubNodes[k];
                if (n.Name == "PART" && n.Kind == NodeKind.Brace)
                    return n;
            }
            throw new ArgumentException("PartDefinition: expected a top-level 'PART' node.");
        }

        /// <summary>
        /// Last occurrence of the named child (KSP last-wins contract; the
        /// parser keeps earlier duplicates as flagged siblings).
        /// </summary>
        private static ConfigNode? FindChildLast(ConfigNode parent, string name)
        {
            ConfigNode? last = null;
            for (int k = 0; k < parent.SubNodes.Count; k++)
                if (parent.SubNodes[k].Name == name)
                    last = parent.SubNodes[k];
            return last;
        }

        private static string RequireString(ConfigNode parent, string field)
        {
            var n = FindChildLast(parent, field);
            if (n == null || n.Kind != NodeKind.Value || string.IsNullOrEmpty(n.Value))
                throw new ArgumentException($"PartDefinition: required field '{field}' is missing");
            return StripQuotes(n.Value!);
        }

        private static string? StrOrNull(ConfigNode parent, string field)
        {
            var n = FindChildLast(parent, field);
            if (n == null || n.Kind != NodeKind.Value || string.IsNullOrEmpty(n.Value))
                return null;
            return StripQuotes(n.Value!);
        }

        private static double RequireMass(ConfigNode parent)
        {
            var n = FindChildLast(parent, "mass");
            if (n == null || n.Kind != NodeKind.Value || string.IsNullOrEmpty(n.Value))
                throw new ArgumentException("PartDefinition: required field 'mass' is missing");
            return ParseMass(n.Value!);
        }

        private static double ParseMass(string token)
        {
            try
            {
                double v = double.Parse(token, CultureInfo.InvariantCulture);
                if (double.IsNaN(v) || double.IsInfinity(v))
                    throw new ArgumentException($"PartDefinition: mass value '{token}' is not a number");
                return v;
            }
            catch (FormatException)
            {
                throw new ArgumentException($"PartDefinition: mass value '{token}' is not a number");
            }
        }

        private static bool HasNodeStack(ConfigNode part)
        {
            for (int k = 0; k < part.SubNodes.Count; k++)
                if (part.SubNodes[k].Name.StartsWith("node_stack", StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool HasModuleEngines(ConfigNode part)
        {
            for (int k = 0; k < part.SubNodes.Count; k++)
            {
                var m = part.SubNodes[k];
                if (m.Kind != NodeKind.Brace || m.Name != "MODULE")
                    continue;
                var nameNode = FindChildLast(m, "name");
                if (nameNode != null && nameNode.Kind == NodeKind.Value &&
                    StripQuotes(nameNode.Value!) == "ModuleEngines")
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Strips matching outer quotes from a verbatim value token
        /// (KSP quoted strings arrive with their quote characters).
        /// </summary>
        private static string StripQuotes(string v)
        {
            if (v.Length >= 2 && v[0] == '"' && v[v.Length - 1] == '"')
                return v.Substring(1, v.Length - 2);
            return v;
        }

        // ---- JSON helpers (thin mapping onto the existing internal Json) ------

        private static string RequireString(Celestial.CBodyLoader.Json.Value v, string field)
        {
            if (!v.TryGetMember(field, out var m) || m.Kind != Celestial.CBodyLoader.Json.Kind.String ||
                string.IsNullOrEmpty(m.Text))
                throw new ArgumentException($"PartDefinition: required field '{field}' is missing");
            return m.Text!;
        }

        private static string? StrOrNull(Celestial.CBodyLoader.Json.Value v, string field)
        {
            if (!v.TryGetMember(field, out var m) || m.Kind != Celestial.CBodyLoader.Json.Kind.String)
                return null;
            return string.IsNullOrEmpty(m.Text) ? null : m.Text!;
        }

        private static double RequireMass(Celestial.CBodyLoader.Json.Value v)
        {
            if (!v.TryGetMember("mass", out var m) || m.Kind != Celestial.CBodyLoader.Json.Kind.Number)
                throw new ArgumentException("PartDefinition: required field 'mass' is missing");
            if (double.IsNaN(m.Num) || double.IsInfinity(m.Num))
                throw new ArgumentException("PartDefinition: mass value '" + m.Num.ToString(CultureInfo.InvariantCulture) + "' is not a number");
            return m.Num;
        }

        private static bool BoolOrFalse(Celestial.CBodyLoader.Json.Value v, string field)
        {
            if (v.TryGetMember(field, out var m) &&
                m.Kind == Celestial.CBodyLoader.Json.Kind.Bool)
                return m.Bool;
            return false;
        }
    }
}
