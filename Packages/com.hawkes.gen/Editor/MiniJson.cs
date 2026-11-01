// A small JSON reader/writer for Hawkes Gen's API (objects become
// Dictionary<string, object>, arrays List<object>, numbers double/long), so the
// package needs no Newtonsoft or other dependency.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HawkesGen
{
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var p = new Parser(json);
            var v = p.Value();
            return v;
        }

        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value);
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string s: WriteString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case IDictionary d:
                    sb.Append('{');
                    var first = true;
                    foreach (DictionaryEntry e in d)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteString(sb, e.Key.ToString());
                        sb.Append(':');
                        WriteValue(sb, e.Value);
                    }
                    sb.Append('}');
                    break;
                case IEnumerable list:
                    sb.Append('[');
                    var f = true;
                    foreach (var item in list)
                    {
                        if (!f) sb.Append(',');
                        f = false;
                        WriteValue(sb, item);
                    }
                    sb.Append(']');
                    break;
                case float fl: sb.Append(fl.ToString("R", CultureInfo.InvariantCulture)); break;
                case double db: sb.Append(db.ToString("R", CultureInfo.InvariantCulture)); break;
                case IFormattable n: sb.Append(n.ToString(null, CultureInfo.InvariantCulture)); break;
                default: WriteString(sb, v.ToString()); break;
            }
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        sealed class Parser
        {
            readonly string s;
            int i;
            public Parser(string s) { this.s = s; }

            void Ws() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

            public object Value()
            {
                Ws();
                if (i >= s.Length) return null;
                var c = s[i];
                if (c == '{') return Obj();
                if (c == '[') return Arr();
                if (c == '"') return Str();
                if (c == 't' && s.Substring(i).StartsWith("true")) { i += 4; return true; }
                if (c == 'f' && s.Substring(i).StartsWith("false")) { i += 5; return false; }
                if (c == 'n' && s.Substring(i).StartsWith("null")) { i += 4; return null; }
                return Num();
            }

            Dictionary<string, object> Obj()
            {
                var d = new Dictionary<string, object>();
                i++;
                Ws();
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws();
                    var k = Str();
                    Ws();
                    i++; // :
                    d[k] = Value();
                    Ws();
                    if (s[i] == ',') { i++; continue; }
                    i++; // }
                    return d;
                }
            }

            List<object> Arr()
            {
                var l = new List<object>();
                i++;
                Ws();
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value());
                    Ws();
                    if (s[i] == ',') { i++; continue; }
                    i++; // ]
                    return l;
                }
            }

            string Str()
            {
                var sb = new StringBuilder();
                i++;
                while (s[i] != '"')
                {
                    var c = s[i++];
                    if (c != '\\') { sb.Append(c); continue; }
                    var e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                        default: sb.Append(e); break;
                    }
                }
                i++;
                return sb.ToString();
            }

            object Num()
            {
                var start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                var t = s.Substring(start, i - start);
                if (t.IndexOfAny(new[] { '.', 'e', 'E' }) < 0 && long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
                return double.Parse(t, CultureInfo.InvariantCulture);
            }
        }
    }
}
