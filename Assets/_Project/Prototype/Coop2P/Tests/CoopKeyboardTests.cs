using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CCT.Prototype.Coop2P.Tests
{
    /// <summary>한 키보드 입력 분리(InputTestFixture: 격리된 입력 상태·프레임 처리). P1 키는 P1만, P2 키는 P2만 움직인다.</summary>
    public class CoopKeyboardTests : InputTestFixture
    {
        /// <summary>한 프레임의 키보드 전체 상태(실빌드 검증기와 같은 방식).</summary>
        static void Set(Keyboard kb, params Key[] keys)
        {
            InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(keys));
            InputSystem.Update();
        }

        [Test]
        public void KeyboardInput_P1KeysOnlyAffectP1_P2KeysOnlyAffectP2()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            Set(kb, Key.D, Key.E);
            var p1 = CoopInput.Read(kb, CoopInput.P1);
            var p2 = CoopInput.Read(kb, CoopInput.P2);
            Assert.That(p1.Move, Is.EqualTo(Vector2.right));
            Assert.IsTrue(p1.Interact, "E = P1 상호작용");
            Assert.That(p2.Move, Is.EqualTo(Vector2.zero));
            Assert.IsFalse(p2.Interact || p2.Drink);
            Set(kb);
            Set(kb, Key.UpArrow, Key.LeftArrow, Key.RightShift);
            p1 = CoopInput.Read(kb, CoopInput.P1);
            p2 = CoopInput.Read(kb, CoopInput.P2);
            Assert.That(p1.Move, Is.EqualTo(Vector2.zero));
            Assert.IsFalse(p1.Interact || p1.Drink);
            Assert.That(p2.Move.x, Is.LessThan(0f));
            Assert.That(p2.Move.y, Is.GreaterThan(0f));
            Assert.IsTrue(p2.Drink, "오른쪽 Shift = P2 마시기");
            Set(kb);
            Set(kb, Key.RightCtrl, Key.Q);
            p1 = CoopInput.Read(kb, CoopInput.P1);
            p2 = CoopInput.Read(kb, CoopInput.P2);
            Assert.IsTrue(p2.Interact, "오른쪽 Ctrl = P2 상호작용");
            Assert.IsTrue(p1.Drink, "Q = P1 마시기");
            Assert.IsFalse(p1.Interact);
            Assert.IsFalse(p2.Drink);
        }

        [Test]
        public void HeldKey_IsNotRepeatedPressOnNextFrame()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            Set(kb, Key.E);
            Assert.IsTrue(CoopInput.Read(kb, CoopInput.P1).Interact);
            Set(kb, Key.E); // 계속 누름
            Assert.IsFalse(CoopInput.Read(kb, CoopInput.P1).Interact, "누르고 있는 동안 다음 프레임에 중복 입력 없음");
        }
    }
}
