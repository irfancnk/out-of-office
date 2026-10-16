using UnityEngine;

namespace OutOfOffice
{
    // The office staff, in round, soft shapes (OUT-1): a jelly-bean body, a round head, capsule limbs, mitten hands,
    // and puffy hair shells. One rig is shared by the employee and the coworkers, built as named parts under one "Roll"
    // pivot at the collision ball's center (Mascot finds the pivots by name). +Z is down, toward the board; -Y is the front.
    // The head stays upright, but the face and hairline are laid out around FaceDir, the front of the head tipped
    // up toward the camera, so the face reads straight on from the game's high camera and the hair can never cover it.
    // Everything here is cosmetic: the collider is the puck's circle.
    public static class People
    {
        public const float HeadPitch = 32f;
        static readonly Quaternion Tilt = Quaternion.Euler(HeadPitch, 0f, 0f);

        // The face's center and its "up", in the head's frame.
        public static readonly Vector3 FaceDir = Tilt * Vector3.down;
        public static readonly Vector3 FaceUp = Tilt * Vector3.back;
        // Rotation of a face sprite layer printed at FaceDir (sprites face their -Z), relative to the head.
        public static readonly Quaternion FaceFrame = Quaternion.LookRotation(-FaceDir, FaceUp);

        static readonly Color ShirtColor = Themes.Hex(0xF7F5EF);
        static readonly Color TrouserColor = Themes.Hex(0x2F3A52);
        static readonly Color ShoeColor = Themes.Hex(0x3A2A25);
        static readonly Color BeltColor = Themes.Hex(0x4A3428);
        static readonly Color BrassColor = Themes.Hex(0xD8B25C);
        static readonly Color TattooInk = Themes.Hex(0x2E5E7A);

        // The head, in its tipped frame: half sizes. The front (-Y) sits at Mascot.HeadRadius, where the face is printed.
        static readonly Vector3 Skull = new Vector3(0.272f, Mascot.HeadRadius, 0.25f);

        public static void Build(Transform parent, SkinDef skin)
        {
            var roll = Pivot(parent, "Roll", new Vector3(0f, 0f, -Mascot.BodyHalfHeight));
            var skinMat = Look.Person(skin.SkinTone, 0.12f);
            var shirt = Look.Person(ShirtColor, 0.12f);
            var trousers = Look.Person(TrouserColor, 0.15f);

            BuildTorso(roll, skin, shirt, trousers);
            foreach (float side in new[] { -1f, 1f })
            {
                // Arms: a puffy sleeve, a short round forearm, and a mitten hand.
                var arm = Pivot(roll, side < 0f ? "Arm L" : "Arm R", new Vector3(side * 0.218f, 0f, 0.07f));
                arm.localRotation = Quaternion.Euler(0f, side * 8f, 0f);
                Round("Sleeve", new Vector3(0.135f, 0.13f, 0.13f), shirt, arm, new Vector3(0f, 0f, 0.045f));
                Round("Forearm", new Vector3(0.088f, 0.088f, 0.1f), skinMat, arm, new Vector3(0f, 0f, 0.11f));
                Round("Hand", new Vector3(0.112f, 0.106f, 0.1f), skinMat, arm, new Vector3(0f, -0.004f, 0.168f));
                if ((skin.Extras & PersonLook.Tattoos) != 0)
                    foreach (float z in new[] { 0.09f, 0.128f })  // two ink bands round the forearm (OUT-30)
                        Round("Tattoo", new Vector3(0.096f, 0.096f, 0.024f), Look.Person(TattooInk, 0.3f), arm, new Vector3(0f, 0f, z), false, 3f);

                // Legs: a round trouser leg and a bean of a shoe that pokes out in front.
                var leg = Pivot(roll, side < 0f ? "Leg L" : "Leg R", new Vector3(side * 0.078f, 0f, 0.215f));
                Round("Trousers", new Vector3(0.155f, 0.165f, 0.14f), trousers, leg, new Vector3(0f, 0f, 0.05f));
                Round("Shoe", new Vector3(0.15f, 0.205f, 0.085f), Look.Person(ShoeColor, 0.55f), leg, new Vector3(0f, -0.035f, 0.1f));
            }
            BuildHead(roll, skin, skinMat);
        }

