using System;
using System.Collections;
using System.Globalization;
using System.Management.Automation;
using System.Text;

namespace Site24x7
{
    /// <summary>
    /// A small, dependency-free JSON reader and writer.
    /// The module ships without Newtonsoft.Json or System.Text.Json so it never conflicts with the
    /// versions Windows PowerShell 5.1 or PowerShell 7 already have loaded.
    /// Objects are read as PSCustomObject so results behave exactly like ConvertFrom-Json output.
    /// </summary>
    public static class Json
    {
        #region Writer

        public static string Serialize(object value)
        {
            var sb = new StringBuilder();
            Write(sb, value, 0);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object value, int depth)
        {
            if (depth > 100)
            {
                throw new InvalidOperationException("The object is nested more than 100 levels deep and cannot be converted to JSON.");
            }

            if (value is PSObject pso)
            {
                if (pso.BaseObject is PSCustomObject)
                {
                    sb.Append('{');
                    bool first = true;
                    foreach (var prop in pso.Properties)
                    {
                        if (!(prop is PSNoteProperty) && !(prop is PSProperty)) continue;
                        if (!first) sb.Append(',');
                        first = false;
                        WriteString(sb, prop.Name);
                        sb.Append(':');
                        Write(sb, prop.Value, depth + 1);
                    }
                    sb.Append('}');
                    return;
                }
                value = pso.BaseObject;
            }

            switch (value)
            {
                case null:
                    sb.Append("null");
                    return;
                case string s:
                    WriteString(sb, s);
                    return;
                case char c:
                    WriteString(sb, c.ToString());
                    return;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    return;
                case Enum e:
                    sb.Append(Convert.ToInt64(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                    return;
                case DateTime dt:
                    WriteString(sb, dt.ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture));
                    return;
                case DateTimeOffset dto:
                    WriteString(sb, dto.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture));
                    return;
                case float f:
                    sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                    return;
                case double d:
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    return;
                case decimal m:
                    sb.Append(m.ToString(CultureInfo.InvariantCulture));
                    return;
                case sbyte _:
                case byte _:
                case short _:
                case ushort _:
                case int _:
                case uint _:
                case long _:
                case ulong _:
                    sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
                case IDictionary dict:
                {
                    sb.Append('{');
                    bool first = true;
                    foreach (DictionaryEntry entry in dict)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteString(sb, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
                        sb.Append(':');
                        Write(sb, entry.Value, depth + 1);
                    }
                    sb.Append('}');
                    return;
                }
                case IEnumerable list:
                {
                    sb.Append('[');
                    bool first = true;
                    foreach (var item in list)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        Write(sb, item, depth + 1);
                    }
                    sb.Append(']');
                    return;
                }
                default:
                    WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
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
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }

        #endregion

        #region Reader

        /// <summary>Parses JSON text. Objects become PSCustomObject, arrays become object[].</summary>
        public static object Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            int pos = 0;
            var result = ReadValue(text, ref pos);
            SkipWhitespace(text, ref pos);
            if (pos != text.Length)
            {
                throw new FormatException($"Unexpected character '{text[pos]}' at position {pos} in JSON.");
            }
            return result;
        }

        private static object ReadValue(string t, ref int p)
        {
            SkipWhitespace(t, ref p);
            if (p >= t.Length) throw new FormatException("Unexpected end of JSON.");
            char c = t[p];
            switch (c)
            {
                case '{': return ReadObject(t, ref p);
                case '[': return ReadArray(t, ref p);
                case '"': return ReadString(t, ref p);
                case 't': Expect(t, ref p, "true"); return true;
                case 'f': Expect(t, ref p, "false"); return false;
                case 'n': Expect(t, ref p, "null"); return null;
                default:
                    if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber(t, ref p);
                    throw new FormatException($"Unexpected character '{c}' at position {p} in JSON.");
            }
        }

