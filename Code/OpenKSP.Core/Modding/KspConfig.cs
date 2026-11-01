using System;
using System.Collections.Generic;
using System.Text;

namespace OpenKSP.Core.Modding
{
    /// <summary>Container/leaf kind of a parsed ConfigNode.</summary>
    public enum NodeKind
    {
        Root,
        Brace,   // NAME { ... }
        Paren,   // NAME( attrs )
        Value    // NAME = value, or a bare (valueless) node name
    }

    /// <summary>
    /// A parsed KSP1 ConfigNode. Immutable; built once per parse.
    /// </summary>
    public sealed class ConfigNode
    {
        public string Name { get; }
        public NodeKind Kind { get; }

        /// <summary>Value text, verbatim (Value nodes only; containers: null).</summary>
        public string? Value { get; }

        /// <summary>
        /// Value tokens split on whitespace and/or commas (Value nodes only;
        /// empty for containers and valueless nodes).
        /// </summary>
        public IReadOnlyList<string> Items { get; }

        /// <summary>
        /// Direct children (Brace containers only; empty for Paren containers
        /// and Value nodes).
        /// </summary>
        public IReadOnlyList<ConfigNode> SubNodes { get; }

        /// <summary>
        /// Attribute entries — the children of a Paren container (key=value or
        /// bare entries inside "NAME(...)"); empty for Brace containers and
        /// Value nodes.
        /// </summary>
        public IReadOnlyList<ConfigNode> Attributes { get; }

        /// <summary>
        /// Condition token ":BEFORE(...)"/":FOR(...)"/":AFTER(...)" attached to
        /// a value; else null. Parsed as data, not executed.
        /// </summary>
        public string? Condition { get; }

        /// <summary>
        /// Number of earlier siblings (same parent) with the same Name — the
        /// KSP "last wins" duplicate flag; 0 when this is the only one.
        /// </summary>
        public int DuplicateCount { get; }

        internal ConfigNode(
            string name, NodeKind kind, string? value,
            IReadOnlyList<string> items, IReadOnlyList<ConfigNode> subNodes,
            IReadOnlyList<ConfigNode> attributes, string? condition, int duplicateCount)
        {
            Name = name; Kind = kind; Value = value; Items = items;
            SubNodes = subNodes; Attributes = attributes;
            Condition = condition; DuplicateCount = duplicateCount;
        }
    }

    /// <summary>
    /// Pure-C# KSP1 ConfigNode (.cfg) reader.
    ///
    /// Grammar (grounded on step-03-research.md §5 and the real KSP fixtures):
    ///   - Container: NAME { ... } or NAME( attrs ); NAME and bracket on one
    ///     line ("NAME{"/"NAME (") or two lines (NAME on one line, then a lone
    ///     "{" / "(" line).
    ///   - Key-value: NAME = value. Values: int/float (incl. scientific
    ///     1.2e-3), booleans, quoted strings, or raw tokens; stored verbatim.
    ///     Multi-token values are whitespace- or comma-separated (Items).
    ///   - Bare name (no "="): a valueless node; its children are the following
    ///     deeper-indented entries closed by its bracket line.
    ///   - Comments: // to end-of-line; /* ... */ (may span lines). Line
    ///     numbers are preserved when comments are stripped.
    ///   - Paren attributes: key=value (or bare) lines inside a "NAME("
    ///     container are the container's Attributes.
    ///   - Conditions: a value token starting with ':' ("BEFORE(...)"/
    ///     "FOR(...)"/"AFTER(...)" style) is parsed as data (Condition).
    ///   - Indentation: spaces and tabs are significant; one node per indent
    ///     level; ragged sibling indentation within a container is allowed.
    ///   - Duplicate keys in a container: LAST WINS; the kept node carries
    ///     DuplicateCount = number of earlier same-named siblings.
    ///
    /// Errors (FormatException, deterministic — file + line):
    ///   - unclosed container              -> "Unclosed node '{name}' opened at {file}:{line}"
    ///   - closer shallower than opener   -> "Bad indent at {file}:{line}"
    ///   - closer with no open container  -> "Unexpected closer '{c}' at {file}:{line}"
    ///   - mixed-bracket closer           -> "Mismatched closer '{c}' at {file}:{line}; expected '{e}' for '{name}'"
    ///   - lone bracket line with no name -> "Missing node name at {file}:{line}"
    ///   - key line missing name or value -> "Missing value at {file}:{line}"
    ///
    /// The tree is allocated once per parse; a fixture is parsed once per load,
    /// not per frame.
    /// </summary>
    public static class KspConfig
    {
        private static readonly List<string> EmptyItems = new List<string>(0);
        private static readonly List<ConfigNode> EmptyNodes = new List<ConfigNode>(0);

