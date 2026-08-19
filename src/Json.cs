using System;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;

namespace OpenInSheets
{
    /// <summary>
    /// Thin helpers over JavaScriptSerializer. Google's replies are small, but the
    /// Apps Script request body carries a base64 CSV, so the length caps are lifted.
    /// </summary>
    static class Json
    {
        static JavaScriptSerializer Serializer()
        {
            JavaScriptSerializer s = new JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            return s;
        }

        public static Dictionary<string, object> Parse(string text)
        {
            return Serializer().Deserialize<Dictionary<string, object>>(text);
        }

        public static string Serialize(object value)
        {
            return Serializer().Serialize(value);
        }

        public static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v != null) return v.ToString();
            return null;
        }

        public static Dictionary<string, object> Obj(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return v as Dictionary<string, object>;
            return null;
        }

        public static string Escape(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder(s.Length + 16);
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
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
