namespace CCT
{
    /// <summary>
    /// UI 가독성 감사 S-11(2026-11-23): "칵테일을(를)", "창 모드(으)로"처럼 조사 괄호가 화면에 그대로 보였다.
    /// 앞 낱말 마지막 글자의 받침으로 조사를 고른다. 한글 음절·숫자는 판정, 그 밖(영문·기호·빈 글)은 기존 괄호 표기 그대로 둔다(틀린 조사보다 낫다).
    /// </summary>
    public static class KoreanParticle
    {
        /// <summary>마지막 글자 받침: 1 = 있음, 0 = 없음, -1 = 판정 불가. rieul = 받침이 ㄹ인지(으로/로 판정용).</summary>
        static int Batchim(string word, out bool rieul)
        {
            rieul = false;
            if (string.IsNullOrEmpty(word)) return -1;
            char c = word[word.Length - 1];
            if (c >= '가' && c <= '힣')
            {
                int jong = (c - 0xAC00) % 28;
                rieul = jong == 8;
                return jong == 0 ? 0 : 1;
            }
            if (c >= '0' && c <= '9')
            {
                // 영 일 이 삼 사 오 육 칠 팔 구
                rieul = c == '1' || c == '7' || c == '8';
                return c == '2' || c == '4' || c == '5' || c == '9' ? 0 : 1;
            }
            return -1;
        }

        /// <summary>조사만("을"/"를"/판정 불가 "을(를)") — 낱말을 따옴표로 감쌀 때.</summary>
        public static string EulReulOf(string word) => Batchim(word, out _) switch { 1 => "을", 0 => "를", _ => "을(를)" };
        /// <summary>"칵테일을" / "차를" (판정 불가면 "을(를)").</summary>
        public static string EulReul(string word) => word + EulReulOf(word);
        /// <summary>"전체 화면으로" / "창 모드로" / "레일로"(ㄹ 받침은 로) (판정 불가면 "(으)로").</summary>
        public static string EuRo(string word)
        {
            int b = Batchim(word, out bool rieul);
            return word + (b < 0 ? "(으)로" : b == 1 && !rieul ? "으로" : "로");
        }
        /// <summary>"설탕이" / "우유가" (판정 불가면 "이(가)").</summary>
        public static string IGa(string word) => word + (Batchim(word, out _) switch { 1 => "이", 0 => "가", _ => "이(가)" });
        /// <summary>"설탕은" / "우유는" (판정 불가면 "은(는)").</summary>
        public static string EunNeun(string word) => word + (Batchim(word, out _) switch { 1 => "은", 0 => "는", _ => "은(는)" });
    }
}
