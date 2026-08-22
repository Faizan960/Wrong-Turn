using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WrongDirection.Leaderboards
{
    /// <summary>
    /// Compact JSON parser/serializer (MiniJSON-style, MIT). Used to read
    /// Firebase REST responses (arbitrary nested/typed JSON that Unity's
    /// JsonUtility cannot handle) and to build request bodies. Parse returns
    /// Dictionary&lt;string,object&gt; / List&lt;object&gt; / string / double /
    /// bool / null.
    /// </summary>
    public static class MiniJson
    {
        public static object Deserialize(string json)
        {
            if (json == null) return null;
            return Parser.Parse(json);
        }

        public static string Serialize(object obj)
        {
            var sb = new StringBuilder();
            Serializer.SerializeValue(obj, sb);
            return sb.ToString();
        }

        // ----- Convenience accessors (null-safe) -------------------------------
        public static Dictionary<string, object> AsDict(object o) =>
            o as Dictionary<string, object>;

        public static List<object> AsList(object o) => o as List<object>;

        public static string GetString(Dictionary<string, object> d, string k) =>
            d != null && d.TryGetValue(k, out var v) && v != null ? v.ToString() : null;

        public static long GetLong(Dictionary<string, object> d, string k)
        {
            if (d != null && d.TryGetValue(k, out var v) && v != null)
            {
                if (v is double dv) return (long)dv;
                if (long.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var lv))
                    return lv;
            }
            return 0;
        }

        public static bool GetBool(Dictionary<string, object> d, string k) =>
            d != null && d.TryGetValue(k, out var v) && v is bool b && b;

        // ---------------------------------------------------------------------
        private sealed class Parser
        {
            private readonly string _s;
            private int _i;

            private Parser(string s) { _s = s; }

            public static object Parse(string s)
            {
                var p = new Parser(s);
                return p.ParseValue();
            }

            private void SkipWhite()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            private object ParseValue()
            {
                SkipWhite();
                if (_i >= _s.Length) return null;
                char c = _s[_i];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    default: return ParseLiteral();
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var dict = new Dictionary<string, object>();
                _i++; // {
                while (true)
                {
                    SkipWhite();
                    if (_i >= _s.Length) break;
                    if (_s[_i] == '}') { _i++; break; }
                    string key = ParseString();
                    SkipWhite();
                    if (_i < _s.Length && _s[_i] == ':') _i++;
                    object val = ParseValue();
                    dict[key] = val;
                    SkipWhite();
                    if (_i < _s.Length && _s[_i] == ',') { _i++; continue; }
                    if (_i < _s.Length && _s[_i] == '}') { _i++; break; }
                }
                return dict;
            }

            private List<object> ParseArray()
            {
                var list = new List<object>();
                _i++; // [
                while (true)
                {
                    SkipWhite();
                    if (_i >= _s.Length) break;
                    if (_s[_i] == ']') { _i++; break; }
                    list.Add(ParseValue());
                    SkipWhite();
                    if (_i < _s.Length && _s[_i] == ',') { _i++; continue; }
                    if (_i < _s.Length && _s[_i] == ']') { _i++; break; }
                }
                return list;
            }

            private string ParseString()
            {
                SkipWhite();
                var sb = new StringBuilder();
                _i++; // opening quote
                while (_i < _s.Length)
                {
                    char c = _s[_i++];
                    if (c == '"') break;
                    if (c == '\\' && _i < _s.Length)
                    {
                        char e = _s[_i++];
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
                                if (_i + 4 <= _s.Length)
                                {
                                    var hex = _s.Substring(_i, 4);
                                    if (int.TryParse(hex, NumberStyles.HexNumber,
                                            CultureInfo.InvariantCulture, out var code))
                                        sb.Append((char)code);
                                    _i += 4;
                                }
                                break;
                            default: sb.Append(e); break;
                        }
                    }
                    else sb.Append(c);
                }
                return sb.ToString();
            }

            private object ParseLiteral()
            {
                int start = _i;
                while (_i < _s.Length && ",]}".IndexOf(_s[_i]) < 0 && !char.IsWhiteSpace(_s[_i]))
                    _i++;
                string tok = _s.Substring(start, _i - start);
                if (tok == "true") return true;
                if (tok == "false") return false;
                if (tok == "null") return null;
                if (double.TryParse(tok, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    return d;
                return tok;
            }
        }

        // ---------------------------------------------------------------------
        private static class Serializer
        {
            public static void SerializeValue(object value, StringBuilder sb)
            {
                switch (value)
                {
                    case null: sb.Append("null"); break;
                    case string s: SerializeString(s, sb); break;
                    case bool b: sb.Append(b ? "true" : "false"); break;
                    case IDictionary dict: SerializeObject(dict, sb); break;
                    case IList list: SerializeArray(list, sb); break;
                    case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
                    case double dd: sb.Append(dd.ToString("R", CultureInfo.InvariantCulture)); break;
                    default: sb.Append(System.Convert.ToString(value, CultureInfo.InvariantCulture)); break;
                }
            }

            private static void SerializeObject(IDictionary obj, StringBuilder sb)
            {
                sb.Append('{');
                bool first = true;
                foreach (var key in obj.Keys)
                {
                    if (!first) sb.Append(',');
                    SerializeString(key.ToString(), sb);
                    sb.Append(':');
                    SerializeValue(obj[key], sb);
                    first = false;
                }
                sb.Append('}');
            }

            private static void SerializeArray(IList array, StringBuilder sb)
            {
                sb.Append('[');
                for (int i = 0; i < array.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    SerializeValue(array[i], sb);
                }
                sb.Append(']');
            }

            private static void SerializeString(string str, StringBuilder sb)
            {
                sb.Append('"');
                foreach (char c in str)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < ' ')
                                sb.Append("\\u").Append(((int)c).ToString("x4"));
                            else sb.Append(c);
                            break;
                    }
                }
                sb.Append('"');
            }
        }
    }
}
