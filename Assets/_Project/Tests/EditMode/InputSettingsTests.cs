using NUnit.Framework;

namespace CCT.Tests
{
    public class InputSettingsTests
    {
        MovementScheme original;

        [SetUp]
        public void SetUp() => original = InputSettings.Scheme;

        [TearDown]
        public void TearDown() => InputSettings.Scheme = original; // 로컬 PlayerPrefs 오염 방지

        [Test]
        public void Toggle_SwitchesBetweenKeyboardAndMouse()
        {
            InputSettings.Scheme = MovementScheme.Keyboard;

            InputSettings.Toggle();
            Assert.AreEqual(MovementScheme.Mouse, InputSettings.Scheme);

            InputSettings.Toggle();
            Assert.AreEqual(MovementScheme.Keyboard, InputSettings.Scheme);
        }

        [Test]
        public void Scheme_PersistsAcrossReads()
        {
            InputSettings.Scheme = MovementScheme.Mouse;
            Assert.AreEqual(MovementScheme.Mouse, InputSettings.Scheme);
        }

        [Test]
        public void Label_IsKoreanForBothSchemes()
        {
            Assert.AreEqual("키보드", InputSettings.Label(MovementScheme.Keyboard));
            Assert.AreEqual("마우스", InputSettings.Label(MovementScheme.Mouse));
        }
    }
}