        public static ConfigNode Parse(string source, string filePath)
        {
            var lines = GetLines(StripComments(source));

            // Stack of open frames; index 0 is the phantom root.
            var stack = new List<Frame>
            {
                new Frame("", NodeKind.Root, -1, -1, new List<ConfigNode>(), false)
            };

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                string content = line.Content;
                int ln = line.LineNo;

                // Lone closer line: "}" or ")".
                char closer = CloserOf(content);
                if (closer != '\0')
                {
                    if (stack.Count == 1)
                        throw new FormatException($"Unexpected closer '{closer}' at {filePath}:{ln}");

                    var f = stack[stack.Count - 1];
                    char expected = f.Kind == NodeKind.Paren ? ')' : '}';
                    if (closer != expected)
                        throw new FormatException(
                            $"Mismatched closer '{closer}' at {filePath}:{ln}; expected '{expected}' for '{f.Name}'");
                    if (line.Indent < f.Indent)
                        throw new FormatException($"Bad indent at {filePath}:{ln}");

                    stack.RemoveAt(stack.Count - 1);
                    Attach(stack, BuildContainer(f));
                    continue;
                }

                // Container opener: a bracket before any key separator (=).
                int open = FindOpener(content);
                if (open >= 0)
                {
                    string name = content.Substring(0, open).Trim();
                    if (name.Length == 0)
                    {
                        // Lone bracket line: it opens the pending bare-name frame.
                        if (stack.Count < 2)
                            throw new FormatException($"Missing node name at {filePath}:{ln}");
                        var pending = stack[stack.Count - 1];
                        pending.Kind = content[open] == '{' ? NodeKind.Brace : NodeKind.Paren;
                        pending.Pending = false;
                    }
                    else
                    {
                        stack.Add(new Frame(
                            name,
                            content[open] == '{' ? NodeKind.Brace : NodeKind.Paren,
                            line.Indent, ln, new List<ConfigNode>(), false));
                    }
                    continue;
                }

                // Key-value line: NAME = value.
                int eq = content.IndexOf('=');
                if (eq >= 0)
                {
                    string name = content.Substring(0, eq).Trim();
                    string value = content.Substring(eq + 1).Trim();
                    if (name.Length == 0 || value.Length == 0)
                        throw new FormatException($"Missing value at {filePath}:{ln}");
                    AddValue(stack, name, value, ln);
                    continue;
                }

                // Bare name: valueless node (a pending container if a lone
                // bracket line follows; otherwise it is closed at EOF).
                stack.Add(new Frame(content, NodeKind.Brace, line.Indent, ln,
                    new List<ConfigNode>(), true));
            }

            if (stack.Count > 1)
            {
                var f = stack[stack.Count - 1];
                throw new FormatException(
                    $"Unclosed node '{f.Name}' opened at {filePath}:{f.OpenedAtLn}");
            }

            return BuildContainer(stack[0]);
        }

        // ---- Line preparation -------------------------------------------------

        private static List<Line> GetLines(string stripped)
        {
            var lines = new List<Line>();
            int lineNo = 0;
            int i = 0;
            int count = stripped.Length;
            while (i < count)
            {
                int end = stripped.IndexOf('\n', i);
                if (end < 0) end = count;
                lineNo++;
                string raw = stripped.Substring(i, end - i);
                int indent = 0;
                while (indent < raw.Length && (raw[indent] == ' ' || raw[indent] == '\t'))
                    indent++;
                string content = raw.Substring(indent);
                int t = content.Length;
                while (t > 0 && (content[t - 1] == ' ' || content[t - 1] == '\t' || content[t - 1] == '\r'))
                    t--;
                content = content.Substring(0, t);
                if (content.Length > 0)
                    lines.Add(new Line(indent, content, lineNo));
                i = end + 1;
            }
            return lines;
        }

