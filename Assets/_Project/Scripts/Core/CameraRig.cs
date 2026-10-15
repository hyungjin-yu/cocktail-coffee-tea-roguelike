using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 벨트스크롤(던파식) 카메라: 플레이어를 좌우로만 따라가고 위아래는 고정.
    /// 57(2026-10-15): 유한 전투 구역(BattleArena) 끝에서 멈추고, 배경은 월드에 고정한다.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {

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
            float baseX = Mathf.Lerp(cam.transform.position.x - lastShake.x, TargetX, t);
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
            cam.transform.position = new Vector3(TargetX, fixedCamY, -10f);
            SnapBackground(TargetX);
        }

        /// <summary>57 P0-1: 플레이어를 따라가되 유한 전투 구역 끝에서 멈춘다(벽 너머는 OutsideReveal만 보임).</summary>
        float TargetX => BattleArena.ClampCameraX(target.position.x, cam.orthographicSize * cam.aspect);

        void SnapBackground(float camX)
        {
            if (background == null) return;
            // 57 P0-1: 유한 구역 배경은 월드에 고정(벽·경계가 실제 위치에 보이도록). 무한 스크롤 스냅은 쓰지 않는다.
            background.position = new Vector3(0f, fixedBackgroundY, 0f);
        }
    }
}
