using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Starting points for a new room in the level editor (OUT-12). Each is a furnished, lint-clean board, so a designer
    // starts from something playable and edits from there.
    public static class LevelTemplates
    {
        public static readonly string[] Names = { "blank", "tall", "round", "figure8", "stadium" };

        public static LevelDef Make(string template, string id, string name)
        {
            var d = new LevelDef(id, name, 0f, 5.6f).WithHint("FIND A WAY TO THE BED").WithPar(2);
            switch (template)
            {
                case "tall":
                    d.Taller(8f).StartAt(0f, -13f);
                    d.Wall(0f, -5f, 5f, 0.6f).Post(BlockKind.Bouncer, 3f, -9.5f, 0.45f)
                        .Star(-3f, -8f).Star(3f, -1f).Star(-2.4f, 3.4f);
                    break;
                case "round":
                    d.WithShape(ShapeDef.Circle(7.2f)).StartAt(0f, -5.4f);
                    d.Post(BlockKind.Wall, 0f, 0f, 2.4f).Post(BlockKind.Bouncer, -4.6f, -2.6f, 0.45f)
                        .Star(-4.2f, 2.4f).Star(4.2f, 2.4f).Star(3.6f, -3.4f);
                    break;
                case "figure8":
                    d.WithShape(ShapeDef.FigureEight(6f, 7.4f, 3.2f)).StartAt(0f, -12.2f);
                    d.Hole = new Vector2(0f, 12.2f);
                    d.Post(BlockKind.Wall, 0f, -7.4f, 2.4f).Post(BlockKind.Wall, 0f, 7.4f, 2.4f)
                        .Star(4.2f, -4.6f).Star(0f, 2.6f).Star(-4.2f, 9.8f);
                    break;
                case "stadium":
                    d.WithShape(ShapeDef.Stadium(9f, 16f));
                    d.Wall(0f, 1f, 4.4f, 0.6f).Post(BlockKind.Bouncer, -2.8f, -3.6f, 0.45f)
                        .Star(-2.8f, -0.6f).Star(2.8f, 2.4f).Star(-1.6f, 4.4f);
                    break;
                default:
                    d.Wall(0f, 1.2f, 4.6f, 0.6f).Post(BlockKind.Bouncer, -3f, -3.6f, 0.45f).Post(BlockKind.Bouncer, 3f, -3.6f, 0.45f)
                        .Star(-3.2f, -0.6f).Star(3.2f, 2.6f).Star(-1.6f, 4.4f);
                    break;
            }
            return d;
        }
    }
}
