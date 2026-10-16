using System;
using UnityEngine;

namespace OutOfOffice
{
    public enum ProductKind { Consumable, NonConsumable, Subscription }

    // Something the shop sells for real money (OUT-32, OUT-33). Ids match the Play Console / App Store products.
    public class Product
    {
        public string Id, Title, Blurb, FallbackPrice;
        public ProductKind Kind;
        public int Gems;          // gems granted on purchase
        public bool Chest;        // a chest opens on purchase
        public int PassDays;      // Office Pass days granted (subscriptions)
    }

    public static class Catalog
    {
        public static readonly Product Pass = new Product
        {
            Id = "ooo.pass.monthly", Title = "OFFICE PASS", Blurb = "Unlimited energy. 25 gems and a chest a day. No ad videos.",
            FallbackPrice = "$4.99/mo", Kind = ProductKind.Subscription, PassDays = 30,
        };
        public static readonly Product Starter = new Product
        {
            Id = "ooo.starter", Title = "STARTER PACK", Blurb = "150 gems and a chest. Once only.", FallbackPrice = "$1.99",
            Kind = ProductKind.NonConsumable, Gems = 150, Chest = true,
        };
        public static readonly Product NoAds = new Product
        {
            Id = "ooo.noads", Title = "NO ADS", Blurb = "Every ad reward, without the video.", FallbackPrice = "$2.99",
            Kind = ProductKind.NonConsumable,
        };
        public static readonly Product[] GemPacks =
        {
            new Product { Id = "ooo.gems.80", Title = "80 GEMS", Blurb = "A small bonus.", FallbackPrice = "$0.99", Kind = ProductKind.Consumable, Gems = 80 },
            new Product { Id = "ooo.gems.500", Title = "500 GEMS", Blurb = "A healthy raise.", FallbackPrice = "$4.99", Kind = ProductKind.Consumable, Gems = 500 },
            new Product { Id = "ooo.gems.1200", Title = "1,200 GEMS", Blurb = "Executive compensation.", FallbackPrice = "$9.99", Kind = ProductKind.Consumable, Gems = 1200 },
        };

        public static readonly Product[] All = { Pass, Starter, NoAds, GemPacks[0], GemPacks[1], GemPacks[2] };

        public const string PassTie = "c.gold";  // the exclusive look that comes with the pass

        // What gems buy in the game (OUT-48). Each price sits below the thing it saves: an undo (10) and a continue
        // (15) keep a play going, so they cost less than a new play (20); a full bar of six plays comes at a discount.
        public const int ContinueCost = 15;   // +shots when out of shots: the same play goes on
        public const int PlayGems = 20;       // +5 energy: one play
        public const int RefillGems = 90;     // a full bar: six plays
        public const int ChestCost = 60;      // one chest
        public const int AdGems = 10;         // a rewarded ad in the shop
        public const int AdEnergy = Energy.PlayCost;  // a rewarded ad on the energy card: one play

        public static Product Find(string id) => Array.Find(All, p => p.Id == id);
    }

    // A store that can take payments. The real one (Unity IAP on Google Play and the App Store) needs the owner's
    // Play Console and App Store accounts with these product ids; until then development builds use TestStore.
    public interface IStoreProvider
    {
        bool Available { get; }
        bool IsTest { get; }
        string PriceOf(Product product);
        void Buy(Product product, Action<bool> done);
    }

    // Development builds only: every purchase succeeds at once and costs nothing. The shop labels it TEST.
    public class TestStore : IStoreProvider
    {
        public bool Available => true;
        public bool IsTest => true;
        public string PriceOf(Product product) => product.FallbackPrice;
        public void Buy(Product product, Action<bool> done) => done?.Invoke(true);
    }

    // Release builds before a real store exists: nothing for sale.
    public class NoStore : IStoreProvider
    {
        public bool Available => false;
        public bool IsTest => false;
        public string PriceOf(Product product) => product.FallbackPrice;
        public void Buy(Product product, Action<bool> done) => done?.Invoke(false);
    }

    public static class Store
    {
        public static IStoreProvider Provider = Debug.isDebugBuild ? new TestStore() : (IStoreProvider)new NoStore();

        // Buys and grants, or reports failure. Grants go through Progress.Grant only.
        public static void Buy(Progress progress, Product product, Action<bool> done)
        {
            if (!Provider.Available || !progress.CanBuy(product)) { done?.Invoke(false); return; }
            Analytics.Event("purchase_start", ("product", product.Id), ("test", Provider.IsTest));
            Provider.Buy(product, ok =>
            {
                if (ok)
                {
                    progress.Grant(product);
                    Analytics.Event("purchase", ("product", product.Id), ("price", Provider.PriceOf(product)), ("test", Provider.IsTest));
                }
                else Analytics.Event("purchase_fail", ("product", product.Id));
                done?.Invoke(ok);
            });
        }
    }
}
