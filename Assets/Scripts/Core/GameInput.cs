using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PokaGoryu
{
    public enum GKey { W, A, S, D, E, F, Q, R, G, Tab, Shift, Esc, Space, Enter, Minus, Equals, F1, F2, F3, F4, F5, F6, Up, Down, Left, Right, F7 }

    /// <summary>
    /// Единая точка ввода. Работает и со старым Input Manager, и с новым Input System
    /// (какой включён в Player Settings -> Active Input Handling — выбирается автоматически).
    /// </summary>
    public interface IGameInput
    {
        bool Held(GKey key);
        bool Down(GKey key);
        bool MouseHeld(int button);
        bool MouseDown(int button);
        Vector2 LookDelta();
        float Scroll();
    }

    public static class GameInput
    {
        public static IGameInput Provider { get; set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetProvider() { Provider = null; }
        public static bool Held(GKey k) { return Provider != null ? Provider.Held(k) : ReadHeld(k); }
        public static bool Down(GKey k) { return Provider != null ? Provider.Down(k) : ReadDown(k); }
        public static bool MouseHeld(int b) { return Provider != null ? Provider.MouseHeld(b) : ReadMouseHeld(b); }
        public static bool MouseDown(int b) { return Provider != null ? Provider.MouseDown(b) : ReadMouseDown(b); }
        public static Vector2 LookDelta() { return Provider != null ? Provider.LookDelta() : ReadLookDelta(); }
        public static float Scroll() { return Provider != null ? Provider.Scroll() : ReadScroll(); }

#if ENABLE_INPUT_SYSTEM
        static Key Map(GKey k)
        {
            switch (k)
            {
                case GKey.W: return Key.W;
                case GKey.A: return Key.A;
                case GKey.S: return Key.S;
                case GKey.D: return Key.D;
                case GKey.E: return Key.E;
                case GKey.F: return Key.F;
                case GKey.Q: return Key.Q;
                case GKey.R: return Key.R;
                case GKey.G: return Key.G;
                case GKey.Minus: return Key.Minus;
                case GKey.Equals: return Key.Equals;
                case GKey.Tab: return Key.Tab;
                case GKey.Shift: return Key.LeftShift;
                case GKey.Esc: return Key.Escape;
                case GKey.Space: return Key.Space;
                case GKey.Enter: return Key.Enter;
                case GKey.F1: return Key.F1;
                case GKey.F2: return Key.F2;
                case GKey.F3: return Key.F3;
                case GKey.F4: return Key.F4;
                case GKey.F5: return Key.F5;
                case GKey.F6: return Key.F6;
                case GKey.F7: return Key.F7;
                case GKey.Up: return Key.UpArrow;
                case GKey.Down: return Key.DownArrow;
                case GKey.Left: return Key.LeftArrow;
                case GKey.Right: return Key.RightArrow;
            }
            return Key.Space;
        }

        static bool ReadHeld(GKey k)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            return kb[Map(k)].isPressed;
        }

        static bool ReadDown(GKey k)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            return kb[Map(k)].wasPressedThisFrame;
        }

        static bool ReadMouseHeld(int button)
        {
            Mouse m = Mouse.current;
            if (m == null) return false;
            if (button == 0) return m.leftButton.isPressed;
            return m.rightButton.isPressed;
        }

        static bool ReadMouseDown(int button)
        {
            Mouse m = Mouse.current;
            if (m == null) return false;
            if (button == 0) return m.leftButton.wasPressedThisFrame;
            return m.rightButton.wasPressedThisFrame;
        }

        /// <summary>Сдвиг мыши за кадр (в тех же единицах, что Input.GetAxis("Mouse X")).</summary>
        static Vector2 ReadLookDelta()
        {
            Mouse m = Mouse.current;
            if (m == null) return Vector2.zero;
            return m.delta.ReadValue() * 0.1f;
        }

        static float ReadScroll()
        {
            Mouse m = Mouse.current;
            if (m == null) return 0f;
            float y = m.scroll.ReadValue().y;
            if (Mathf.Abs(y) < 0.01f) return 0f;
            return Mathf.Sign(y);
        }
#else
        static KeyCode Map(GKey k)
        {
            switch (k)
            {
                case GKey.W: return KeyCode.W;
                case GKey.A: return KeyCode.A;
                case GKey.S: return KeyCode.S;
                case GKey.D: return KeyCode.D;
                case GKey.E: return KeyCode.E;
                case GKey.F: return KeyCode.F;
                case GKey.Q: return KeyCode.Q;
                case GKey.R: return KeyCode.R;
                case GKey.G: return KeyCode.G;
                case GKey.Minus: return KeyCode.Minus;
                case GKey.Equals: return KeyCode.Equals;
                case GKey.Tab: return KeyCode.Tab;
                case GKey.Shift: return KeyCode.LeftShift;
                case GKey.Esc: return KeyCode.Escape;
                case GKey.Space: return KeyCode.Space;
                case GKey.Enter: return KeyCode.Return;
                case GKey.F1: return KeyCode.F1;
                case GKey.F2: return KeyCode.F2;
                case GKey.F3: return KeyCode.F3;
                case GKey.F4: return KeyCode.F4;
                case GKey.F5: return KeyCode.F5;
                case GKey.F6: return KeyCode.F6;
                case GKey.F7: return KeyCode.F7;
                case GKey.Up: return KeyCode.UpArrow;
                case GKey.Down: return KeyCode.DownArrow;
                case GKey.Left: return KeyCode.LeftArrow;
                case GKey.Right: return KeyCode.RightArrow;
            }
            return KeyCode.None;
        }

        static bool ReadHeld(GKey k) { return Input.GetKey(Map(k)); }
        static bool ReadDown(GKey k) { return Input.GetKeyDown(Map(k)); }
        static bool ReadMouseHeld(int button) { return Input.GetMouseButton(button); }
        static bool ReadMouseDown(int button) { return Input.GetMouseButtonDown(button); }

        static Vector2 ReadLookDelta()
        {
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        }

        static float ReadScroll()
        {
            float y = Input.mouseScrollDelta.y;
            if (Mathf.Abs(y) < 0.01f) return 0f;
            return Mathf.Sign(y);
        }
#endif
    }
}
