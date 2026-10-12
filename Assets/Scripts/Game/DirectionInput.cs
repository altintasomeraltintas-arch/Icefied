using UnityEngine;
using UnityEngine.InputSystem;

namespace Icefied.Game
{
    /// <summary>
    /// Ekranın herhangi bir yerinde çalışan görünmez joystick. Parmağın sürükleme yönü 4 yöne snap'lenir.
    /// Parmak basılı kaldıkça son yön korunur; çapa noktası parmağı takip eder, böylece ters yöne dönmek anlıktır.
    /// Editörde ok tuşları / WASD da çalışır.
    /// </summary>
    public class DirectionInput
    {
        public Vector2Int Dir { get; private set; }
        public bool Held { get; private set; }
        public Vector2 Anchor { get; private set; }
        public Vector2 Current { get; private set; }
        public bool PointerActive { get; private set; }

        bool _down;

        public void Update()
        {
            var pointer = Pointer.current;
            bool pressed = pointer != null && pointer.press.isPressed;

            if (pressed)
            {
                Vector2 pos = pointer.position.ReadValue();
                if (!_down)
                {
                    _down = true;
                    Anchor = pos;
                    Dir = Vector2Int.zero;
                }

                Current = pos;
                float unit = Mathf.Min(Screen.width, Screen.height);
                float dead = unit * 0.025f;
                float max = unit * 0.09f;
                Vector2 d = pos - Anchor;
                if (d.magnitude > max)
                {
                    Anchor = pos - d.normalized * max;
                    d = pos - Anchor;
                }
                if (d.magnitude > dead)
                {
                    Dir = Mathf.Abs(d.x) > Mathf.Abs(d.y)
                        ? new Vector2Int(d.x > 0 ? 1 : -1, 0)
                        : new Vector2Int(0, d.y > 0 ? 1 : -1);
                }
            }
            else
            {
                _down = false;
                Dir = Vector2Int.zero;
            }

            PointerActive = _down;
            Held = _down;

            var kb = Keyboard.current;
            if (kb != null)
            {
                Vector2Int k = Vector2Int.zero;
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) k = Vector2Int.left;
                else if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) k = Vector2Int.right;
                else if (kb.upArrowKey.isPressed || kb.wKey.isPressed) k = Vector2Int.up;
                else if (kb.downArrowKey.isPressed || kb.sKey.isPressed) k = Vector2Int.down;
                if (k != Vector2Int.zero)
                {
                    Dir = k;
                    Held = true;
                }
            }
        }

        public void Reset()
        {
            _down = false;
            Dir = Vector2Int.zero;
            Held = false;
            PointerActive = false;
        }
    }
}
