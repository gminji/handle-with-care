namespace SlopCo.Items
{
    /// <summary>Which inventory slot an item lives in.</summary>
    public enum ItemKind { Consumable, Permanent }

    /// <summary>What an item does when used. Self-* affect the user; Ally-* affect a teammate (networked in part 3).</summary>
    public enum ItemEffect { SelfSpeedBurst, SelfStaminaRefill, AllySpeedBuff, AllyStaminaRefill }

    /// <summary>
    /// One item definition (data only, no UnityEngine dependency → unit-testable). Mirrors the AugmentSystem
    /// static-Catalog pattern. Consumables drop in the world (gacha capsules); Permanents are granted via the
    /// augment shop. <c>modelKey</c> is resolved to a kit mesh in part 5.
    /// </summary>
    public struct ItemDef
    {
        public int id;
        public string nameKey, descKey;
        public ItemKind kind;
        public ItemEffect effect;
        public float magnitude;   // effect strength (speed-mult delta, or stamina fraction 0..1)
        public float duration;    // seconds for timed effects (0 = instant)
        public int cost;          // Cash price for PERMANENT items in the augment shop (consumables = 0, dropped free)
        public string modelKey;   // kit-mesh key, resolved at part 5
    }

    /// <summary>Static item catalog (the single source of truth). Consumables = ids 0..3, Permanents = 4..5.</summary>
    public static class ItemCatalog
    {
        public static readonly ItemDef[] All =
        {
            // ── Consumables (dropped, one-time; stronger than permanents per design; cost=0, free pickups) ──
            new ItemDef{ id=0, nameKey="item.shoes.name",  descKey="item.shoes.desc",  kind=ItemKind.Consumable, effect=ItemEffect.SelfSpeedBurst,    magnitude=0.8f,  duration=3f, cost=0,   modelKey="shoes"  },
            new ItemDef{ id=1, nameKey="item.flag.name",   descKey="item.flag.desc",   kind=ItemKind.Consumable, effect=ItemEffect.AllyStaminaRefill, magnitude=0.5f,  duration=0f, cost=0,   modelKey="flag"   },
            new ItemDef{ id=2, nameKey="item.horn.name",   descKey="item.horn.desc",   kind=ItemKind.Consumable, effect=ItemEffect.AllySpeedBuff,     magnitude=0.4f,  duration=4f, cost=0,   modelKey="horn"   },
            new ItemDef{ id=3, nameKey="item.energy.name", descKey="item.energy.desc", kind=ItemKind.Consumable, effect=ItemEffect.SelfStaminaRefill, magnitude=0.6f,  duration=0f, cost=0,   modelKey="energy" },
            // ── Permanents (augment-choice, reusable but weaker than consumables; priced in Cash) ──
            new ItemDef{ id=4, nameKey="item.insole.name", descKey="item.insole.desc", kind=ItemKind.Permanent,  effect=ItemEffect.SelfSpeedBurst,    magnitude=0.4f,  duration=2f, cost=130, modelKey="insole" },
            new ItemDef{ id=5, nameKey="item.flask.name",  descKey="item.flask.desc",  kind=ItemKind.Permanent,  effect=ItemEffect.SelfStaminaRefill, magnitude=0.35f, duration=0f, cost=120, modelKey="flask"  },
        };

        public static int Count => All.Length;

        public static ItemDef Get(int id)
        {
            if (id < 0) id = 0;
            if (id >= All.Length) id = All.Length - 1;
            return All[id];
        }

        public static bool IsValid(int id) => id >= 0 && id < All.Length;
        public static bool IsConsumable(int id) => IsValid(id) && All[id].kind == ItemKind.Consumable;
        public static bool IsPermanent(int id)  => IsValid(id) && All[id].kind == ItemKind.Permanent;
    }
}
