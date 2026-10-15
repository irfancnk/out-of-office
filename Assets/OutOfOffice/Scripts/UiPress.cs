using UnityEngine;
using UnityEngine.EventSystems;

namespace OutOfOffice
{
    // Squishes a button down while pressed and springs it back on release.
    public class UiPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public System.Action Clicked;
        bool down;

        public void OnPointerDown(PointerEventData e)
        {
            down = true;
            Tweens.Kill(this);
            var t = transform;
            Vector3 from = t.localScale;
            Tweens.Run(0.08f, k => t.localScale = Vector3.LerpUnclamped(from, new Vector3(1.08f, 0.88f, 1f), k), Tweens.OutCubic, owner: this);
            Haptics.Play(Haptics.Style.Light, 0.6f);
        }

        public void OnPointerUp(PointerEventData e)
        {
            Tweens.Kill(this);
            var t = transform;
            Vector3 from = t.localScale;
            Tweens.Run(0.35f, k => t.localScale = Vector3.LerpUnclamped(from, Vector3.one, k), Tweens.OutElastic, owner: this);
            bool inside = e.pointerCurrentRaycast.gameObject != null && e.pointerCurrentRaycast.gameObject.transform.IsChildOf(transform);
            if (down && inside) Clicked?.Invoke();
            down = false;
        }
    }
}
