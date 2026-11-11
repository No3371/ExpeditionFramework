using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ExpeditionFramework.Core
{
    /// <summary>
    /// Reads JSON into dictionaries, lists, strings, doubles and bools. Allows // comments and trailing commas, since
    /// people write these files by hand.
    /// </summary>
    public sealed class Json
    {
        private readonly string text;
        private int at;

        private Json(string text) { this.text = text; }

        public static object Parse(string text)
        {
            var reader = new Json(text);
            object value = reader.ReadValue();
            reader.SkipSpace();
            if (reader.at < text.Length) throw reader.Fail("unexpected text after the end");
            return value;
        }

        private FormatException Fail(string message)
        {
            int line = 1;
            for (int i = 0; i < at && i < text.Length; i++) if (text[i] == '\n') line++;
            return new FormatException("line " + line + ": " + message);
        }

        private void SkipSpace()
        {
            while (at < text.Length)
            {
                if (char.IsWhiteSpace(text[at]) || text[at] == '﻿') at++;
                else if (at + 1 < text.Length && text[at] == '/' && text[at + 1] == '/')
                {
                    while (at < text.Length && text[at] != '\n') at++;
                }
                else break;
            }
        }

        private object ReadValue()
        {
            SkipSpace();
            if (at >= text.Length) throw Fail("unexpected end of file");
            char c = text[at];
            if (c == '{') return ReadObject();
            if (c == '[') return ReadList();
            if (c == '"') return ReadString();
            if (Match("true")) return true;
            if (Match("false")) return false;
            if (Match("null")) return null;
            return ReadNumber();
        }

        private bool Match(string word)
        {
            if (string.CompareOrdinal(text, at, word, 0, word.Length) != 0) return false;
            at += word.Length;
            return true;
        }

        private Dictionary<string, object> ReadObject()
        {
            var result = new Dictionary<string, object>();
            at++;
            while (true)
            {
                SkipSpace();
                if (at >= text.Length) throw Fail("unclosed {");
                if (text[at] == '}') { at++; return result; }
                if (text[at] != '"') throw Fail("expected a quoted key");
                string key = ReadString();
                if (result.ContainsKey(key)) throw Fail("duplicate key \"" + key + "\"");
                SkipSpace();
                if (at >= text.Length || text[at] != ':') throw Fail("expected : after \"" + key + "\"");
                at++;
                result[key] = ReadValue();
                SkipSpace();
                if (at < text.Length && text[at] == ',') at++;
                else if (at < text.Length && text[at] != '}') throw Fail("expected , or }");
            }
        }

        private List<object> ReadList()
        {
            var result = new List<object>();
            at++;
            while (true)
            {
                SkipSpace();
                if (at >= text.Length) throw Fail("unclosed [");
                if (text[at] == ']') { at++; return result; }
                result.Add(ReadValue());
                SkipSpace();
                if (at < text.Length && text[at] == ',') at++;
                else if (at < text.Length && text[at] != ']') throw Fail("expected , or ]");
            }
        }

        private string ReadString()
        {
            var result = new StringBuilder();
            at++;
            while (true)
            {
                if (at >= text.Length) throw Fail("unclosed string");
                char c = text[at++];
                if (c == '"') return result.ToString();
                if (c != '\\') { result.Append(c); continue; }
                if (at >= text.Length) throw Fail("unclosed string");
                char e = text[at++];
                switch (e)
                {
                    case 'n': result.Append('\n'); break;
                    case 't': result.Append('\t'); break;
                    case 'r': result.Append('\r'); break;
                    case 'u':
                        if (at + 4 > text.Length) throw Fail("bad \\u escape");
                        result.Append((char)int.Parse(text.Substring(at, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        at += 4;
                        break;
                    default: result.Append(e); break;
                }
            }
        }

        private object ReadNumber()
        {
            int start = at;
            while (at < text.Length && "+-0123456789.eE".IndexOf(text[at]) >= 0) at++;
            if (start == at || !double.TryParse(text.Substring(start, at - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
            {
                throw Fail("expected a value");
            }
            return number;
        }
    }
}
