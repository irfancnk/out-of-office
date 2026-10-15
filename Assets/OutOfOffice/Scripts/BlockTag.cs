using UnityEngine;

namespace OutOfOffice
{
    // Marks a wall, hazard, bouncer, or sticky block, and plays its hit flash and jelly wobble.
    public class BlockTag : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock block;

        public BlockKind Kind { get; private set; }

        MeshRenderer visual;
        Color baseColor;
        float flash, wobble, wobbleVel, swivel, swivelVel;
        bool active;

        public void Init(BlockKind kind, MeshRenderer renderer)
        {
            Kind = kind;
            visual = renderer;
            if (renderer != null) baseColor = renderer.sharedMaterial.GetColor(BaseColorId);
        }

        public void Flash()
        {
            if (visual == null) return;
            flash = 1f;
            active = true;
        }

        // Kicks the jelly spring. Positive squashes the block down.
        public void Wobble(float strength)
        {
            if (visual == null) return;
            float softness = Kind == BlockKind.Sticky ? 2.2f : Kind == BlockKind.Bouncer ? 1.5f : 1f;
            wobbleVel += strength * softness;
            // Chairs also swivel on their base and spring back. Cosmetic: the collider is round.
            if (Kind == BlockKind.Bouncer || Kind == BlockKind.Kicker)
                swivelVel += Mathf.Clamp(strength, 0f, 3f) * 70f * Puck.MotionScale * (Random.value < 0.5f ? -1f : 1f);
            active = true;
        }

        void Update()
        {
            if (!active) return;
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);

            const float stiffness = 380f, damping = 9f;
            wobbleVel += (-stiffness * wobble - damping * wobbleVel) * dt;
            wobble = Mathf.Clamp(wobble + wobbleVel * dt, -0.6f, 0.6f);
            // Mostly vertical: the footprint stays within a few percent of the collider.
            visual.transform.localScale = new Vector3(1f + 0.04f * wobble, 1f + 0.04f * wobble, 1f - 0.6f * wobble);
            if (swivel != 0f || swivelVel != 0f)
            {
                swivelVel += (-55f * swivel - 5f * swivelVel) * dt;
                swivel += swivelVel * dt;
                visual.transform.localRotation = Quaternion.Euler(0f, 0f, swivel);
            }

            if (flash > 0f)
            {
                flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 4f);
                block ??= new MaterialPropertyBlock();
                visual.GetPropertyBlock(block);
                block.SetColor(BaseColorId, Color.Lerp(baseColor, Color.white, flash * 0.7f));
                visual.SetPropertyBlock(block);
                if (flash <= 0f) visual.SetPropertyBlock(null);
            }

            if (flash <= 0f && Mathf.Abs(wobble) < 0.001f && Mathf.Abs(wobbleVel) < 0.01f && Mathf.Abs(swivel) < 0.05f && Mathf.Abs(swivelVel) < 0.5f)
            {
                wobble = wobbleVel = swivel = swivelVel = 0f;
                visual.transform.localScale = Vector3.one;
                visual.transform.localRotation = Quaternion.identity;
                active = false;
            }
        }
    }
}
