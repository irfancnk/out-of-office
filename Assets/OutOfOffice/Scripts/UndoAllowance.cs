namespace OutOfOffice
{
    // Undos on one attempt (OUT-41): three free, then each one costs gems or a rewarded ad. Restart is always free and
    // starts a new attempt with three again (OUT-13). The first rooms, before any ads or continues are offered, keep
    // undos unlimited, so nobody meets a price while learning the game.
    public class UndoAllowance
    {
        public const int Free = 3, GemCost = 10;

        public enum Next { Free, Pay, Shop }

        public int FreeLeft { get; private set; } = Free;
        public bool Limited { get; private set; }

        // bonus: extra free undos from the worn employee's perk (OUT-45).
        public void Reset(bool limited, int bonus = 0)
        {
            Limited = limited;
            FreeLeft = Free + System.Math.Max(0, bonus);
        }

        // What the next undo takes: nothing, a payment (gems or an ad), or a trip to the shop when neither is possible.
        public Next Check(int gems, bool adReady, int cost = GemCost) =>
            !Limited || FreeLeft > 0 ? Next.Free : gems >= cost || adReady ? Next.Pay : Next.Shop;

        public void UseFree()
        {
            if (Limited && FreeLeft > 0) FreeLeft--;
        }

        public static bool PayWithGems(Progress progress, int cost = GemCost) => progress.SpendGems(cost, "undo");
    }
}
