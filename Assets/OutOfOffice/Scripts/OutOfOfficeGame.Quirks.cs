using UnityEngine;

namespace OutOfOffice
{
    // Small details players discover and share (OUT-26). All of them are faces, motion and sound: nothing here touches
    // physics. Proximity reactions only run while the employee waits for the next shot, so they never fight the faces
    // the game shows while a shot is in flight.
    public partial class OutOfOfficeGame
    {
        const float BossFear = 1.9f;        // this close to the boss's chair, the employee trembles
        const float CoffeeLove = 2.3f;      // ... to a coffee machine: starstruck
        const float BedNear = 2.4f;         // ... to the bed: sleepy already
        const float HazardWince = 1.35f;    // ... to a cactus or a pacing coworker: eyes squeezed shut
        const float BuzzSeconds = 2.4f;     // caffeine buzz after the last coffee
        const int PinballBounces = 4;       // bounces in one shot that leave the employee dizzy

        float buzzUntil = -1f;
        int shotBounces;
        bool sessionOpenerUsed;
        Secretary speaker;  // who the speech bubble belongs to while the receptionist talks

        // The receptionist (OUT-38) talks only while the employee waits, nobody else is talking and no card is open.
        void WireReceptionist()
        {
            Secretary.MayTalk = () => state == State.Ready && !hud.Speaking && !hud.PanelOpen;
            Secretary.Say = (who, line) => { speaker = who; hud.Say(line, 2.4f); };
            // Silent behind the home screen: the room there is only a backdrop.
            Secretary.Sound = (clip, volume) => { if (state != State.Title) sound.Play(clip, volume, Random.Range(0.96f, 1.04f), 0.4f); };
        }

        void UpdateQuirks(float dt)
        {
            if (level == null || mascot == null) return;
            Vector2 pos = level.Puck.Body.position;
            float tremble = 0f;

            bool buzzing = Time.time < buzzUntil;
            if (buzzing) mascot.Express(Eyes.Wide, Mouth.Grin, 0.2f, 1.2f);  // wired, but no shiver: it read as shaking

            bool waiting = state == State.Ready || state == State.Aiming;
            if (waiting && !buzzing)
            {
                if (Near(pos, BlockKind.Kicker, BossFear))
                {
                    if (state == State.Ready) tremble = 1f;  // never while charging a shot
                    mascot.Express(Eyes.Wide, Mouth.Flat, 0.2f, 0.9f);
                }
                else if (NearZone(pos, ZoneKind.Magnet, CoffeeLove)) mascot.Express(Eyes.Star, Mouth.Grin, 0.2f, 0.8f);
                else if (NearHazard(pos)) mascot.Express(Eyes.Squeeze, Mouth.Wavy, 0.2f, 0.8f);
                else if (state == State.Ready && (Def.Hole - pos).magnitude < BedNear) mascot.Express(Eyes.Sleepy, Mouth.Smile, 0.2f, 0.6f);
            }
            else if (state == State.Moving && level.Puck.CurrentSurface == Surface.Ice && level.Puck.Body.linearVelocity.sqrMagnitude > 4f)
                mascot.Express(Eyes.Wide, Mouth.O, 0.2f, 0.7f);  // whoa, wet floor

            mascot.Tremble = Mathf.MoveTowards(mascot.Tremble, progress.ReducedMotion ? 0f : tremble, dt * 4f);

            // Coworkers look up when the employee passes close.
            CoworkerFace.Player = level.Puck.Visual != null ? level.Puck.Visual.position : (Vector3)pos;
        }

        bool Near(Vector2 pos, BlockKind kind, float reach)
        {
            foreach (var b in Def.Blocks)
                if (b.Kind == kind && (b.Center - pos).magnitude < reach) return true;
            return false;
        }

        bool NearZone(Vector2 pos, ZoneKind kind, float reach)
        {
            foreach (var z in level.Zones)
                if (z.Def.Kind == kind && (z.Def.Center - pos).magnitude < reach) return true;
            return false;
        }

        // Static hazards (cacti) by their place on the board; pacing coworkers by where they are right now.
        bool NearHazard(Vector2 pos)
        {
            foreach (var b in Def.Blocks)
                if (b.Kind == BlockKind.Hazard && b.Period <= 0f && (b.Center - pos).magnitude < HazardWince) return true;
            foreach (var face in CoworkerFace.Active)
                if (face != null && ((Vector2)face.transform.position - pos).magnitude < HazardWince) return true;
            return false;
        }

        // Pinged around like a pinball: dizzy when it finally stops.
        void OnPinballFinish()
        {
            CountStat(Achievements.Pinball);
            mascot.OnDizzyShort();
            mascot.Twirl(progress.ReducedMotion ? 0f : 360f, 0.6f);
        }

        // The first room of a session knows what day and hour it is: Monday mornings, Friday afternoons, weekends
        // and late nights get their own opener. Read from the guarded clock, so it matches the daily rewards.
        string SessionOpener()
        {
            if (sessionOpenerUsed) return null;
            sessionOpenerUsed = true;
            return Quips.Calendar(progress.Now);
        }
    }
}
