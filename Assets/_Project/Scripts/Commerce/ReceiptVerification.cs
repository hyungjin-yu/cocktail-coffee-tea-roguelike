using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace CCT
{
    [Serializable]
    public class VerifyRequest
    {
        public string productId;
        public string purchaseToken;
        public string installationId;
    }

    [Serializable]
    public class VerifyResponse
    {
        public bool verified;
        public string productId;
    }

    public enum VerifyResult
    {
        /// <summary>서버가 verified=true + 같은 productId — 지급 가능.</summary>
        Verified,
        /// <summary>서버가 거부(verified=false 또는 productId 불일치) — 지급·승인하지 않는다(재시도 없음).</summary>
        Rejected,
        /// <summary>네트워크/서버 오류/응답 파싱 실패 — 승인하지 않고 나중에 재시도.</summary>
        Error,
    }

    /// <summary>Detail은 고정 문구 + 예외 타입 이름만(영수증·토큰·서버 본문·예외 메시지를 담지 않는다 — 로그로 나간다).</summary>
    public struct VerifyOutcome
    {
        public VerifyResult Result;
        public string Detail;
        public static VerifyOutcome Of(VerifyResult r, string detail = null) => new VerifyOutcome { Result = r, Detail = detail };
    }

    public interface IReceiptVerifier
    {
        void Verify(VerifyRequest request, Action<VerifyOutcome> done);
    }

    public static class ReceiptVerification
    {
        /// <summary>HTTP 200 응답 본문 해석. verified && productId 일치일 때만 Verified. 깨진 JSON은 Error(재시도).</summary>
        public static VerifyOutcome ParseResponse(string json, string expectedProductId)
        {
            if (string.IsNullOrWhiteSpace(json) || !JsonSyntax.IsCompleteObject(json))
                return VerifyOutcome.Of(VerifyResult.Error, "응답 JSON 형식 오류");
            if (!JsonSyntax.TryGetRootMembers(json, out var members) || !members.ContainsKey("verified"))
                return VerifyOutcome.Of(VerifyResult.Error, "verified 필드 없음");
            VerifyResponse r;
            try { r = JsonUtility.FromJson<VerifyResponse>(json); }
            catch (Exception e) { return VerifyOutcome.Of(VerifyResult.Error, "응답 파싱 예외 " + e.GetType().Name); }
            if (r == null) return VerifyOutcome.Of(VerifyResult.Error, "응답 없음");
            if (!r.verified) return VerifyOutcome.Of(VerifyResult.Rejected, "verified=false");
            if (string.IsNullOrEmpty(expectedProductId) || r.productId != expectedProductId)
                return VerifyOutcome.Of(VerifyResult.Rejected, "productId 불일치");
            return VerifyOutcome.Of(VerifyResult.Verified);
        }

        /// <summary>HTTP 상태 → 결과. 2xx만 본문 해석, 4xx(408/429 제외)는 거부, 나머지는 재시도.</summary>
        public static VerifyOutcome FromHttp(long status, string body, string expectedProductId, bool networkError)
        {
            if (networkError || status == 0) return VerifyOutcome.Of(VerifyResult.Error, "네트워크 오류");
            if (status >= 200 && status < 300) return ParseResponse(body, expectedProductId);
            if (status == 408 || status == 429 || status >= 500) return VerifyOutcome.Of(VerifyResult.Error, $"HTTP {status}");
            // 4xx 중 서버가 본문으로 verified=false를 준 경우도 거부로 본다. 형식 없는 4xx는 서버 설정 문제일 수 있어 재시도.
            var parsed = ParseResponse(body, expectedProductId);
            return parsed.Result == VerifyResult.Rejected ? parsed : VerifyOutcome.Of(VerifyResult.Error, $"HTTP {status}");
        }
    }

    /// <summary>검증 서버 POST(JSON). https 주소가 아니면 보내지 않는다.</summary>
    public sealed class HttpReceiptVerifier : IReceiptVerifier
    {
        readonly CommerceConfig config;

        public HttpReceiptVerifier(CommerceConfig config) => this.config = config;

        public void Verify(VerifyRequest request, Action<VerifyOutcome> done)
        {
            if (config == null || !config.PurchasesEnabled)
            {
                done?.Invoke(VerifyOutcome.Of(VerifyResult.Error, "결제 비활성"));
                return;
            }
            UnityWebRequest www;
            try
            {
                www = new UnityWebRequest(config.verifierEndpoint, UnityWebRequest.kHttpVerbPOST)
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(request))),
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = Mathf.CeilToInt(config.TimeoutSeconds),
                };
                www.SetRequestHeader("Content-Type", "application/json");
            }
            catch (Exception e)
            {
                done?.Invoke(VerifyOutcome.Of(VerifyResult.Error, "요청 생성 예외 " + e.GetType().Name));
                return;
            }
            var op = www.SendWebRequest();
            op.completed += _ =>
            {
                VerifyOutcome outcome;
                try
                {
                    bool netErr = www.result == UnityWebRequest.Result.ConnectionError;
                    outcome = ReceiptVerification.FromHttp(www.responseCode, www.downloadHandler?.text, request.productId, netErr);
                }
                catch (Exception e) { outcome = VerifyOutcome.Of(VerifyResult.Error, "응답 처리 예외 " + e.GetType().Name); }
                finally { www.Dispose(); }
                done?.Invoke(outcome);
            };
        }
    }
}
