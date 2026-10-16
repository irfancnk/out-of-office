using UnityEngine;
using UnityEngine.EventSystems;

namespace OutOfOffice
{
    // Every button's feel (OUT-40): squashes wide and flat while held, with a soft boing and a light tap of haptics,
    // then wobbles back like jelly on release. Big panels squash less than round buttons, so the feel is the same at
    // every size. Reduced Motion keeps a small, plain press.
    public class UiPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public System.Action Clicked;
        public static System.Action Pressed;  // the press sound, set by the game
        public static bool Calm;              // Reduced Motion
        bool down;

        static readonly Vector3 Squash = new Vector3(1.15f, 0.8f, 1f);
        const float PressTime = 0.09f, WobbleTime = 0.5f, WobbleCycles = 2.5f;

        // Round buttons get the full squash; a wide row or a card-sized element about half of it.
        float Depth
        {
            get
            {
                var r = transform as RectTransform;
                float size = r != null ? Mathf.Max(r.rect.width, r.rect.height) : 0f;
                return Calm ? 0.25f : Mathf.Lerp(1f, 0.5f, Mathf.InverseLerp(220f, 600f, size));
            }
        }

        public void OnPointerDown(PointerEventData e)
        {
            down = true;
            Tweens.Kill(this);
            var t = transform;
            Vector3 from = t.localScale, to = Vector3.LerpUnclamped(Vector3.one, Squash, Depth);
            Tweens.Run(PressTime, k => t.localScale = Vector3.LerpUnclamped(from, to, k), Tweens.OutCubic, owner: this);
            Pressed?.Invoke();
            Haptics.Play(Haptics.Style.Light, 0.6f);
        }

        public void OnPointerUp(PointerEventData e)
        {
            Tweens.Kill(this);
            var t = transform;
            Vector3 from = t.localScale;
            if (Calm) Tweens.Run(0.12f, k => t.localScale = Vector3.LerpUnclamped(from, Vector3.one, k), Tweens.OutCubic, owner: this);
            else
                // Jelly: the squash swings through a stretch and back, smaller each time.
                Tweens.Run(WobbleTime, k =>
                {
                    float swing = Mathf.Cos(k * Mathf.PI * 2f * WobbleCycles) * (1f - k) * (1f - k);
                    t.localScale = Vector3.one + (from - Vector3.one) * swing;
                }, Tweens.Linear, 0f, () => t.localScale = Vector3.one, this);
            bool inside = e.pointerCurrentRaycast.gameObject != null && e.pointerCurrentRaycast.gameObject.transform.IsChildOf(transform);
            // A press that turned into a drag was scrolling a list, not choosing: in a scroll view the tile moves with the
            // finger, so it would still be "inside" on release and start a room or equip an item by accident.
            if (down && inside && !e.dragging) Clicked?.Invoke();
            down = false;
        }
    }
}