        private static PSObject ReadObject(string t, ref int p)
        {
            var obj = new PSObject();
            p++; // {
            SkipWhitespace(t, ref p);
            if (p < t.Length && t[p] == '}')
            {
                p++;
                return obj;
            }
            while (true)
            {
                SkipWhitespace(t, ref p);
                if (p >= t.Length || t[p] != '"') throw new FormatException($"Expected a property name at position {p} in JSON.");
                string name = ReadString(t, ref p);
                SkipWhitespace(t, ref p);
                if (p >= t.Length || t[p] != ':') throw new FormatException($"Expected ':' at position {p} in JSON.");
                p++;
                object value = ReadValue(t, ref p);
                // Later duplicates win, matching ConvertFrom-Json in Windows PowerShell.
                if (obj.Properties[name] != null) obj.Properties.Remove(name);
                obj.Properties.Add(new PSNoteProperty(name, value));
                SkipWhitespace(t, ref p);
                if (p >= t.Length) throw new FormatException("Unexpected end of JSON.");
                if (t[p] == ',') { p++; continue; }
                if (t[p] == '}') { p++; return obj; }
                throw new FormatException($"Expected ',' or '}}' at position {p} in JSON.");
            }
        }

        private static object[] ReadArray(string t, ref int p)
        {
            var list = new ArrayList();
            p++; // [
            SkipWhitespace(t, ref p);
            if (p < t.Length && t[p] == ']')
            {
                p++;
                return new object[0];
            }
            while (true)
            {
                list.Add(ReadValue(t, ref p));
                SkipWhitespace(t, ref p);
                if (p >= t.Length) throw new FormatException("Unexpected end of JSON.");
                if (t[p] == ',') { p++; continue; }
                if (t[p] == ']') { p++; return list.ToArray(); }
                throw new FormatException($"Expected ',' or ']' at position {p} in JSON.");
            }
        }

        private static string ReadString(string t, ref int p)
        {
            var sb = new StringBuilder();
            p++; // opening quote
            while (p < t.Length)
            {
                char c = t[p++];
                if (c == '"') return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (p >= t.Length) break;
                char esc = t[p++];
                switch (esc)
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
                        if (p + 4 > t.Length) throw new FormatException("Invalid unicode escape in JSON.");
                        sb.Append((char)int.Parse(t.Substring(p, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        p += 4;
                        break;
                    default:
                        throw new FormatException($"Invalid escape '\\{esc}' in JSON.");
                }
            }
            throw new FormatException("Unterminated string in JSON.");
        }

        private static object ReadNumber(string t, ref int p)
        {
            int start = p;
            if (t[p] == '-') p++;
            while (p < t.Length && char.IsDigit(t[p])) p++;
            bool isInteger = true;
            if (p < t.Length && t[p] == '.')
            {
                isInteger = false;
                p++;
                while (p < t.Length && char.IsDigit(t[p])) p++;
            }
            if (p < t.Length && (t[p] == 'e' || t[p] == 'E'))
            {
                isInteger = false;
                p++;
                if (p < t.Length && (t[p] == '+' || t[p] == '-')) p++;
                while (p < t.Length && char.IsDigit(t[p])) p++;
            }
            string s = t.Substring(start, p - start);
            if (isInteger)
            {
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) return i;
                if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) return l;
                if (decimal.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out decimal big)) return big;
            }
            return double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static void Expect(string t, ref int p, string word)
        {
            if (string.CompareOrdinal(t, p, word, 0, word.Length) != 0)
            {
                throw new FormatException($"Unexpected token at position {p} in JSON.");
            }
            p += word.Length;
        }

        private static void SkipWhitespace(string t, ref int p)
        {
            while (p < t.Length && char.IsWhiteSpace(t[p])) p++;
        }

        #endregion

        /// <summary>Reads a property from a parsed JSON object, or null if it is missing.</summary>
        public static object Get(object obj, string name)
        {
            if (obj is PSObject pso) return pso.Properties[name]?.Value;
            if (obj is IDictionary d) return d.Contains(name) ? d[name] : null;
            return null;
        }
    }
}
