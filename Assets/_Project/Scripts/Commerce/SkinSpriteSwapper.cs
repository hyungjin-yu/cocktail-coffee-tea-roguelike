using System.Collections.Generic;
using UnityEngine;

namespace CCT
{
    /// <summary>
    /// 치장 스킨 적용: 매 프레임 마지막(모든 Update/LateUpdate 뒤)에 몸체 SpriteRenderer의 현재 스프라이트를
    /// 같은 이름의 스킨 스프라이트로 바꾼다. 원본 애니메이션·피벗·anchor·활/검 액션 클립을 그대로 따른다.
    /// 판정·능력치·무기 렌더러에는 손대지 않는다. 같은 이름의 스킨 프레임이 없으면 원본 그대로.
    /// </summary>
    [DefaultExecutionOrder(5000)]
    public class SkinSpriteSwapper : MonoBehaviour
    {
        SpriteRenderer target;
        Dictionary<string, Sprite> map;
        /// <summary>이미 스킨인 스프라이트 — 다음 프레임에 원본으로 되돌리지 않도록 그대로 둔다.</summary>
        HashSet<Sprite> skinSprites;

        public string ProductId { get; private set; }
        public SpriteRenderer Target => target;

        /// <summary>이번 런 직업에 장착·소유·아트가 모두 있으면 붙인다(없으면 null, 기본 아트 유지).</summary>
        public static SkinSpriteSwapper AttachIfEquipped(PlayerVisual visual, Job job)
        {
            if (visual == null || visual.Body == null || !visual.UsesBodyArt) return null;
            SkinDefinition def;
            try { def = SkinCommerce.Shared.ResolveForRun(job, SkinArt.Exists); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Commerce] 스킨 해석 실패 — 기본 아트: {e.GetType().Name}");
                return null;
            }
            return def == null ? null : Attach(visual.Body, def);
        }

        public static SkinSpriteSwapper Attach(SpriteRenderer body, SkinDefinition def)
        {
            if (body == null || def == null) return null;
            var map = SkinArt.Sprites(def);
            if (map.Count == 0) return null;
            var swapper = body.gameObject.GetComponent<SkinSpriteSwapper>();
            if (swapper == null) swapper = body.gameObject.AddComponent<SkinSpriteSwapper>();
            swapper.target = body;
            swapper.map = map;
            swapper.skinSprites = new HashSet<Sprite>(map.Values);
            swapper.ProductId = def.productId;
            swapper.Apply();
            return swapper;
        }

        void LateUpdate() => Apply();

        /// <summary>현재 프레임을 스킨으로. 바꿨으면 true.</summary>
        public bool Apply()
        {
            if (target == null || map == null) return false;
            var s = target.sprite;
            if (s == null || (skinSprites != null && skinSprites.Contains(s))) return false;
            if (map.TryGetValue(s.name, out var skin) && skin != null && skin != s)
            {
                target.sprite = skin;
                return true;
            }
            return false;
        }
    }
}
