using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CCT
{
    /// <summary>
    /// 2026-11-15 피드백 10: 지식카드 상세의 출처 영역. 본문(게임 효과·음료 지식·해석)과 나눠 아래에 "출처" 제목 + 항목별
    /// 문서명(보조 글꼴) · 짧은 사이트 주소 · [주소 복사] 버튼으로 보인다. 긴 URL 원문은 화면에 늘어놓지 않고 복사 버튼이 원문 그대로
    /// 클립보드에 넣는다(이전에는 원문 URL을 본문에 그대로 보였다 — 정보 손실 없음). 데이터(CardData 출처 필드)는 바꾸지 않는다.
    /// </summary>
    public static class CardSourceList
    {
        public struct Entry
        {
            public string title;
            public string url;
            public string host;
        }

        public const int TitleFont = 24, HostFont = 22, HeaderFont = 26;
        public const float RowGap = 10f, ButtonWidth = 200f, ButtonHeight = 52f;
        static readonly Color HeaderColor = new Color(0.86f, 0.74f, 0.52f);
        static readonly Color TitleColor = new Color(0.86f, 0.86f, 0.84f);
        static readonly Color HostColor = new Color(0.62f, 0.72f, 0.78f);

        /// <summary>시험에서 교체(실제 클립보드 대신).</summary>
        public static Action<string> Copier = s => GUIUtility.systemCopyBuffer = s;

        /// <summary>주 출처(sourceName/sourceUrl) + 추가 출처("이름\nURL" 줄 쌍)를 순서대로. 이름 없는 URL은 사이트 주소를 이름으로.</summary>
        public static List<Entry> For(CardData c)
        {
            var list = new List<Entry>();
            if (c == null) return list;
            if (!string.IsNullOrEmpty(c.sourceName) || !string.IsNullOrEmpty(c.sourceUrl))
                list.Add(Make(c.sourceName, c.sourceUrl));
            if (!string.IsNullOrEmpty(c.additionalSources))
            {
                string pending = null;
                foreach (var raw in c.additionalSources.Split('\n'))
                {
                    var line = raw.Trim();
                    if (line.Length == 0) continue;
                    if (IsUrl(line)) { list.Add(Make(pending, line)); pending = null; }
                    else
                    {
                        if (pending != null) list.Add(Make(pending, ""));
                        pending = line;
                    }
                }
                if (pending != null) list.Add(Make(pending, ""));
            }
            return list;
        }

        static bool IsUrl(string s) => s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        static Entry Make(string title, string url)
        {
            string host = ShortHost(url);
            return new Entry { title = string.IsNullOrEmpty(title) ? host : title, url = url ?? "", host = host };
        }

        /// <summary>"https://www.diageobaracademy.com/en-zz/..." → "diageobaracademy.com". URL이 아니면 빈 문자열.</summary>
        public static string ShortHost(string url)
        {
            if (string.IsNullOrEmpty(url) || !IsUrl(url)) return "";
            var rest = url.Substring(url.IndexOf("://", StringComparison.Ordinal) + 3);
            int cut = rest.IndexOfAny(new[] { '/', '?', '#' });
            if (cut >= 0) rest = rest.Substring(0, cut);
            if (rest.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) rest = rest.Substring(4);
            return rest;
        }

        /// <summary>스크롤 내용(content, 위 기준) 안 y(음수 아래 방향)에서부터 출처 영역을 만든다. 쓴 높이를 돌려준다.</summary>
        public static float Build(RectTransform content, float y, float width, CardData c, List<Button> buttonsOut = null)
        {
            var entries = For(c);
            if (entries.Count == 0) return 0f;
            var root = UiFactory.Place("Sources", content, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(width, 10f));
            float top = 0f;
            // 구분선 + 제목
            var line = UiFactory.AddImage(UiFactory.Place("Divider", root, new Vector2(0.5f, 1f), new Vector2(0f, top), new Vector2(width, 2f)),
                new Color(0.86f, 0.74f, 0.52f, 0.45f));
            line.raycastTarget = false;
            top -= 12f;
            float headerH = UiFactory.TruncatedBoxHeight(HeaderFont);
            var header = UiFactory.AddText(UiFactory.Place("Header", root, new Vector2(0f, 1f), new Vector2(0f, top), new Vector2(width, headerH)),
                $"출처 {entries.Count}", HeaderFont, TextAnchor.MiddleLeft, HeaderColor);
            header.raycastTarget = false;
            top -= headerH + 6f;
            float textW = width - ButtonWidth - 24f;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var row = UiFactory.Place("Source" + i, root, new Vector2(0.5f, 1f), new Vector2(0f, top), new Vector2(width, 10f));
                var t = UiFactory.AddText(UiFactory.Place("Title", row, new Vector2(0f, 1f), Vector2.zero, new Vector2(textW, 40f)),
                    e.title, TitleFont, TextAnchor.UpperLeft, TitleColor);
                t.supportRichText = false;
                t.lineSpacing = 1.1f;
                t.raycastTarget = false;
                float titleH = Mathf.Max(UiFactory.TruncatedBoxHeight(TitleFont), Mathf.Ceil(t.preferredHeight) + 4f);
                t.rectTransform.sizeDelta = new Vector2(textW, titleH);
                float hostH = 0f;
                if (!string.IsNullOrEmpty(e.host))
                {
                    hostH = UiFactory.TruncatedBoxHeight(HostFont);
                    var h = UiFactory.AddText(UiFactory.Place("Host", row, new Vector2(0f, 1f), new Vector2(0f, -titleH), new Vector2(textW, hostH)),
                        e.host, HostFont, TextAnchor.UpperLeft, HostColor);
                    h.supportRichText = false;
                    h.raycastTarget = false;
                }
                float rowH = Mathf.Max(titleH + hostH, ButtonHeight);
                if (!string.IsNullOrEmpty(e.url))
                {
                    var bRt = UiFactory.Place("Copy", row, new Vector2(1f, 1f), Vector2.zero, new Vector2(ButtonWidth, ButtonHeight));
                    var bg = UiFactory.AddImage(bRt, new Color(0.22f, 0.3f, 0.34f));
                    var b = bRt.gameObject.AddComponent<Button>();
                    b.targetGraphic = bg;
                    UiFactory.ApplyHoverColors(b);
                    UiFactory.AddFocusSfx(b);
                    var label = UiFactory.AddText(UiFactory.Stretch("Label", bRt), "주소 복사", 22, TextAnchor.MiddleCenter, Color.white);
                    string url = e.url;
                    b.onClick.AddListener(() =>
                    {
                        Copier?.Invoke(url);
                        label.text = "복사됨";
                        AudioService.PlaySfx("ui_confirm");
                    });
                    buttonsOut?.Add(b);
                }
                row.sizeDelta = new Vector2(width, rowH);
                top -= rowH + RowGap;
            }
            float used = -top;
            root.sizeDelta = new Vector2(width, used);
            return used;
        }
    }
}
