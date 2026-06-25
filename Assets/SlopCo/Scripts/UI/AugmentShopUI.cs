using UnityEngine;
using UnityEngine.UI;
using SlopCo.Core;
using SlopCo.Gameplay;

namespace SlopCo.UI
{
    /// <summary>
    /// Between-round augment shop. Appears during Payout (the "DAY N SURVIVED" beat): rolls a few random
    /// not-yet-owned augments as cards (name / description / cost) and lets the crew spend the persistent
    /// Cash via <see cref="AugmentSystem.RequestBuyRpc"/>. Cards disable when unaffordable or owned. All
    /// refs optional (null-checked).
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

        private void Roll()
        {
            if (titleText != null) titleText.text = Localization.Get("shop.title");
            var aug = ServiceLocator.Get<AugmentSystem>();

            var pool = new System.Collections.Generic.List<int>();
            for (int id = 0; id < AugmentSystem.CatalogCount; id++)
                if (aug == null || !aug.Owns(id)) pool.Add(id);

            int cards = cardButtons != null ? cardButtons.Length : 0;
            _offerCount = Mathf.Min(cards, Mathf.Min(GameConstants.AugmentShopChoices, pool.Count));
            for (int i = 0; i < _offerCount; i++)
            {
                int r = Random.Range(i, pool.Count);
                (pool[i], pool[r]) = (pool[r], pool[i]);
                _offered[i] = pool[i];
            }

            for (int i = 0; i < cards; i++)
            {
                bool active = i < _offerCount;
                if (cardButtons[i] != null) cardButtons[i].gameObject.SetActive(active);
                if (!active) continue;
                var d = AugmentSystem.Get(_offered[i]);
                Set(cardNameTexts, i, Localization.Get(d.nameKey));
                Set(cardDescTexts, i, Localization.Get(d.descKey));
                Set(cardCostTexts, i, "$" + d.cost);
            }
        }

        private void Update()
        {
            if (panel == null || !panel.activeSelf) return;
            var aug = ServiceLocator.Get<AugmentSystem>();
            var quota = ServiceLocator.Get<QuotaSystem>();
            int cash = quota != null ? quota.Cash.Value : 0;
            for (int i = 0; i < _offerCount; i++)
            {
                if (cardButtons[i] == null) continue;
                var d = AugmentSystem.Get(_offered[i]);
                bool owned = aug != null && aug.Owns(_offered[i]);
                cardButtons[i].interactable = !owned && cash >= d.cost;
            }
        }

        private void Buy(int slot)
        {
            if (slot < 0 || slot >= _offerCount) return;
            ServiceLocator.Get<AugmentSystem>()?.RequestBuyRpc(_offered[slot]);
        }

        private static void Set(Text[] arr, int i, string s)
        {
            if (arr != null && i < arr.Length && arr[i] != null) arr[i].text = s;
        }
    }
}
