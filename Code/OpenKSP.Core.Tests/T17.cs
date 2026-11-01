using OpenKSP.Core.Modding;
using System.Linq;
using Xunit;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T17: Task 1 of Phase 3 — the .cfg ConfigNode parser (KSP1 grammar).
    ///
    /// Parses a hand-written fixture carrying nested brace + paren nodes,
    /// scalar and quoted-string values, // comments, a :BEFORE[...] condition
    /// token, and a deliberately-duplicate key — and asserts the exact tree
    /// (names, kinds, values, sub-node counts, condition token, duplicate flag
    /// per "last wins"). Two malformed inputs — an unclosed brace and a
    /// bad-indent line — yield the exact deterministic error string
    /// (file path + line number), plus two more (unclosed paren, mixed
    /// closer).
    /// </summary>
    public class T17
    {
        // Hand-written KSP1 part-style fixture. Comments (line 1 and line 28)
        // must produce no nodes. Line 30's repeated "thrust" key is the
        // deliberate duplicate (last wins, flagged).
        private const string Fixture =
            "// hand-written fixture for T17\n" +
            "PART\n" +
            "{\n" +
            "  name = \"Sep Motor (1)\"\n" +
            "  module = Part\n" +
            "  category = propulsionTech\n" +
            "  mass = 250.5\n" +
            "  enginePower = 125\n" +
            "  maxThrottle = 1.2\n" +
            "  airflow = 1.2e-3\n" +
            "  nodeStack = 0.0, 0, 0\n" +
            "\n" +
            "  model {\n" +
            "    meshPath = Squad/Parts/Engines/sepMotor.mu\n" +
            "    texturePath = Squad/Parts/Engines/sepMotor.dds\n" +
            "    transform(\n" +
            "      x = 0.0\n" +
            "      y = -1.35\n" +
            "      z = 0.0\n" +
            "    )\n" +
            "  }\n" +
            "\n" +
            "  // duplicate key: last wins\n" +
            "  thrust = 100\n" +
            "  thrust = 250\n" +
            "\n" +
            "  pre = :BEFORE[engineModule]\n" +
            "}";

        private static ConfigNode ParseFixture() => KspConfig.Parse(Fixture, "fixture.cfg");

        private static ConfigNode PartNode(ConfigNode root) => Child(root, "PART");

        private static ConfigNode Child(ConfigNode parent, string name)
        {
            for (int i = 0; i < parent.SubNodes.Count; i++)
                if (parent.SubNodes[i].Name == name)
                    return parent.SubNodes[i];
            throw new System.InvalidOperationException($"child '{name}' not found");
        }

        private static int IndexOf(ConfigNode parent, string name, int start)
        {
            for (int i = start; i < parent.SubNodes.Count; i++)
                if (parent.SubNodes[i].Name == name)
                    return i;
            throw new System.InvalidOperationException($"child '{name}' not found after index {start}");
        }

        [Fact]
        public void RootIsUnNamedRootContainerHoldingPart()
        {
            var root = ParseFixture();

            // Root is the unnamed container; its single child is the PART node.
            Assert.Equal("", root.Name);
            Assert.Equal(NodeKind.Root, root.Kind);
            Assert.Null(root.Value);
            Assert.Empty(root.Items);
            Assert.Equal(new[] { "PART" },
                root.SubNodes.Select(n => n.Name).ToArray());

            // Comments (lines 1 and 28) and blank lines produce no nodes:
            // name, module, category, mass, enginePower, maxThrottle, airflow,
            // nodeStack, model, thrust, thrust, pre.
            Assert.Equal(
                new[] { "name", "module", "category", "mass", "enginePower",
                        "maxThrottle", "airflow", "nodeStack", "model",
                        "thrust", "thrust", "pre" },
                PartNode(root).SubNodes.Select(n => n.Name).ToArray());
        }

        [Fact]
        public void ValuesAreStoredVerbatimIncludingQuotedStrings()
        {
            var part = PartNode(ParseFixture());

            // Quoted string, verbatim (quotes included).
            var name = Child(part, "name");
            Assert.Equal(NodeKind.Value, name.Kind);
            Assert.Equal("\"Sep Motor (1)\"", name.Value);
            Assert.Null(name.Condition);

            // Scalars verbatim: int, float, scientific notation.
            Assert.Equal("250.5", Child(part, "mass").Value);
            Assert.Equal("125", Child(part, "enginePower").Value);
            Assert.Equal("1.2", Child(part, "maxThrottle").Value);
            Assert.Equal("1.2e-3", Child(part, "airflow").Value);

            // Multi-token value: whitespace/comma split into Items.
            var nodeStack = Child(part, "nodeStack");
            Assert.Equal(NodeKind.Value, nodeStack.Kind);
            Assert.Collection(nodeStack.Items, i => Assert.Equal("0.0", i),
                i => Assert.Equal("0", i), i => Assert.Equal("0", i));
        }

        [Fact]
        public void NestedBraceAndParenNodesFormTheExactSubtree()
        {
            var part = PartNode(ParseFixture());
            var model = Child(part, "model");

            // Brace container: children are sub-nodes, no attributes/values.
            Assert.Equal(NodeKind.Brace, model.Kind);
            Assert.Null(model.Value);
            Assert.Empty(model.Attributes);
            Assert.Equal(new[] { "meshPath", "texturePath", "transform" },
                model.SubNodes.Select(n => n.Name).ToArray());
            Assert.Equal("Squad/Parts/Engines/sepMotor.mu",
                Child(model, "meshPath").Value);
            Assert.Equal("Squad/Parts/Engines/sepMotor.dds",
                Child(model, "texturePath").Value);

            // Paren container: children are attributes, not sub-nodes.
            var transform = Child(model, "transform");
            Assert.Equal(NodeKind.Paren, transform.Kind);
            Assert.Null(transform.Value);
            Assert.Empty(transform.SubNodes);
            Assert.Equal(3, transform.Attributes.Count);
            Assert.Equal(new[] { "x", "y", "z" },
                transform.Attributes.Select(n => n.Name).ToArray());
            Assert.Equal("0.0", transform.Attributes[0].Value);
            Assert.Equal("-1.35", transform.Attributes[1].Value);
            Assert.Equal("0.0", transform.Attributes[2].Value);
            Assert.All(transform.Attributes, a =>
            {
                Assert.Equal(NodeKind.Value, a.Kind);
                Assert.Null(a.Condition);
            });
        }

        [Fact]
        public void ConditionTokenIsParsedAsDataNotExecuted()
        {
            var part = PartNode(ParseFixture());
            var pre = Child(part, "pre");

            Assert.Equal(NodeKind.Value, pre.Kind);
            Assert.Equal(":BEFORE[engineModule]", pre.Value);
            // The condition is a data token on the node, carried verbatim.
            Assert.Equal(":BEFORE[engineModule]", pre.Condition);
            Assert.NotEmpty(pre.Items);
        }

        [Fact]
        public void DuplicateKeyIsFlaggedAndLastWins()
        {
            var part = PartNode(ParseFixture());

            int first = IndexOf(part, "thrust", 0);
            int last = IndexOf(part, "thrust", first + 1);

            // First occurrence: unflagged (0 earlier same-named siblings).
            Assert.Equal(NodeKind.Value, part.SubNodes[first].Kind);
            Assert.Equal("100", part.SubNodes[first].Value);
            Assert.Equal(0, part.SubNodes[first].DuplicateCount);

            // Second occurrence: flagged as the 1st prior duplicate; its value
            // ("250") is the one that wins per the KSP contract.
            Assert.Equal("250", part.SubNodes[last].Value);
            Assert.Equal(1, part.SubNodes[last].DuplicateCount);
        }

        [Fact]
        public void LineCommentsDoNotProduceNodes()
        {
            var root = KspConfig.Parse(
                "A\n{\n  a = 1\n  // a comment\n  b = 2\n}", "comments.cfg");

            Assert.Equal(new[] { "A" },
                root.SubNodes.Select(n => n.Name).ToArray());
            Assert.Equal(new[] { "a", "b" },
                Child(root, "A").SubNodes.Select(n => n.Name).ToArray());
        }

        [Fact]
        public void UnclosedBraceProducesExactDeterministicError()
        {
            const string src = "PART\n{\n  mass = 5";
            var ex = Assert.Throws<System.FormatException>(
                () => KspConfig.Parse(src, "broken.cfg"));
            Assert.Equal("Unclosed node 'PART' opened at broken.cfg:1", ex.Message);
        }

        [Fact]
        public void UnclosedParenProducesExactDeterministicError()
        {
            const string src = "transform(\n  x = 0.0";
            var ex = Assert.Throws<System.FormatException>(
                () => KspConfig.Parse(src, "broken.cfg"));
            Assert.Equal("Unclosed node 'transform' opened at broken.cfg:1", ex.Message);
        }

        [Fact]
        public void BadIndentCloserProducesExactDeterministicError()
        {
            // Closer at indent 1 is shallower than the opener at indent 2.
            const string src = "  PART {\n      mass = 5\n }";
            var ex = Assert.Throws<System.FormatException>(
                () => KspConfig.Parse(src, "broken.cfg"));
            Assert.Equal("Bad indent at broken.cfg:3", ex.Message);
        }

        [Fact]
        public void MismatchedCloserProducesExactDeterministicError()
        {
            const string src = "PART {\n  mass = 5\n)";
            var ex = Assert.Throws<System.FormatException>(
                () => KspConfig.Parse(src, "broken.cfg"));
            Assert.Equal("Mismatched closer ')' at broken.cfg:3; expected '}' for 'PART'",
                ex.Message);
        }
    }
}
