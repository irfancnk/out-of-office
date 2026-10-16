using System.Collections.Generic;

namespace OutOfOffice
{
    // A/B tests (OUT-34). Each install falls into a stable bucket per experiment, so a player always sees the same
    // variant. Remote config (OUT-6) can later pin a variant through Overrides. The first time a session uses an
    // experiment, an experiment_exposure event records which variant the player saw.
    public static class Experiments
    {
        public static string InstallId = "";
        public static readonly Dictionary<string, int> Overrides = new Dictionary<string, int>();
        static readonly HashSet<string> exposed = new HashSet<string>();

        // How many shots a continue gives (ad or gems): 3 or 5.
        public static int ContinueShots => Variant("continue_shots", 3, 5);

        // Which shop row comes first: the Office Pass or the gem packs.
        public static bool ShopPassFirst => Variant("shop_pass_first", true, false);

        public static T Variant<T>(string name, params T[] variants)
        {
            int index = Overrides.TryGetValue(name, out int pinned) ? pinned : Bucket(InstallId, name, variants.Length);
            index = ((index % variants.Length) + variants.Length) % variants.Length;
            if (exposed.Add(name)) Analytics.Event("experiment_exposure", ("experiment", name), ("variant", index));
            return variants[index];
        }

        // FNV-1a over the install id and the experiment name: stable across sessions, platforms and app versions.
        public static int Bucket(string installId, string experiment, int count)
        {
            if (count <= 1) return 0;
            uint hash = 2166136261;
            foreach (char c in installId + ":" + experiment) { hash ^= c; hash *= 16777619; }
            return (int)(hash % (uint)count);
        }

        public static void ResetExposure() => exposed.Clear();
    }
}
