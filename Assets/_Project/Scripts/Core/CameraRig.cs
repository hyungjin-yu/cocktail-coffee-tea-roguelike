using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 벨트스크롤(던파식) 카메라: 플레이어를 좌우로만 따라가고 위아래는 고정.
    /// 바닥 타일도 좌우로만 스냅 이동해 무한 스크롤처럼 보이게 한다.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        /// <summary>배경 스냅 단위 — 배경 반복 단위의 배수(CafeBackground.SnapStep).</summary>
        float TileSize => CafeBackground.SnapStep;

        static CameraRig current;
        float shakeRemaining, shakeDuration, shakeAmplitude;
        Vector2 lastShake;

        /// <summary>08:122 화면 흔들림 — 설정 shakePercent(0이면 완전 비활성)를 곱한 진폭으로 짧게 흔든다.
        /// 판정·카메라 추적과 무관한 시각 오프셋.</summary>
        public static void Shake(float amplitude, float duration)
        {
            if (current == null) return;
            float scaled = amplitude * SettingsService.Current.shakePercent / 100f;
            if (scaled <= 0f) return;
            float remainingStrength = current.shakeRemaining > 0f
                ? current.shakeAmplitude * current.shakeRemaining / Mathf.Max(0.01f, current.shakeDuration) : 0f;
            if (scaled < remainingStrength) return;
            current.shakeAmplitude = scaled;
            current.shakeDuration = current.shakeRemaining = Mathf.Max(0.01f, duration);
        }

        /// <summary>테스트용 — 현재 흔들림 진폭(0이면 없음).</summary>
        public static float CurrentShakeAmplitude => current == null || current.shakeRemaining <= 0f ? 0f : current.shakeAmplitude;

        void OnEnable() => current = this;

        void OnDisable()
        {
            if (current == this) current = null;
        }

        Camera cam;
        Transform target;
        Transform background;
        float fixedCamY;
        float fixedBackgroundY;

        public void Init(Camera cam, Transform target, Transform background)
        {
            this.cam = cam;
            this.target = target;
            current = this;
            this.background = background;
            fixedCamY = cam.transform.position.y;
            fixedBackgroundY = background != null ? background.position.y : 0f;
            Snap();
        }

        void LateUpdate()
        {
            if (cam == null || target == null) return;

            float t = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
            float baseX = Mathf.Lerp(cam.transform.position.x - lastShake.x, target.position.x, t);
            Vector2 shake = Vector2.zero;
            if (shakeRemaining > 0f)
            {
                shakeRemaining -= Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(shakeRemaining / shakeDuration);
                shake = Random.insideUnitCircle * shakeAmplitude * k;
            }
            lastShake = shake;
            cam.transform.position = new Vector3(baseX + shake.x, fixedCamY + shake.y, -10f);
            SnapBackground(baseX);
        }

        void Snap()
        {
            if (cam == null || target == null) return;
            cam.transform.position = new Vector3(target.position.x, fixedCamY, -10f);
            SnapBackground(target.position.x);
        }

        void SnapBackground(float camX)
        {
            if (background == null) return;
            background.position = new Vector3(Mathf.Round(camX / TileSize) * TileSize, fixedBackgroundY, 0f);
        }
    }
}
