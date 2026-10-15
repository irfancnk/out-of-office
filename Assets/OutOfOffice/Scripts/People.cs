using UnityEngine;

namespace OutOfOffice
{
    // The office staff, in soft blocks (boxes with rounded edges): one rig shared by the employee and the coworkers, built as named parts under one "Roll"
    // pivot at the collision ball's center (Mascot finds them by name). +Z is down, toward the board; -Y is the front.
    // The head stays upright, but the face and hairline are laid out around FaceDir, the front of the head tipped
    // up toward the camera, so the face reads straight on from the game's high camera and the hair can never cover it.
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

        public static void Build(Transform parent, SkinDef skin)
        {
            var roll = Pivot(parent, "Roll", new Vector3(0f, 0f, -Mascot.BodyHalfHeight));
            var skinMat = Look.Person(skin.SkinTone, 0.12f);
            var shirt = Look.Person(ShirtColor, 0.12f);
            var trousers = Look.Person(TrouserColor, 0.15f);

            BuildTorso(roll, skin, shirt, trousers, skinMat);
            foreach (float side in new[] { -1f, 1f })
            {
                // Arms: a short white sleeve, a forearm, and a chunky block hand.
                var arm = Pivot(roll, side < 0f ? "Arm L" : "Arm R", new Vector3(side * 0.205f, 0f, 0.07f));
                arm.localRotation = Quaternion.Euler(0f, side * 8f, 0f);
                Block("Sleeve", new Vector3(0.1f, 0.1f, 0.1f), 0.035f, shirt, arm, new Vector3(0f, 0f, 0.045f));
                Block("Forearm", new Vector3(0.07f, 0.07f, 0.07f), 0.022f, skinMat, arm, new Vector3(0f, 0f, 0.115f));
                Block("Hand", new Vector3(0.085f, 0.085f, 0.08f), 0.03f, skinMat, arm, new Vector3(0f, -0.004f, 0.165f));

                // Legs: a trouser block and a shoe that pokes out in front.
                var leg = Pivot(roll, side < 0f ? "Leg L" : "Leg R", new Vector3(side * 0.08f, 0f, 0.215f));
                Block("Trousers", new Vector3(0.13f, 0.14f, 0.12f), 0.04f, trousers, leg, new Vector3(0f, 0f, 0.055f));
                Block("Shoe", new Vector3(0.13f, 0.18f, 0.06f), 0.026f, Look.Person(ShoeColor, 0.55f), leg, new Vector3(0f, -0.025f, 0.095f));
            }
            BuildHead(roll, skin, skinMat);
        }

        static void BuildTorso(Transform roll, SkinDef skin, Material shirt, Material trousers, Material skinMat)
        {
            // Origin at the waist. A soft block of a shirt, tucked into the trousers under a belt.
            var torso = Pivot(roll, "Torso", new Vector3(0f, 0f, 0.25f));
            Block("Shirt", new Vector3(0.34f, 0.28f, 0.27f), 0.075f, shirt, torso, new Vector3(0f, 0f, -0.135f), true);
            Block("Waist", new Vector3(0.345f, 0.285f, 0.07f), 0.03f, trousers, torso, new Vector3(0f, 0f, 0.0f));
            Block("Belt", new Vector3(0.35f, 0.29f, 0.026f), 0.012f, Look.Person(BeltColor, 0.35f), torso, new Vector3(0f, 0f, -0.024f));
            Block("Buckle", new Vector3(0.05f, 0.02f, 0.032f), 0.008f, Look.Person(BrassColor, 0.7f), torso, new Vector3(0f, -0.148f, -0.024f));

            // A collar of two small tabs on the top front edge, and the loosened tie hanging below.
            foreach (float side in new[] { -1f, 1f })
            {
                var flap = Block("Collar", new Vector3(0.07f, 0.03f, 0.045f), 0.012f, shirt, torso, new Vector3(side * 0.04f, -0.13f, -0.262f));
                flap.transform.localRotation = Quaternion.Euler(0f, side * 25f, 0f);
            }
            var tieMat = Look.Person(skin.Body, 0.3f);
            Block("Knot", new Vector3(0.042f, 0.028f, 0.04f), 0.012f, tieMat, torso, new Vector3(0.004f, -0.146f, -0.24f));
            var tie = Pivot(torso, "Tie", new Vector3(0.006f, -0.148f, -0.222f));
            tie.localRotation = Quaternion.Euler(8f, 0f, 6f);
            Block("Blade", new Vector3(0.05f, 0.016f, 0.13f), 0.008f, tieMat, tie, new Vector3(0f, 0f, 0.068f));
        }