        static void BuildTorso(Transform roll, SkinDef skin, Material shirt, Material trousers)
        {
            // Origin at the waist. A jelly-bean shirt over round hips, with a belt ring where they meet.
            var torso = Pivot(roll, "Torso", new Vector3(0f, 0f, 0.25f));
            Round("Shirt", new Vector3(0.39f, 0.33f, 0.35f), shirt, torso, new Vector3(0f, 0f, -0.13f), true, 2.5f);
            Round("Waist", new Vector3(0.36f, 0.305f, 0.17f), trousers, torso, new Vector3(0f, 0f, 0.0f), false, 2.5f);
            Round("Belt", new Vector3(0.368f, 0.312f, 0.036f), Look.Person(BeltColor, 0.35f), torso, new Vector3(0f, 0f, -0.03f), false, 3.2f);
            Round("Buckle", new Vector3(0.052f, 0.03f, 0.036f), Look.Person(BrassColor, 0.7f), torso, new Vector3(0f, -0.153f, -0.03f));

            // A collar of two small rounded tabs on the top front, and the loosened tie hanging below.
            foreach (float side in new[] { -1f, 1f })
            {
                var flap = Round("Collar", new Vector3(0.075f, 0.03f, 0.045f), shirt, torso, new Vector3(side * 0.042f, -0.112f, -0.262f));
                flap.transform.localRotation = Quaternion.Euler(-30f, side * 25f, 0f);
            }
            var tieMat = Look.Person(skin.Body, 0.3f);
            Round("Knot", new Vector3(0.046f, 0.032f, 0.042f), tieMat, torso, new Vector3(0.004f, -0.132f, -0.24f));
            var tie = Pivot(torso, "Tie", new Vector3(0.006f, -0.148f, -0.222f));
            tie.localRotation = Quaternion.Euler(14f, 0f, 6f);
            Round("Blade", new Vector3(0.055f, 0.02f, 0.13f), tieMat, tie, new Vector3(0f, 0f, 0.068f), false, 2.8f);
        }

        static void BuildHead(Transform roll, SkinDef skin, Material skinMat)
        {
            // Sits a little back over the shoulders, so the shirt and tie show under the chin from the game camera.
            // A round head tipped back by HeadPitch: its front is the face, at HeadRadius along FaceDir.
            var head = Pivot(roll, "Head", new Vector3(0f, 0.04f, -0.2f));
            var frame = Pivot(head, "Frame", Vector3.zero);
            frame.localRotation = Tilt;
            Round("Skull", Skull * 2f, skinMat, frame, Vector3.zero, true, 2.3f).transform.SetParent(head, true);
            foreach (float side in new[] { -1f, 1f })
                Round("Ear", new Vector3(0.06f, 0.08f, 0.1f), skinMat, frame, new Vector3(side * 0.28f, 0.03f, 0.03f));

            var hair = Look.Person(skin.HairColor, 0.18f);
            BuildHair(frame, skin, hair);
            if ((skin.Extras & PersonLook.Earrings) != 0)
                foreach (float side in new[] { -1f, 1f })
                    Round("Earring", Vector3.one * 0.042f, Look.Person(BrassColor, 0.7f), frame, new Vector3(side * 0.3f, 0.03f, 0.1f));
            if ((skin.Extras & PersonLook.Beard) != 0)  // a round beard under the mouth, in the hair color
                Round("Beard", new Vector3(0.3f, 0.12f, 0.12f), hair, frame, new Vector3(0f, -0.15f, 0.215f), false, 2.4f);

            // Above the hair, a little toward the back of the crown, square to the top of the head.
            var acc = Pivot(head, "Accessory", Tilt * new Vector3(0f, 0.03f, -0.285f));
            acc.localRotation = Tilt;
            acc.localScale = Vector3.one * 1.05f;
            // Worn pushed back: the face looks up at the camera, and a level brim would cover the eyes.
            if (skin.Accessory == Accessory.Cap) acc.localRotation = Tilt * Quaternion.Euler(20f, 0f, 0f);
            LevelBuilder.BuildAccessory(acc, skin);
        }

