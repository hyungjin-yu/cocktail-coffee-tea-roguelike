using System;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 스킨 결제 설정(Resources/Commerce/commerce_config.json). 결제는 "명시적으로 켜고(enabled) + https 검증 서버 주소가
    /// 있을 때만" 동작한다. 그 밖에는 상점이 스킨·미리보기만 보여 주고 구매 버튼을 "결제 준비 중"으로 막는다.
    /// 가격은 여기 두지 않는다 — 항상 스토어가 준 현지화 가격 문자열만 표시한다.
    /// </summary>
    [Serializable]
    public class CommerceConfig
    {
        public const string ResourcePath = "Commerce/commerce_config";
        public const float DefaultTimeoutSeconds = 15f;

        /// <summary>결제 기능 스위치. false면 스토어 연결도 하지 않는다.</summary>
        public bool enabled;
        /// <summary>영수증 검증 서버 POST 주소. 반드시 https://.</summary>
        public string verifierEndpoint = "";
        /// <summary>검증 요청 제한 시간(초). 0 이하이면 기본 15초.</summary>
        public float requestTimeoutSeconds = DefaultTimeoutSeconds;

        public float TimeoutSeconds => requestTimeoutSeconds > 0f ? requestTimeoutSeconds : DefaultTimeoutSeconds;

        public bool EndpointIsHttps => IsHttpsUrl(verifierEndpoint);

        /// <summary>구매 가능 조건: enabled && https 검증 주소.</summary>
        public bool PurchasesEnabled => enabled && EndpointIsHttps;

        /// <summary>구매가 꺼진 이유(로그·문서용). 켜져 있으면 null.</summary>
        public string DisabledReason =>
            PurchasesEnabled ? null
            : !enabled ? "commerce_config.enabled=false"
            : string.IsNullOrWhiteSpace(verifierEndpoint) ? "verifierEndpoint 비어 있음"
            : "verifierEndpoint가 https가 아님";

        public static bool IsHttpsUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;
            return uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host);
        }

        /// <summary>JSON 파싱. 비었거나 깨졌으면 "꺼짐" 설정을 돌려준다(예외 없음).</summary>
        public static CommerceConfig Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Disabled();
            try
            {
                var c = JsonUtility.FromJson<CommerceConfig>(json);
                if (c == null) return Disabled();
                if (c.verifierEndpoint == null) c.verifierEndpoint = "";
                c.verifierEndpoint = c.verifierEndpoint.Trim();
                return c;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Commerce] commerce_config 파싱 실패 — 결제 비활성: {e.GetType().Name}");
                return Disabled();
            }
        }

        public static CommerceConfig Disabled() => new CommerceConfig { enabled = false, verifierEndpoint = "" };

        public static CommerceConfig LoadFromResources()
        {
            var text = Resources.Load<TextAsset>(ResourcePath);
            var config = Parse(text != null ? text.text : null);
            if (!config.PurchasesEnabled) Debug.Log($"[Commerce] 결제 비활성: {config.DisabledReason}");
            return config;
        }
    }
}
