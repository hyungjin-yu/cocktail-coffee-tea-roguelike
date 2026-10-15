using System;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 2026-10-13 스토어 표시 정보(기획 52): 게임 표시 제목과 공개 개인정보처리방침 URL. Android 패키지 ID와는 별개다
    /// (BuildRelease.AndroidPackageId 유지). Windows의 PlayerSettings.productName은 세이브 경로(persistentDataPath)를 지키기 위해 바꾸지 않는다.
    /// </summary>
    public static class GameInfo
    {
        public const string Title = "음료 마스터";
        public const string PrivacyPolicyUrl = "https://hyungjin-yu.itch.io/coffee-cocktail-tea/devlog/1704962/-privacy-policy";
        public const string PrivacyPolicyLabel = "개인정보처리방침";

        /// <summary>시험에서 교체(실제 브라우저를 열지 않음).</summary>
        public static Action<string> UrlOpener = Application.OpenURL;

        public static void OpenPrivacyPolicy() => UrlOpener?.Invoke(PrivacyPolicyUrl);
    }
}
