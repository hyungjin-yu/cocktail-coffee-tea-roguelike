using System;
using System.Collections.Generic;
using System.Text;

namespace CCT
{
    /// <summary>
    /// 69 레시피 공유 코드(서버·계정 없음, 복사/붙여넣기). 형식: "CCT1-" + Crockford Base32(바이트) (5자마다 '-').
    /// 바이트: [버전 1][종류][기법][(커피만) 추출 도구][얼음][물 u16][재료 수 n][(재료 색인 u8, 양 u16)×n][이름 길이 u8][이름 UTF-8][CRC32 4바이트].
    /// 추출 도구 바이트는 종류가 커피(3)일 때만 있다 — 기존 칵테일·차 코드는 그대로 읽힌다.
    /// 버프·색은 넣지 않는다(가져온 쪽의 현재 밸런스로 재계산). 코드는 데이터로만 해석하며 명령·경로·실행 코드로 쓰지 않는다.
    /// </summary>
    public static class DrinkShareCode
    {
        public const string Prefix = "CCT1-";
        public const int Version = 1;
        /// <summary>최대 코드 길이(접두어·구분자 포함). 최대 바이트 = 헤더 8(커피) + 재료 8×3 + 이름 1+48 + CRC 4 = 85 → Base32 136자 + '-' 27 + "CCT1-" 5 = 168.</summary>
        public const int MaxCodeLength = 170;
        /// <summary>붙여넣기 입력 허용 길이(공백·줄바꿈 포함). 넘으면 해석 전에 거부.</summary>
        public const int MaxPasteLength = 400;
        const int MaxSteps = 8;
        const int MaxNameBytes = 48;
        const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        public static string Encode(DrinkRecipe r)
        {
            var bytes = new List<byte> { Version, (byte)r.kind, (byte)r.technique };
            if (r.kind == (int)DrinkKind.Coffee) bytes.Add((byte)Math.Max(0, Math.Min(255, (int)r.coffeeMethod)));
            bytes.Add((byte)Math.Max(0, Math.Min(255, r.ice)));
            U16(bytes, r.water);
            bytes.Add((byte)r.steps.Count);
            foreach (var s in r.steps)
            {
                int idx = Array.IndexOf(DrinkIngredients.ShareIndex, s.id);
                bytes.Add((byte)(idx < 0 ? 255 : idx));
                U16(bytes, s.amount);
            }
            var name = Encoding.UTF8.GetBytes(DrinkValidator.SanitizeName(r.name, "") ?? "");
            int nameLen = Math.Min(name.Length, MaxNameBytes);
            // UTF-8 문자 중간에서 자르지 않는다.
            while (nameLen > 0 && nameLen < name.Length && (name[nameLen] & 0xC0) == 0x80) nameLen--;
            bytes.Add((byte)nameLen);
            for (int i = 0; i < nameLen; i++) bytes.Add(name[i]);
            uint crc = Crc32(bytes, bytes.Count);
            bytes.Add((byte)(crc >> 24)); bytes.Add((byte)(crc >> 16)); bytes.Add((byte)(crc >> 8)); bytes.Add((byte)crc);
            var body = Base32(bytes);
            var sb = new StringBuilder(Prefix);
            for (int i = 0; i < body.Length; i++)
            {
                if (i > 0 && i % 5 == 0) sb.Append('-');
                sb.Append(body[i]);
            }
            return sb.ToString();
        }

