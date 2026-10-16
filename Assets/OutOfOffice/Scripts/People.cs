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

        // The head, in its tipped frame: half sizes. The front (-Y) sits at Mascot.HeadRadius, where the face is printed.
        static readonly Vector3 Skull = new Vector3(0.285f, Mascot.HeadRadius, 0.26f);

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
                var arm = Pivot(roll, side < 0f ? "Arm L" : "Arm R", new Vector3(side * 0.2f, 0f, 0.07f));
                arm.localRotation = Quaternion.Euler(0f, side * 8f, 0f);
                Round("Sleeve", new Vector3(0.12f, 0.115f, 0.12f), shirt, arm, new Vector3(0f, 0f, 0.045f));
                Round("Forearm", new Vector3(0.075f, 0.075f, 0.1f), skinMat, arm, new Vector3(0f, 0f, 0.11f));
                Round("Hand", new Vector3(0.1f, 0.095f, 0.09f), skinMat, arm, new Vector3(0f, -0.004f, 0.165f));

                // Legs: a round trouser leg and a bean of a shoe that pokes out in front.
                var leg = Pivot(roll, side < 0f ? "Leg L" : "Leg R", new Vector3(side * 0.078f, 0f, 0.215f));
                Round("Trousers", new Vector3(0.14f, 0.15f, 0.14f), trousers, leg, new Vector3(0f, 0f, 0.05f));
                Round("Shoe", new Vector3(0.135f, 0.19f, 0.075f), Look.Person(ShoeColor, 0.55f), leg, new Vector3(0f, -0.03f, 0.1f));
            }
            BuildHead(roll, skin, skinMat);
        }

        static void BuildTorso(Transform roll, SkinDef skin, Material shirt, Material trousers)
        {
            // Origin at the waist. A jelly-bean shirt over round hips, with a belt ring where they meet.
            var torso = Pivot(roll, "Torso", new Vector3(0f, 0f, 0.25f));
            Round("Shirt", new Vector3(0.36f, 0.3f, 0.34f), shirt, torso, new Vector3(0f, 0f, -0.13f), true, 2.6f);
            Round("Waist", new Vector3(0.33f, 0.28f, 0.16f), trousers, torso, new Vector3(0f, 0f, 0.0f), false, 2.6f);
            Round("Belt", new Vector3(0.336f, 0.286f, 0.036f), Look.Person(BeltColor, 0.35f), torso, new Vector3(0f, 0f, -0.03f), false, 3.2f);
            Round("Buckle", new Vector3(0.052f, 0.03f, 0.036f), Look.Person(BrassColor, 0.7f), torso, new Vector3(0f, -0.141f, -0.03f));

            // A collar of two small rounded tabs on the top front, and the loosened tie hanging below.
            foreach (float side in new[] { -1f, 1f })
            {
                var flap = Round("Collar", new Vector3(0.075f, 0.03f, 0.045f), shirt, torso, new Vector3(side * 0.04f, -0.104f, -0.262f));
                flap.transform.localRotation = Quaternion.Euler(-30f, side * 25f, 0f);
            }
            var tieMat = Look.Person(skin.Body, 0.3f);
            Round("Knot", new Vector3(0.046f, 0.032f, 0.042f), tieMat, torso, new Vector3(0.004f, -0.122f, -0.24f));
            var tie = Pivot(torso, "Tie", new Vector3(0.006f, -0.136f, -0.222f));
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
                Round("Ear", new Vector3(0.06f, 0.08f, 0.1f), skinMat, frame, new Vector3(side * 0.292f, 0.03f, 0.03f));

            var hair = Look.Person(skin.HairColor, 0.18f);
            BuildHair(frame, skin, hair);

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
                    Round("Bun", new Vector3(0.21f, 0.21f, 0.19f), hair, f, new Vector3(0f, 0.11f, -0.33f));
                    Round("Hair Tie", new Vector3(0.17f, 0.17f, 0.045f), Look.Person(skin.Body, 0.3f), f, new Vector3(0f, 0.11f, -0.265f), false, 2.8f);
                    break;
                case HairStyle.Short:
                    // Short and neat, with a little quiff that has given up on the week.
                    Shell(f, hair, 1.045f, 0.06f, 0.035f);
                    foreach (float side in new[] { -1f, 1f })
                        Round("Sideburn", new Vector3(0.06f, 0.12f, 0.15f), hair, f, new Vector3(side * 0.282f, 0.0f, -0.1f));
                    Round("Quiff", new Vector3(0.22f, 0.14f, 0.11f), hair, f, new Vector3(0.06f, -0.17f, -0.25f)).transform.localRotation = Quaternion.Euler(-10f, 0f, -8f);
                    break;
                default:
                    // Big curls: a thick shell under a crown of round lumps.
                    Shell(f, hair, 1.08f, 0.05f, 0.03f);
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
