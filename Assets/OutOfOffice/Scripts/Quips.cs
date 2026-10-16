using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // The writing bank. Dry office sarcasm, a little absurd menace, affectionate rather than cruel. One line per room
    // when it starts, short reactions for coffee, knocks and failures, and a line when the employee finally rests.
    // Essential instructions never live here: hints and labels say plainly what to do.
    public static class Quips
    {
        // ---- Welcome screen ----
        public const string WelcomeTitle = "Welcome back.";
        public const string WelcomeLine = "Your inbox is awake.\nYou shouldn't be.";
        public const string WelcomeFirstTitle = "Good morning. Allegedly.";
        public const string Tagline = "Collect coffee. Survive the office. Reach the bed.";

        // ---- One line per room (by level id) ----
        static readonly Dictionary<string, string> LevelLines = new Dictionary<string, string>
        {
            { "first", "First day. The bed is over there. Professionally speaking." },
            { "easy", "A quick sync. Historically inaccurate." },
            { "bank", "Bounce off the wall. Like feedback." },
            { "graze", "Corners: the only thing here with rounded edges." },
            { "curve", "Per my last remaining nerve." },
            { "kicker", "The boss's chair sends everyone back. Nothing personal." },
            { "boing", "This email could have been a sword fight." },
            { "rubber", "Office chairs: built for spinning, not sitting." },
            { "breeze", "The air con has opinions." },
            { "oneway", "One-way doors. Like my career path." },
            { "switch", "Badge in. The door is not impressed either." },
            { "spinner", "The spreadsheet wizard is spinning again." },
            { "popwalls", "Standing desks. They stand when you least expect it." },
            { "portal", "The mail chute. Faster than the IT ticket." },
            { "ramp", "A shortcut. Facilities would like a word." },
            { "popspikes", "Staples on the floor. HR is aware." },
            { "magnet", "The coffee machine is calling. It always is." },
            { "ice", "Wet floor. The sign was technically there." },
            { "fan", "Urgent, according to someone sitting down." },
            { "hopscotch", "Mind the gap in the budget. And the floor." },
            { "thorns", "Cacti. The only plants that survive here." },
            { "maze", "Submit the form to request the form." },
            { "crate", "Boxes of old reports. Nobody read them either." },
            { "speedpad", "Fast track. Approved by no one." },
            { "whirlpool", "Someone spilled the coffee. Productivity is spiralling." },
            { "lagoon", "Jump over the spill. Pretend it was a strategy." },
            { "corner", "The corner office. Not for you. Go around." },
            { "timing", "Rush hour at the printer." },
            { "turnstile", "The printer has chosen violence." },
            { "soft", "Beanbags: where meetings go to sleep." },
            { "tidepool", "Two spills. Twice the paperwork." },
            { "conveyor", "The mail belt delivers. Rarely to you." },
            { "tidegate", "Your request has been escalated to the coffee machine." },
            { "icebreak", "Stacks of folders. Break through the bureaucracy." },
            { "avalanche", "Polished floors. Expensive. Slippery. Very on brand." },
            { "door", "These doors have more meetings than you." },
            { "blizzard", "The executive air con. Bring a scarf." },
            { "curveswitch", "Badge, curve, bed. Easy. Said someone once." },
            { "rampbumper", "Jump, bounce, regret. The quarterly cycle." },
            { "skijump", "Synergy. Whatever that is." },
            { "spikeway", "Up, over, down. Like the share price." },
            { "pinball", "This meeting has no natural predators." },
            { "track", "The long way round. It's company policy." },
            { "summit", "Almost out of office. Don't make eye contact." },
            { "finale", "Last task of the day. The bed is right there." },
            { "rows", "Desk rows. Every seat has a view of another seat." },
            { "cubicles", "Cubicle farm. Free range, technically." },
            { "microwave", "Someone is reheating fish. Move quickly." },
            { "fridge", "The fridge is for labelled items only. Nothing is labelled." },
            { "boardroom", "Let's take this offline. Into the bed." },
            { "standup", "A quick standup. We've been standing for forty minutes." },
            { "feedback", "Your feedback has been noted. And ignored." },
            { "window", "A window seat. Visible from all angles. Great." },
            { "filing", "Filed under 'later'. Like everything." },
            { "paperwork", "Paperwork, in triplicate. Smash it." },
            { "elevator", "The executive elevator. Pretend you belong." },
            { "green", "The CEO's indoor putting green. Of course." },
            { "nightshift", "Night shift. The printer is finally quiet." },
            { "cleaner", "The cleaner mopped. You were warned." },
            { "deadline", "Three deadlines. One you. No pressure." },
        };

        // ---- Bubbles appear at three moments only: a short opener, a failure, the bed (OUT-22) ----

        // Openers are read in about a second, so they stay a few words long.
        public const int OpenerMaxLength = 24;
        static readonly string[] Openers = { "Clocking in.", "Here we go.", "Coffee first.", "Look busy.", "New room.", "Bed. Over there." };

        static readonly string[] Failures =
        {
            "Wrong meeting.",
            "Per my last attempt...",
            "Low battery.",
            "Escalating to undo.",
        };

        static readonly string[] Completion =
        {
            "Unavailable until morale improves.",
            "Finally, a productive meeting.",
            "Rest approved.",
            "Please find me asleep.",
            "Deadline met. Bed acquired.",
            "Action item: lie down.",
            "Circle back to bed.",
            "Another meeting survived.",
        };

        static readonly string[] Ace =
        {
            "One shot. Put it in my review.",
            "Efficient. Someone tell my manager.",
        };

        // ---- Rewards (gift box) ----
        static readonly string[] RewardLines =
        {
            "For exceeding expectations. Slightly.",
            "In recognition of showing up.",
            "Your hard work has been noticed. By this box.",
        };

        public const string RewardDuplicate = "Already yours. Recognised twice.";

        public static int Count => LevelLines.Count + Openers.Length + Failures.Length + Completion.Length + Ace.Length;

        public static string ForLevel(string id) => LevelLines.TryGetValue(id, out var line) ? line : "Another room. Another reason to lie down.";

        // The room's line cut to its first sentence (or the part before a colon), if that is short enough to read
        // at a glance; otherwise a stock opener. "First day. The bed is over there..." opens with "First day."
        public static string Opener(string id)
        {
            if (LevelLines.TryGetValue(id, out var line))
            {
                int end = line.IndexOfAny(new[] { '.', '!', '?' });
                string sentence = end >= 0 ? line.Substring(0, end + 1) : line;
                if (sentence.Length <= OpenerMaxLength) return sentence;
                int colon = line.IndexOf(':');
                if (colon > 0 && colon + 1 <= OpenerMaxLength) return line.Substring(0, colon) + ".";
            }
            int hash = 0;
            foreach (char c in id ?? "") hash = hash * 31 + c;  // stable per room, unlike string.GetHashCode
            return Openers[(hash & 0x7FFFFFFF) % Openers.Length];
        }

        public static string Fail() => Pick(Failures);
        public static string Rested() => Pick(Completion);
        public static string HoleInOne() => Pick(Ace);
        public static string Reward() => Pick(RewardLines);

        static string Pick(string[] lines) => lines[Random.Range(0, lines.Length)];
    }
}