        /// <summary>코드 해석 + 배합 검증. 실패면 null과 사용자용 한국어 오류. 성공해도 창고·버프·레시피 목록은 바꾸지 않는다.</summary>
        public static DrinkRecipe Decode(string code, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(code)) { error = "코드를 붙여 넣어 주세요."; return null; }
            if (code.Length > MaxPasteLength) { error = "코드가 너무 깁니다."; return null; }
            // 붙여넣기 중 섞인 공백·줄바꿈·탭은 무시(코드 문자로 쓰지 않음).
            var compact = new StringBuilder();
            foreach (char ch in code) if (!char.IsWhiteSpace(ch)) compact.Append(ch);
            var trimmed = compact.ToString();
            if (trimmed.Length > MaxCodeLength) { error = "코드가 너무 깁니다."; return null; }
            if (!trimmed.StartsWith("CCT", StringComparison.OrdinalIgnoreCase)) { error = "레시피 코드가 아닙니다(CCT로 시작해야 합니다)."; return null; }
            int dash = trimmed.IndexOf('-');
            if (dash < 0) { error = "코드 형식이 올바르지 않습니다."; return null; }
            var ver = trimmed.Substring(3, dash - 3);
            if (ver != "1") { error = ver.Length > 0 && IsDigits(ver) ? "지원하지 않는 코드 버전입니다. 게임을 업데이트해 주세요." : "코드 형식이 올바르지 않습니다."; return null; }
            var sb = new StringBuilder();
            for (int i = dash + 1; i < trimmed.Length; i++)
            {
                char c = char.ToUpperInvariant(trimmed[i]);
                if (c == '-' || c == ' ') continue;
                if (c == 'O') c = '0';
                if (c == 'I' || c == 'L') c = '1';
                if (Alphabet.IndexOf(c) < 0) { error = $"코드에 쓸 수 없는 문자가 있습니다: '{Printable(trimmed[i])}'"; return null; }
                sb.Append(c);
            }
            var bytes = FromBase32(sb.ToString());
            if (bytes == null || bytes.Count < 12) { error = "코드가 잘렸거나 손상되었습니다."; return null; }
            uint stored = (uint)(bytes[bytes.Count - 4] << 24 | bytes[bytes.Count - 3] << 16 | bytes[bytes.Count - 2] << 8 | bytes[bytes.Count - 1]);
            if (Crc32(bytes, bytes.Count - 4) != stored) { error = "검사값이 맞지 않습니다. 코드를 다시 복사해 주세요."; return null; }
            int p = 0;
            int version = bytes[p++];
            if (version != Version) { error = "지원하지 않는 코드 버전입니다."; return null; }
            var r = new DrinkRecipe { kind = bytes[p++], technique = bytes[p++] };
            if (r.kind == (int)DrinkKind.Coffee) r.coffeeMethod = (CoffeeBrewMethod)bytes[p++];
            r.ice = bytes[p++];
            r.water = bytes[p] << 8 | bytes[p + 1]; p += 2;
            int n = bytes[p++];
            if (n > MaxSteps) { error = "재료 수가 너무 많습니다."; return null; }
            if (p + n * 3 + 1 > bytes.Count - 4) { error = "코드가 잘렸거나 손상되었습니다."; return null; }
            for (int i = 0; i < n; i++)
            {
                int idx = bytes[p++];
                int amt = bytes[p] << 8 | bytes[p + 1]; p += 2;
                if (idx >= DrinkIngredients.ShareIndex.Length) { error = "알 수 없는 재료가 들어 있습니다."; return null; }
                r.steps.Add(new DrinkStep(DrinkIngredients.ShareIndex[idx], amt));
            }
            int nameLen = bytes[p++];
            if (nameLen > MaxNameBytes || p + nameLen != bytes.Count - 4) { error = "코드가 잘렸거나 손상되었습니다."; return null; }
            string name;
            try { name = new UTF8Encoding(false, true).GetString(bytes.GetRange(p, nameLen).ToArray()); }
            catch (Exception) { error = "레시피 이름이 손상되었습니다."; return null; }
            r.name = DrinkValidator.SanitizeName(name, "가져온 레시피");
            if (r.kind != (int)DrinkKind.Cocktail && r.kind != (int)DrinkKind.Tea && r.kind != (int)DrinkKind.Coffee) { error = "알 수 없는 음료 종류입니다."; return null; }
            if (!Enum.IsDefined(typeof(DrinkTechnique), r.technique) || !DrinkRules.AllowedTechnique(r.Kind, r.Technique))
            { error = "허용되지 않는 제조 기법입니다."; return null; }
            var check = DrinkValidator.Check(r);
            if (!check.Ok) { error = check.First; return null; }
            return r;
        }

        static bool IsDigits(string s)
        {
            foreach (char c in s) if (c < '0' || c > '9') return false;
            return true;
        }

        static string Printable(char c) => char.IsControl(c) ? "제어 문자" : c.ToString();

        static void U16(List<byte> b, int v)
        {
            v = Math.Max(0, Math.Min(65535, v));
            b.Add((byte)(v >> 8));
            b.Add((byte)v);
        }

        static string Base32(List<byte> data)
        {
            var sb = new StringBuilder();
            int buffer = 0, bits = 0;
            foreach (var b in data)
            {
                buffer = (buffer << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    sb.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                    bits -= 5;
                }
            }
            if (bits > 0) sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
            return sb.ToString();
        }

        static List<byte> FromBase32(string s)
        {
            var result = new List<byte>();
            int buffer = 0, bits = 0;
            foreach (char c in s)
            {
                int v = Alphabet.IndexOf(c);
                if (v < 0) return null;
                buffer = ((buffer << 5) | v) & 0xFFFF;
                bits += 5;
                if (bits >= 8)
                {
                    result.Add((byte)(buffer >> (bits - 8)));
                    bits -= 8;
                }
            }
            // 정규 형식만: 남은 비트는 5 미만이고 모두 0(끝 문자 패딩 비트 변조·여분 문자 거부).
            if (bits >= 5 || (buffer & ((1 << bits) - 1)) != 0) return null;
            return result;
        }

        static uint[] table;

        static uint Crc32(List<byte> data, int count)
        {
            if (table == null)
            {
                table = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    uint c = i;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    table[i] = c;
                }
            }
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < count; i++) crc = table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }
    }
}
