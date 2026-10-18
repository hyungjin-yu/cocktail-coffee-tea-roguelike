using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
namespace CCT.Tests
{
    public class CardEducationSettingsTests
    {
        string path;
        [SetUp] public void Setup(){path=Path.Combine(Application.temporaryCachePath,"education-settings-"+Guid.NewGuid().ToString("N")+".json");}
        [TearDown] public void Cleanup()
        {
            SettingsService.SetForTests(TestSaveIsolation.SettingsSessionPath,new MemoryLegacySettingsStore(),new MemoryDisplayAdapter());
            foreach(var suffix in new[]{"",".bak",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);
        }
        [Test] public void ExistingV2Settings_ReceiveNewGuideWithoutLosingCombatTutorialOrOtherSettings()
        {
            File.WriteAllText(path,"{\"schemaVersion\":2,\"tutorialSeen\":true,\"masterVolume\":37,\"movementScheme\":\"mouse\"}");
            SettingsService.SetForTests(path,new MemoryLegacySettingsStore(),new MemoryDisplayAdapter());
            Assert.IsTrue(CardEducationGuide.NeedsIntroduction);Assert.IsTrue(FirstRunTutorial.Seen);
            SettingsService.CompleteCardEducationGuide(CardEducationGuide.Version);
            SettingsService.SetForTests(path,new MemoryLegacySettingsStore(),new MemoryDisplayAdapter());
            Assert.IsFalse(CardEducationGuide.NeedsIntroduction);Assert.IsTrue(FirstRunTutorial.Seen);
            Assert.AreEqual(37,SettingsService.Current.masterVolume);Assert.AreEqual(MovementScheme.Mouse,SettingsService.Current.Scheme);
            Assert.AreEqual(2,SettingsService.Current.schemaVersion);
        }
        [Test] public void FutureSettingsRemainByteIdenticalAfterGuideCompletion()
        {
            string original="{\"schemaVersion\":999,\"tutorialSeen\":true,\"unknownFutureField\":\"keep\"}";
            File.WriteAllText(path,original);
            SettingsService.SetForTests(path,new MemoryLegacySettingsStore(),new MemoryDisplayAdapter());
            SettingsService.CompleteCardEducationGuide(CardEducationGuide.Version);
            Assert.AreEqual(original,File.ReadAllText(path));
        }
    }
}
