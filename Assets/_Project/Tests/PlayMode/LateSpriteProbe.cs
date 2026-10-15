using System.Collections.Generic;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>
    /// 49 시험용: 모든 스킨 교체(SkinSpriteSwapper 5000, WeaponSkinApplier 5001) 뒤 LateUpdate에서 렌더될 스프라이트를 기록한다.
    /// 배치 모드에서는 WaitForEndOfFrame이 오지 않으므로 렌더 시점 확인에 이 탐침을 쓴다.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class LateSpriteProbe : MonoBehaviour
    {
        public SpriteRenderer body, weapon;
        public readonly List<Sprite> bodyFrames = new List<Sprite>();
        public readonly List<Sprite> weaponFrames = new List<Sprite>();

        public static LateSpriteProbe Attach(PlayerVisual visual)
        {
            var p = visual.gameObject.AddComponent<LateSpriteProbe>();
            p.body = visual.Body;
            p.weapon = visual.Weapon;
            return p;
        }

        void LateUpdate()
        {
            if (body != null) bodyFrames.Add(body.sprite);
            if (weapon != null) weaponFrames.Add(weapon.sprite);
        }
    }
}
