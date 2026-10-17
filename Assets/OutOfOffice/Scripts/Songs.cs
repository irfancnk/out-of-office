namespace OutOfOffice
{
    // The soundtrack (OUT-17): a menu theme and one song per chapter, written as data for the Composer.
    //
    // Direction: a toy-office band. Mallets, music box and kalimba for the tunes, a soft round bass, a light kit with
    // a typewriter woodblock. Every chapter has its own key, mode, tempo and groove, so a new chapter sounds like a
    // new floor of the building. The leitmotif is the opening phrase of the Open-Plan song (E G A G E, "clocking
    // in"): the menu theme states it slowly, and it comes back in the final chapter, the corner office.
    // Loudness: every song is mixed to the same RMS (Composer.TargetRms, -18 dBFS), so chapters never jump.
    public static class Songs
    {
        // The game's first tune, kept note for note: players already know it.
        static readonly string[] OpenPlanMelody =
        {
            "E5:2 G5:2 A5:1 G5:1 E5:2", "C5:2 E5:2 D5:1 C5:1 A4:2", "C5:2 D5:2 E5:1 F5:1 A5:2", "G5:3 F5:1 E5:2 D5:2",
            "E5:2 G5:2 C6:1 B5:1 G5:2", "A5:2 G5:1 E5:1 C5:2 E5:2", "F5:1 E5:1 D5:1 C5:1 A4:2 C5:2", "D5:3 B4:1 G4:2 R:2",
            "A5:3 G5:1 F5:2 A5:2", "G5:3 F5:1 E5:2 D5:2", "E5:2 G5:2 B5:2 G5:2", "A5:4 E5:2 R:2",
            "F5:2 A5:2 C6:2 A5:2", "G5:2 B5:2 D6:2 B5:1 A5:1", "C6:4 G5:2 E5:2", "C5:3 D5:1 E5:2 G4:2",
        };

        public static readonly SongSpec Menu = new SongSpec
        {
            Id = "menu", Title = "Clocking In",
            Bpm = 92f, Root = 60, Mode = Composer.Major,
            Chords = new[] { 0, 5, 3, 4, 0, 5, 1, 4 },
            Lead = Instrument.MusicBox, Counter = Instrument.Glock, Comp = Instrument.EPiano,
            Bass = BassStyle.Sustain, Comping = Comping.Arp, Pad = 0.8f,
            Kick = "x.........x.....", Snare = "....x.......x...", Hat = "..x...x...x...x.", Tick = "",
            Melody = new[]
            {
                "E5:4 G5:2 A5:2", "G5:4 E5:4", "F5:2 A5:2 C6:2 A5:2", "G5:6 R:2",
                "E5:4 G5:2 A5:2", "C6:4 B5:2 A5:2", "A5:2 F5:2 D5:2 F5:2", "G5:4 D5:2 R:2",
            },
        };

        public static readonly SongSpec[] Chapters =
        {
            new SongSpec  // 1 Open-Plan Office
            {
                Id = "open_plan", Title = "Busy Appearing Busy",
                Bpm = 104f, Root = 60, Mode = Composer.Major,
                Chords = new[] { 0, 5, 3, 4, 0, 5, 3, 4, 3, 4, 2, 5, 3, 4, 0, 0 },
                Lead = Instrument.Kalimba, Counter = Instrument.Glock, Comp = Instrument.Pluck,
                Bass = BassStyle.Bounce, Comping = Comping.Offbeat, Pad = 0.35f,
                Melody = OpenPlanMelody,
            },
            new SongSpec  // 2 Office Kitchen
            {
                Id = "kitchen", Title = "Coffee Run",
                Bpm = 116f, Root = 65, Mode = Composer.Mixolydian, Seed = 7,
                Chords = new[] { 0, 3, 0, 6, 0, 3, 4, 0 },
                Lead = Instrument.Marimba, Counter = Instrument.Glock, Comp = Instrument.EPiano,
                Bass = BassStyle.Synco, Comping = Comping.Stabs, Swing = 0.2f, Pad = 0.3f,
                Kick = "x.....x...x.....", Snare = "....x.......x...", Hat = "x.x.x.x.x.x.x.x.", Tick = "..x...x...x..x.x",
            },
            new SongSpec  // 3 Meeting Room
            {
                Id = "meeting", Title = "Agenda Item Seven",
                Bpm = 92f, Root = 62, Mode = Composer.Dorian, Seed = 21, Sevenths = true,
                Chords = new[] { 0, 3, 0, 3, 6, 3, 4, 0 },
                Lead = Instrument.Vibes, Counter = Instrument.MusicBox, Comp = Instrument.EPiano,
                Bass = BassStyle.Walk, Comping = Comping.Stabs, Swing = 0.33f, Pad = 0.45f,
                Kick = "x.......x.......", Snare = "....x.......x...", Hat = "x..x..x.x..x..x.", Tick = "",
            },
            new SongSpec  // 4 Manager's Office
            {
                Id = "manager", Title = "Urgent Feedback",
                Bpm = 100f, Root = 57, Mode = Composer.Minor, Seed = 13,
                Chords = new[] { 0, 5, 2, 6, 0, 5, 2, 4 },
                Lead = Instrument.EPiano, Counter = Instrument.Glock, Comp = Instrument.Pluck,
                Bass = BassStyle.Bounce, Comping = Comping.Offbeat, Pad = 0.5f,
                Kick = "x.......x.x.....", Snare = "....x.......x...", Hat = "x.x.x.x.x.x.x.xx", Tick = "......x.......x.",
            },
            new SongSpec  // 5 HR & Admin
            {
                Id = "hr", Title = "Mandatory Wellness",
                Bpm = 84f, Root = 55, Mode = Composer.Lydian, Seed = 5,
                Chords = new[] { 0, 1, 0, 1, 3, 4, 0, 0 },
                Lead = Instrument.Ocarina, Counter = Instrument.Glock, Comp = Instrument.Glock,
                Bass = BassStyle.Sustain, Comping = Comping.Arp, Pad = 0.9f,
                Kick = "x.........x.....", Snare = "........x.......", Hat = "..x...x...x...x.", Tick = "",
            },
            new SongSpec  // 6 Executive Floor
            {
                Id = "executive", Title = "Synergy Lounge",
                Bpm = 96f, Root = 63, Mode = Composer.Major, Seed = 31, Sevenths = true,
                Chords = new[] { 1, 4, 0, 5, 1, 4, 0, 0 },
                Lead = Instrument.Vibes, Counter = Instrument.Glock, Comp = Instrument.EPiano,
                Bass = BassStyle.Walk, Comping = Comping.Stabs, Swing = 0.33f, Pad = 0.55f,
                Kick = "x.......x.......", Snare = "....x.......x...", Hat = "x..xx..xx..xx..x", Tick = "",
            },
            new SongSpec  // 7 After Hours
            {
                Id = "after_hours", Title = "Lights Out",
                Bpm = 88f, Root = 52, Mode = Composer.Dorian, Seed = 44,
                Chords = new[] { 0, 5, 3, 4, 0, 5, 6, 4 },
                Lead = Instrument.Kalimba, Counter = Instrument.Vibes, Comp = Instrument.Pluck,
                Bass = BassStyle.Synco, Comping = Comping.Arp, Pad = 0.8f,
                Kick = "x.......x.......", Snare = "....x.......x...", Hat = "..x...x...x...x.", Tick = "x.......x.......",
            },
            new SongSpec  // 8 Server Room
            {
                Id = "server", Title = "Works On My Machine",
                Bpm = 120f, Root = 57, Mode = Composer.Dorian, Seed = 88,
                Chords = new[] { 0, 0, 6, 6, 5, 5, 4, 4 },
                Lead = Instrument.Marimba, Counter = Instrument.MusicBox, Comp = Instrument.Pluck,
                Bass = BassStyle.Synco, Comping = Comping.Arp, Pad = 0.3f,
                Kick = "x...x...x...x...", Snare = "....x.......x...", Hat = "xxxxxxxxxxxxxxxx", Tick = "x..x..x..x..x.x.",
            },
            new SongSpec  // 9 Rooftop Terrace
            {
                Id = "rooftop", Title = "Voluntary Team Building",
                Bpm = 110f, Root = 62, Mode = Composer.Major, Seed = 61,
                Chords = new[] { 0, 4, 5, 3, 0, 4, 3, 4 },
                Lead = Instrument.Kalimba, Counter = Instrument.Glock, Comp = Instrument.Pluck,
                Bass = BassStyle.Bounce, Comping = Comping.Offbeat, Swing = 0.15f, Pad = 0.4f,
                Kick = "x.......x.......", Snare = "....x.......x...", Hat = "x.x.x.x.x.x.x.x.", Tick = "...x.......x....",
            },
            new SongSpec  // 10 The Corner Office: the leitmotif returns, grander
            {
                Id = "corner_office", Title = "The View From Up Here",
                Bpm = 100f, Root = 60, Mode = Composer.Major, Sevenths = true,
                Chords = new[] { 0, 5, 3, 4, 0, 5, 3, 4, 3, 4, 2, 5, 3, 4, 0, 0 },
                Lead = Instrument.Vibes, Counter = Instrument.Glock, Comp = Instrument.EPiano,
                Bass = BassStyle.Walk, Comping = Comping.Arp, Pad = 0.7f,
                Hat = "x..xx..xx..xx..x",
                Melody = OpenPlanMelody,
            },
        };

        // The song of a chapter (Themes.ChapterOf); chapters past the list reuse it in order.
        public static SongSpec ForChapter(int chapter) => Chapters[((chapter % Chapters.Length) + Chapters.Length) % Chapters.Length];
    }
}
