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
            Assert.IsNotEmpty(JobPreset.Bartender.weaponName);
            Assert.IsNotEmpty(JobPreset.TeaSommelier.weaponName);
        }
    }
}
