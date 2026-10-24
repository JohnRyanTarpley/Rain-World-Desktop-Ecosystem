using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RWDE.GameAssets.Install
{
    /// <summary>A node in a Valve KeyValues (VDF) text document: either a string value or a child block.</summary>
    public sealed class VdfNode
    {
        private readonly List<KeyValuePair<string, VdfNode>> _children = new List<KeyValuePair<string, VdfNode>>();

        public VdfNode(string value = null)
        {
            Value = value;
        }

        /// <summary>The string value, or null for a block.</summary>
        public string Value { get; }

        public IReadOnlyList<KeyValuePair<string, VdfNode>> Children => _children;

        internal void Add(string key, VdfNode node) => _children.Add(new KeyValuePair<string, VdfNode>(key, node));

        /// <summary>First child with this key (keys compare case-insensitively, as Steam does), or null.</summary>
        public VdfNode this[string key]
        {
            get
            {
                foreach (var child in _children)
                    if (string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase))
                        return child.Value;
                return null;
            }
        }
    }

    /// <summary>Parses the text VDF format used by Steam's <c>libraryfolders.vdf</c>.</summary>
    public static class VdfParser
    {
        public static VdfNode Parse(string text)
        {
            var root = new VdfNode();
            var stack = new Stack<VdfNode>();
            stack.Push(root);
            var position = 0;
            string pendingKey = null;

            while (true)
            {
                var token = NextToken(text, ref position, out var quoted);
                if (token == null) break;

                if (!quoted && token == "{")
                {
                    if (pendingKey == null) throw new InvalidDataException("VDF block without a key");
                    var block = new VdfNode();
                    stack.Peek().Add(pendingKey, block);
                    stack.Push(block);
                    pendingKey = null;
                }
                else if (!quoted && token == "}")
                {
                    if (stack.Count == 1) throw new InvalidDataException("Unbalanced '}' in VDF");
                    stack.Pop();
                }
                else if (pendingKey == null)
                {
                    pendingKey = token;
                }
                else
                {
                    stack.Peek().Add(pendingKey, new VdfNode(token));
                    pendingKey = null;
                }
            }

            if (stack.Count != 1) throw new InvalidDataException("Unclosed block in VDF");
            return root;
        }

        private static string NextToken(string text, ref int position, out bool quoted)
        {
            quoted = false;
            while (position < text.Length)
            {
                var c = text[position];
                if (char.IsWhiteSpace(c))
                {
                    position++;
                }
                else if (c == '/' && position + 1 < text.Length && text[position + 1] == '/')
                {
                    while (position < text.Length && text[position] != '\n') position++;
                }
                else
                {
                    break;
                }
            }
            if (position >= text.Length) return null;

            var first = text[position];
            if (first == '{' || first == '}')
            {
                position++;
                return first.ToString();
            }

            var builder = new StringBuilder();
            if (first == '"')
            {
                quoted = true;
                position++;
                while (position < text.Length && text[position] != '"')
                {
                    var c = text[position++];
                    if (c == '\\' && position < text.Length)
                    {
                        var e = text[position++];
                        switch (e)
                        {
                            case 'n': builder.Append('\n'); break;
                            case 't': builder.Append('\t'); break;
                            default: builder.Append(e); break;
                        }
                    }
                    else
                    {
                        builder.Append(c);
                    }
                }
                if (position >= text.Length) throw new InvalidDataException("Unterminated string in VDF");
                position++;
                return builder.ToString();
            }

            while (position < text.Length && !char.IsWhiteSpace(text[position]) && text[position] != '{' &&
                   text[position] != '}' && text[position] != '"')
                builder.Append(text[position++]);
            return builder.ToString();
        }
    }
}