        // Hair, in the head's tipped frame: -Y is the face, -Z the top of the head. Each style starts from a shell,
        // a slightly bigger head shape shifted back and up, so it covers the crown, the back and the sides while the
        // face pushes through in front, below a soft hairline. Puffs, curls and buns sit on top of the shell.
        static void BuildHair(Transform f, SkinDef skin, Material hair)
        {
            switch (skin.Hair)
            {
                case HairStyle.Bob:
                    // A chin-length bob: a full shell, long rounded sides, and a side-swept fringe.
                    Shell(f, hair, 1.07f, 0.05f, 0.02f);
                    foreach (float side in new[] { -1f, 1f })
                        Round("Side", new Vector3(0.11f, 0.36f, 0.4f), hair, f, new Vector3(side * 0.255f, 0.06f, 0.04f));
                    Round("Back", new Vector3(0.5f, 0.2f, 0.42f), hair, f, new Vector3(0f, 0.17f, 0.02f));
                    Round("Fringe", new Vector3(0.3f, 0.1f, 0.12f), hair, f, new Vector3(-0.085f, -0.2f, -0.17f)).transform.localRotation = Quaternion.Euler(0f, -10f, -14f);
                    break;
                case HairStyle.Bun:
                    // Pulled back smooth into a round bun on top, with a tie around it.
                    Shell(f, hair, 1.04f, 0.055f, 0.03f);
                    Bangs(f, hair, 0.85f, 0f);
                    Round("Bun", new Vector3(0.21f, 0.21f, 0.19f), hair, f, new Vector3(0f, 0.11f, -0.33f));
                    Round("Hair Tie", new Vector3(0.17f, 0.17f, 0.045f), Look.Person(skin.Body, 0.3f), f, new Vector3(0f, 0.11f, -0.265f), false, 2.8f);
                    break;
                case HairStyle.Short:
                    // Short and neat, with a little quiff that has given up on the week.
                    Shell(f, hair, 1.045f, 0.06f, 0.035f);
                    Bangs(f, hair, 0.9f, 1f);
                    foreach (float side in new[] { -1f, 1f })
                        Round("Sideburn", new Vector3(0.06f, 0.12f, 0.15f), hair, f, new Vector3(side * 0.282f, 0.0f, -0.1f));
                    Round("Quiff", new Vector3(0.22f, 0.14f, 0.11f), hair, f, new Vector3(0.06f, -0.17f, -0.25f)).transform.localRotation = Quaternion.Euler(-10f, 0f, -8f);
                    break;
                case HairStyle.Mohawk:
                    // Shaved close at the sides, with a tall crest from forehead to crown (OUT-30).
                    Shell(f, hair, 1.015f, 0.05f, 0.015f);
                    for (int i = 0; i < 6; i++)
                    {
                        float k = i / 5f;
                        var crest = Round("Crest", new Vector3(0.12f, 0.15f, 0.22f + 0.07f * Mathf.Sin(k * Mathf.PI)), hair, f,
                            new Vector3(0f, Mathf.Lerp(-0.16f, 0.22f, k), -0.29f - 0.07f * Mathf.Sin(k * Mathf.PI)));
                        crest.transform.localRotation = Quaternion.Euler(Mathf.Lerp(-28f, 30f, k), 0f, 0f);
                    }
                    break;
                case HairStyle.Long:
                    // Long and loose: past the shoulders at the sides and back, with soft bangs.
                    Shell(f, hair, 1.06f, 0.05f, 0.025f);
                    Bangs(f, hair, 0.95f, -1f);
                    foreach (float side in new[] { -1f, 1f })
                        Round("Side", new Vector3(0.13f, 0.3f, 0.62f), hair, f, new Vector3(side * 0.255f, 0.07f, 0.14f));
                    Round("Back", new Vector3(0.52f, 0.2f, 0.64f), hair, f, new Vector3(0f, 0.18f, 0.12f));
                    break;
                case HairStyle.Afro:
                    // A big round afro: the shell set well back so the face still shows in front.
                    Shell(f, hair, 1.32f, 0.13f, 0.08f);
                    break;
                case HairStyle.Spiky:
                    // Gelled spikes all over the crown.
                    Shell(f, hair, 1.035f, 0.055f, 0.03f);
                    Bangs(f, hair, 0.8f, 1f);
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i / 7f * Mathf.PI * 2f;
                        var dir = new Vector3(Mathf.Sin(a) * 0.55f, Mathf.Cos(a) * 0.55f + 0.15f, -1f).normalized;
                        var spike = Round("Spike", new Vector3(0.085f, 0.085f, 0.2f), hair, f, dir * 0.27f + new Vector3(0f, 0.04f, 0f), false, 2.1f);
                        spike.transform.localRotation = Quaternion.FromToRotation(Vector3.back, dir);
                    }
                    break;
                case HairStyle.Ponytail:
                    // Pulled back into a high ponytail, with a tie.
                    Shell(f, hair, 1.04f, 0.055f, 0.03f);
                    Bangs(f, hair, 0.85f, 0f);
                    Round("Hair Tie", new Vector3(0.1f, 0.06f, 0.1f), Look.Person(skin.Body, 0.3f), f, new Vector3(0f, 0.28f, -0.14f), false, 2.8f);
                    Round("Tail", new Vector3(0.15f, 0.17f, 0.36f), hair, f, new Vector3(0f, 0.33f, 0.04f)).transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);
                    break;
                case HairStyle.Buzz:
                    // A neat buzz cut, close to the head.
                    Shell(f, hair, 1.03f, 0.05f, 0.022f);
                    break;
                default:
                    // Big curls: a thick shell under a crown of round lumps.
                    Shell(f, hair, 1.08f, 0.05f, 0.03f);
                    Bangs(f, hair, 1.15f, 0f);
                    var rng = new System.Random(5);
                    for (int i = 0; i < 9; i++)
                    {
                        // Two rings of curls over the crown and toward the back; none in front of the hairline.
                        float a = (i < 5 ? i / 4f : (i - 5) / 3f) * Mathf.PI - Mathf.PI * 0.5f;
                        float ring = i < 5 ? 0.2f : 0.1f;
                        var pos = new Vector3(Mathf.Sin(a) * ring, (i < 5 ? -0.02f : 0.12f) + Mathf.Cos(a) * 0.08f, i < 5 ? -0.25f : -0.3f);
                        var lump = Round("Curl", Vector3.one * (0.14f + 0.03f * (float)rng.NextDouble()), hair, f, pos);
                        lump.transform.localRotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 40f);
                    }
                    break;
            }
        }

        // Soft bangs: a row of round puffs along the hairline, above the brows, so the hair reads as hair and not as
        // a helmet (OUT-29). `fullness` scales the puffs; `sweep` tilts the row to one side.
        static void Bangs(Transform f, Material hair, float fullness, float sweep)
        {
            const float above = 37f * Mathf.Deg2Rad;  // how far above the face center the hairline sits
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 0.115f;
                float lift = above + sweep * (i - 1) * 0.08f;
                var at = new Vector3(x, -Mathf.Cos(lift) * Skull.y * 0.98f, -Mathf.Sin(lift) * Skull.z * 1.02f);
                var puff = Round("Bang", new Vector3(0.15f, 0.085f, 0.1f) * fullness, hair, f, at);
                puff.transform.localRotation = Quaternion.Euler(-lift * Mathf.Rad2Deg * 0.6f, 0f, (i - 1) * -12f + sweep * 10f);
            }
        }

        // The base of every style: the head's own shape, `scale` times bigger, shifted `back` toward the back of the
        // head and `up` toward its top. The face shows where the head pokes out of it.
        static void Shell(Transform f, Material hair, float scale, float back, float up) =>
            Round("Hair", Skull * 2f * scale, hair, f, new Vector3(0f, back, -up), false, 2.3f);

        // A round part of the given full size, centered at `center`.
        static MeshRenderer Round(string name, Vector3 size, Material m, Transform parent, Vector3 center, bool shadows = false, float exponent = 2.4f) =>
            Look.AddMesh(name, Meshes.Blob(size, exponent), m, parent, center, shadows);

        static Transform Pivot(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }
    }
}
