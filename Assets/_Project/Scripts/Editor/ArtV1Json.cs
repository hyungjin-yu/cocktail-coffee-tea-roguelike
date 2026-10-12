using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CCT.Editor
{
    /// <summary>
    /// Art v1 manifest.json 전용 최소 JSON 파서. JsonUtility는 clips/anchorsByFrame 같은
    /// 이름→값 사전을 읽지 못해서 따로 둔다. 결과: 객체=Dictionary&lt;string, object&gt;,
    /// 배열=List&lt;object&gt;, 숫자=double, 문자열=string, true/false=bool, null=null.
    /// UnityEngine에 의존하지 않아 Unity 밖에서도 테스트할 수 있다.
    /// </summary>
    public static class ArtV1Json
    {
        public static object Parse(string text)
        {
            if (text == null) throw new FormatException("JSON 텍스트가 null이다");
            var p = new Parser(text);
            p.SkipWhitespace();
            var value = p.ReadValue();
            p.SkipWhitespace();
            if (!p.AtEnd) throw p.Error("JSON 값 뒤에 불필요한 문자가 있다");
            return value;
        }

        public static bool TryParse(string text, out object value, out string error)
        {
            try
            {
                value = Parse(text);
                error = null;
                return true;
            }
            catch (FormatException e)
            {
                value = null;
                error = e.Message;
                return false;
            }
        }

        class Parser
        {
            readonly string s;
            int i;

            public Parser(string text)
            {
                s = text;
                // UTF-8 BOM이 문자열로 남아 있으면 건너뛴다.
                if (s.Length > 0 && s[0] == '﻿') i = 1;
            }

            public bool AtEnd => i >= s.Length;

            public FormatException Error(string message) =>
                new FormatException($"{message} (위치 {i})");

            public void SkipWhitespace()
            {
                while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
            }

            public object ReadValue()
            {
                if (AtEnd) throw Error("JSON 값이 예상보다 일찍 끝났다");
                char c = s[i];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Error($"예상하지 못한 문자 '{c}'");
                }
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw Error($"'{word}'가 와야 한다");
                i += word.Length;
            }

            Dictionary<string, object> ReadObject()
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                i++; // {
                SkipWhitespace();
                if (!AtEnd && s[i] == '}') { i++; return result; }
                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || s[i] != '"') throw Error("객체 키는 문자열이어야 한다");
                    string key = ReadString();
                    SkipWhitespace();
                    if (AtEnd || s[i] != ':') throw Error("객체 키 뒤에 ':'가 와야 한다");
                    i++;
                    SkipWhitespace();
                    result[key] = ReadValue();
                    SkipWhitespace();
                    if (AtEnd) throw Error("객체가 닫히지 않았다");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return result; }
                    throw Error("객체 항목 사이에는 ',' 또는 '}'가 와야 한다");
                }
            }

            List<object> ReadArray()
            {
                var result = new List<object>();
                i++; // [
                SkipWhitespace();
                if (!AtEnd && s[i] == ']') { i++; return result; }
                while (true)
                {
                    SkipWhitespace();
                    result.Add(ReadValue());
                    SkipWhitespace();
                    if (AtEnd) throw Error("배열이 닫히지 않았다");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return result; }
                    throw Error("배열 항목 사이에는 ',' 또는 ']'가 와야 한다");
                }
            }

            string ReadString()
            {
                i++; // "
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Error("문자열이 닫히지 않았다");
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c < 0x20) throw Error("문자열 안에 제어 문자가 있다");
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Error("잘못된 이스케이프");
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
                            if (i + 4 > s.Length) throw Error("잘못된 \\u 이스케이프");
                            if (!int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                throw Error("잘못된 \\u 이스케이프");
                            sb.Append((char)code);
                            i += 4;
                            break;
                        default: throw Error($"알 수 없는 이스케이프 '\\{e}'");
                    }
                }
            }

            double ReadNumber()
            {
                int start = i;
                if (s[i] == '-') i++;
                while (!AtEnd && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-')) i++;
                string token = s.Substring(start, i - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                    throw Error($"숫자 형식이 잘못됐다: {token}");
                return v;
            }
        }
    }
}
