using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CCT.Prototype.Coop2P
{
    /// <summary>한 키보드 2인 배치. 두 사람의 키 집합과 시스템 키(Esc/R/F10)는 서로 겹치지 않는다(EditMode 검사).</summary>
    public sealed class CoopKeyMap
    {
        public string Label;
        public Key Up, Down, Left, Right;
        public Key[] Interact;
        public Key[] Drink;
        public string MoveText, InteractText, DrinkText;

        public Key[] All()
        {
            var list = new System.Collections.Generic.List<Key> { Up, Down, Left, Right };
            list.AddRange(Interact);
            list.AddRange(Drink);
            return list.ToArray();
        }
    }

    public static class CoopInput
    {
        public static readonly CoopKeyMap P1 = new CoopKeyMap
        {
            Label = "P1", Up = Key.W, Down = Key.S, Left = Key.A, Right = Key.D,
            Interact = new[] { Key.E }, Drink = new[] { Key.Q },
            MoveText = "WASD", InteractText = "E", DrinkText = "Q",
        };

        // 오른쪽 Ctrl/Shift가 없는 키보드용 대체키: 숫자패드 0 / 숫자패드 .
        public static readonly CoopKeyMap P2 = new CoopKeyMap
        {
            Label = "P2", Up = Key.UpArrow, Down = Key.DownArrow, Left = Key.LeftArrow, Right = Key.RightArrow,
            Interact = new[] { Key.RightCtrl, Key.Numpad0 }, Drink = new[] { Key.RightShift, Key.NumpadPeriod },
            MoveText = "방향키", InteractText = "오른쪽 Ctrl", DrinkText = "오른쪽 Shift",
        };

        public const Key Pause = Key.Escape;
        public const Key Restart = Key.R;
        public const Key Quit = Key.F10;
        public static readonly Key[] SystemKeys = { Pause, Restart, Quit };

        public static PlayerInput Read(Keyboard kb, CoopKeyMap m)
        {
            var input = new PlayerInput();
            if (kb == null) return input;
            var v = Vector2.zero;
            if (Held(kb, m.Up)) v.y += 1f;
            if (Held(kb, m.Down)) v.y -= 1f;
            if (Held(kb, m.Left)) v.x -= 1f;
            if (Held(kb, m.Right)) v.x += 1f;
            input.Move = v.sqrMagnitude > 1f ? v.normalized : v;
            foreach (var k in m.Interact) input.Interact |= Pressed(kb, k);
            foreach (var k in m.Drink) input.Drink |= Pressed(kb, k);
            return input;
        }

        static KeyControl Control(Keyboard kb, Key k) => k == Key.None ? null : kb[k];
        public static bool Held(Keyboard kb, Key k) { var c = Control(kb, k); return c != null && c.isPressed; }
        public static bool Pressed(Keyboard kb, Key k) { var c = Control(kb, k); return c != null && c.wasPressedThisFrame; }
    }
}
