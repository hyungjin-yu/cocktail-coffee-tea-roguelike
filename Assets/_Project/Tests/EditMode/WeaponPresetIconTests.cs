using NUnit.Framework;
using UnityEngine;

namespace CCT.Tests
{
    /// <summary>무기 선택 카드가 쓰는 아이콘(WeaponPreset.iconResourcePath)이 실제로 Resources에 있는지 확인.</summary>
    public class WeaponPresetIconTests
    {
        static readonly WeaponPreset[] AllWeapons =
        {
            WeaponPreset.Rapier, WeaponPreset.Gun, WeaponPreset.Sword, WeaponPreset.Bow,
        };

        [Test]
        public void EveryWeaponPreset_HasAResolvableIcon()
        {
            foreach (var weapon in AllWeapons)
            {
                Assert.IsFalse(string.IsNullOrEmpty(weapon.iconResourcePath), weapon.kind.ToString());
                Assert.IsNotNull(Resources.Load<Sprite>(weapon.iconResourcePath),
                    $"{weapon.kind} -> {weapon.iconResourcePath} 아이콘이 Resources 에 있어야 한다");
            }
        }

        [Test]
        public void EachWeapon_HasItsOwnDistinctIcon()
        {
            var paths = new System.Collections.Generic.HashSet<string>();
            foreach (var weapon in AllWeapons)
                Assert.IsTrue(paths.Add(weapon.iconResourcePath), $"{weapon.kind} 아이콘 경로가 다른 무기와 겹친다");
        }
    }
}
