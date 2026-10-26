using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;

namespace OpenKSP.Core.Celestial
{
    // Loads celestial bodies from JSON per Design Doc §6.
    // Schema:
    //   { "units": {...},
    //     "bodies": [ { "id", "name", "mass", "radius", "mu", "parent" } ] }
    // Required: id, name, mass, radius, mu (GM, m³/s²).
    // Optional: parent (hierarchy, Phase 2). Derived values are computed in
    // Core from mu, never stored in the JSON.
    public static class CBodyLoader
    {
        private const string PlanetsJsonRelativePath =
            "Assets/StreamingAssets/Planets/planets.json";

        // Load bodies from the canonical data file.
        // <param name="path">Absolute path, or null for the default <c>Assets/StreamingAssets/Planets/planets.json</c> (resolved relative to the repo root).</param>
        // <returns>Immutable list of bodies. Throws <see cref="InvalidDataException"/> for schema violations.</returns>
        public static IImmutableList<CBody> Load(string? path = null)
        {
            var resolvedPath = path ?? ResolveDefaultPath();
            var json = File.ReadAllText(resolvedPath);
            return Parse(json);
        }

        // Default path relative to the repo root (AppDomain base dir walked up 4 levels).
        public static string ResolveDefaultPath()
        {
            // AppDomain.BaseDirectory is the build output dir (…/Code/OpenKSP.Core(.Tests)/bin/Debug/net8.0);
            // five parents reach the repo root: net8.0 -> Debug -> bin -> OpenKSP.Core(.Tests) -> Code -> root.
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (var i = 0; i < 5 && dir != null; i++)
            {
                dir = dir.Parent;
            }
            return Path.Combine(dir?.FullName ?? "", PlanetsJsonRelativePath);
        }

        // Parse a §6-schema JSON body string.
        // Position/velocity are left at origin; world state (task 3) initializes them.
        public static IImmutableList<CBody> Parse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("bodies", out var bodiesEl) ||
                bodiesEl.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("planets.json: 'bodies' array is required.");
            }

            var list = new List<CBody>();
            foreach (var bodyEl in bodiesEl.EnumerateArray())
            {
                var id = ReadRequiredString(bodyEl, "id");
                var name = ReadRequiredString(bodyEl, "name");
                ReadRequiredDouble(bodyEl, "mass");
                var radius = ReadRequiredDouble(bodyEl, "radius");
                var mu = ReadRequiredDouble(bodyEl, "mu");

                list.Add(new CBody
                {
                    Id = id,
                    Name = name,
                    Position = D3.Zero,
                    Velocity = DVector.Zero,
                    Radius = radius,
                    Mu3PerS2 = mu
                });

                // Optional 'parent' is accepted but unused in Phase 1 (Phase 2 hierarchy).
            }

            return list.ToImmutableList();
        }

        private static string ReadRequiredString(JsonElement obj, string prop)
        {
            if (!obj.TryGetProperty(prop, out var el) || el.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException($"planets.json: body property '{prop}' (string) is required.");
            }
            return el.GetString()!;
        }

        private static double ReadRequiredDouble(JsonElement obj, string prop)
        {
            if (!obj.TryGetProperty(prop, out var el) || el.ValueKind != JsonValueKind.Number)
            {
                throw new InvalidDataException($"planets.json: body property '{prop}' (number) is required.");
            }
            return el.GetDouble();
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
    }
}
