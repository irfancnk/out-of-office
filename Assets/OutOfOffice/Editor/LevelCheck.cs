using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Proves every level can be finished with real gameplay physics, in up to MaxShots shots, and reports par and star reach.
    // Shots continue from where the puck rests (golf style), including switch, crate, and star state.
    public static class LevelCheck
    {
        const int MaxShots = 5;
        [MenuItem("Out of Office/Check Levels")]
        static void RunFromMenu()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Out of Office: stop Play mode before you check levels.");
                return;
            }
            Run(Levels.All);
        }

        // Batch entry point: Unity -batchmode -executeMethod OutOfOffice.EditorTools.LevelCheck.RunBatch
        public static void RunBatch()
        {
            int bad = Run(Levels.All);
            EditorApplication.Exit(bad == 0 ? 0 : 2);
        }

        public struct ShotOutcome
        {
            public ShotResult Result;
            public LevelSnapshot After;   // state after the puck and crates settle
            public int Stars;
        }

        // One room's result: the report line, and the numbers the level editor shows (OUT-12).
        public class Report
        {
            public LevelDef Def;
            public bool Ok, Solved;
            public string Line;
            public int MinShots, SuggestedPar, StarsReached;
            public float OneShotWin, Hazard;  // percent of the first-shot sweep
            public double Seconds;
            // The shots of the first solution found, each from the state it was played from.
            public readonly List<(LevelSnapshot From, Vector2 Aim)> Solution = new List<(LevelSnapshot, Vector2)>();
        }

        public static int Run(LevelDef[] levels)
        {
            var previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            var report = new StringBuilder("OUT OF OFFICE LEVEL CHECK\n");
            int bad = 0;
            if (levels == Levels.All)
            {
                string chapters = CheckChapters(out bool chaptersOk);
                if (!chaptersOk) bad++;
                report.AppendLine($"HSLEVEL chapters {(chaptersOk ? "PASS" : "FAIL")} {chapters}");
                string desks = CheckReceptions(out bool desksOk);
                if (!desksOk) bad++;
                report.AppendLine($"HSLEVEL receptionists {(desksOk ? "PASS" : "FAIL")} {desks}");
            }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                for (int i = 0; i < levels.Length; i++)
                {
                    var r = CheckBuilt(levels[i]);
                    if (!r.Ok) bad++;
                    report.AppendLine($"HSLEVEL {i + 1,2} {r.Def.Id,-12} {r.Line}");
                }
            }
            finally
            {
                Physics2D.simulationMode = previousMode;
            }
            report.AppendLine($"HSLEVEL done in {watch.Elapsed.TotalSeconds:0.0}s, levels failing: {bad}");
            Debug.Log(report.ToString());
            return bad;
        }

        // Checks one room on its own: what the level editor's "Run LevelCheck" button calls.
        public static Report Check(LevelDef def)
        {
            var previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            try { return CheckBuilt(def); }
            finally { Physics2D.simulationMode = previousMode; }
        }

        static Report CheckBuilt(LevelDef def)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var root = new GameObject("LevelCheck");
            try
            {
                var inst = LevelBuilder.Build(def, root.transform, false);
                var r = new Report { Def = def };
                r.Line = CheckLevel(inst, def, r, out r.Ok);
                r.Seconds = watch.Elapsed.TotalSeconds;
                return r;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Replays a report's solution and returns where the puck went on each shot, for the editor to draw.
        public static List<List<Vector2>> Trace(LevelDef def, Report report)
        {
            var paths = new List<List<Vector2>>();
            var previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            var root = new GameObject("LevelCheck Trace");
            try
            {
                var inst = LevelBuilder.Build(def, root.transform, false);
                foreach (var (from, aim) in report.Solution)
                {
                    var path = new List<Vector2> { from.Puck };
                    Shoot(inst, from, aim, path);
                    paths.Add(path);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Physics2D.simulationMode = previousMode;
            }
            return paths;
        }

        // The catalog must give every chapter a known theme and a non-empty run of rooms, and every room it names must
        // load, or the home screen's chapter bar and the level banners point at the wrong chapter (OUT-12).
        static string CheckChapters(out bool ok)
        {
            var starts = Themes.ChapterStart;
            var chapters = Levels.Chapters;
            ok = chapters.Count > 0 && starts.Length == chapters.Count + 1 && starts[0] == 0 && starts[starts.Length - 1] == Levels.All.Length;
            for (int c = 1; c < starts.Length; c++) ok &= starts[c] > starts[c - 1];
            var keys = new HashSet<string>();
            var notes = new StringBuilder();
            int named = 0;
            foreach (var ch in chapters)
            {
                named += ch.LevelIds.Count;
                if (!keys.Add(ch.Key)) { ok = false; notes.Append($" DUPLICATE CHAPTER {ch.Key}"); }
                if (Themes.ByKey(ch.ThemeKey) == null) { ok = false; notes.Append($" UNKNOWN THEME {ch.ThemeKey}"); }
            }
            if (named != Levels.All.Length) { ok = false; notes.Append($" {named - Levels.All.Length} ROOMS MISSING OR REPEATED"); }
            for (int i = 0; i < Levels.All.Length; i++)
                if (Levels.IndexOf(Levels.All[i].Id) != i) { ok = false; notes.Append($" ROOM {Levels.All[i].Id} LISTED TWICE"); }
            return $"{chapters.Count} chapters, starts {string.Join(",", starts)}, {Levels.All.Length} levels{notes}";
        }

        static string CheckLevel(LevelInstance inst, LevelDef def, Report report, out bool ok)
        {
            var start = inst.Capture(def.Start, 0, 0f);
            bool timed = inst.Movers.Count > 0 || inst.PopWalls.Count > 0 || inst.Zones.Exists(z => z.Kind == ZoneKind.Spikes);
            float[] phases = timed ? new[] { 0f, 0.8f, 1.6f } : new[] { 0f };
            int shots = 0, wins = 0, hazards = 0, maxStars = 0;
            var touched = new bool[def.Stars.Count];  // coffees any explored shot collected, to name the unreachable ones
            var rests = new Dictionary<Vector3Int, LevelSnapshot>();
            // How each remembered resting state was reached, so the first solution can be replayed and drawn.
            var parents = new Dictionary<LevelSnapshot, (LevelSnapshot From, Vector2 Aim)>();
            string example = "-";

            foreach (float t0 in phases)
            for (int angle = 0; angle < 360; angle += 3)
            for (int p = 1; p <= 10; p++)
            {
                start.Time = t0;
                var aim = Aim(angle, p / 10f);
                var o = Shoot(inst, start, aim);
                Touch(touched, o);
                shots++;
                maxStars = Mathf.Max(maxStars, o.Stars);
                if (o.Result == ShotResult.Sunk)
                {
                    if (wins++ == 0)
                    {
                        example = $"{angle} deg power {p / 10f:0.0}";
                        report.Solution.Add((At(start, t0), aim));
                    }
                }
                else if (o.Result == ShotResult.Hazard || o.Result == ShotResult.Fell) hazards++;
                else if (o.Result == ShotResult.Rest && Remember(rests, o.After)) parents[o.After] = (At(start, t0), aim);
            }

            int minShots = wins > 0 ? 1 : 0;
            // Deeper shots, golf style, from a spread of resting places (and the switch/crate state they produced).
            var frontier = rests;
            // Keep exploring up to par even after a win, so stars on multi-shot routes are counted too.
            for (int depth = 2; depth <= MaxShots && (minShots == 0 || depth <= def.Par + 1) && !(minShots > 0 && maxStars >= def.Stars.Count); depth++)
            {
                var next = new Dictionary<Vector3Int, LevelSnapshot>();
                int found = 0;
                foreach (var snap in Spread(frontier, 12, def.Hole))
                {
                    for (int angle = 0; angle < 360; angle += 5)
                    for (int p = 2; p <= 10; p += 2)
                    {
                        var aim = Aim(angle, p / 10f);
                        var o = Shoot(inst, snap, aim);
                        Touch(touched, o);
                        maxStars = Mathf.Max(maxStars, o.Stars + Count(snap.Stars));
                        if (o.Result == ShotResult.Sunk)
                        {
                            if (found++ == 0 && minShots == 0)
                            {
                                example = $"{depth} shots, last {angle} deg power {p / 10f:0.0}";
                                var chain = new List<(LevelSnapshot, Vector2)> { (snap, aim) };
                                for (var s = snap; parents.TryGetValue(s, out var up); s = up.From) chain.Insert(0, up);
                                report.Solution.AddRange(chain);
                            }
                        }
                        else if (o.Result == ShotResult.Rest && Remember(next, o.After)) parents[o.After] = (snap, aim);
                    }
                }
                if (found > 0 && minShots == 0) minShots = depth;
                frontier = next;
            }

            ok = minShots > 0;
            report.Solved = ok;
            report.MinShots = minShots;
            report.StarsReached = maxStars;
            report.OneShotWin = 100f * wins / shots;
            report.Hazard = 100f * hazards / shots;
            // Par is the fewest shots, plus one when the one-shot win is a needle in a haystack.
            report.SuggestedPar = minShots == 1 && report.OneShotWin < 2f ? 2 : Mathf.Max(1, minShots);
            string parNote = !ok ? "" : minShots > def.Par ? $"  PAR {def.Par} TOO LOW (min {minShots})" : minShots < def.Par - 1 ? $"  PAR {def.Par} LOOSE (min {minShots})" : "";
            string starNote = def.Stars.Count == 0 ? "no stars" : $"stars reached {maxStars}/{def.Stars.Count}";
            bool solved = ok;
            starNote += CoffeeRules(def, maxStars, touched, out bool coffeeOk);
            // The shot limit (OUT-27) must leave two spare shots over the fewest this level can be finished in.
            bool limitOk = !solved || def.MaxShots >= minShots + 2;
            starNote += $"  limit {def.MaxShots}  empty {EmptyRun(def):0.0}" + (limitOk ? "" : $"  LIMIT TOO TIGHT (min {minShots})");
            // No room opens on a long stretch of bare floor (OUT-25): something to play with comes early.
            bool furnished = EmptyRun(def) <= MaxEmptyRun;
            if (!furnished) starNote += $"  EMPTY START ({EmptyRun(def):0.0} of bare floor, max {MaxEmptyRun})";
            limitOk &= furnished;
            starNote += ReceptionRules(def, out bool receptionOk);
            limitOk &= receptionOk;
            coffeeOk &= limitOk;
            ok = solved && coffeeOk;
            string stats = $"min shots {minShots}  par {def.Par}  one-shot win {100f * wins / shots,5:0.0}%  hazard {100f * hazards / shots,4:0.0}%  {starNote}  e.g. {example}{parNote}";
            return !solved ? $"UNSOLVED in {MaxShots} shots  ({starNote})" : coffeeOk ? "OK  " + stats : "BAD " + stats;
        }

        // The receptionist's corner (OUT-38) only takes floor no route needs: it keeps a puck's width and a little more
        // from everything else in the room, so it never narrows a lane or traps the employee.
        public const float ReceptionClearance = 0.9f;

        static string ReceptionRules(LevelDef def, out bool ok)
        {
            ok = true;
            var notes = new StringBuilder();
            foreach (var desk in def.Blocks)
            {
                if (desk.Reception == 0) continue;
                var r = new Rect(desk.Center - desk.Size / 2f, desk.Size);
                float clear = float.MaxValue;
                string what = "";
                void Near(string name, float d) { if (d < clear) { clear = d; what = name; } }
                foreach (var b in def.Blocks)
                {
                    if (b == desk) continue;
                    float reach = b.Shape == BlockShape.Post ? b.Size.x * 0.5f : 0.5f * b.Size.magnitude;
                    Near(b.Kind.ToString().ToLowerInvariant(), Geometry.DistanceToRect(b.Center, r) - reach - b.MoveOffset.magnitude);
                }
                foreach (var s in def.Stars) Near("coffee", Geometry.DistanceToRect(s, r) - 0.3f);
                foreach (var z in def.Zones) Near(z.Kind.ToString().ToLowerInvariant(), Geometry.DistanceToRect(z.Center, r) - 0.5f * Mathf.Max(z.Size.x, z.Size.y));
                foreach (var rail in def.Rails) foreach (var p in rail.Path) Near("rail", Geometry.DistanceToRect(p, r) - rail.Width * 0.5f);
                Near("bed", Geometry.DistanceToRect(def.Hole, r) - 0.6f);
                Near("start", Geometry.DistanceToRect(def.Start, r) - 0.4f);
                notes.Append($"  reception {clear:0.0} clear");
                if (clear < ReceptionClearance) { ok = false; notes.Append($" RECEPTION CROWDED (by the {what})"); }
            }
            return notes.ToString();
        }

        // One receptionist a chapter (OUT-38).
        static string CheckReceptions(out bool ok)
        {
            var counts = new int[Themes.ChapterCount];
            for (int i = 0; i < Levels.All.Length; i++)
                foreach (var b in Levels.All[i].Blocks)
                    if (b.Reception != 0) counts[Themes.ChapterOf(i)]++;
            ok = System.Array.TrueForAll(counts, c => c == 1);
            return "per chapter " + string.Join(",", counts);
        }

        // Coffee placement rules (OUT-25). Coffees should reward a clever line, not sit where the player goes anyway:
        //   - every coffee can be collected within par + 1 shots, together with finishing
        //   - none sits on the start or on the bed
        //   - they are not all lined up on the plain straight shot from the start to the bed
        public const float CoffeeClearance = 1.2f;   // from the start and from the bed
        public const float StraightLane = 0.7f;      // how close to the start-bed line counts as "on the straight shot"
        public const float MaxEmptyRun = 3.5f;       // bare floor allowed between the start and the first thing in the room

        static string CoffeeRules(LevelDef def, int reached, bool[] touched, out bool ok)
        {
            ok = true;
            if (def.Stars.Count == 0) return "";
            var notes = new StringBuilder();
            if (reached < def.Stars.Count)
            {
                ok = false;
                notes.Append("  COFFEE UNREACHABLE");
                for (int i = 0; i < touched.Length; i++)
                    if (!touched[i]) notes.Append($" never touched ({def.Stars[i].x:0.#},{def.Stars[i].y:0.#})");
            }
            int onLine = 0;
            foreach (var s in def.Stars)
            {
                if ((s - def.Start).magnitude < CoffeeClearance) { ok = false; notes.Append($"  COFFEE ON START ({s.x:0.#},{s.y:0.#})"); }
                if ((s - def.Hole).magnitude < CoffeeClearance) { ok = false; notes.Append($"  COFFEE ON BED ({s.x:0.#},{s.y:0.#})"); }
                if (DistanceToSegment(s, def.Start, def.Hole) < StraightLane) onLine++;
            }
            if (onLine == def.Stars.Count && def.Stars.Count > 1) { ok = false; notes.Append($"  COFFEES ALL ON THE STRAIGHT SHOT"); }
            return $" (on straight shot {onLine}){notes}";
        }

        // How much bare floor lies between the start and the first thing in the room (OUT-25): the empty lower half
        // that makes a board look unfinished.
        public static float EmptyRun(LevelDef def)
        {
            float lowest = float.MaxValue;
            foreach (var b in def.Blocks)
            {
                float reach = b.Shape == BlockShape.Post ? b.Size.x * 0.5f : 0.5f * Mathf.Max(b.Size.x, b.Size.y) * Mathf.Abs(Mathf.Sin(b.Angle * Mathf.Deg2Rad)) + 0.5f * b.Size.y * Mathf.Abs(Mathf.Cos(b.Angle * Mathf.Deg2Rad));
                lowest = Mathf.Min(lowest, b.Center.y - reach - Mathf.Abs(b.MoveOffset.y));
            }
            foreach (var z in def.Zones) lowest = Mathf.Min(lowest, z.Center.y - 0.5f * Mathf.Max(z.Size.x, z.Size.y));
            foreach (var r in def.Rails) foreach (var p in r.Path) lowest = Mathf.Min(lowest, p.y);
            foreach (var s in def.Stars) lowest = Mathf.Min(lowest, s.y);
            return lowest == float.MaxValue ? 0f : Mathf.Max(0f, lowest - def.Start.y);
        }

        static void Touch(bool[] touched, ShotOutcome o)
        {
            if (o.After?.Stars == null) return;
            for (int i = 0; i < touched.Length && i < o.After.Stars.Length; i++) touched[i] |= o.After.Stars[i];
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
        }

        static Vector2 Aim(int angle, float power)
        {
            float rad = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * (Rules.MaxSpeed * Mathf.Pow(power, Rules.PowerCurve));
        }

        static int Count(bool[] flags)
        {
            int n = 0;
            foreach (bool f in flags) if (f) n++;
            return n;
        }

        // True when this is a new resting state worth exploring from.
        static bool Remember(Dictionary<Vector3Int, LevelSnapshot> rests, LevelSnapshot snap)
        {
            int switches = 0;
            for (int i = 0; i < snap.Switches.Length; i++) if (snap.Switches[i]) switches |= 1 << i;
            int crates = 0;
            foreach (var c in snap.CratePositions) crates = crates * 7 + Mathf.RoundToInt(c.x) * 3 + Mathf.RoundToInt(c.y);
            int broken = 0;
            for (int i = 0; i < snap.Broken.Length; i++) if (snap.Broken[i]) broken |= 1 << i;
            var key = new Vector3Int(Mathf.RoundToInt(snap.Puck.x / 1.2f), Mathf.RoundToInt(snap.Puck.y / 1.2f), switches * 1000 + crates + broken * 7919);
            if (rests.ContainsKey(key)) return false;
            rests[key] = snap;
            return true;
        }

        // Picks resting states that differ in switch/crate state first; within each, half are the ones closest to the
        // hole and half are spread over the rest of the board.
        static IEnumerable<LevelSnapshot> Spread(Dictionary<Vector3Int, LevelSnapshot> rests, int count, Vector2 hole)
        {
            var byGroup = new SortedDictionary<int, List<LevelSnapshot>>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
            foreach (var kv in rests)
            {
                if (!byGroup.TryGetValue(kv.Key.z, out var list)) byGroup[kv.Key.z] = list = new List<LevelSnapshot>();
                list.Add(kv.Value);
            }
            var picked = new List<LevelSnapshot>();
            int perGroup = Mathf.Max(2, count / Mathf.Max(1, byGroup.Count));
            foreach (var list in byGroup.Values)
            {
                list.Sort((a, b) => (a.Puck - hole).sqrMagnitude.CompareTo((b.Puck - hole).sqrMagnitude));
                int near = (perGroup + 1) / 2;
                for (int i = 0; i < list.Count && i < near; i++) picked.Add(list[i]);
                // A couple of the states that collected the most stars, then a spread over the rest.
                var byStars = new List<LevelSnapshot>(list);
                byStars.Sort((a, b) => Count(b.Stars).CompareTo(Count(a.Stars)));
                for (int i = 0; i < 2 && i < byStars.Count; i++) if (!picked.Contains(byStars[i])) picked.Add(byStars[i]);
                int rest = perGroup - near;
                for (int i = 0; i < rest && near + i < list.Count; i++)
                    picked.Add(list[near + (i * 7919) % (list.Count - near)]);
                if (picked.Count >= count) break;
            }
            return picked;
        }

        // With a trace list, records where the puck is after every step (the level editor draws it).
        public static ShotOutcome Shoot(LevelInstance inst, LevelSnapshot from, Vector2 velocity, List<Vector2> trace = null)
        {
            inst.Restore(from);
            inst.Puck.PlaceAt(from.Puck);
            PhysicsTests.Settle();  // the game always simulates between placing the puck and the next shot
            inst.Puck.Launch(velocity);
            float t = from.Time;
            int maxSteps = Mathf.CeilToInt(Rules.SafetyShotTime / Rules.Dt) + 5;
            var outcome = new ShotOutcome { Result = ShotResult.Rest };
            // Same order as the game's FixedUpdate: movers, Puck.Step (reads the last simulation), then simulate.
            for (int i = 0; i < maxSteps; i++)
            {
                t += Rules.Dt;
                inst.TickMovers(t, false);
                var info = inst.Puck.Step(Rules.Dt);
                if (info.Result != ShotResult.None && !(info.Result == ShotResult.Rest && Moving(inst) && i < maxSteps - 1))
                {
                    outcome.Result = info.Result;
                    break;
                }
                Physics2D.Simulate(Rules.Dt);
                trace?.Add(inst.Puck.Body.position);
            }
            // Resting on pop-up spikes only lasts until they rise, and that sends the puck back, as in the game.
            if (outcome.Result == ShotResult.Rest && inst.Zones.Exists(z => z.Kind == ZoneKind.Spikes && z.Contains(inst.Puck.Body.position)))
                outcome.Result = ShotResult.Hazard;
            outcome.Stars = inst.StarsCollected - Count(from.Stars);
            outcome.After = inst.Capture(inst.Puck.Body.position, from.Shots + 1, t);
            return outcome;
        }

        // The start state at the given moment of the movers' cycle: the sweep reuses one start and changes its time.
        static LevelSnapshot At(LevelSnapshot s, float time) => new LevelSnapshot
        {
            Puck = s.Puck, Shots = s.Shots, Time = time, Switches = s.Switches, Stars = s.Stars, Broken = s.Broken,
            CratePositions = s.CratePositions, CrateRotations = s.CrateRotations,
        };

        static bool Moving(LevelInstance inst)
        {
            foreach (var c in inst.Crates)
                if (c.Body.linearVelocity.sqrMagnitude > 0.0025f) return true;
            return false;
        }
    }
}
