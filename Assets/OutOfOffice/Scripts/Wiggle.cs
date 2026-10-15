using UnityEngine;

namespace OutOfOffice
{
    // Small idle animation for decorations: pulse, bob up and down, or spin around the board's up axis.
    public class Wiggle : MonoBehaviour
    {
        public enum Mode { Pulse, Bob, Spin }

        Mode mode;
        float amount, speed, phase;
        Vector3 basePos, baseScale;
        Quaternion baseRot;

        public Wiggle Init(Mode m, float amount, float speed)
        {
            mode = m;
            this.amount = amount;
            this.speed = speed;
            phase = Random.value * 6.28f;
            basePos = transform.localPosition;
            baseScale = transform.localScale;
            baseRot = transform.localRotation;
            return this;
        }

        void Update()
        {
            float t = Time.time * speed + phase;
            switch (mode)
            {
                case Mode.Pulse:
                    transform.localScale = baseScale * (1f + amount * Mathf.Sin(t));
                    break;
                case Mode.Bob:
                    transform.localPosition = basePos + new Vector3(0f, 0f, -amount * (0.5f + 0.5f * Mathf.Sin(t)));
                    break;
                case Mode.Spin:
                    transform.localRotation = Quaternion.Euler(0f, 0f, t * Mathf.Rad2Deg) * baseRot;
                    break;
            }
        }
    }
}
