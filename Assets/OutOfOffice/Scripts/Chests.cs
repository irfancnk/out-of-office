using System;
using System.Collections.Generic;
using System.Globalization;

namespace OutOfOffice
{
    public enum ChestTier { DeskDrawer, SupplyCloset, ExecutiveSafe }

    // Chests (OUT-14). Three tiers with their own odds and a rarity floor; a pity timer that guarantees an Epic or better
    // within 10 opens and a Legendary within 30; odds shown wherever chests are offered; and duplicates, once every
    // item a chest could give is owned, turn into paper clips that buy a chosen item in the Clip Shop. Within a rarity
    // every item the player doesn't own is equally likely.
    public static class Chests
    {
        // Weights by rarity: Common, Rare, Epic, Legendary.
        static readonly int[][] Weights =
        {
            new[] { 55, 30, 12, 3 },  // Desk Drawer: tasks, the daily chest, the pass
            new[] { 0, 65, 27, 8 },   // Supply Closet: the coffee card, the starter pack, the shop
            new[] { 0, 0, 75, 25 },   // Executive Safe: a mastered chapter, the shop
        };

        public const int EpicPity = 10, LegendaryPity = 30;
        public const int ClosetGems = 60, SafeGems = 150;
        public static readonly int[] DuplicateClips = { 5, 15, 40, 100 };
        public static readonly int[] ClipPrice = { 30, 80, 200, 500 };

        public static string Name(ChestTier t) => t == ChestTier.DeskDrawer ? "DESK DRAWER" : t == ChestTier.SupplyCloset ? "SUPPLY CLOSET" : "EXECUTIVE SAFE";

        public static string Promise(ChestTier t) =>
            t == ChestTier.DeskDrawer ? "Anything could be in here." : t == ChestTier.SupplyCloset ? "Rare or better, every time." : "Epic or better, every time.";

        // Paid chests are hidden where paid loot boxes are restricted (Belgium, the Netherlands).
        public static bool PaidAllowed => PaidAllowedIn(Region);

        public static bool PaidAllowedIn(string region) => region != "BE" && region != "NL";

        static string Region
        {
            get
            {
                try { return RegionInfo.CurrentRegion.TwoLetterISORegionName.ToUpperInvariant(); }
                catch (Exception) { return ""; }
            }
        }

        // The chance of each rarity on the next open of this tier: the tier's weights, raised to the pity floor when a
        // dry run is due, over the rarities that can still give something new (or, with everything owned, duplicates).
        public static float[] Odds(ChestTier tier, Func<CosmeticItem, bool> owned, int sinceEpic, int sinceLegendary)
        {
            var w = WeightsNow(tier, sinceEpic, sinceLegendary);
            bool anyNew = false;
            for (int r = 0; r < 4; r++) if (w[r] > 0 && Left((Rarity)r, owned) > 0) anyNew = true;
            float total = 0f;
            var odds = new float[4];
            for (int r = 0; r < 4; r++)
            {
                bool has = anyNew ? Left((Rarity)r, owned) > 0 : InChests((Rarity)r) > 0;
                odds[r] = has ? w[r] : 0f;
                total += odds[r];
            }
            for (int r = 0; r < 4; r++) odds[r] = total > 0f ? odds[r] / total : 0f;
            return odds;
        }

        static int[] WeightsNow(ChestTier tier, int sinceEpic, int sinceLegendary)
        {
            var w = (int[])Weights[(int)tier].Clone();
            int floor = sinceLegendary >= LegendaryPity - 1 ? 3 : sinceEpic >= EpicPity - 1 ? 2 : 0;
            for (int r = 0; r < floor; r++) w[r] = 0;
            return w;
        }

        public static int Left(Rarity r, Func<CosmeticItem, bool> owned)
        {
            int n = 0;
            foreach (var i in Cosmetics.All) if (i.FromChest && i.Rarity == r && !owned(i)) n++;
            return n;
        }

        public static int InChests(Rarity r)
        {
            int n = 0;
            foreach (var i in Cosmetics.All) if (i.FromChest && i.Rarity == r) n++;
            return n;
        }

        // One roll. A duplicate (everything owned) avoids the last duplicate when it can.
        public static CosmeticItem Roll(ChestTier tier, Func<CosmeticItem, bool> owned, Random rng, int sinceEpic, int sinceLegendary,
            string lastDuplicate, out bool duplicate)
        {
            var odds = Odds(tier, owned, sinceEpic, sinceLegendary);
            double pick = rng.NextDouble(), run = 0.0;
            var rarity = Rarity.Common;
            for (int r = 0; r < 4; r++)
            {
                if (odds[r] <= 0f) continue;
                rarity = (Rarity)r;
                run += odds[r];
                if (pick < run) break;
            }
            var fresh = new List<CosmeticItem>();
            foreach (var i in Cosmetics.All) if (i.FromChest && i.Rarity == rarity && !owned(i)) fresh.Add(i);
            duplicate = fresh.Count == 0;
            if (!duplicate) return fresh[rng.Next(fresh.Count)];
            var all = new List<CosmeticItem>();
            foreach (var i in Cosmetics.All) if (i.FromChest && i.Rarity == rarity) all.Add(i);
            if (all.Count == 0) foreach (var i in Cosmetics.All) if (i.FromChest) all.Add(i);
            if (all.Count > 1) all.RemoveAll(i => i.Id == lastDuplicate);
            return all[rng.Next(all.Count)];
        }
    }
}
