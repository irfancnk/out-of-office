using UnityEngine;

namespace OutOfOffice
{
    // Raises and sinks a pop wall with the level clock. Down, only a faint outline shows where it will come up;
    // just before it rises the outline flashes and the wall peeks out.
    public class PopWallAnim : MonoBehaviour
    {
        PopWallInstance wall;
        LevelInstance level;
        SpriteRenderer outline;
        Color tint;

        public void Init(PopWallInstance w, LevelInstance inst, SpriteRenderer mark)
        {
            wall = w;
            level = inst;
            outline = mark;
            tint = mark.color;
        }

        void Update()
        {
            if (wall == null || level == null || wall.Visual == null) return;
            float t = level.Time;
            float up = wall.Height(t);
            float until = wall.UntilRise(t);
            float warn = until > 0f && until < 0.5f ? 1f - until / 0.5f : 0f;
            float h = Mathf.Max(up, warn * 0.15f);
            wall.Visual.localScale = new Vector3(1f, 1f, Mathf.Max(0.02f, h));
            wall.Visual.gameObject.SetActive(h > 0.01f);
            float flash = warn > 0f ? 0.5f + 0.5f * Mathf.Sin(Time.time * 30f) : 0f;
            outline.color = new Color(tint.r, tint.g, tint.b, tint.a * (1f - up) * (0.6f + 0.4f * flash));
        }
    }
}
