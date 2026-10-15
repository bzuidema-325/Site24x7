using System;
using System.Collections;
using System.Globalization;
using System.Management.Automation;
using System.Security;
using System.Text;

namespace Site24x7
{
    /// <summary>
    /// A small, dependency-free JSON reader and writer.
    /// The module ships without Newtonsoft.Json or System.Text.Json so it never conflicts with the
    /// versions Windows PowerShell 5.1 or PowerShell 7 already have loaded.
    /// Objects are read as PSCustomObject so results behave exactly like ConvertFrom-Json output.
    /// </summary>
    internal static class Json
    {
        #region Writer

        /// <summary>The deepest nesting the reader and writer accept. Deeper input would exhaust the stack.</summary>
        public const int MaxDepth = 100;

        /// <param name="value">A hashtable, PSCustomObject, array or scalar. A SecureString is sent as its plain text.</param>
        /// <param name="redactSecrets">
        /// For logging: replace secrets with "***". That covers fields named like passwords, secrets,
        /// tokens and keys, the value of a {name, value} pair naming a sensitive header, and SecureStrings.
        /// </param>
        public static string Serialize(object value, bool redactSecrets = false)
        {
            var sb = new StringBuilder();
            Write(sb, value, 0, redactSecrets);
            return sb.ToString();
        }

        /// <summary>
        /// JSON text with its secrets replaced, for logging. Text that is not JSON is not shown,
        /// because there is no telling what it contains.
        /// </summary>
        public static string Redact(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;
            try
            {
                return Serialize(Parse(text), redactSecrets: true);
            }
            catch (FormatException)
            {
                return $"<{text.Length} characters that are not JSON, not shown>";
            }
        }

        internal static bool IsSecretField(string name)
        {
            var n = name.ToLowerInvariant();
            return n.Contains("password") || n.Contains("passphrase") || n.Contains("secret") || n.Contains("token") ||
                   n.Contains("credential") || n.Contains("authorization") || n == "auth_pass" ||
                   n == "key" || n.EndsWith("_key", StringComparison.Ordinal) || n.EndsWith("apikey", StringComparison.Ordinal);
        }

        /// <summary>Header names whose values are credentials, as sent in custom_headers and similar {name, value} lists.</summary>
        internal static bool IsSecretHeader(string name)
        {
            var n = name.Trim().ToLowerInvariant().Replace('_', '-');
            return n == "cookie" || n == "set-cookie" || n.Contains("authorization") || n.Contains("api-key") || n.Contains("apikey") ||
                   IsSecretField(n);
        }

        private static void WriteMember(StringBuilder sb, string name, object value, int depth, bool redact, bool secretPair)
        {
            WriteString(sb, name);
            sb.Append(':');
            bool hide = redact && value != null &&
                (IsSecretField(name) || (secretPair && string.Equals(name, "value", StringComparison.OrdinalIgnoreCase)));
            if (hide) WriteString(sb, "***");
            else Write(sb, value, depth + 1, redact);
        }

        /// <summary>True when an object is a {name, value} pair whose name is a sensitive header.</summary>
        private static bool IsSecretPair(object nameValue) =>
            nameValue is string headerName && IsSecretHeader(headerName);

        private static object Unwrap(object value) => value is PSObject pso && !(pso.BaseObject is PSCustomObject) ? pso.BaseObject : value;

        private static void Write(StringBuilder sb, object value, int depth, bool redact)
        {
            if (depth > MaxDepth)
            {
                throw new Site24x7ArgumentException($"The object is nested more than {MaxDepth} levels deep and cannot be converted to JSON.");
            }

            if (value is PSObject pso)
            {
                if (pso.BaseObject is PSCustomObject)
                {
                    sb.Append('{');
                    bool first = true;
                    bool secretPair = redact && IsSecretPair(Unwrap(pso.Properties["name"]?.Value));
                    foreach (var prop in pso.Properties)
                    {
                        if (!(prop is PSNoteProperty) && !(prop is PSProperty)) continue;
                        if (!first) sb.Append(',');
                        first = false;
                        WriteMember(sb, prop.Name, prop.Value, depth, redact, secretPair);
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
                case SecureString secure:
                    WriteString(sb, redact ? "***" : Secrets.ToPlain(secure));
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
                    WriteNumber(sb, f);
                    return;
                case double d:
                    WriteNumber(sb, d);
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
                    bool secretPair = false;
                    if (redact)
                    {
                        foreach (DictionaryEntry entry in dict)
                        {
                            if (string.Equals(Convert.ToString(entry.Key, CultureInfo.InvariantCulture), "name", StringComparison.OrdinalIgnoreCase))
                            {
                                secretPair = IsSecretPair(Unwrap(entry.Value));
                            }
                        }
                    }
                    foreach (DictionaryEntry entry in dict)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteMember(sb, Convert.ToString(entry.Key, CultureInfo.InvariantCulture), entry.Value, depth, redact, secretPair);
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
                        Write(sb, item, depth + 1, redact);
                    }
                    sb.Append(']');
                    return;
                }
                default:
                    WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
            }
        }

        private static void WriteNumber(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                throw new Site24x7ArgumentException($"{d} cannot be sent as JSON.");
            }
            sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
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
            var result = ReadValue(text, ref pos, 0);
            SkipWhitespace(text, ref pos);
            if (pos != text.Length)
            {
                throw new FormatException($"Unexpected character '{text[pos]}' at position {pos} in JSON.");
            }
            return result;
        }

        private static object ReadValue(string t, ref int p, int depth)
        {
            if (depth > MaxDepth) throw new FormatException($"JSON is nested more than {MaxDepth} levels deep.");
            SkipWhitespace(t, ref p);
            if (p >= t.Length) throw new FormatException("Unexpected end of JSON.");
            char c = t[p];
            switch (c)
            {
                case '{': return ReadObject(t, ref p, depth);
                case '[': return ReadArray(t, ref p, depth);
                case '"': return ReadString(t, ref p);
                case 't': Expect(t, ref p, "true"); return true;
                case 'f': Expect(t, ref p, "false"); return false;
                case 'n': Expect(t, ref p, "null"); return null;
                default:
                    if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber(t, ref p);
                    throw new FormatException($"Unexpected character '{c}' at position {p} in JSON.");
            }
        }

        private static PSObject ReadObject(string t, ref int p, int depth)
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
                object value = ReadValue(t, ref p, depth + 1);
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

        private static object[] ReadArray(string t, ref int p, int depth)
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
                list.Add(ReadValue(t, ref p, depth + 1));
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
            // .NET Framework throws on numbers beyond double's range, where .NET Core returns infinity.
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && !double.IsInfinity(d)) return d;
            throw new FormatException($"The number {s} at position {start} in JSON is out of range.");
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