        static void BuildHead(Transform roll, SkinDef skin, Material skinMat)
        {
            // Sits a little back over the shoulders, so the shirt and tie show under the chin from the game camera.
            // The head is a soft cube tipped back by HeadPitch: its flat front is the face, at HeadRadius along FaceDir.
            var head = Pivot(roll, "Head", new Vector3(0f, 0.04f, -0.2f));
            var frame = Pivot(head, "Frame", Vector3.zero);
            frame.localRotation = Tilt;
            float R = Mascot.HeadRadius;
            Block("Skull", new Vector3(0.54f, 2f * R, 0.5f), 0.1f, skinMat, frame, Vector3.zero, true).transform.SetParent(head, true);
            foreach (float side in new[] { -1f, 1f })
                Block("Ear", new Vector3(0.05f, 0.08f, 0.1f), 0.02f, skinMat, frame, new Vector3(side * 0.282f, 0.02f, 0.03f));

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

        // Hair in soft blocks around the head cube, in its tipped frame: -Y is the face, -Z the top of the head.
        // Every style leaves the face (the front below the fringe) clear.
        static void BuildHair(Transform f, SkinDef skin, Material hair)
        {
            const float W = 0.575f;
            switch (skin.Hair)
            {
                case HairStyle.Bob:
                    // A chin-length bob: a cap, long sides, a full back, and a side-swept fringe.
                    Block("Hair", new Vector3(W, 0.56f, 0.13f), 0.06f, hair, f, new Vector3(0f, 0.005f, -0.215f));
                    Block("Back", new Vector3(W, 0.12f, 0.5f), 0.05f, hair, f, new Vector3(0f, 0.23f, -0.01f));
                    foreach (float side in new[] { -1f, 1f })
                        Block("Side", new Vector3(0.075f, 0.44f, 0.46f), 0.035f, hair, f, new Vector3(side * 0.268f, 0.04f, 0.0f));
                    Block("Fringe", new Vector3(0.3f, 0.06f, 0.1f), 0.03f, hair, f, new Vector3(-0.12f, -0.262f, -0.17f)).transform.localRotation = Quaternion.Euler(0f, -8f, 0f);
                    break;
                case HairStyle.Bun:
                    // Pulled back tight into a square bun on top.
                    Block("Hair", new Vector3(W, 0.56f, 0.1f), 0.05f, hair, f, new Vector3(0f, 0.005f, -0.225f));
                    Block("Back", new Vector3(W, 0.1f, 0.36f), 0.045f, hair, f, new Vector3(0f, 0.235f, -0.08f));
                    Block("Bun", new Vector3(0.2f, 0.2f, 0.17f), 0.07f, hair, f, new Vector3(0f, 0.13f, -0.33f));
                    Block("Hair Tie", new Vector3(0.21f, 0.21f, 0.04f), 0.02f, Look.Person(skin.Body, 0.3f), f, new Vector3(0f, 0.13f, -0.27f));
                    break;
                case HairStyle.Short:
                    // Short and neat, with a little quiff that has given up on the week.
                    Block("Hair", new Vector3(W, 0.56f, 0.1f), 0.05f, hair, f, new Vector3(0f, 0.005f, -0.225f));
                    Block("Back", new Vector3(W, 0.1f, 0.3f), 0.045f, hair, f, new Vector3(0f, 0.235f, -0.11f));
                    foreach (float side in new[] { -1f, 1f })
                        Block("Sideburn", new Vector3(0.05f, 0.14f, 0.16f), 0.02f, hair, f, new Vector3(side * 0.278f, 0.04f, -0.12f));
                    Block("Quiff", new Vector3(0.2f, 0.12f, 0.08f), 0.035f, hair, f, new Vector3(0.07f, -0.2f, -0.29f)).transform.localRotation = Quaternion.Euler(-10f, 0f, -8f);
                    break;
                default:
                    // Big curls: a thick cap with chunky lumps on top.
                    Block("Hair", new Vector3(W + 0.02f, 0.58f, 0.15f), 0.07f, hair, f, new Vector3(0f, 0.01f, -0.22f));
                    Block("Back", new Vector3(W + 0.02f, 0.13f, 0.42f), 0.06f, hair, f, new Vector3(0f, 0.235f, -0.04f));
                    var rng = new System.Random(5);
                    for (int i = 0; i < 7; i++)
                    {
                        float x = -0.2f + 0.4f * (i % 4) / 3f + (i >= 4 ? 0.07f : 0f), y = i < 4 ? -0.12f : 0.12f;
                        var lump = Block("Curl", Vector3.one * (0.13f + 0.03f * (float)rng.NextDouble()), 0.05f, hair, f, new Vector3(x, y, -0.3f));
                        lump.transform.localRotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 40f);
                    }
                    break;
            }
        }

        // A soft block of the given full size, centered at `center`.
        static MeshRenderer Block(string name, Vector3 size, float radius, Material m, Transform parent, Vector3 center, bool shadows = false) =>
            Look.AddMesh(name, Meshes.SoftBox(size, radius), m, parent, center, shadows);

        static Transform Pivot(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }
    }
}
