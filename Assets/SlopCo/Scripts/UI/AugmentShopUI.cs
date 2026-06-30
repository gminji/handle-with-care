using UnityEngine;
using UnityEngine.UI;
using SlopCo.Core;
using SlopCo.Gameplay;
using SlopCo.Items;
using SlopCo.Player;

namespace SlopCo.UI
{
    /// <summary>
    /// Between-round shop (Payout). Rolls a few not-yet-owned choices as cards: crew AUGMENTS (shared, bought via
    /// <see cref="AugmentSystem.RequestBuyRpc"/>) AND per-player PERMANENT ITEMS (granted to the local player via
    /// <see cref="PlayerInventory.RequestBuyPermanentRpc"/>). Spends the persistent Cash; cards disable when
    /// unaffordable or already owned. All refs optional (null-checked).
    /// </summary>
    public sealed class AugmentShopUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Button[] cardButtons;
        [SerializeField] private Text[] cardNameTexts;
        [SerializeField] private Text[] cardDescTexts;
        [SerializeField] private Text[] cardCostTexts;

        private readonly int[] _offered = new int[8];
        private readonly bool[] _offeredIsItem = new bool[8];   // true = permanent item, false = crew augment
        private int _offerCount;

        private void Awake()
        {
            if (cardButtons != null)
                for (int i = 0; i < cardButtons.Length; i++)
                {
                    int slot = i; // capture per-iteration
                    if (cardButtons[i] != null) cardButtons[i].onClick.AddListener(() => Buy(slot));
                }
        }

        private void OnEnable()
        {
            RoundManager.OnPhaseChanged += HandlePhase;
            if (panel != null) panel.SetActive(false);
        }

        private void OnDisable() => RoundManager.OnPhaseChanged -= HandlePhase;

        private void HandlePhase(RoundPhase phase)
        {
            if (phase == RoundPhase.Payout) { Roll(); if (panel != null) panel.SetActive(true); }
            else if (panel != null) panel.SetActive(false);
        }

        private static PlayerInventory LocalInventory()
        {
            var p = PlayerController.LocalHuman;
            return p != null ? p.GetComponent<PlayerInventory>() : null;
        }

        private void Roll()
        {
            if (titleText != null) titleText.text = Localization.Get("shop.title");
            var aug = ServiceLocator.Get<AugmentSystem>();
            var inv = LocalInventory();

            // Mixed pool of (isItem, id) — choices the buyer does not already own.
            var poolId = new System.Collections.Generic.List<int>();
            var poolItem = new System.Collections.Generic.List<bool>();
            for (int id = 0; id < AugmentSystem.CatalogCount; id++)
                if (aug == null || !aug.Owns(id)) { poolId.Add(id); poolItem.Add(false); }
            for (int id = 0; id < ItemCatalog.Count; id++)
                if (ItemCatalog.IsPermanent(id) && (inv == null || !InventoryLogic.OwnsPermanent(inv.PermanentMask.Value, id)))
                    { poolId.Add(id); poolItem.Add(true); }

            int cards = cardButtons != null ? cardButtons.Length : 0;
            _offerCount = Mathf.Min(cards, Mathf.Min(GameConstants.AugmentShopChoices, poolId.Count));
            for (int i = 0; i < _offerCount; i++)
            {
                int r = Random.Range(i, poolId.Count);
                (poolId[i], poolId[r]) = (poolId[r], poolId[i]);
                (poolItem[i], poolItem[r]) = (poolItem[r], poolItem[i]);
                _offered[i] = poolId[i];
                _offeredIsItem[i] = poolItem[i];
            }

            for (int i = 0; i < cards; i++)
            {
                bool active = i < _offerCount;
                if (cardButtons[i] != null) cardButtons[i].gameObject.SetActive(active);
                if (!active) continue;
                string nameKey, descKey; int cost;
                if (_offeredIsItem[i]) { var d = ItemCatalog.Get(_offered[i]); nameKey = d.nameKey; descKey = d.descKey; cost = d.cost; }
                else { var a = AugmentSystem.Get(_offered[i]); nameKey = a.nameKey; descKey = a.descKey; cost = a.cost; }
                Set(cardNameTexts, i, Localization.Get(nameKey));
                Set(cardDescTexts, i, Localization.Get(descKey));
                Set(cardCostTexts, i, "$" + cost);
            }
        }

        private void Update()
        {
            if (panel == null || !panel.activeSelf) return;
            var aug = ServiceLocator.Get<AugmentSystem>();
            var quota = ServiceLocator.Get<QuotaSystem>();
            var inv = LocalInventory();
            int cash = quota != null ? quota.Cash.Value : 0;
            for (int i = 0; i < _offerCount; i++)
            {
                if (cardButtons[i] == null) continue;
                bool owned; int cost;
                if (_offeredIsItem[i])
                {
                    var d = ItemCatalog.Get(_offered[i]); cost = d.cost;
                    owned = inv != null && InventoryLogic.OwnsPermanent(inv.PermanentMask.Value, _offered[i]);
                }
                else
                {
                    var a = AugmentSystem.Get(_offered[i]); cost = a.cost;
                    owned = aug != null && aug.Owns(_offered[i]);
                }
                cardButtons[i].interactable = !owned && cash >= cost;
            }
        }

        private void Buy(int slot)
        {
            if (slot < 0 || slot >= _offerCount) return;
            if (_offeredIsItem[slot]) LocalInventory()?.RequestBuyPermanentRpc(_offered[slot]);
            else ServiceLocator.Get<AugmentSystem>()?.RequestBuyRpc(_offered[slot]);
        }

        private static void Set(Text[] arr, int i, string s)
        {
            if (arr != null && i < arr.Length && arr[i] != null) arr[i].text = s;
        }
    }
}
