using UnityEngine;

namespace OutOfOffice
{
    // Slides a gate down into the floor when its switch is pressed, and back up on undo.
    public class GateAnim : MonoBehaviour
    {
        GateInstance gate;
        float height, k;

        public void Init(GateInstance g, float h)
        {
            gate = g;
            height = h;
        }

        void Update()
        {
            float target = gate.Open ? 1f : 0f;
            if (Mathf.Approximately(k, target)) return;
            k = Mathf.MoveTowards(k, target, Time.deltaTime * 2.8f);
            float e = k * k * (3f - 2f * k);
            gate.Visual.localPosition = new Vector3(0f, 0f, (height + 0.05f) * e);
        }
    }
}
