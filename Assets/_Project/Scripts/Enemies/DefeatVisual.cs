using UnityEngine;

namespace CCT
{
    /// <summary>04 §6 defeat 클립을 판정 없이 재생하고 사라지는 시각 오브젝트(보상·판정 제거는 원래 사망 시점 그대로).</summary>
    public class DefeatVisual : MonoBehaviour
    {
        float remaining;

        public static DefeatVisual Spawn(ArtClip clip, Vector3 position, Vector3 scale, Transform parent)
        {
            var go = new GameObject("DefeatVisual");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = SortOrders.Actor;
            var anim = go.AddComponent<SpriteAnimator>();
            anim.Bind(sr);
            anim.Play(clip, true);
            var v = go.AddComponent<DefeatVisual>();
            v.anim = anim;
            v.remaining = Mathf.Max(0.1f, clip.Duration) + 0.15f;
            v.SyncClock();
            return v;
        }

        SpriteAnimator anim;

        /// <summary>런이 끝나 timeScale이 0이 된 뒤(최종 보스 처치 등)에는 실시간으로 재생한다 — 그렇지 않으면 결과 화면 뒤에서
        /// defeat 첫 프레임에 멈춘 채 남는다. 보상 창처럼 런 중 정지에서는 기존대로 게임 시간과 함께 멈춘다.</summary>
        bool RunEnded => GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver;

        void SyncClock()
        {
            if (anim != null) anim.UseUnscaledTime = RunEnded;
        }

        void Update()
        {
            SyncClock();
            remaining -= RunEnded ? Time.unscaledDeltaTime : Time.deltaTime;
            if (remaining <= 0f) Destroy(gameObject);
        }
    }
}