        /// <summary>
        /// Removes // line comments and /* ... */ block comments. Newlines
        /// *inside* block comments are preserved so that file line numbers
        /// stay stable.
        /// </summary>
        private static string StripComments(string source)
        {
            var sb = new StringBuilder(source.Length);
            int i = 0;
            int n = source.Length;
            while (i < n)
            {
                char c = source[i];
                if (c == '/' && i + 1 < n && source[i + 1] == '/')
                {
                    while (i < n && source[i] != '\n')
                        i++;
                }
                else if (c == '/' && i + 1 < n && source[i + 1] == '*')
                {
                    sb.Append(' ');
                    i += 2;
                    while (i + 1 < n && !(source[i] == '*' && source[i + 1] == '/'))
                    {
                        if (source[i] == '\n')
                            sb.Append('\n');
                        i++;
                    }
                    i += 2;
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }
            return sb.ToString();
        }

        // ---- Per-line structure detection ---------------------------------------

        /// <summary>First char of a lone-closer line: '}' / ')'; else '\0'.
        /// A line whose first char is '{' or '(' is an opener, not a closer.
        /// </summary>
        private static char CloserOf(string content)
        {
            if (content.Length == 0)
                return '\0';
            char c = content[0];
            if (c == '}' || c == ')')
                return c;
            return '\0';
        }

        /// <summary>
        /// Index of the first '{' or '(' that appears before any key separator
        /// ('='). Returns -1 when none (i.e. the line is key-value or bare).
        /// </summary>
        private static int FindOpener(string content)
        {
            int eq = content.IndexOf('=');
            int limit = eq >= 0 ? eq : content.Length;
            for (int i = 0; i < limit; i++)
            {
                char c = content[i];
                if (c == '{' || c == '(')
                    return i;
            }
            return -1;
        }

        // ---- Node construction --------------------------------------------------

        private static ConfigNode BuildContainer(Frame f)
        {
            return new ConfigNode(
                f.Name, f.Kind, null, EmptyItems,
                // The phantom root is a brace-like container: it passes its
                // children through as SubNodes.
                f.Kind == NodeKind.Brace || f.Kind == NodeKind.Root ? f.Children : EmptyNodes,
                f.Kind == NodeKind.Paren ? f.Children : EmptyNodes,
                null, -1);
        }

        private static void AddValue(List<Frame> stack, string name, string value, int ln)
        {
            var frame = stack[stack.Count - 1];
            int dup = CountDuplicates(frame.Children, name);
            frame.Children.Add(new ConfigNode(
                name, NodeKind.Value, value, SplitItems(value),
                EmptyNodes, EmptyNodes, ConditionOf(value), dup));
            // ln is retained by callers via file line numbers; value nodes are
            // reported through the container they attach to.
        }

        private static void Attach(List<Frame> stack, ConfigNode node)
        {
            var parent = stack[stack.Count - 1];
            int dup = CountDuplicates(parent.Children, node.Name);
            parent.Children.Add(new ConfigNode(
                node.Name, node.Kind, node.Value, node.Items,
                node.SubNodes, node.Attributes, node.Condition, dup));
        }

        private static int CountDuplicates(List<ConfigNode> children, string name)
        {
            int dup = 0;
            for (int k = 0; k < children.Count; k++)
                if (children[k].Name == name)
                    dup++;
            return dup;
        }

        private static string? ConditionOf(string value)
        {
            return value.Length > 0 && value[0] == ':' ? value : null;
        }

        private static string[] SplitItems(string value)
        {
            return value.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        // ---- Frame (internal) ---------------------------------------------------

        private sealed class Frame
        {
            public Frame(string name, NodeKind kind, int indent, int openedAtLn,
                List<ConfigNode> children, bool pending)
            {
                Name = name; Kind = kind; Indent = indent; OpenedAtLn = openedAtLn;
                Children = children; Pending = pending;
            }

            public string Name { get; }
            public NodeKind Kind { get; set; }
            public int Indent { get; }
            public int OpenedAtLn { get; }
            public List<ConfigNode> Children { get; }
            public bool Pending { get; set; }
        }

        private struct Line
        {
            public Line(int indent, string content, int lineNo)
            {
                Indent = indent; Content = content; LineNo = lineNo;
            }
            public int Indent { get; }
            public string Content { get; }
            public int LineNo { get; }
        }
    }
}
