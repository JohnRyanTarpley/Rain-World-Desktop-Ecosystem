using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RWDE.Graphics
{
    /// <summary>
    /// A small JSON reader for atlas data, in place of Futile's <c>dictionaryFromJson</c>. Objects keep their
    /// key order, because an element's <see cref="FAtlasElement.indexInAtlas"/> is its position in the file.
    /// Numbers come back as double, strings as string, true/false as bool, null as null.
    /// </summary>
    internal static class AtlasJson
    {
        public sealed class JsonObject
        {
            private readonly Dictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);
            private readonly List<string> _keys = new List<string>();

            public IReadOnlyList<string> Keys => _keys;

            /// <exception cref="FutileException">The key is missing.</exception>
            public object Get(string key)
            {
                if (_values.TryGetValue(key, out var value)) return value;
                throw new FutileException("Atlas data is missing \"" + key + "\"");
            }

            internal void Set(string key, object value)
            {
                if (!_values.ContainsKey(key)) _keys.Add(key);
                _values[key] = value;
            }
        }

        /// <exception cref="FutileException">The text is not valid JSON.</exception>
        public static object Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            int i = 0;
            // A UTF-8 byte order mark may survive decoding.
            if (json.Length > 0 && json[0] == '﻿') i = 1;
            var value = ReadValue(json, ref i);
            SkipWhitespace(json, ref i);
            if (i != json.Length) throw Error(json, i, "trailing characters");
            return value;
        }

        private static object ReadValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw Error(s, i, "unexpected end");
            char c = s[i];
            switch (c)
            {
                case '{': return ReadObject(s, ref i);
                case '[': return ReadArray(s, ref i);
                case '"': return ReadString(s, ref i);
                case 't': return ReadLiteral(s, ref i, "true", true);
                case 'f': return ReadLiteral(s, ref i, "false", false);
                case 'n': return ReadLiteral(s, ref i, "null", null);
                default:
                    if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber(s, ref i);
                    throw Error(s, i, "unexpected '" + c + "'");
            }
        }

        private static JsonObject ReadObject(string s, ref int i)
        {
            var obj = new JsonObject();
            i++;
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}')
            {
                i++;
                return obj;
            }
            while (true)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != '"') throw Error(s, i, "expected a key");
                var key = ReadString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw Error(s, i, "expected ':'");
                i++;
                obj.Set(key, ReadValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    continue;
                }
                if (i < s.Length && s[i] == '}')
                {
                    i++;
                    return obj;
                }
                throw Error(s, i, "expected ',' or '}'");
            }
        }

        private static List<object> ReadArray(string s, ref int i)
        {
            var list = new List<object>();
            i++;
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']')
            {
                i++;
                return list;
            }
            while (true)
            {
                list.Add(ReadValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    continue;
                }
                if (i < s.Length && s[i] == ']')
                {
                    i++;
                    return list;
                }
                throw Error(s, i, "expected ',' or ']'");
            }
        }

        private static string ReadString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (i >= s.Length) break;
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
                        if (i + 4 > s.Length) throw Error(s, i, "bad \\u escape");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw Error(s, i - 1, "bad escape");
                }
            }
            throw Error(s, i, "unterminated string");
        }

        private static double ReadNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw Error(s, start, "bad number");
            return value;
        }

        private static object ReadLiteral(string s, ref int i, string literal, object value)
        {
            if (string.CompareOrdinal(s, i, literal, 0, literal.Length) != 0) throw Error(s, i, "unexpected token");
            i += literal.Length;
            return value;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static FutileException Error(string s, int i, string what) =>
            new FutileException("Invalid atlas JSON at character " + i + ": " + what);
    }
}
