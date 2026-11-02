using System;
using System.Globalization;
using System.IO;
using OpenKSP.Core.Modding;
using Xunit;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T20: Task 2 of Phase 3 — part definitions from `.cfg` (PartDefinition).
    ///
    /// Fixture: a hand-built `part.cfg` in fixtures/ (modeled on the real
    /// structuralMiniNode.cfg, simplified: one body, one mesh, one texture) —
    /// parsed via Task 1's tree, then PartDefinition.FromConfig. Asserts every
    /// field: id, name, category, mass (exact double round-trip), mesh path,
    /// texture path, collision path (absent), and the display flags. Also:
    /// duplicate keys resolve last-wins; a missing required field throws a
    /// deterministic error naming the field; a non-numeric mass is rejected;
    /// model-node paths override the top-level mesh key; isStack comes from
    /// node_stack* keys and isLaunchable from a ModuleEngines MODULE node;
    /// and the JSON path maps the same fields (reusing the existing Json
    /// parser, no new syntax).
    /// </summary>
    public class T20
    {
        // Resolves the committed fixture relative to the test output directory
        // (the csproj copies fixtures/ next to the test assembly).
        private static string FixtureText =>
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "part.cfg"));

        private static PartDefinition ParseFixture()
        {
            var root = KspConfig.Parse(FixtureText, "part.cfg");
            return PartDefinition.FromConfig(root);
        }

        [Fact]
        public void AllFixtureFieldsMatchExactly()
        {
            var p = ParseFixture();

            Assert.Equal("structuralMiniNode", p.Id);
            Assert.Equal("structuralMiniNode", p.Name);
            Assert.Equal("Structural", p.Category);
            Assert.Equal(0.15, p.Mass);
            // Top-level "mesh" key supplies the mesh path (no model meshPath).
            Assert.Equal("model.mu", p.MeshPath);
            // model node supplies the texture path.
            Assert.Equal("Squad/Parts/structural/structuralMini.dds", p.TexturePath);
            // No collision key in the fixture.
            Assert.Null(p.CollisionPath);
            // node_stack_* keys present -> stack part; no MODULE node -> not launchable.
            Assert.True(p.IsStack);
            Assert.False(p.IsLaunchable);
        }

        [Fact]
        public void MassRoundTripsAsExactDouble()
        {
            var root = KspConfig.Parse(FixtureText, "part.cfg");
            var part = PartChild(root);
            var massNode = Child(part, "mass");

            Assert.Equal("0.15", massNode.Value);
            var p = PartDefinition.FromConfig(root);
            // The parsed double is bit-exact and round-trips to the same tokens.
            Assert.Equal("0.15", p.Mass.ToString(CultureInfo.InvariantCulture));
        }

        [Fact]
        public void DuplicateKeyResolvesLastWins()
        {
            const string src =
                "PART\n{\n" +
                "  name = dupTest\n" +
                "  category = Test\n" +
                "  mass = 1.25\n" +
                "  mass = 3.75\n" +
                "  mesh = model.mu\n" +
                "}";

            var p = PartDefinition.FromConfig(KspConfig.Parse(src, "dup.cfg"));
            // The parser keeps both occurrences (flagged); the last wins here.
            Assert.Equal(3.75, p.Mass);
        }

        [Fact]
        public void MissingRequiredFieldThrowsErrorNamingTheField()
        {
            var baseCfg =
                "PART\n{\n" +
                "  name = m1\n" +
                "  category = Test\n" +
                "  mass = 2.5\n" +
                "  mesh = model.mu\n" +
                "}";

            // Each required field, removed one at a time; the error names it.
            string[] fields = new[] { "name", "category", "mass", "mesh" };
            for (int k = 0; k < fields.Length; k++)
            {
                string src = baseCfg;
                int idx = src.IndexOf("  " + fields[k] + " = ", StringComparison.Ordinal);
                int end = src.IndexOf('\n', idx);
                src = src.Substring(0, idx) + src.Substring(end + 1);

                var ex = Assert.Throws<ArgumentException>(
                    () => PartDefinition.FromConfig(KspConfig.Parse(src, "missing.cfg")));
                Assert.Equal(
                    "PartDefinition: required field '" + fields[k] + "' is missing",
                    ex.Message);
            }
        }

        [Fact]
        public void NonNumericMassIsRejected()
        {
            const string src =
                "PART\n{\n" +
                "  name = badMass\n" +
                "  category = Test\n" +
                "  mass = not-a-number\n" +
                "  mesh = model.mu\n" +
                "}";

            var ex = Assert.Throws<ArgumentException>(
                () => PartDefinition.FromConfig(KspConfig.Parse(src, "badmass.cfg")));
            Assert.Equal(
                "PartDefinition: mass value 'not-a-number' is not a number", ex.Message);
        }

        [Fact]
        public void MissingTopLevelPartNodeIsRejected()
        {
            const string src =
                "Vessel\n{\n" +
                "  name = nope\n" +
                "  category = Test\n" +
                "  mass = 1\n" +
                "  mesh = model.mu\n" +
                "}";

            var ex = Assert.Throws<ArgumentException>(
                () => PartDefinition.FromConfig(KspConfig.Parse(src, "nopart.cfg")));
            Assert.Equal("PartDefinition: expected a top-level 'PART' node.", ex.Message);
        }

        [Fact]
        public void ModelNodePathsOverrideTopLevelMeshKey()
        {
            const string src =
                "PART\n{\n" +
                "  name = m2\n" +
                "  category = Test\n" +
                "  mass = 4\n" +
                "  mesh = legacy.mu\n" +
                "  model\n" +
                "  {\n" +
                "    meshPath = model/updated.mu\n" +
                "    texturePath = model/updated.dds\n" +
                "    collisionPath = model/collision.mu\n" +
                "  }\n" +
                "}";

            var p = PartDefinition.FromConfig(KspConfig.Parse(src, "modelnode.cfg"));
            Assert.Equal("model/updated.mu", p.MeshPath);
            Assert.Equal("model/updated.dds", p.TexturePath);
            Assert.Equal("model/collision.mu", p.CollisionPath);
        }

        [Fact]
        public void ModuleEnginesMakesPartLaunchable()
        {
            const string src =
                "PART\n{\n" +
                "  name = m3\n" +
                "  category = Test\n" +
                "  mass = 1\n" +
                "  mesh = model.mu\n" +
                "  node_stack_top = 0, 0, 1, 0, 0, 1, 0\n" +
                "  MODULE\n" +
                "  {\n" +
                "    name = ModuleEngines\n" +
                "  }\n" +
                "}";

            var p = PartDefinition.FromConfig(KspConfig.Parse(src, "engines.cfg"));
            Assert.True(p.IsStack);
            Assert.True(p.IsLaunchable);
        }

        [Fact]
        public void JsonPathMapsTheSameFields()
        {
            const string json =
                "{\"id\":\"miniNode\",\"name\":\"Mini Node\"," +
                "\"category\":\"Test\",\"mass\":0.15," +
                "\"meshPath\":\"M.mbm\",\"texturePath\":\"T.dds\"," +
                "\"collisionPath\":null,\"isStack\":true,\"isLaunchable\":false}";

            var p = PartDefinition.FromJson(json);
            Assert.Equal("miniNode", p.Id);
            Assert.Equal("Mini Node", p.Name);
            Assert.Equal("Test", p.Category);
            Assert.Equal("0.15", p.Mass.ToString(CultureInfo.InvariantCulture));
            Assert.Equal("M.mbm", p.MeshPath);
            Assert.Equal("T.dds", p.TexturePath);
            Assert.Null(p.CollisionPath);
            Assert.True(p.IsStack);
            Assert.False(p.IsLaunchable);
        }

        [Fact]
        public void JsonMissingRequiredFieldThrowsErrorNamingTheField()
        {
            var baseJson =
                "{\"id\":\"jid\",\"name\":\"j1\",\"category\":\"Test\"," +
                "\"mass\":2.5,\"meshPath\":\"M.mu\"}";
            string[] fields = new[] { "id", "name", "category", "mass", "meshPath" };
            string[] valueTokens = new[] { "\"jid\"", "\"j1\"", "\"Test\"", "2.5", "\"M.mu\"" };

            for (int k = 0; k < fields.Length; k++)
            {
                // Remove the whole "field":value, segment so the remaining JSON
                // stays well-formed (a key with its comma stripped is not valid).
                string segment = k == fields.Length - 1
                    ? "," + "\"" + fields[k] + "\":" + valueTokens[k]
                    : "\"" + fields[k] + "\":" + valueTokens[k] + ",";
                string json = baseJson.Replace(segment, "");
                var ex = Assert.Throws<ArgumentException>(() => PartDefinition.FromJson(json));
                Assert.Equal(
                    "PartDefinition: required field '" + fields[k] + "' is missing",
                    ex.Message);
            }
        }

        [Fact]
        public void JsonRootMustBeAnObject()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => PartDefinition.FromJson("[1,2,3]"));
            Assert.Equal("PartDefinition: JSON part must be an object.", ex.Message);
        }

        // ---- tree helpers ------------------------------------------------------

        private static ConfigNode PartChild(ConfigNode root)
        {
            for (int i = 0; i < root.SubNodes.Count; i++)
                if (root.SubNodes[i].Name == "PART")
                    return root.SubNodes[i];
            throw new InvalidOperationException("PART node not found");
        }

        private static ConfigNode Child(ConfigNode parent, string name)
        {
            ConfigNode? last = null;
            for (int i = 0; i < parent.SubNodes.Count; i++)
                if (parent.SubNodes[i].Name == name)
                    last = parent.SubNodes[i];
            Assert.NotNull(last);
            return last!;
        }
    }
}
