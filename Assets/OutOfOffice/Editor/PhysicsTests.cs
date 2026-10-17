using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Automated checks for the puck simulation, run with the real colliders and Puck.Step on the developer lab board.
    // Each test prints "HSTEST name PASS|FAIL details".
    public static class PhysicsTests
    {
        struct Frame
        {
            public Vector2 Pos, Vel;
            public StepInfo Info;
        }

        static StringBuilder log;
        static int failures;

        [MenuItem("Out of Office/Run Physics Tests")]
        static void RunFromMenu() => Run();

        public static void RunBatch()
        {
            Run();
            EditorApplication.Exit(failures == 0 ? 0 : 3);
        }

        public static int Run()
        {
            var previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            log = new StringBuilder("OUT OF OFFICE PHYSICS TESTS\n");
            failures = 0;
            var root = new GameObject("PhysicsTests");
            try
            {
                var lab = LevelBuilder.Build(Levels.Lab, root.transform, false);
                Tunneling(lab);
                Energy(lab);
                Materials(lab);
                Graze(lab);
                CurveRide(lab);
                Seams(lab);
                Rest(lab);
                Preview(lab);
                Penetration(lab);
                OrderParity(lab);
                Object.DestroyImmediate(lab.Root);
                RimLatch(root.transform);
                MovingHit(root.transform);
                MeshRelease(root.transform);
                Crystal(root.transform);
                Belt(root.transform);
                NewParts(root.transform);
                CoffeeMachine(root.transform);
                Gadgets(root.transform);
                MoversClear(root.transform);
                JumpLanding(root.transform);
                RimSeam();
                Capture(null);
                UndoRestore(root.transform);
                PortalLoop(root.transform);
                SaveMigration();
                Wardrobe();
                OutfitSlots();
                StaffRoster();
                DailyClock();
                SaveFlush();
                ChapterChest();
                Openers();
                CoffeeCard();
                ShotLimits();
                Money();
                Undos();
                EnergyPlays();
                QualityTiers();
                GadgetGuide();
                BoosterChecks(root.transform);
                ChestChecks();
                AchievementChecks();
                Data();
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.simulationMode = previousMode;
            }
            log.AppendLine($"HSTEST done, failures: {failures}");
            Debug.Log(log.ToString());
            return failures;
        }

        static void Report(string name, bool pass, string details)
        {
            if (!pass) failures++;
            log.AppendLine($"HSTEST {name} {(pass ? "PASS" : "FAIL")} {details}");
        }

        // Runs one shot in the same order as the game: each fixed step ticks the movers, runs Puck.Step (which reads
        // the contacts of the previous simulation, like FixedUpdate does), then simulates. simulateFirst flips the
        // order, only for the parity test.
        static List<Frame> Simulate(LevelInstance inst, Vector2 start, Vector2 velocity, int maxSteps = 1800, bool simulateFirst = false)
        {
            var frames = new List<Frame>();
            inst.TickMovers(0f, true);
            inst.Puck.PlaceAt(start);
            Settle();
            inst.Puck.Launch(velocity);
            for (int i = 0; i < maxSteps; i++)
            {
                inst.TickMovers((i + 1) * Rules.Dt, false);
                if (simulateFirst) Physics2D.Simulate(Rules.Dt);
                var info = inst.Puck.Step(Rules.Dt);
                frames.Add(new Frame { Pos = inst.Puck.Body.position, Vel = inst.Puck.Body.linearVelocity, Info = info });
                if (info.Result != ShotResult.None) break;
                if (!simulateFirst) Physics2D.Simulate(Rules.Dt);
            }
            return frames;
        }

        // 1. Fast shots never pass through the thin wall (0.3 thick) at (-3.2, -3.5).
        static void Tunneling(LevelInstance lab)
        {
            int shots = 0, tunnels = 0;
            foreach (float speed in new[] { 20f, 30f, 45f })
            for (float x = -3.9f; x <= -2.5f; x += 0.1f)
            for (float a = -20f; a <= 20f; a += 5f)
            {
                shots++;
                var v = Quaternion.Euler(0f, 0f, a) * Vector2.up * speed;
                var frames = Simulate(lab, new Vector2(x, -5.6f), v, 120);
                for (int i = 1; i < frames.Count; i++)
                {
                    Vector2 p0 = frames[i - 1].Pos, p1 = frames[i].Pos;
                    bool crossed = p0.y < -3.5f && p1.y >= -3.5f || p1.y > -3.15f && p0.y <= -3.15f;
                    float xAt = p1.x;
                    if (crossed && xAt > -4.2f + 0.3f && xAt < -2.2f - 0.36f) { tunnels++; break; }
                }
            }
            Report("tunneling", tunnels == 0, $"{shots} shots up to 45 u/s (max shot 20), crossings through the wall: {tunnels}");
        }

        // 2. Stationary walls never add energy: speed never rises from one step to the next away from the hole.
        static void Energy(LevelInstance lab)
        {
            int shots = 0, violations = 0;
            float worst = 0f;
            var rng = new System.Random(5);
            for (int s = 0; s < 300; s++)
            {
                shots++;
                float a = (float)rng.NextDouble() * 360f, speed = 4f + (float)rng.NextDouble() * 16f;
                var frames = Simulate(lab, new Vector2(0f, -6f), Quaternion.Euler(0f, 0f, a) * Vector2.up * speed);
                for (int i = 1; i < frames.Count; i++)
                {
                    if ((frames[i].Pos - lab.Puck.Hole).magnitude < Rules.HoleRadius + 0.5f) continue;
                    float before = frames[i - 1].Vel.magnitude, after = frames[i].Vel.magnitude;
                    float gain = after - before;
                    if (gain > 0.005f + before * 0.002f)
                    {
                        violations++;
                        worst = Mathf.Max(worst, gain);
                    }
                }
            }
            Report("energy", violations == 0, $"{shots} random shots, speed gains at walls: {violations} (worst +{worst:0.000} u/s)");
        }

        // In the game the physics always runs at least one step between placing the puck and the next shot, so
        // its contact list is fresh. Do the same here.
        public static void Settle()
        {
            Physics2D.SyncTransforms();
            Physics2D.Simulate(Rules.Dt);
        }

        // Contact correction never forces the puck into a surface: across many random shots on the lab (flat walls,
        // convex corners, the concave guide, the narrow channel), the deepest overlap after Puck.Step stays at the
        // level Box2D itself reaches while riding a curve.
        static void Penetration(LevelInstance lab)
        {
            var rng = new System.Random(17);
            var contacts = new Collider2D[8];
            float worst = 0f;
            int shots = 0, deep = 0;
            for (int s = 0; s < 200; s++)
            {
                shots++;
                float a = (float)rng.NextDouble() * 360f, speed = 3f + (float)rng.NextDouble() * 17f;
                var start = new Vector2(((float)rng.NextDouble() - 0.5f) * 6f, -6f + (float)rng.NextDouble() * 2f);
                lab.TickMovers(0f, true);
                lab.Puck.PlaceAt(start);
                Settle();
                lab.Puck.Launch(Quaternion.Euler(0f, 0f, a) * Vector2.up * speed);
                bool counted = false;
                for (int i = 0; i < 900; i++)
                {
                    var info = lab.Puck.Step(Rules.Dt);
                    int n = lab.Puck.Body.GetContacts(contacts);
                    for (int k = 0; k < n; k++)
                    {
                        if (contacts[k].usedByEffector) continue;
                        var d = Physics2D.Distance(lab.Puck.Collider, contacts[k]);
                        if (!d.isValid) continue;
                        worst = Mathf.Min(worst, d.distance);
                        if (d.distance < -0.03f && !counted) { deep++; counted = true; }
                    }
                    if (info.Result != ShotResult.None) break;
                    Physics2D.Simulate(Rules.Dt);
                }
            }
            Report("penetration", deep == 0, $"{shots} random shots, deepest overlap after correction {worst:0.0000} units (Box2D rests at {Rules.ContactRestGap}), shots deeper than 0.03: {deep}");
        }

        // The game steps the puck before each automatic simulation; older tests simulated first. Both orders must agree,
        // so the checks describe the game.
        static void OrderParity(LevelInstance lab)
        {
            var rng = new System.Random(23);
            int shots = 0, same = 0;
            float worst = 0f;
            for (int s = 0; s < 60; s++)
            {
                shots++;
                float a = (float)rng.NextDouble() * 360f, speed = 3f + (float)rng.NextDouble() * 12f;
                var v = Quaternion.Euler(0f, 0f, a) * Vector2.up * speed;
                var game = Simulate(lab, new Vector2(0f, -6f), v);
                var old = Simulate(lab, new Vector2(0f, -6f), v, 1800, true);
                var g = game[game.Count - 1];
                var o = old[old.Count - 1];
                float d = (g.Pos - o.Pos).magnitude;
                if (g.Info.Result == o.Info.Result && d < 0.25f) same++;
                worst = Mathf.Max(worst, d);
            }
            Report("order-parity", same >= shots * 0.9f, $"{shots} shots, same outcome and end within 0.25 units: {same}; largest end difference {worst:0.00}");
        }

        // The rim sound fires once per approach: on fast passes that skip the middle band, on slow ones, and again
        // only after the puck has left the outer band.
        static void RimLatch(Transform parent)
        {
            var root = new GameObject("RimLatch");
            root.transform.SetParent(parent, false);
            var inst = LevelBuilder.Build(new LevelDef("rimlatch", "Rim Latch", 0f, 2f), root.transform, false);
            int passes = 0, wrong = 0;
            var notes = new StringBuilder();
            foreach (float speed in new[] { 5f, 9f, 15f, 19f })
            foreach (float offset in new[] { 0.55f, 0.75f, 0.9f })
            {
                passes++;
                var frames = Simulate(inst, new Vector2(offset, -0.6f), Vector2.up * speed, 400);
                int events = 0;
                foreach (var f in frames)
                {
                    if (f.Pos.y > 3.5f) break;  // count one pass; a rebound off the far wall is a new approach
                    if (f.Info.RimTouch) events++;
                }
                int expected = offset < Rules.HoleRadius + Rules.PuckRadius * 0.6f ? 1 : 0;
                if (events != expected)
                {
                    wrong++;
                    notes.Append($"[v{speed} off{offset}: {events}] ");
                }
            }
            Object.DestroyImmediate(root);
            Report("rim-latch", wrong == 0, $"{passes} passes past the hole, wrong event counts: {wrong} {notes}");
        }

        // Fixed boards for mechanic tests, so level design can change freely without moving the tests.
        static class Fixtures
        {
            public static readonly LevelDef Spinner = new LevelDef("fx-spinner", "Spinner fixture", 0f, 4.6f)
                .WithShape(ShapeDef.Stadium(9f, 16f))
                .StartAt(0f, -5.5f)
                .Bouncer(0f, 1.4f, 5f, 0.55f).Spinning(60f);

            public static readonly LevelDef Crystal = new LevelDef("fx-crystal", "Crystal fixture", 0f, 5.8f)
                .Breakable(-3f, 3.4f, 3f, 0.6f)
                .Breakable(0f, 3.4f, 3f, 0.6f)
                .Breakable(3f, 3.4f, 3f, 0.6f);

            public static readonly LevelDef Parts = new LevelDef("fx-parts", "New parts fixture", 0f, 7.2f)
                .Wind(-3f, -3f, 2f, 6f)
                .Vortex(2.5f, -4f, 1.8f)
                .Kicker(2.5f, 3.5f, 0.5f)
                .Spikes(-2.2f, 4f, 2f, 1f, 2f, 0f)
                .Spikes(0.2f, 4f, 2f, 1f, 2f, 0.5f);

            public static readonly LevelDef Gadgets = new LevelDef("fx-gadgets", "Gadget fixture", 0f, 7.2f)
                .OneWay(-2.5f, 0f, 3f, 0f)
                .Jump(2.5f, -4f, 0.8f)
                .Wall(2.5f, -1.5f, 3f, 0.5f)
                .PopWall(0f, 4f, 9f, 0.5f, 2f, 0f);

            public static readonly LevelDef Belt = new LevelDef("fx-belt", "Belt fixture", 3f, 5.4f)
                .StartAt(-2.6f, -6f)
                .Conveyor(0f, 1.4f, 2.2f, 9f, -90f)
                .Wall(-1.6f, 3.6f, 5.8f, 0.5f);

            public static readonly LevelDef Machine = new LevelDef("fx-machine", "Coffee machine fixture", 0f, 6.4f)
                .Magnet(0f, 0f, 1.8f);
        }

        // A moving obstacle that hits a resting puck counts as a hard hit (relative speed), like the game wakes it.
        static void MovingHit(Transform parent)
        {
            var root = new GameObject("MovingHit");
            root.transform.SetParent(parent, false);
            var def = Fixtures.Spinner;
            var inst = LevelBuilder.Build(def, root.transform, false);
            // The spinner (5 long, at (0, 1.4), 60 deg/s) sweeps through a puck resting 1.9 from its center.
            Vector2 center = new Vector2(0f, 1.4f);
            Vector2 pos = center + new Vector2(Mathf.Cos(30f * Mathf.Deg2Rad), Mathf.Sin(30f * Mathf.Deg2Rad)) * 1.9f;
            inst.TickMovers(0f, true);
            inst.Puck.PlaceAt(pos);
            Physics2D.SyncTransforms();
            float impact = -1f, armSpeed = def.Blocks[0].Spin * Mathf.Deg2Rad * 1.9f;
            bool moving = false;
            for (int i = 0; i < 240 && impact < 0f; i++)
            {
                inst.TickMovers((i + 1) * Rules.Dt, false);
                if (!moving && inst.Puck.Body.linearVelocity.sqrMagnitude > 0.09f)
                {
                    inst.Puck.Wake();
                    moving = true;
                }
                if (moving)
                {
                    var info = inst.Puck.Step(Rules.Dt);
                    if (info.Impact) impact = info.ImpactSpeed;
                }
                Physics2D.Simulate(Rules.Dt);
            }
            Object.DestroyImmediate(root);
            Report("moving-hit", impact >= armSpeed * 0.6f, $"spinner arm at {armSpeed:0.00} u/s hits a resting puck: impact speed {impact:0.00}");
        }

        // Crystal blocks: a hard hit shatters the block and the puck carries on through with part of its speed;
        // a soft hit bounces off and leaves it whole. Undo brings it back.
        static void Crystal(Transform parent)
        {
            var root = new GameObject("Crystal");
            root.transform.SetParent(parent, false);
            var inst = LevelBuilder.Build(Fixtures.Crystal, root.transform, false);
            var start = inst.Capture(inst.Def.Start, 0, 0f);
            var hard = Simulate(inst, new Vector2(1.2f, -2f), Vector2.up * 14f, 300);
            bool broke = hard.Exists(f => f.Info.Broke), through = hard.Exists(f => f.Pos.y > 3.8f);
            inst.Restore(start);
            bool restored = !inst.Breakables[1].Broken && inst.Breakables[1].Collider.enabled;
            var soft = Simulate(inst, new Vector2(1.2f, 1.6f), Vector2.up * 4f, 300);
            bool softBroke = soft.Exists(f => f.Info.Broke), softBounced = soft.Exists(f => f.Vel.y < -0.3f);
            Object.DestroyImmediate(root);
            Report("crystal", broke && through && restored && !softBroke && softBounced,
                $"hard hit breaks {broke}, passes through {through}; undo restores {restored}; soft hit breaks {softBroke}, bounces {softBounced}");
        }

        // Wind pushes along its arrow; a whirlpool swirls the puck counter-clockwise and inward; a kicker fires it
        // away at kick speed; pop-up spikes send it back only while they are up.
        static void NewParts(Transform parent)
        {
            var root = new GameObject("Parts");
            root.transform.SetParent(parent, false);
            var inst = LevelBuilder.Build(Fixtures.Parts, root.transform, false);
            var wind = Simulate(inst, new Vector2(-3f, -5.6f), Vector2.up * 0.5f, 40);
            float windSpeed = wind[wind.Count - 1].Vel.y;
            // Spinning platform (OUT-49): a puck dropped on it is carried round the way it turns and flung off the rim to
            // rest clear of it; a shot across it leaves bent the way it turns.
            var hub = new Vector2(2.5f, -4f);
            var swirl = Simulate(inst, new Vector2(3.6f, -4f), new Vector2(0f, 0.05f), 900);
            Vector2 sv = swirl[Mathf.Min(29, swirl.Count - 1)].Vel, sp = swirl[swirl.Count - 1].Pos;
            bool swirls = sv.y > 0.5f && (sp - hub).magnitude > 1.8f + 0.3f && swirl[swirl.Count - 1].Info.Result == ShotResult.Rest;
            var across = Simulate(inst, new Vector2(3.3f, -6.4f), Vector2.up * 12f, 120);
            float bend = 0f;
            for (int i = 1; i < across.Count; i++)
                if ((across[i - 1].Pos - hub).magnitude < 1.8f && (across[i].Pos - hub).magnitude >= 1.8f)
                {
                    bend = Mathf.Atan2(-across[i].Vel.x, across[i].Vel.y) * Mathf.Rad2Deg;
                    break;
                }
            swirls &= bend > 12f;
            var kick = Simulate(inst, new Vector2(2.5f, 1.8f), Vector2.up * 4f, 90);
            bool kicked = kick.Exists(f => f.Info.Kicked) && kick.Exists(f => f.Vel.y < -Rules.KickSpeed * 0.8f);
            var up = Simulate(inst, new Vector2(-2.2f, 2.8f), Vector2.up * 4f, 60);
            var down = Simulate(inst, new Vector2(0.2f, 2.8f), Vector2.up * 4f, 60);
            bool upHurts = up.Exists(f => f.Info.Result == ShotResult.Hazard);
            bool downSafe = !down.Exists(f => f.Info.Result == ShotResult.Hazard) && down[down.Count - 1].Pos.y > 4.6f;
            // Pinned by a pushing zone: a belt shoving the puck into the rim must still end the shot.
            var settle = Simulate(inst, new Vector2(2.5f, -5.2f), new Vector2(0f, 0.05f), 900);
            bool vortexRests = settle[settle.Count - 1].Info.Result == ShotResult.Rest && settle.Count < 900;
            Object.DestroyImmediate(root);
            var beltRoot = new GameObject("Pinned");
            beltRoot.transform.SetParent(parent, false);
            var belt = LevelBuilder.Build(Fixtures.Belt, beltRoot.transform, false);
            var pinned = Simulate(belt, new Vector2(3.4f, 1.4f), new Vector2(0.3f, 0f), 600);
            bool beltRests = pinned[pinned.Count - 1].Info.Result == ShotResult.Rest && pinned.Count < 600;
            Object.DestroyImmediate(beltRoot);
            Report("pinned-rest", beltRests && vortexRests,
                $"belt into the rim rests after {pinned.Count * Rules.Dt:0.00} s; whirlpool settles after {settle.Count * Rules.Dt:0.00} s");
            Report("new-parts", windSpeed > 3f && swirls && kicked && upHurts && downSafe,
                $"wind speed after 0.67 s {windSpeed:0.0}; platform carries {sv.x:0.0},{sv.y:0.0} and flings to r {(sp - hub).magnitude:0.00}, " +
                $"bends a 12 u/s shot {bend:0} deg; kicked {kicked}; spikes up hurt {upHurts}, down safe {downSafe}");
        }

        // The coffee machine (OUT-49) pulls across its whole ring: a puck stopped near the edge is drawn in to the
        // machine, and a roll passing close bends toward it.
        static void CoffeeMachine(Transform parent)
        {
            var root = new GameObject("Machine");
            root.transform.SetParent(parent, false);
            var inst = LevelBuilder.Build(Fixtures.Machine, root.transform, false);
            var drawn = Simulate(inst, new Vector2(1.65f, 0f), new Vector2(0f, 0.02f), 600);
            float end = drawn[drawn.Count - 1].Pos.magnitude;
            bool rests = drawn[drawn.Count - 1].Info.Result == ShotResult.Rest;
            var pass = Simulate(inst, new Vector2(1.3f, -4f), Vector2.up * 10f, 120);
            float bend = 0f;
            for (int i = 1; i < pass.Count; i++)
                if (pass[i - 1].Pos.magnitude < 1.8f && pass[i].Pos.magnitude >= 1.8f)
                {
                    bend = Mathf.Atan2(-pass[i].Vel.x, pass[i].Vel.y) * Mathf.Rad2Deg;
                    break;
                }
            Object.DestroyImmediate(root);
            Report("coffee-machine", end < 0.9f && rests && bend > 8f,
                $"a stopped roll at r 1.65 is drawn to r {end:0.00} (rests {rests}); a 10 u/s roll passing at 1.3 bends {bend:0} deg toward it");
        }

        // One-way gate: passes along its arrow, bounces back against it. Trampoline: lifts the puck over a wall.
        // Pop wall: blocks while up, lets the puck through while down.
        static void Gadgets(Transform parent)
        {
            var root = new GameObject("Gadgets");
            root.transform.SetParent(parent, false);
            var inst = LevelBuilder.Build(Fixtures.Gadgets, root.transform, false);
            var along = Simulate(inst, new Vector2(-2.5f, -2f), Vector2.up * 8f, 120);
            bool passes = along.Exists(f => f.Pos.y > 1f);
            var against = Simulate(inst, new Vector2(-2.5f, 2f), Vector2.down * 8f, 120);
            bool blocked = !against.Exists(f => f.Pos.y < -0.6f) && against.Exists(f => f.Vel.y > 0.5f);
            var jump = Simulate(inst, new Vector2(2.5f, -6f), Vector2.up * 9f, 200);
            bool jumped = jump.Exists(f => f.Info.Jumped != null) && jump.Exists(f => f.Pos.y > -0.8f);
            var wallUp = Simulate(inst, new Vector2(0f, 2f), Vector2.up * 8f, 60);     // up during 0..1 s
            bool upBlocks = !wallUp.Exists(f => f.Pos.y > 4.6f);
            inst.TickMovers(1.2f, true);
            var frames = new List<Frame>();
            inst.Puck.PlaceAt(new Vector2(0f, 2.5f));
            Settle();
            inst.Puck.Launch(Vector2.up * 8f);
            bool downPasses = false;
            for (int i = 0; i < 40; i++)
            {
                inst.TickMovers(1.2f + (i + 1) * Rules.Dt, false);
                inst.Puck.Step(Rules.Dt);
                Physics2D.Simulate(Rules.Dt);
                if (inst.Puck.Body.position.y > 4.6f) downPasses = true;
            }
            Object.DestroyImmediate(root);
            Report("gadgets", passes && blocked && jumped && upBlocks && downPasses,
                $"one-way passes {passes}, blocks back {blocked}; trampoline clears the wall {jumped}; pop wall up blocks {upBlocks}, down passes {downPasses}");
        }

        // Jumps (trampolines and ramps) never land the puck inside a wall or a post, and never leave it outside the
        // floor, wherever they come down: random shots into every level with a jump, corners included.
        static void JumpLanding(Transform parent)
        {
            int shots = 0, landings = 0, inside = 0, outside = 0, fell = 0;
            string example = "";
            var rng = new System.Random(11);
            foreach (var def in Levels.All)
            {
                if (!def.Zones.Exists(z => z.Kind == ZoneKind.Jump || z.Kind == ZoneKind.Ramp)) continue;
                var root = new GameObject("Jumps " + def.Id);
                root.transform.SetParent(parent, false);
                var inst = LevelBuilder.Build(def, root.transform, false);
                var targets = def.Zones.FindAll(z => z.Kind == ZoneKind.Jump || z.Kind == ZoneKind.Ramp);
                for (int s = 0; s < 80; s++)
                {
                    shots++;
                    var z = targets[s % targets.Count];
                    // From the start, or from a random point near the jump, toward the jump with some spread.
                    Vector2 from = s % 2 == 0 ? def.Start : z.Center - z.Direction * (1.5f + (float)rng.NextDouble() * 2f) + new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f);
                    if (!InsideFloor(def, from, Rules.PuckRadius + 0.05f) || Physics2D.OverlapCircle(from, Rules.PuckRadius + 0.05f) != null) continue;
                    Vector2 dir = (z.Center - from).normalized;
                    dir = Quaternion.Euler(0f, 0f, ((float)rng.NextDouble() - 0.5f) * 50f) * dir;
                    var frames = Simulate(inst, from, dir * (6f + (float)rng.NextDouble() * 14f), 900);
                    if (frames.Count > 0 && frames[frames.Count - 1].Info.Result == ShotResult.Fell)
                    {
                        // Fell right after coming down from a jump that was still moving: the corner case players hit.
                        int last = frames.Count - 1, landed = frames.FindLastIndex(fr => fr.Info.Landed);
                        if (landed >= 0 && last - landed < 6 && frames[landed].Vel.magnitude > 2f)
                        {
                            fell++;
                            if (example.Length < 400) example += $" [{def.Id} from {from} dir {dir} landed {frames[landed].Pos} v {frames[landed].Vel} fell {frames[last].Pos}]";
                        }
                    }
                    for (int i = 0; i < frames.Count; i++)
                    {
                        if (!frames[i].Info.Landed) continue;
                        landings++;
                        // A few steps after touching down, the puck must be clear of every static block and on the floor.
                        // (A pacing hazard can still walk into a puck that has already landed; that is a normal hit.)
                        var f = frames[Mathf.Min(frames.Count - 1, i + 3)];
                        bool hit = false;
                        foreach (var c in inst.BlockColliders)
                            if (c != null && c.enabled && c.GetComponentInParent<Mover>() == null && c.OverlapPoint(f.Pos)) hit = true;
                        bool off = !InsideFloor(def, f.Pos, 0f);
                        if (hit) inside++;
                        if (off) outside++;
                        if ((hit || off) && example.Length < 400) example += $" [{def.Id} from {from} dir {dir} at {f.Pos}{(hit ? " in a block" : "")}{(off ? " off the floor" : "")} result {f.Info.Result}]";
                    }
                }
                Object.DestroyImmediate(root);
            }
            Report("jump-landing", inside == 0 && outside == 0 && fell == 0, $"{shots} shots, {landings} landings; inside a block {inside}, off the floor {outside}, into a pit on touchdown {fell} {example}");
        }

        static bool InsideFloor(LevelDef def, Vector2 p, float margin)
        {
            var o = def.Shape.Outline;
            bool inPoly = false;
            for (int i = 0, j = o.Count - 1; i < o.Count; j = i++)
                if ((o[i].y > p.y) != (o[j].y > p.y) && p.x < (o[j].x - o[i].x) * (p.y - o[i].y) / (o[j].y - o[i].y) + o[i].x) inPoly = !inPoly;
            if (!inPoly) return false;
            for (int i = 0, j = o.Count - 1; i < o.Count; j = i++)
            {
                Vector2 a = o[j], b = o[i], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                if ((a + ab * t - p).magnitude < margin) return false;
            }
            return true;
        }

        // Moving parts (sliding doors, pacing managers, swinging bars) never pass through a static block, a rail, or
        // another moving part at the same depth, at any point of their cycle, in any level.
        static void MoversClear(Transform parent)
        {
            var bad = new List<string>();
            foreach (var def in Levels.All)
            {
                var root = new GameObject("Movers " + def.Id);
                root.transform.SetParent(parent, false);
                var inst = LevelBuilder.Build(def, root.transform, false);
                var moving = new List<Collider2D>();
                foreach (var m in inst.Movers) moving.Add(m.GetComponent<Collider2D>());
                var solid = new List<Collider2D>();
                foreach (var c in inst.BlockColliders)
                    if (c != null && !moving.Contains(c) && !c.usedByEffector && c.attachedRigidbody == null) solid.Add(c);
                bool hit = false;
                for (int k = 0; k < 96 && !hit; k++)
                {
                    inst.TickMovers(k * 0.0625f, true);
                    Physics2D.SyncTransforms();
                    for (int i = 0; i < moving.Count && !hit; i++)
                    {
                        foreach (var other in solid)
                            if (Physics2D.Distance(moving[i], other).distance < -0.02f) { hit = true; bad.Add($"{def.Id} t={k * 0.0625f:0.00} {moving[i].name}/{other.name}"); break; }
                        for (int j = i + 1; j < moving.Count && !hit; j++)
                            if (Physics2D.Distance(moving[i], moving[j]).distance < -0.02f) { hit = true; bad.Add($"{def.Id} t={k * 0.0625f:0.00} {moving[i].name}/{moving[j].name}"); }
                    }
                }
                Object.DestroyImmediate(root);
            }
            Report("movers-clear", bad.Count == 0, bad.Count == 0 ? "no moving part overlaps a wall or another moving part" : string.Join("; ", bad));
        }

        // A conveyor brings a puck up to the belt speed along its direction, and the preview stops at it.
        static void Belt(Transform parent)
        {
            var root = new GameObject("Belt");
            root.transform.SetParent(parent, false);
            var inst = LevelBuilder.Build(Fixtures.Belt, root.transform, false);
            var frames = Simulate(inst, new Vector2(-2.5f, 1.4f), new Vector2(0f, 0.2f), 50);
            float vx = frames.Count > 30 ? frames[30].Vel.x : 0f;
            var path = new List<Vector2>();
            var pred = AimPreview.Compute(inst, new Vector2(-2.5f, -3f), Vector2.up * 8f, path);
            Object.DestroyImmediate(root);
            Report("conveyor", vx > Rules.ConveyorSpeed * 0.6f && vx <= Rules.ConveyorSpeed + 0.01f && pred.Uncertain,
                $"belt speed {Rules.ConveyorSpeed}, puck speed after 0.5 s {vx:0.00}; preview stops at the belt {pred.Uncertain}");
        }

        // Level teardown frees the meshes a level built: repeated rebuilds keep the mesh count flat.
        static void MeshRelease(Transform parent)
        {
            int Count() => Resources.FindObjectsOfTypeAll<Mesh>().Length;
            int firstRound = 0;
            // Four levels, one per chapter, rebuilt three times: after the first round every new mesh must be freed.
            int[] picks = { 4, 8, 13, 20 };
            for (int i = 0; i < picks.Length * 3; i++)
            {
                var root = new GameObject("MeshRelease");
                root.transform.SetParent(parent, false);
                int level = picks[i % picks.Length];
                var inst = LevelBuilder.Build(Levels.All[level], root.transform, true, 0, Themes.ForLevel(level));
                Meshes.ReleaseLevel(inst.Root);
                Object.DestroyImmediate(root);
                if (i == picks.Length - 1) firstRound = Count();
            }
            int end = Count();
            Report("mesh-release", end == firstRound, $"meshes after one round of 4 levels {firstRound}, after three rounds {end} (growth {end - firstRound})");
        }

        // 3. Head-on rebounds match the material: wood modest, rubber lively, never above 1.
        static void Materials(LevelInstance lab)
        {
            float wood = Rebound(lab, new Vector2(-3.2f, -5.2f), Vector2.up);                 // thin wood wall
            float rubber = Rebound(lab, new Vector2(-2.6f, -1.6f), Vector2.up);               // rubber post, dead center
            bool ok = wood > 0.5f && wood < 0.72f && rubber > 0.82f && rubber <= 1.0f;
            Report("materials", ok, $"rebound ratio wood {wood:0.00} (target {Rules.WoodBounce}), rubber {rubber:0.00} (target {Rules.RubberBounce})");
        }

        static float Rebound(LevelInstance lab, Vector2 start, Vector2 dir)
        {
            var frames = Simulate(lab, start, dir * 10f, 90);
            for (int i = 1; i < frames.Count; i++)
                if (frames[i].Info.Impact)
                {
                    float before = Vector2.Dot(frames[i - 1].Vel, dir), after = -Vector2.Dot(frames[i].Vel, dir);
                    return after / Mathf.Max(0.01f, before);
                }
            return -1f;
        }

        // 4. Grazing the rounded block corner: deflection right after contact grows steadily with overlap,
        //    from a gentle redirection to a full rebound, with no sudden jumps or reversals.
        static void Graze(LevelInstance lab)
        {
            // Block spans x 1.5..3.7, y -3.5..-1.3, corner radius 0.8. Shots go straight up past its lower-left corner.
            var angles = new List<float>();
            var offsets = new List<float>();
            for (float x = 1.0f; x <= 1.9f; x += 0.02f)
            {
                var frames = Simulate(lab, new Vector2(x, -6.6f), Vector2.up * 12f, 120);
                int hit = frames.FindIndex(f => f.Info.Impact || f.Info.RubSpeed > 0f);
                float deflect = 0f;
                if (hit >= 0)
                {
                    Vector2 v = frames[Mathf.Min(frames.Count - 1, hit + 2)].Vel;
                    deflect = Vector2.Angle(Vector2.up, v);
                }
                angles.Add(deflect);
                offsets.Add(x);
            }
            float maxJump = 0f, reversal = 0f;
            for (int i = 1; i < angles.Count; i++)
            {
                float step = angles[i] - angles[i - 1];
                maxJump = Mathf.Max(maxJump, Mathf.Abs(step));
                if (step < -1f) reversal = Mathf.Max(reversal, -step);
            }
            var sample = new StringBuilder();
            for (int i = 0; i < angles.Count; i += 9) sample.Append($"{offsets[i]:0.00}:{angles[i]:0} ");
            Report("graze", maxJump < 35f && reversal < 3f,
                $"deflection vs offset [{sample}] largest step {maxJump:0.0} deg per 0.02 units, reversal {reversal:0.0} deg");
        }

        // 5. Riding the concave guide: after entry, the puck holds a long continuous contact run with a steady
        //    radius, a normal that turns only as fast as the geometry, and no speed spikes.
        static void CurveRide(LevelInstance lab)
        {
            Vector2 center = new Vector2(0.6f, 4.2f);
            const float rideRadius = 2.6f - 0.25f - Rules.PuckRadius;
            float a230 = 230f * Mathf.Deg2Rad;
            Vector2 radial = new Vector2(Mathf.Cos(a230), Mathf.Sin(a230)), tangent = new Vector2(Mathf.Sin(a230), -Mathf.Cos(a230));
            int runs = 0, bad = 0, minRun = int.MaxValue;
            float worstExcess = 0f, worstRadius = 0f;
            foreach (float speed in new[] { 7f, 11f, 15f })
            for (float r = 1.84f; r <= 1.97f; r += 0.04f)
            {
                runs++;
                var frames = Simulate(lab, center + radial * r, tangent * speed, 160);
                int best = 0, run = 0, bestEnd = -1;
                for (int i = 0; i < frames.Count; i++)
                {
                    bool riding = frames[i].Info.RubSpeed > 0f && frames[i].Pos.x < center.x;
                    run = riding ? run + 1 : 0;
                    if (run > best) { best = run; bestEnd = i; }
                }
                bool spike = false;
                float excess = 0f, radiusDev = 0f;
                // Skip the settling frames at the start and the roll-off around the rod's round end cap.
                for (int i = bestEnd - best + 3; i <= bestEnd - 3 && i > 0; i++)
                {
                    var f = frames[i];
                    float geometric = f.Vel.magnitude * Rules.Dt / rideRadius * Mathf.Rad2Deg;
                    excess = Mathf.Max(excess, Vector2.Angle(frames[i - 1].Info.RubNormal, f.Info.RubNormal) - geometric * 1.6f);
                    radiusDev = Mathf.Max(radiusDev, Mathf.Abs((f.Pos - center).magnitude - rideRadius));
                    if (f.Vel.magnitude > frames[i - 1].Vel.magnitude + 0.01f) spike = true;
                }
                minRun = Mathf.Min(minRun, best);
                worstExcess = Mathf.Max(worstExcess, excess);
                worstRadius = Mathf.Max(worstRadius, radiusDev);
                if (best < 10 || excess > 3f || radiusDev > 0.03f || spike) bad++;
            }
            Report("curve-ride", bad == 0, $"{runs} entries, unstable {bad}; shortest continuous ride {minRun} steps, radius deviation {worstRadius:0.000}, normal turn beyond geometry {worstExcess:0.0} deg");
        }

        // 6. Collider seams do not snag: sliding across the join of two rods, and across the point where
        //    the rim's edge chain opens and closes.
        static void Seams(LevelInstance lab)
        {
            // Rods meet at x = -2.6 along y = 7.6 (bottom face at y = 7.35). Slide just under them, pressing upward.
            var frames = Simulate(lab, new Vector2(-4.0f, 6.98f), new Vector2(1f, 0.03f).normalized * 10f, 90);
            float worst = 0f;
            for (int i = 1; i < frames.Count; i++)
            {
                if (frames[i].Pos.x < -3.2f || frames[i].Pos.x > -2f) continue;
                worst = Mathf.Max(worst, Vector2.Angle(frames[i - 1].Vel, frames[i].Vel));
            }
            Report("seams", worst < 2f, $"two rods meeting end to end: max turn while sliding across the join {worst:0.00} deg");
        }

        // 6b. The rim's edge chain opens and closes on a straight edge with ghost points: sliding across is seamless.
        static void RimSeam()
        {
            // The rim chain starts and ends at points[0]; on a clean board, slide along that wall across it.
            var root = new GameObject("SeamTest");
            var clean = LevelBuilder.Build(new LevelDef("seam-test", "Seam", 0f, -7f), root.transform, false);
            var edge = clean.Root.transform.Find("Boundary").GetComponent<EdgeCollider2D>();
            Vector2 seam = edge.points[0], along = (edge.points[1] - edge.points[0]).normalized;
            Vector2 inward = new Vector2(-along.y, along.x);
            if (Vector2.Dot(inward, -seam) < 0f) inward = -inward;  // toward the board center
            Vector2 start = seam - along * 1.5f + inward * (Rules.PuckRadius + 0.02f);
            var rim = Simulate(clean, start, (along - inward * 0.03f).normalized * 9f, 50);
            float rimWorst = 0f, drop = 0f;
            for (int i = 1; i < rim.Count; i++)
            {
                if ((rim[i].Pos - seam).magnitude > 0.8f) continue;
                rimWorst = Mathf.Max(rimWorst, Vector2.Angle(rim[i - 1].Vel, rim[i].Vel));
                drop = Mathf.Max(drop, rim[i - 1].Vel.magnitude - rim[i].Vel.magnitude);
            }
            Object.DestroyImmediate(root);
            float decelPerStep = 9f * Rules.Wood.Drag * Rules.Dt + Rules.Wood.Decel * Rules.Dt;
            bool ok = rimWorst < 2f && drop < decelPerStep * 1.5f;
            Report("rim-seam", ok, $"max turn across the chain join {rimWorst:0.00} deg, max speed drop per step {drop:0.000} (surface alone {decelPerStep:0.000})");
        }

        // 7. Every shot settles in finite time and stays settled.
        static void Rest(LevelInstance lab)
        {
            var rng = new System.Random(9);
            int shots = 0, unsettled = 0;
            float longest = 0f;
            for (int s = 0; s < 120; s++)
            {
                shots++;
                float a = (float)rng.NextDouble() * 360f, speed = 2f + (float)rng.NextDouble() * 18f;
                var frames = Simulate(lab, new Vector2(0f, -6f), Quaternion.Euler(0f, 0f, a) * Vector2.up * speed, 1200);
                var last = frames[frames.Count - 1];
                if (last.Info.Result == ShotResult.None) unsettled++;
                if (last.Info.Result == ShotResult.Rest) longest = Mathf.Max(longest, frames.Count * Rules.Dt);
            }
            Report("rest", unsettled == 0 && longest < 12f, $"{shots} shots, never settled: {unsettled}, slowest settle {longest:0.0}s");
        }

        // 8. Hole capture on a clean board: shots over most of the bed drop at any speed, fast edge grazes skim across
        //    on the first pass, and a near miss outside the opening is neither captured nor pulled sideways.
        static void Capture(LevelInstance unused)
        {
            var root = new GameObject("CaptureTest");
            var inst = LevelBuilder.Build(new LevelDef("capture-test", "Capture", 0f, 0f), root.transform, false);
            Vector2 hole = inst.Puck.Hole;
            var lines = new StringBuilder();
            bool ok = true;
            // OUT-23: anywhere over most of the bed counts at any speed, full power included; fast grazes near the edge still skim.
            foreach (var (offset, speed, expect) in new[] { (0f, 4f, true), (0f, 14f, true), (0f, 27f, true), (0.2f, 24f, true), (0.3f, 4f, true), (0.38f, 10f, true), (0.45f, 8f, false), (0.45f, 20f, false), (0.85f, 3f, false) })
            {
                // Roll toward the hole from 3 units below at a lateral offset; speed is measured on arrival.
                float launch = speed;
                for (int k = 0; k < 40; k++) launch += (speed - AimPreview.SpeedAfter(launch, 3f)) * 0.9f;
                var frames = Simulate(inst, hole + new Vector2(offset, -3f), Vector2.up * launch, 400);
                int firstImpact = frames.FindIndex(f => f.Info.Impact);
                int sunkAt = frames.FindIndex(f => f.Info.Result == ShotResult.Sunk);
                bool firstPassDrop = sunkAt >= 0 && (firstImpact < 0 || sunkAt < firstImpact);
                float drift = 0f;
                for (int i = 0; i < frames.Count && (firstImpact < 0 || i < firstImpact); i++)
                    drift = Mathf.Max(drift, Mathf.Abs(frames[i].Pos.x - (hole.x + offset)));
                bool pass = firstPassDrop == expect && (offset < Rules.HoleRadius || drift < 0.001f);
                ok &= pass;
                lines.Append($"[off {offset:0.00} v {speed:0}: {(firstPassDrop ? "drop" : "skim")}{(offset >= Rules.HoleRadius ? $", sideways pull {drift:0.0000}" : "")}{(pass ? "" : " WRONG")}] ");
            }
            Object.DestroyImmediate(root);
            Report("capture", ok, lines.ToString());
        }

        // 9. The aim preview predicts where the first bounce happens and where the puck goes next.
        static void Preview(LevelInstance lab)
        {
            var rng = new System.Random(3);
            var path = new List<Vector2>();
            int checkedShots = 0;
            float worstPoint = 0f, worstAngle = 0f;
            var angleErrors = new List<float>();
            string worstCase = "";
            for (int s = 0; s < 600 && checkedShots < 120; s++)
            {
                Vector2 start = new Vector2(-1f + (float)rng.NextDouble() * 2f, -6f + (float)rng.NextDouble() * 2f);
                float a = -80f + (float)rng.NextDouble() * 160f, power = 0.4f + (float)rng.NextDouble() * 0.6f;
                var v = Quaternion.Euler(0f, 0f, a) * Vector2.up * (Rules.MaxSpeed * Mathf.Pow(power, Rules.PowerCurve));
                lab.Puck.PlaceAt(start);
                Physics2D.SyncTransforms();
                var pred = AimPreview.Compute(lab, start, v, path);
                if (!pred.Bounced || path.Count < 3 || (path[2] - path[1]).magnitude < 0.6f) continue;
                var frames = Simulate(lab, start, v, 300);
                int hit = frames.FindIndex(f => f.Info.Impact || f.Info.RubSpeed > 0f);
                if (hit < 1) continue;
                checkedShots++;
                // The contact happened during the step before the event was read.
                Vector2 contact = frames[hit - 1].Pos;
                float pointError = Mathf.Min((contact - pred.BouncePoint).magnitude, (frames[hit].Pos - pred.BouncePoint).magnitude);
                // A few steps past the bounce, but never across the next one (fast shots reach a corner's second wall quickly).
                int after = Mathf.Min(frames.Count - 1, hit + 3);
                for (int k = hit + 1; k < after; k++)
                    if (frames[k].Info.Impact) { after = k - 1; break; }
                Vector2 simDir = frames[after].Pos - frames[hit].Pos;
                Vector2 predDir = path[2] - path[1];
                float angleError = simDir.sqrMagnitude > 1e-4f ? Vector2.Angle(simDir, predDir) : 0f;
                if (angleError > worstAngle)
                    worstCase = $"start {start} aim {a:0} power {power:0.00} hit {frames[hit].Info.ImpactBlock?.Kind} vn {frames[hit].Info.ImpactSpeed:0.0} rub {frames[hit].Info.RubSpeed:0.0} pred {predDir.normalized} sim {simDir.normalized}";
                worstPoint = Mathf.Max(worstPoint, pointError);
                worstAngle = Mathf.Max(worstAngle, angleError);
                angleErrors.Add(angleError);
            }
            angleErrors.Sort();
            float p90 = angleErrors.Count > 0 ? angleErrors[(int)(angleErrors.Count * 0.9f)] : 0f;
            Report("preview", checkedShots >= 20 && worstPoint < 0.4f && p90 < 4f && worstAngle < 10f,
                $"{checkedShots} bounce shots, worst bounce point error {worstPoint:0.00} units, direction error 90th pct {p90:0.0} deg, worst {worstAngle:0.0} deg ({worstCase})");
        }

        // 10. Undo restores switches, gates, stars, crates, and moving parts exactly.
        static void UndoRestore(Transform parent)
        {
            var def = new LevelDef("undo-test", "Undo Test", 3f, 6f)
                .Switch(0, -2f, -2f).Gate(0, 2f, 2f, 3f, 0.5f)
                .Crate(0f, 1f, 1.2f).Star(-2f, 3f)
                .Wall(-2f, 5f, 2f, 0.5f).Moving(1.5f, 0f, 3f);
            var inst = LevelBuilder.Build(def, parent, false);
            inst.TickMovers(0.7f, true);
            var snap = inst.Capture(new Vector2(0f, -6f), 0, 0.7f);
            Vector2 moverBefore = inst.Movers[0].transform.position;

            inst.PressSwitch(inst.Zones[0]);
            inst.Stars[0].Collected = true;
            inst.Crates[0].Body.position = new Vector2(2.5f, 4f);
            inst.Crates[0].Body.rotation = 33f;
            inst.TickMovers(2.2f, true);

            inst.Restore(snap);
            bool ok = !inst.Zones[0].Pressed && !inst.Gates[0].Open && inst.Gates[0].Collider.enabled && !inst.Stars[0].Collected
                      && (inst.Crates[0].Body.position - new Vector2(0f, 1f)).magnitude < 1e-4f && Mathf.Abs(inst.Crates[0].Body.rotation) < 1e-3f
                      && ((Vector2)inst.Movers[0].transform.position - moverBefore).magnitude < 1e-4f;
            Report("undo-restore", ok, "switch, gate collider, star, crate pose, and mover position after restore");
            Object.DestroyImmediate(inst.Root);
        }

        // 11. Two portals facing each other cannot trap the puck in a loop.
        static void PortalLoop(Transform parent)
        {
            var def = new LevelDef("portal-test", "Portal Test", 3.5f, 7f).Portal(0f, -2f, 0f, 2f, 0f, 180f);
            var inst = LevelBuilder.Build(def, parent, false);
            var frames = Simulate(inst, new Vector2(0f, -5f), Vector2.up * 14f, 1800);
            int teleports = 0;
            foreach (var f in frames) if (f.Info.Teleported != null) teleports++;
            var end = frames[frames.Count - 1].Info.Result;
            Report("portal-loop", teleports <= Rules.MaxTeleportsPerShot && end != ShotResult.None,
                $"teleports {teleports} (limit {Rules.MaxTeleportsPerShot}), shot ended with {end}");
            Object.DestroyImmediate(inst.Root);
        }

        // Wardrobe: an old skin save wears that skin's color and hat; chests give every chest item exactly once
        // before duplicates; star items unlock with stars; daily quests track, complete, and claim once.
        static void Wardrobe()
        {
            var prefs = new MemoryPrefs();
            prefs.SetString("buca.skin", "grape");
            prefs.SetInt("buca.save", Progress.Version);
            var p = new Progress(prefs);
            var o = p.Outfit;
            bool migrated = o.Color == "c.grape" && o.Hat == "h.catears" && o.Face == Cosmetics.Default.Face && o.Employee == Cosmetics.Default.Employee;
            int chestItems = Cosmetics.All.FindAll(i => i.FromChest).Count;
            var rng = new System.Random(3);
            var seen = new HashSet<string>();
            int rares = 0, dupesBeforeAll = 0;
            for (int i = 0; i < chestItems; i++)
            {
                var item = p.OpenChest(rng, out bool dup);
                if (dup) dupesBeforeAll++;
                seen.Add(item.Id);
                if (item.Rarity != Rarity.Common) rares++;
            }
            p.OpenChest(rng, out bool dupAfter);
            bool starLocked = !p.Owns(Cosmetics.Find("c.midnight"));
            var quests = p.TodayQuests;
            int completed = 0;
            foreach (var q in quests) completed += p.Track(q.Kind, q.Target);
            bool claimOnce = p.ClaimQuest(0) && !p.ClaimQuest(0);
            bool daily = p.ClaimDailyChest() && !p.ClaimDailyChest();
            var sameDay = Quests.For(20261010);
            bool stable = sameDay[0] == Quests.For(20261010)[0] && sameDay.Length == 3;
            bool ok = migrated && seen.Count == chestItems && dupesBeforeAll == 0 && dupAfter && starLocked && completed == 3 && claimOnce && daily && stable
                      && Quips.Count >= 40 && Cosmetics.All.Count >= 40;
            Report("wardrobe", ok, $"migrated {migrated} ({o.Color}, {o.Hat}); {seen.Count}/{chestItems} chest items with no early duplicate ({rares} above common), duplicate after {dupAfter}; " +
                $"star item locked {starLocked}; quests completed {completed}/3, claim once {claimOnce}, daily chest once {daily}; {Cosmetics.All.Count} items, {Quips.Count} lines");
        }

        // 12. A Buca save migrates: stars move to the new level ids and reached levels stay unlocked.
        static void SaveMigration()
        {
            var prefs = new MemoryPrefs();
            prefs.SetInt("buca.level", 8);        // was on "Portal"
            prefs.SetInt("buca.stars.0", 3);      // Warm up
            prefs.SetInt("buca.stars.3", 2);      // Speed pad
            prefs.SetInt("buca.stars.8", 1);      // Portal
            var p = new Progress(prefs);
            bool ok = p.Best("first") == 3 && p.Best("speedpad") == 2 && p.Best("portal") == 1
                      && p.Unlocked > Levels.IndexOf("speedpad") && p.Unlocked > Levels.IndexOf("portal")
                      && Levels.All[p.CurrentIndex].Id == "portal" && prefs.GetInt("buca.save", 0) == Progress.Version
                      && prefs.GetInt("buca.stars.0", 0) == 3;
            var fresh = new Progress(new MemoryPrefs());
            ok &= fresh.Unlocked == 1 && fresh.CurrentIndex == 0;
            Report("save-migration", ok, $"unlocked {p.Unlocked}, current {Levels.All[p.CurrentIndex].Id}, stars first/speedpad/portal {p.Best("first")}/{p.Best("speedpad")}/{p.Best("portal")}, old keys kept");
        }

        // 13. Every wardrobe slot reads and writes its own field (the STAFF tab once read the face slot).
        static void OutfitSlots()
        {
            bool ok = true;
            var slots = (Slot[])System.Enum.GetValues(typeof(Slot));
            foreach (var slot in slots)
            {
                var o = Cosmetics.Default.With(slot, "x.test");
                ok &= o.Get(slot) == "x.test";
                foreach (var other in slots)
                    if (other != slot) ok &= o.Get(other) == Cosmetics.Default.Get(other);
            }
            var staff = Cosmetics.All.Find(i => i.Slot == Slot.Employee && i.Id != Cosmetics.Default.Employee);
            var prefs = new MemoryPrefs();
            var p = new Progress(prefs);
            p.Grant(staff);
            p.Equip(staff);
            ok &= p.Outfit.Get(Slot.Employee) == staff.Id && p.Outfit.Face == Cosmetics.Default.Face;
            Report("outfit-slots", ok, $"{slots.Length} slots isolated; equipping {staff.Id} shows on the STAFF tab");
        }

        // 14. Winding the device clock back never hands out a day's rewards twice, and a far-future clock that gets
        // corrected doesn't lock rewards away for years.
        static void DailyClock()
        {
            var start = new System.DateTime(2026, 10, 16, 6, 0, 0, System.DateTimeKind.Utc);
            var clock = new FakeClock(start, System.TimeSpan.FromHours(4));  // 10:00 local
            var prefs = new MemoryPrefs();
            var p = new Progress(prefs, clock);
            int day0 = p.Today;
            bool first = p.ClaimDailyChest() && !p.ClaimDailyChest();

            // Forward a day: that day's chest can be taken early...
            clock.Advance(System.TimeSpan.FromDays(1));
            var quests = p.TodayQuests;
            p.Track(quests[0].Kind, 1);
            int progressAhead = p.QuestProgress(0);
            bool borrowed = p.ClaimDailyChest();
            // ...but going back doesn't open today again, and the borrowed day's quest progress stays.
            clock.Advance(System.TimeSpan.FromDays(-1));
            bool heldBack = !p.DailyChestReady && p.Today == day0 + 1 && p.QuestProgress(0) == progressAhead && p.ClaimDailyChest() == false;
            p.Now.ToString(); p.Now.ToString();  // more reads while behind don't count again
            int rollbacks = p.ClockRollbacks;

            // Relaunch: the guard survives a restart.
            p.Flush();
            var relaunched = new Progress(prefs, clock);
            // (each launch that finds the clock behind counts once more)
            bool survives = !relaunched.DailyChestReady && relaunched.ClockRollbacks == 2;

            // Real time catches up: the borrowed day is spent, the one after is fresh.
            clock.Advance(System.TimeSpan.FromDays(1) + System.TimeSpan.FromHours(2));
            bool spent = !relaunched.DailyChestReady;
            clock.Advance(System.TimeSpan.FromDays(1));
            bool fresh = relaunched.DailyChestReady;

            // Flying west moves local time back, but UTC goes on: not a rollback.
            clock.Offset = System.TimeSpan.FromHours(-7);
            int rollbacksBefore = relaunched.ClockRollbacks;
            relaunched.Now.ToString();
            bool travel = relaunched.ClockRollbacks == rollbacksBefore;

            // A clock set years ahead and then corrected: the false future is closed out and tomorrow works.
            var far = new FakeClock(new System.DateTime(2031, 1, 1, 12, 0, 0, System.DateTimeKind.Utc));
            var farPrefs = new MemoryPrefs();
            var fp = new Progress(farPrefs, far);
            fp.ClaimDailyChest();
            fp.TodayQuests.ToString();  // quest progress now belongs to that false day
            far.UtcNow = start;
            bool closedToday = !fp.DailyChestReady && fp.Today == Quests.DayOf(start) && fp.QuestClaimed(0);
            far.Advance(System.TimeSpan.FromDays(1));
            bool reopened = fp.DailyChestReady && !fp.QuestClaimed(0) && fp.QuestProgress(0) == 0;

            bool ok = first && borrowed && heldBack && rollbacks == 1 && survives && spent && fresh && travel
                      && closedToday && reopened;
            Report("daily-clock", ok, $"claim once {first}; borrow ahead {borrowed}, wound back holds {heldBack} (rollbacks {rollbacks}); " +
                $"survives relaunch {survives}; borrowed day spent {spent}, next day fresh {fresh}; time zone change ignored {travel}; " +
                $"far-future clock closed {closedToday}, reopens next day {reopened}");
        }

        // 15. Progress that isn't saved on every event reaches disk when the app is backgrounded.
        static void SaveFlush()
        {
            var prefs = new MemoryPrefs();
            var p = new Progress(prefs, new FakeClock(new System.DateTime(2026, 10, 16, 8, 0, 0, System.DateTimeKind.Utc)));
            int qi = System.Array.FindIndex(p.TodayQuests, q => q.Target > 1);
            var quest = p.TodayQuests[qi];
            int before = prefs.Saves;
            p.Track(quest.Kind, 1);
            bool lazy = prefs.Saves == before;
            p.Flush();
            bool ok = lazy && prefs.Saves == before + 1 && p.QuestProgress(qi) == 1;
            Report("save-flush", ok, $"quest step saved lazily {lazy}, flush writes {prefs.Saves - before}");
        }

        // 16. The chest at the end of a chapter's star bar opens only with every star in that chapter, and only once.
        static void ChapterChest()
        {
            var prefs = new MemoryPrefs();
            var p = new Progress(prefs);
            int first = Themes.ChapterStart[0], end = Themes.ChapterStart[1];
            for (int i = first; i < end - 1; i++) prefs.SetInt("buca.best." + Levels.All[i].Id, 3);
            prefs.SetInt("buca.best." + Levels.All[end - 1].Id, 2);
            bool early = !p.ChapterMastered(0) && !p.ClaimChapterChest(0);
            prefs.SetInt("buca.best." + Levels.All[end - 1].Id, 3);
            bool once = p.ChapterMastered(0) && p.ClaimChapterChest(0) && !p.ClaimChapterChest(0) && p.ChapterChestClaimed(0);
            bool others = !p.ChapterMastered(1) && !p.ChapterChestClaimed(1) && !p.ChapterMastered(-1) && !p.ChapterMastered(Themes.All.Length);
            Report("chapter-chest", early && once && others, $"locked until all stars {early}, claim once {once}, other chapters untouched {others}");
        }

        // 18. Coffee punch card: new coffees stamp once, replays don't, a full card opens once and extra stamps carry
        //     over, and an older save is credited for its 3-star rooms (OUT-24).
        static void CoffeeCard()
        {
            var p = new Progress(new MemoryPrefs());
            int a = Levels.IndexOf("first"), b = Levels.IndexOf("easy");
            int first = p.StampCoffees(a, 2), replaySame = p.StampCoffees(a, 2), better = p.StampCoffees(a, 3), overCount = p.StampCoffees(a, 9);
            bool stamping = first == 2 && replaySame == 0 && better == 1 && overCount == 0 && p.CoffeesBest("first") == 3;
            for (int i = 0; i < Levels.All.Length && p.Stamps < 12; i++) p.StampCoffees(i, Levels.All[i].Stars.Count);
            bool ready = p.CardReady && p.CardStamps == Progress.StampsPerCard;
            bool claimOnce = p.ClaimCard() && !p.ClaimCard();
            bool carry = p.CardStamps == p.Stamps - Progress.StampsPerCard && p.CardStamps > 0 && !p.CardReady;

            var old = new MemoryPrefs();
            old.SetInt("buca.best.first", 3);
            old.SetInt("buca.best.easy", 2);
            var migrated = new Progress(old);
            bool migrate = migrated.Stamps == Levels.All[a].Stars.Count && migrated.CoffeesBest("easy") == 0 && migrated.StampCoffees(a, 3) == 0
                           && new Progress(old).Stamps == migrated.Stamps && b >= 0;
            bool ok = stamping && ready && claimOnce && carry && migrate;
            Report("coffee-card", ok, $"stamps once {stamping}; full card {ready}, opens once {claimOnce}, carry-over {carry} ({p.CardStamps}/{Progress.StampsPerCard}); " +
                $"old save credited {migrate} ({migrated.Stamps} stamps)");
        }

        // 19. Every level has a shot limit above its par, and a level can set its own (OUT-27).
        static void ShotLimits()
        {
            bool ok = true;
            int tightest = int.MaxValue;
            foreach (var l in Levels.All)
            {
                ok &= l.MaxShots >= l.Par + 1;
                tightest = Mathf.Min(tightest, l.MaxShots - l.Par);
            }
            var custom = new LevelDef("limit-test", "Limit", 0f, 0f).WithPar(2).WithMaxShots(9);
            ok &= custom.MaxShots == 9 && new LevelDef("limit-default", "Limit", 0f, 0f).WithPar(2).MaxShots == 2 + LevelDef.ShotSlack;
            // Room names (OUT-28): short enough for the banner, and no two rooms share a joke.
            var names = new HashSet<string>();
            foreach (var l in Levels.All) ok &= l.Name.Length <= 26 && names.Add(l.Name);
            Report("shot-limits", ok, $"{Levels.All.Length} levels, tightest limit par + {tightest}; override works");
        }

        class StubAds : IAdProvider
        {
            public bool IsTest => true;
            public bool RewardedReady => true;
            public int Shown;
            public void ShowRewarded(string placement, System.Action<bool> done) { Shown++; done(true); }
        }

        // 20. Money (OUT-31..33): gems, one-time products, the Office Pass, and rewarded-ad caps.
        static void Money()
        {
            var clock = new FakeClock(new System.DateTime(2026, 10, 16, 8, 0, 0, System.DateTimeKind.Utc));
            var prefs = new MemoryPrefs();
            var p = new Progress(prefs, clock);

            // Gems: earn, spend, never below zero.
            p.AddGems(50, "test");
            bool wallet = p.SpendGems(30, "test") && !p.SpendGems(30, "test") && p.Gems == 20 && !p.SpendGems(-5, "test");

            // Purchases through the test store: one-time products only once, consumables any time.
            var oldStore = Store.Provider;
            Store.Provider = new TestStore();
            bool bought = true;
            Store.Buy(p, Catalog.Starter, ok => bought &= ok);
            Store.Buy(p, Catalog.Starter, ok => bought &= !ok);     // already owned
            Store.Buy(p, Catalog.GemPacks[0], ok => bought &= ok);
            Store.Buy(p, Catalog.GemPacks[0], ok => bought &= ok);
            bought &= p.Gems == 20 + 150 + 80 + 80 && p.Bought(Catalog.Starter) && !p.CanBuy(Catalog.Starter) && p.CanBuy(Catalog.GemPacks[0]);
            Store.Provider = new NoStore();
            bool closed = true;
            Store.Buy(p, Catalog.GemPacks[1], ok => closed &= !ok);
            Store.Provider = oldStore;

            // The pass: 30 days, a daily claim once per day, renewals stack, expiry.
            var q = new Progress(new MemoryPrefs(), clock);
            q.Grant(Catalog.Pass);
            bool tie = q.Owns(Cosmetics.Find(Catalog.PassTie));
            bool pass = q.PassActive && q.PassDaysLeft == 30 && q.AdFree && q.ClaimPass() && !q.ClaimPass() && q.Gems == Progress.PassDailyGems;
            clock.Advance(System.TimeSpan.FromDays(1));
            pass &= q.PassClaimReady && q.ClaimPass();
            q.Grant(Catalog.Pass);                                    // renew with 29 days left
            pass &= q.PassDaysLeft == 59;
            clock.Advance(System.TimeSpan.FromDays(60));
            pass &= !q.PassActive && !q.PassClaimReady && !q.ClaimPass() && !q.AdFree;

            // Rewarded ads: not in the first rooms, a cooldown, and a daily cap.
            var oldAds = Ads.Provider;
            var stub = new StubAds();
            Ads.Provider = stub;
            Ads.ResetCooldown();
            var a = new Progress(new MemoryPrefs(), clock);
            bool gate = !Ads.CanOffer(a, 1000f);
            for (int i = 0; i < Levels.All.Length; i++) a.Record(i, 1, 9);  // open every room
            int rewarded = 0;
            for (int i = 0; i < 20; i++) Ads.ShowRewarded(a, "test", ok => { if (ok) rewarded++; }, 1000f + i * Ads.Cooldown);
            bool cap = rewarded == Ads.DailyCap && a.AdsToday == Ads.DailyCap;
            Ads.ResetCooldown();
            var b = new Progress(new MemoryPrefs(), clock);
            for (int i = 0; i < Levels.All.Length; i++) b.Record(i, 1, 9);
            int quick = 0;
            Ads.ShowRewarded(b, "test", ok => { if (ok) quick++; }, 5000f);
            Ads.ShowRewarded(b, "test", ok => { if (ok) quick++; }, 5005f);   // inside the cooldown
            bool cooldown = quick == 1;
            clock.Advance(System.TimeSpan.FromDays(1));
            bool newDay = a.AdsToday == 0;
            // Ad-free players get the reward without a video.
            Ads.ResetCooldown();
            int before = stub.Shown;
            var f = new Progress(new MemoryPrefs(), clock);
            for (int i = 0; i < Levels.All.Length; i++) f.Record(i, 1, 9);
            f.Grant(Catalog.NoAds);
            bool freeOk = false;
            Ads.ShowRewarded(f, "test", ok => freeOk = ok, 9000f);
            bool adFree = freeOk && stub.Shown == before;
            Ads.Provider = oldAds;
            Ads.ResetCooldown();

            bool ok = wallet && bought && closed && tie && pass && gate && cap && cooldown && newDay && adFree;
            Report("money", ok, $"wallet {wallet}; purchases once {bought}, closed store sells nothing {closed}; pass {pass} (gold tie {tie}); " +
                $"ads gated in first rooms {gate}, daily cap {cap} ({rewarded}), cooldown {cooldown}, resets daily {newDay}, ad-free skips video {adFree}");
        }

        // 23. Staff (OUT-42, OUT-43): every employee has a department, a job title and a face of their own; every
        // department has people; the wardrobe lists them in department order; star goals stay within reach; the
        // original twelve keep their ids.
        static void StaffRoster()
        {
            var staff = Cosmetics.InSlot(Slot.Employee);
            var faces = new HashSet<string>();
            bool titled = true, faced = true, unique = true, ordered = true;
            for (int i = 0; i < staff.Count; i++)
            {
                var s = staff[i];
                titled &= !string.IsNullOrEmpty(s.Title);
                faced &= s.Features != null;
                if (s.Features != null) unique &= faces.Add(s.Features.Key);
                if (i > 0) ordered &= staff[i - 1].Dept <= s.Dept;
            }
            var perDept = new int[System.Enum.GetValues(typeof(Department)).Length];
            foreach (var s in staff) perDept[(int)s.Dept]++;
            bool everyDept = System.Array.TrueForAll(perDept, n => n >= 2);
            int maxStars = Levels.All.Length * 3, topGoal = 0;
            foreach (var s in staff) topGoal = Mathf.Max(topGoal, s.StarsNeeded);
            bool reachable = topGoal <= maxStars;
            bool kept = true;
            foreach (var id in new[] { "ava", "leo", "mia", "sam", "jax", "nova", "kai", "otto", "luna", "ivy", "zara", "rex" })
                kept &= Cosmetics.Find("e." + id) != null;
            // A face composes into the look the game builds.
            var o = Cosmetics.Default;
            o.Employee = "e.boss";
            var look = Cosmetics.Compose(o);
            bool composed = look.Features != null && look.Features.Eyes == EyeShape.Narrow && (look.Extras & PersonLook.Blazer) != 0;

            bool ok = staff.Count >= 36 && titled && faced && unique && ordered && everyDept && reachable && kept && composed;
            Report("staff", ok, $"{staff.Count} employees in {perDept.Length} departments ({string.Join(",", perDept)}); titles {titled}, faces {faced} " +
                $"all different {unique}; listed by department {ordered}; top star goal {topGoal} of {maxStars}; old ids kept {kept}; composed {composed}");
        }

        // 22. Undos (OUT-41): three free per attempt, then gems or an ad, the shop when neither; restart resets; the
        // first rooms stay unlimited.
        static void Undos()
        {
            var u = new UndoAllowance();
            u.Reset(true);
            bool three = true;
            for (int i = 0; i < UndoAllowance.Free; i++)
            {
                three &= u.Check(0, false) == UndoAllowance.Next.Free;
                u.UseFree();
            }
            bool counted = u.FreeLeft == 0;
            bool shop = u.Check(UndoAllowance.GemCost - 1, false) == UndoAllowance.Next.Shop;
            bool payGems = u.Check(UndoAllowance.GemCost, false) == UndoAllowance.Next.Pay;
            bool payAd = u.Check(0, true) == UndoAllowance.Next.Pay;

            // Paying with gems takes exactly the price, and never more than the wallet holds.
            var clock = new FakeClock(new System.DateTime(2026, 10, 16, 8, 0, 0, System.DateTimeKind.Utc));
            var p = new Progress(new MemoryPrefs(), clock);
            p.AddGems(25, "test");
            bool paid = UndoAllowance.PayWithGems(p) && p.Gems == 25 - UndoAllowance.GemCost;
            var poor = new Progress(new MemoryPrefs(), clock);
            poor.AddGems(UndoAllowance.GemCost - 1, "test");
            bool refused = !UndoAllowance.PayWithGems(poor) && poor.Gems == UndoAllowance.GemCost - 1;

            // Paying with an ad goes through the ad caps like any rewarded video.
            var oldAds = Ads.Provider;
            var stub = new StubAds();
            Ads.Provider = stub;
            Ads.ResetCooldown();
            var watcher = new Progress(new MemoryPrefs(), clock);
            for (int i = 0; i < Levels.All.Length; i++) watcher.Record(i, 1, 9);
            bool adPaid = false;
            Ads.ShowRewarded(watcher, "undo", ok => adPaid = ok, 7000f);
            bool adCounted = adPaid && stub.Shown == 1 && watcher.AdsToday == 1;
            Ads.Provider = oldAds;
            Ads.ResetCooldown();

            // Restart: three free again. First rooms: never limited.
            u.Reset(true);
            bool restart = u.FreeLeft == UndoAllowance.Free && u.Check(0, false) == UndoAllowance.Next.Free;
            var early = new UndoAllowance();
            early.Reset(false);
            for (int i = 0; i < 10; i++) early.UseFree();
            bool unlimited = early.Check(0, false) == UndoAllowance.Next.Free && early.FreeLeft == UndoAllowance.Free;

            bool ok = three && counted && shop && payGems && payAd && paid && refused && adCounted && restart && unlimited;
            Report("undos", ok, $"three free {three} (counted {counted}); then shop when broke {shop}, pay with gems {payGems} or an ad {payAd}; " +
                $"gems charged {paid}, refused when short {refused}; ad path through the caps {adCounted}; restart resets {restart}; first rooms unlimited {unlimited}");
        }

        // 23. Energy (OUT-48): a full bar of six plays to start, a play back every half hour (the genre's pace), a play
        // lasts while you win, ends on a fail or a restart after a shot, a continue revives it, and the pass makes it free.
        static void EnergyPlays()
        {
            var clock = new FakeClock(new System.DateTime(2026, 10, 16, 8, 0, 0, System.DateTimeKind.Utc));
            var p = new Progress(new MemoryPrefs(), clock);
            var e = p.Energy;
            bool full = e.Points == OutOfOffice.Energy.Max && e.UntilNext == System.TimeSpan.Zero;

            // Six plays, then the card.
            int plays = 0;
            while (e.Start(true, "test")) { plays++; e.MarkAttempt(); e.EndPlay("fail", "test"); }
            bool six = plays == OutOfOffice.Energy.Max / OutOfOffice.Energy.PlayCost && e.Points == 0 && !e.CanStart(true);

            // The refill: a point every Regen, a play every half hour, a whole bar in hours, and a play's wait reported right.
            var regen = OutOfOffice.Energy.Regen;
            bool pace = regen.Ticks * OutOfOffice.Energy.PlayCost == System.TimeSpan.FromMinutes(30).Ticks;
            bool waits = System.Math.Abs((e.UntilPlay - System.TimeSpan.FromTicks(regen.Ticks * OutOfOffice.Energy.PlayCost)).TotalSeconds) < 1;
            clock.UtcNow += regen - System.TimeSpan.FromSeconds(30);
            bool notYet = e.Points == 0;
            clock.UtcNow += System.TimeSpan.FromSeconds(30);
            bool one = e.Points == 1;
            clock.UtcNow += System.TimeSpan.FromHours(30);
            bool refilled = e.Points == OutOfOffice.Energy.Max;

            // A play runs on through wins, and a start with a play running is free.
            int before = e.Points;
            e.Start(true, "a");
            e.MarkAttempt();
            e.FinishAttempt();
            bool winFree = e.InPlay && e.StartsFree(true) && e.Start(true, "b") && e.Points == before - OutOfOffice.Energy.PlayCost;
            // Leaving after a shot needs a new play; leaving before one doesn't.
            bool noShot = e.CanStart(true, e.AttemptOpen) && e.StartsFree(true);
            e.MarkAttempt();
            bool atStake = e.AttemptOpen;
            e.EndPlay("restart", "b");
            bool restartPays = e.Start(true, "b") && e.Points == before - 2 * OutOfOffice.Energy.PlayCost;
            // A fail ends it; a continue picks it back up with the room still at stake.
            e.MarkAttempt();
            e.EndPlay("fail", "b");
            bool failed = !e.InPlay && !e.AttemptOpen;
            e.Revive();
            bool revived = e.InPlay && e.AttemptOpen;

            // Rewards add past the bar; the first rooms and the lab never cost.
            e.Add(OutOfOffice.Energy.PlayCost, "test");
            bool over = e.Points > before - 2 * OutOfOffice.Energy.PlayCost;
            var fresh = new Progress(new MemoryPrefs(), clock);
            bool firstRooms = !fresh.PlaysCost(0) && !fresh.PlaysCost(-1);
            for (int i = 0; i < OutOfOffice.Energy.FirstRoom; i++) fresh.Record(i, 1, 9);
            bool laterRooms = fresh.PlaysCost(OutOfOffice.Energy.FirstRoom) && !fresh.PlaysCost(-1);

            // The Office Pass: unlimited, nothing spent.
            var holder = new Progress(new MemoryPrefs(), clock);
            holder.Grant(Catalog.Pass);
            int kept = holder.Energy.Points;
            for (int i = 0; i < 10; i++) holder.Energy.Start(true, "pass");
            bool pass = holder.Energy.Unlimited && holder.Energy.Points == kept;

            // A clock wound back can't refill: the guarded clock holds, and the wait starts again.
            var rewind = new Progress(new MemoryPrefs(), clock);
            rewind.Energy.Spend(OutOfOffice.Energy.Max, "test");
            clock.UtcNow -= System.TimeSpan.FromHours(10);
            bool noRewind = rewind.Energy.Points == 0;
            clock.UtcNow += System.TimeSpan.FromHours(10);

            // The price ladder: what keeps a play going costs less than a new one.
            bool prices = UndoAllowance.GemCost < Catalog.ContinueCost && Catalog.ContinueCost < Catalog.PlayGems
                && Catalog.RefillGems < Catalog.PlayGems * OutOfOffice.Energy.Max / OutOfOffice.Energy.PlayCost;

            bool ok = full && six && pace && waits && notYet && one && refilled && winFree && noShot && atStake && restartPays && failed && revived && over
                && firstRooms && laterRooms && pass && noRewind && prices;
            Report("energy", ok, $"starts full {full}; six plays {six} ({plays}); a play per half hour {pace}, a point per {regen.TotalMinutes} min {notYet}/{one}, a play's wait {waits}, full again {refilled}; " +
                $"wins carry the play {winFree}; no shot no cost {noShot}; at stake after a shot {atStake}, restart pays {restartPays}; fail ends {failed}, " +
                $"continue revives {revived}; rewards stack {over}; first rooms free {firstRooms}, later rooms cost {laterRooms}; pass unlimited {pass}; " +
                $"no rewind refill {noRewind}; price ladder {prices}");
        }

        // 24. Device tiers (OUT-19): known phones land in the right tier, a hot phone steps down a tier, and the
        // presets get lighter tier by tier.
        static void QualityTiers()
        {
            var android = RuntimePlatform.Android;
            bool a06 = Quality.Detect(android, 3700, "Mali-G52 MC2", 8, "samsung SM-A065F") == Tier.Low;
            bool quad = Quality.Detect(android, 8000, "Adreno (TM) 650", 4, "old quad") == Tier.Low;
            bool midPhone = Quality.Detect(android, 4096, "Mali-G68 MC4", 8, "mid") == Tier.Mid;
            bool pixel = Quality.Detect(android, 7900, "Mali-G715-Immortalis MC10", 9, "Pixel 8") == Tier.High;
            bool flagship = Quality.Detect(android, 11800, "Adreno (TM) 750", 8, "SM-S928B") == Tier.Ultra;
            bool iphones = Quality.Detect(RuntimePlatform.IPhonePlayer, 6000, "Apple A16 GPU", 6, "iPhone15,2") == Tier.Ultra
                && Quality.Detect(RuntimePlatform.IPhonePlayer, 4000, "Apple A14 GPU", 6, "iPhone13,2") == Tier.High
                && Quality.Detect(RuntimePlatform.IPhonePlayer, 3000, "Apple A12 GPU", 6, "iPhone11,8") == Tier.Mid
                && Quality.Detect(RuntimePlatform.IPhonePlayer, 2000, "Apple A11 GPU", 6, "iPhone10,3") == Tier.Low;
            bool desktop = Quality.Detect(RuntimePlatform.WindowsEditor, 32000, "NVIDIA", 16, "PC") == Tier.Ultra;

            Quality.Choose((int)Tier.High);
            bool picked = Quality.Current == Tier.High;
            Quality.SetHeat(2);  // moderate
            bool warm = Quality.Current == Tier.Mid && Quality.Heat == 1;
            Quality.SetHeat(4);  // critical
            bool hot = Quality.Current == Tier.Mid && Quality.Heat == 2;
            Quality.SetHeat(0);
            bool cooled = Quality.Current == Tier.High && Quality.Heat == 0;
            Quality.Choose(-1);

            bool lighter = true;
            for (int t = 1; t <= (int)Tier.Ultra; t++)
            {
                var lo = Quality.For((Tier)(t - 1));
                var hi = Quality.For((Tier)t);
                lighter &= lo.RenderScale <= hi.RenderScale && lo.Msaa <= hi.Msaa && lo.Shadows <= hi.Shadows && lo.Fx <= hi.Fx;
            }
            bool lowStays60 = !Quality.For(Tier.Low).HighRefresh && Quality.For(Tier.Low).RenderScale >= 0.8f;

            bool ok = a06 && quad && midPhone && pixel && flagship && iphones && desktop && picked && warm && hot && cooled && lighter && lowStays60;
            Report("quality-tiers", ok, $"Galaxy A06 low {a06}, quad-core low {quad}, mid phone {midPhone}, Pixel 8 high {pixel}, S24 Ultra ultra {flagship}, " +
                $"iPhones {iphones}, desktop {desktop}; pick {picked}; warm steps down {warm}, hot holds {hot}, cools back {cooled}; presets lighter by tier {lighter}, low tier sane {lowStays60}");
        }

        // 25. The field guide (OUT-11): every gadget's demo room builds and is recognized as that gadget, every gadget
        // the rooms use has a card, and an old save counts the gadgets of rooms it has beaten as met.
        static void GadgetGuide()
        {
            bool demos = true;
            string broken = "";
            var holder = new GameObject("Gadget Check");
            try
            {
                foreach (var g in OutOfOffice.Gadgets.All)
                {
                    var def = g.Demo();
                    bool ok = g.In(def) && def.Id == "demo-" + g.Key;
                    try { LevelBuilder.Build(def, holder.transform, false); }
                    catch (System.Exception) { ok = false; }
                    if (!ok) { demos = false; broken += g.Key + " "; }
                }
            }
            finally { Object.DestroyImmediate(holder); }

            // Everything that isn't a plain wall in any room has an intro card.
            bool covered = true;
            string missing = "";
            foreach (var def in Levels.All)
            {
                foreach (var b in def.Blocks)
                {
                    if ((b.Kind == BlockKind.Wall && !b.Moves) || b.Reception != 0) continue;
                    var one = new LevelDef("probe", "probe", 0f, 0f);
                    one.Blocks.Add(b);
                    if (OutOfOffice.Gadgets.In(one).Count == 0) { covered = false; missing += def.Id + ":" + b.Kind + " "; }
                }
                foreach (var z in def.Zones)
                {
                    var one = new LevelDef("probe", "probe", 0f, 0f);
                    one.Zones.Add(z);
                    if (OutOfOffice.Gadgets.In(one).Count == 0) { covered = false; missing += def.Id + ":" + z.Kind + " "; }
                }
            }
            bool unique = new System.Collections.Generic.HashSet<string>(System.Array.ConvertAll(OutOfOffice.Gadgets.All, g => g.Key)).Count == OutOfOffice.Gadgets.All.Length;
            bool written = System.Array.TrueForAll(OutOfOffice.Gadgets.All, g => g.Name.Length > 0 && g.Name.Length <= 20 && g.Text.Length > 10 && g.Text.Length <= 80);

            // An old save at room 30 has met what rooms 1-29 use; a new one has met nothing.
            var clock = new FakeClock(new System.DateTime(2026, 10, 16, 8, 0, 0, System.DateTimeKind.Utc));
            var oldPrefs = new MemoryPrefs();
            oldPrefs.SetInt("buca.unlocked", 30);
            var old = new Progress(oldPrefs, clock);
            bool migrated = true;
            for (int i = 0; i < 29; i++) foreach (var g in OutOfOffice.Gadgets.In(Levels.All[i])) migrated &= old.SeenGadget(g.Key);
            migrated &= old.FtueDone("first_win");
            var fresh = new Progress(new MemoryPrefs(), clock);
            bool clean = !System.Array.Exists(OutOfOffice.Gadgets.All, g => fresh.SeenGadget(g.Key)) && !fresh.FtueDone("hand");
            fresh.MarkFtue("hand");
            bool marked = fresh.FtueDone("hand");

            bool ok2 = demos && covered && unique && written && migrated && clean && marked;
            Report("gadget-guide", ok2, $"{OutOfOffice.Gadgets.All.Length} gadgets, demo rooms build and read right {demos} {broken}; every gadget in the rooms has a card {covered} {missing}; " +
                $"keys unique {unique}; names and lines fit {written}; old saves migrated {migrated}; new saves clean {clean}; steps mark {marked}");
        }

        // 26. Boosters (OUT-13): the Magnet Bed pulls a slow roll beside the bed into it and changes nothing out of its
        // reach; a Coffee Break holds moving parts while pop walls keep their beat; unlocks, the starting three, use
        // counts and prices.
        static void BoosterChecks(Transform parent)
        {
            var def = new LevelDef("magnet-bed-check", "Magnet Bed Check", 0f, 2f).WithShape(ShapeDef.Rect(9f, 12f, 1f)).StartAt(0f, -4f);
            var inst = LevelBuilder.Build(def, parent, false);
            ShotResult Roll(Vector2 from, Vector2 v, bool magnet, out Vector2 end)
            {
                inst.TickMovers(0f, true);
                inst.Puck.PlaceAt(from);
                Settle();
                inst.Puck.Launch(v);
                end = from;
                for (int i = 0; i < 1800; i++)
                {
                    inst.TickMovers((i + 1) * Rules.Dt, false);
                    if (magnet && !inst.Puck.Airborne)
                        inst.Puck.Body.linearVelocity += OutOfOffice.Boosters.BedPull(inst.Puck.Body.position, def.Hole, Rules.Dt);
                    var info = inst.Puck.Step(Rules.Dt);
                    end = inst.Puck.Body.position;
                    if (info.Result != ShotResult.None) return info.Result;
                    Physics2D.Simulate(Rules.Dt);
                }
                return ShotResult.None;
            }
            // A slow roll that passes 1.2 to the side of the bed: it stops beside it, unless the bed pulls it in.
            var beside = def.Hole + new Vector2(1.2f, -1.6f);
            var plain = Roll(beside, new Vector2(0f, 2.4f), false, out var plainEnd);
            var pulled = Roll(beside, new Vector2(0f, 2.4f), true, out _);
            bool pulls = plain == ShotResult.Rest && pulled == ShotResult.Sunk;
            // Out of its reach, the same roll with and without it ends in the same place.
            var far = def.Hole + new Vector2(-3.4f, -4.5f);
            Roll(far, new Vector2(0f, 3f), false, out var farPlain);
            Roll(far, new Vector2(0f, 3f), true, out var farPulled);
            bool reach = (farPlain - farPulled).sqrMagnitude < 1e-8f && (farPlain - def.Hole).magnitude > OutOfOffice.Boosters.MagnetRadius;
            Object.DestroyImmediate(inst.Root);

            var moving = new LevelDef("coffee-break-check", "Coffee Break Check", 0f, 4f).WithShape(ShapeDef.Rect(9f, 12f, 1f)).StartAt(0f, -4f)
                .Wall(0f, 0f, 2f, 0.6f).Moving(2f, 0f, 2f).PopWall(0f, 2.5f, 4f, 0.5f, 2f, 0f)
                .Spikes(2.5f, -2.5f, 2f, 1f, 2f, 0f).Conveyor(-2.5f, -2.5f, 1.6f, 3f, 0f);
            var mi = LevelBuilder.Build(moving, parent, false);
            // Everyone's on break (OUT-51): the movers' clock is held, the pop wall and the timed staples stay down, and
            // the belt carries nothing; afterwards everything picks up where it was.
            mi.TickMovers(0.25f, true, 0.25f);
            Vector3 held = mi.Movers[0].transform.position;
            bool upBefore = mi.PopWalls[0].Collider.enabled && mi.Zones[0].SpikesUp(0.25f);
            mi.OnBreak = true;
            mi.TickMovers(1.25f, true, 0.25f);  // the board's clock runs on; the movers' is held
            bool still = (mi.Movers[0].transform.position - held).sqrMagnitude < 1e-8f;
            bool beat = upBefore && !mi.PopWalls[0].Collider.enabled && !mi.Zones[0].SpikesUp(0.25f);
            var belt = Simulate(mi, new Vector2(-2.5f, -2.5f), new Vector2(0.01f, 0f), 120);
            beat &= belt[belt.Count - 1].Pos.y < -2f;  // the belt (pushing up) is off, so the puck stays near where it was put
            mi.OnBreak = false;
            mi.TickMovers(1.5f, true, 1.5f);
            bool resumes = (mi.Movers[0].transform.position - held).sqrMagnitude > 0.01f && mi.Zones[0].SpikesUp(0.25f);
            Object.DestroyImmediate(mi.Root);

            var clock = new FakeClock(new System.DateTime(2026, 10, 16, 8, 0, 0, System.DateTimeKind.Utc));
            var p = new Progress(new MemoryPrefs(), clock);
            var laser = OutOfOffice.Boosters.Find(BoosterKind.Laser);
            bool locked = !p.BoosterUnlocked(BoosterKind.Laser);
            for (int i = 0; i <= laser.UnlockRoom; i++) p.Record(i, 1, 9);
            bool unlocks = p.BoosterUnlocked(BoosterKind.Laser) && !p.BoosterUnlocked(BoosterKind.MagnetBed);
            p.IntroduceBooster(BoosterKind.Laser);
            p.IntroduceBooster(BoosterKind.Laser);  // only once
            bool gift = p.BoosterCount(BoosterKind.Laser) == OutOfOffice.Boosters.StartGift;
            int used = 0;
            while (p.UseBooster(BoosterKind.Laser)) used++;
            bool counts = used == OutOfOffice.Boosters.StartGift && p.BoosterCount(BoosterKind.Laser) == 0;
            bool ordered = true;
            for (int i = 1; i < OutOfOffice.Boosters.All.Length; i++)
                ordered &= OutOfOffice.Boosters.All[i].UnlockRoom > OutOfOffice.Boosters.All[i - 1].UnlockRoom && OutOfOffice.Boosters.All[i].UnlockRoom < Levels.All.Length;
            bool prices = OutOfOffice.Boosters.BoxGems < OutOfOffice.Boosters.BoxEach * OutOfOffice.Boosters.All.Length * OutOfOffice.Boosters.GemCost;

            bool ok = pulls && reach && still && beat && resumes && locked && unlocks && gift && counts && ordered && prices;
            Report("boosters", ok, $"magnet bed sinks a roll beside the bed {pulls} (without it: {plain} at {plainEnd}, with it: {pulled}); nothing out of reach {reach}; " +
                $"coffee break holds movers {still}, walls, staples and belt stand down {beat}, all resume {resumes}; locked {locked}, unlocks {unlocks}, three to start once {gift}, " +
                $"counts {counts}; unlocks in order {ordered}; box discount {prices}");
        }

        // 27. Chests (OUT-14): the published odds add up and keep each tier's floor; a seeded run of 20,000 opens lands
        // on them; the pity timer guarantees an Epic within 10 and a Legendary within 30; duplicates pay clips and avoid
        // repeating; the Clip Shop sells what's missing; paid chests are off in Belgium and the Netherlands.
        static void ChestChecks()
        {
            System.Func<CosmeticItem, bool> none = i => false;
            bool sums = true, floors = true;
            foreach (ChestTier tier in System.Enum.GetValues(typeof(ChestTier)))
            {
                var odds = Chests.Odds(tier, none, 0, 0);
                float total = 0f;
                foreach (var o in odds) total += o;
                sums &= Mathf.Abs(total - 1f) < 1e-4f;
                if (tier == ChestTier.SupplyCloset) floors &= odds[(int)Rarity.Common] == 0f;
                if (tier == ChestTier.ExecutiveSafe) floors &= odds[(int)Rarity.Common] == 0f && odds[(int)Rarity.Rare] == 0f;
            }

            // Seeded: the drawer's spread over 20,000 opens (nothing owned, no pity) sits within 1.5 points of its odds.
            var drawer = Chests.Odds(ChestTier.DeskDrawer, none, 0, 0);
            var seen = new int[4];
            var rng = new System.Random(4242);
            const int N = 20000;
            for (int i = 0; i < N; i++) seen[(int)Chests.Roll(ChestTier.DeskDrawer, none, rng, 0, 0, "", out _).Rarity]++;
            bool spread = true;
            string got = "";
            for (int r = 0; r < 4; r++)
            {
                float share = seen[r] / (float)N;
                spread &= Mathf.Abs(share - drawer[r]) < 0.015f;
                got += $"{(Rarity)r} {share:P1}/{drawer[r]:P1} ";
            }

            // Pity: 9 dry opens make the 10th Epic or better; 29 make the 30th Legendary.
            bool epicPity = true, legendPity = true;
            for (int i = 0; i < 300; i++)
            {
                epicPity &= Chests.Roll(ChestTier.DeskDrawer, none, new System.Random(i), Chests.EpicPity - 1, 0, "", out _).Rarity >= Rarity.Epic;
                legendPity &= Chests.Roll(ChestTier.DeskDrawer, none, new System.Random(i), 0, Chests.LegendaryPity - 1, "", out _).Rarity == Rarity.Legendary;
            }
            var clock = new FakeClock(new System.DateTime(2026, 10, 16, 8, 0, 0, System.DateTimeKind.Utc));
            var p = new Progress(new MemoryPrefs(), clock);
            int dry = 0;
            bool counted = true;
            for (int i = 0; i < 40; i++)
            {
                var item = p.OpenChest(ChestTier.DeskDrawer, new System.Random(1000 + i), out _, out _);
                dry = item.Rarity >= Rarity.Epic ? 0 : dry + 1;
                counted &= p.SinceEpic == dry && dry < Chests.EpicPity;
            }

            // Everything owned: duplicates pay clips by rarity, never the same one twice running, and nothing is regranted.
            var full = new Progress(new MemoryPrefs(), clock);
            foreach (var i in Cosmetics.All) if (i.FromChest) full.Grant(i);
            bool dupes = true;
            string last = null;
            int clipsBefore = full.Clips;
            for (int i = 0; i < 30; i++)
            {
                var item = full.OpenChest(ChestTier.SupplyCloset, new System.Random(77 + i), out bool dup, out int clips);
                dupes &= dup && clips == Chests.DuplicateClips[(int)item.Rarity] && full.Clips == clipsBefore + clips;
                bool canRepeat = Chests.InChests(item.Rarity) < 2;
                dupes &= canRepeat || item.Id != last;
                last = item.Id;
                clipsBefore = full.Clips;
            }

            // The Clip Shop: a chosen missing item for its price; refused when short or already owned.
            var shopper = new Progress(new MemoryPrefs(), clock);
            var want = System.Array.Find(Cosmetics.All.ToArray(), i => i.FromChest && i.Rarity == Rarity.Rare);
            bool refused = !shopper.BuyWithClips(want);
            shopper.AddClips(Chests.ClipPrice[(int)Rarity.Rare], "test");
            bool bought = shopper.BuyWithClips(want) && shopper.Owns(want) && shopper.Clips == 0 && !shopper.BuyWithClips(want);

            bool region = !Chests.PaidAllowedIn("BE") && !Chests.PaidAllowedIn("NL") && Chests.PaidAllowedIn("TR") && Chests.PaidAllowedIn("US");
            bool prices = Chests.ClosetGems < Chests.SafeGems && Chests.ClipPrice[0] < Chests.ClipPrice[3] && Chests.DuplicateClips[3] < Chests.ClipPrice[3];

            bool ok = sums && floors && spread && epicPity && legendPity && counted && dupes && refused && bought && region && prices;
            Report("chests", ok, $"odds add up {sums}, tier floors {floors}; seeded spread {spread} ({got}); pity epic {epicPity}, legendary {legendPity}, counters {counted}; " +
                $"duplicates pay clips, no repeats {dupes}; clip shop refuses when short {refused}, sells {bought}; paid chests off in BE/NL {region}; prices {prices}");
        }

        // 28. Achievements (OUT-9): 50+ distinct goals, every one reachable (a save that has done everything has them
        // all), counted from stats and the save, claimed once for their gems, announced once, and an old save's done
        // ones wait to be claimed without a pile of toasts.
        static void AchievementChecks()
        {
            var all = OutOfOffice.Achievements.All;
            var keys = new HashSet<string>();
            bool shaped = all.Length >= 50;
            foreach (var a in all) shaped &= keys.Add(a.Key) && a.Name.Length <= 32 && a.Text.Length <= 60 && a.Target > 0 && a.Gems > 0;

            var clock = new FakeClock(new System.DateTime(2026, 10, 16, 8, 0, 0, System.DateTimeKind.Utc));
            var fresh = new Progress(new MemoryPrefs(), clock);
            bool clean = OutOfOffice.Achievements.Ready(fresh) == 0 && OutOfOffice.Achievements.Fresh(fresh).Count == 0;

            // One hole in one: done, announced once, claimed once for its gems.
            fresh.BumpStat(OutOfOffice.Achievements.HoleInOne);
            var hio = OutOfOffice.Achievements.Find("hio1");
            bool counted = OutOfOffice.Achievements.Done(hio, fresh) && OutOfOffice.Achievements.Fresh(fresh).Exists(a => a.Key == "hio1");
            fresh.AnnounceAchievement("hio1");
            int gems = fresh.Gems;
            bool claimOnce = fresh.ClaimAchievement("hio1") && fresh.Gems == gems + hio.Gems && !fresh.ClaimAchievement("hio1")
                && !OutOfOffice.Achievements.Fresh(fresh).Exists(a => a.Key == "hio1");
            bool notEarly = !fresh.ClaimAchievement("hio10");

            // Everything done: every achievement is reachable.
            var maxed = new Progress(new MemoryPrefs(), clock);
            for (int i = 0; i < Levels.All.Length; i++)
            {
                maxed.Record(i, 3, 1);
                maxed.StampCoffees(i, Levels.All[i].Stars.Count);
            }
            foreach (var item in Cosmetics.All) maxed.Grant(item);
            foreach (var g in OutOfOffice.Gadgets.All) maxed.MarkGadget(g.Key);
            for (int i = 0; i < 50; i++) maxed.OpenChest(ChestTier.DeskDrawer, new System.Random(i), out _, out _);
            foreach (var key in new[] { OutOfOffice.Achievements.HoleInOne, OutOfOffice.Achievements.ParFinish, OutOfOffice.Achievements.Shots,
                OutOfOffice.Achievements.BoostersUsed, OutOfOffice.Achievements.QuestsClaimed, OutOfOffice.Achievements.DaysPlayed,
                OutOfOffice.Achievements.BestStreak, OutOfOffice.Achievements.Pokes, OutOfOffice.Achievements.Pinball,
                OutOfOffice.Achievements.BossClean, OutOfOffice.Achievements.ClipBuys })
                maxed.SetStat(key, 100000);
            string unreachable = "";
            foreach (var a in all) if (!OutOfOffice.Achievements.Done(a, maxed)) unreachable += a.Key + " ";
            bool reachable = unreachable.Length == 0;

            // An old save: what it has done waits to be claimed, without toasts.
            var oldPrefs = new MemoryPrefs();
            var before = new Progress(oldPrefs, clock);
            for (int i = 0; i < 12; i++) before.Record(i, 3, 1);
            oldPrefs.Values.Remove("buca.ach.version");
            foreach (var a in all) oldPrefs.Values.Remove("buca.ach.seen." + a.Key);
            var migrated = new Progress(oldPrefs, clock);
            bool quiet = OutOfOffice.Achievements.Fresh(migrated).Count == 0 && OutOfOffice.Achievements.Ready(migrated) > 0;

            bool ok = shaped && clean && counted && claimOnce && notEarly && reachable && quiet;
            Report("achievements", ok, $"{all.Length} achievements, distinct and sized {shaped}; new save clean {clean}; counts {counted}; claimed once {claimOnce}; " +
                $"not before done {notEarly}; all reachable {reachable} {unreachable}; old saves quiet but claimable {quiet}");
        }

        // 21. Data (OUT-34, OUT-35): stable A/B buckets, the event log cap, save snapshots, merge, and server time.
        static void Data()
        {
            // Buckets: stable for an install, spread across installs.
            int[] counts = new int[2];
            for (int i = 0; i < 2000; i++) counts[Experiments.Bucket("install-" + i, "continue_shots", 2)]++;
            bool stable = Experiments.Bucket("abc", "x", 3) == Experiments.Bucket("abc", "x", 3);
            bool spread = counts[0] > 800 && counts[1] > 800;
            Experiments.Overrides["continue_shots"] = 1;
            bool pinned = Experiments.ContinueShots == 5;
            Experiments.Overrides.Remove("continue_shots");

            // The local event log: JSON lines, rolled over when full.
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ooo-analytics-test.jsonl");
            foreach (var old in new[] { path, path + ".1" }) if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
            var sink = new LocalLogSink(path, 2000);
            for (int i = 0; i < 100; i++) sink.Send("test", new Dictionary<string, object> { { "i", i }, { "text", "a \"quoted\" line" } });
            sink.Flush();  // lines are buffered (OUT-36)
            bool rolled = System.IO.File.Exists(path + ".1") && new System.IO.FileInfo(path).Length < 4000;
            string first = System.IO.File.ReadAllLines(path)[0];
            bool json = first.StartsWith("{\"e\":\"test\"") && first.Contains("\\\"quoted\\\"");

            // Snapshots round-trip, and a merge keeps the best progress without duplicating gems.
            var clock = new FakeClock(new System.DateTime(2026, 10, 16, 8, 0, 0, System.DateTimeKind.Utc));
            var pa = new MemoryPrefs();
            var a = new Progress(pa, clock);
            a.Record(0, 3, 2);
            a.Record(1, 1, 6);
            a.AddGems(100, "test");
            var snapA = SaveSnapshot.Capture(pa, clock.UtcNow, a.InstallId);
            var round = SaveSnapshot.FromJson(snapA.ToJson());
            var restored = new MemoryPrefs();
            round.ApplyTo(restored);
            var r = new Progress(restored, clock);
            bool roundTrip = r.Best("first") == 3 && r.Gems == 100 && r.Unlocked == a.Unlocked;

            clock.Advance(System.TimeSpan.FromHours(1));
            var pb = new MemoryPrefs();
            var bProg = new Progress(pb, clock);
            bProg.Record(1, 3, 3);           // better on level 2 on the other device
            bProg.AddGems(40, "test");
            bProg.Grant(Catalog.NoAds);
            var snapB = SaveSnapshot.Capture(pb, clock.UtcNow, bProg.InstallId);
            var merged = SaveSnapshot.Merge(snapA, snapB);
            var mp = new MemoryPrefs();
            merged.ApplyTo(mp);
            var m = new Progress(mp, clock);
            bool merge = m.Best("first") == 3 && m.Best(Levels.All[1].Id) == 3 && m.BestShots(Levels.All[1].Id) == 3
                         && m.Gems == 40 && m.Bought(Catalog.NoAds) && SaveSnapshot.Merge(null, snapA) == snapA;

            // The backup file round-trips too.
            string backupPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ooo-backup-test.json");
            var backup = new LocalBackup(backupPath);
            backup.Upload(snapA);
            bool backupOk = backup.Download()?.Int("buca.gems") == 100;

            // Server time: the HTTP Date header and the clock offset.
            bool parsed = ServerTime.TryParseDate("Fri, 16 Oct 2026 12:00:00 GMT", out var serverUtc) && serverUtc == new System.DateTime(2026, 10, 16, 12, 0, 0, System.DateTimeKind.Utc)
                          && !ServerTime.TryParseDate("not a date", out _);
            var sc = new ServerClock();
            var device = System.DateTime.UtcNow;
            sc.Apply(device.AddMinutes(90), device);
            bool offset = sc.Synced && System.Math.Abs((sc.UtcNow - System.DateTime.UtcNow).TotalMinutes - 90) < 1;

            bool ok = stable && spread && pinned && rolled && json && roundTrip && merge && backupOk && parsed && offset;
            Report("data", ok, $"A/B stable {stable}, spread {counts[0]}/{counts[1]}, override {pinned}; log rolls {rolled}, json {json}; " +
                $"snapshot round-trip {roundTrip}, merge best-of without duplicate gems {merge}, backup {backupOk}; server date {parsed}, offset {offset}");
        }

        // 17. Every room's opener is short enough to read in the second it is shown (OUT-22).
        static void Openers()
        {
            string longest = "";
            foreach (var l in Levels.All)
            {
                string o = Quips.Opener(l.Id);
                if (o.Length > longest.Length) longest = o;
            }
            // Calendar openers (OUT-26): 2026-10-12 is a Monday.
            var monday = new System.DateTime(2026, 10, 12, 9, 0, 0);
            string mon = Quips.Calendar(monday), wed = Quips.Calendar(monday.AddDays(2).AddHours(3)), fri = Quips.Calendar(monday.AddDays(4).AddHours(7)),
                sat = Quips.Calendar(monday.AddDays(5)), late = Quips.Calendar(monday.AddDays(1).AddHours(14));
            bool calendar = mon == "Monday. Again." && wed == null && fri == "Almost the weekend." && sat != null && late == "Working late?";
            foreach (var line in new[] { mon, fri, sat, late }) calendar &= line != null && line.Length <= Quips.OpenerMaxLength;
            bool ok = calendar && longest.Length > 0 && longest.Length <= Quips.OpenerMaxLength && Quips.Opener("no-such-room").Length <= Quips.OpenerMaxLength;
            Report("openers", ok, $"{Levels.All.Length} rooms, longest opener \"{longest}\" ({longest.Length} chars, limit {Quips.OpenerMaxLength})");
        }

        static float Min(List<float> v) { float m = float.MaxValue; foreach (var x in v) m = Mathf.Min(m, x); return m; }
        static float Max(List<float> v) { float m = float.MinValue; foreach (var x in v) m = Mathf.Max(m, x); return m; }
    }
}
