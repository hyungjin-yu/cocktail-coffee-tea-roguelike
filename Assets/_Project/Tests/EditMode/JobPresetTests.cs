using NUnit.Framework;

namespace CCT.Tests
{
    public class JobPresetTests
    {
        [Test]
        public void For_ReturnsMatchingPreset()
        {
            Assert.AreSame(JobPreset.Bartender, JobPreset.For(Job.Bartender));
            Assert.AreSame(JobPreset.TeaSommelier, JobPreset.For(Job.TeaSommelier));
        }

        [Test]
        public void Presets_HaveDistinctNamesAndColors()
        {
            Assert.AreNotEqual(JobPreset.Bartender.name, JobPreset.TeaSommelier.name);
            Assert.AreNotEqual(JobPreset.Bartender.playerColor, JobPreset.TeaSommelier.playerColor);
        }

        [Test]
        public void EachJob_HasExactlyTwoWeaponOptions()
        {
            Assert.AreEqual(2, JobPreset.Bartender.weapons.Length);
            Assert.AreEqual(2, JobPreset.TeaSommelier.weapons.Length);
        }

        [Test]
        public void Bartender_WeaponsAre_RapierAndGun()
        {
            Assert.AreEqual(WeaponKind.Rapier, JobPreset.Bartender.weapons[0].kind);
            Assert.AreEqual(WeaponKind.Gun, JobPreset.Bartender.weapons[1].kind);
        }

        [Test]
        public void TeaSommelier_WeaponsAre_SwordAndBow()
        {
            Assert.AreEqual(WeaponKind.Sword, JobPreset.TeaSommelier.weapons[0].kind);
            Assert.AreEqual(WeaponKind.Bow, JobPreset.TeaSommelier.weapons[1].kind);
        }
    }
}
