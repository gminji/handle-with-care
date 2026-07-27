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
        // Written only by UIManager (single panel-root owner); read here purely as a visibility probe
        // ("did UIManager turn me on?") — see Update()'s early-out below.
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Button[] cardButtons;
        [SerializeField] private Text[] cardNameTexts;
        [SerializeField] private Text[] cardDescTexts;
        [SerializeField] private Text[] cardCostTexts;

        private readonly int[] _offered = new int[8];
        private readonly bool[] _offeredIsItem = new bool[8];   // true = permanent item, false = crew augment
        private int _offerCount;
        private RoundPhase _shown = (RoundPhase)255;   // sentinel — never a real phase, forces first sync

        // Change-detection caches for the cost-label/title text so Update() (≈1600 calls per 9s Payout
        // window) doesn't reallocate strings every frame. -1 / int.MinValue never match a real state/cash.
        private readonly int[] _costState = new int[8];
        private int _lastCash = int.MinValue;

        // ── Crew vote (online only) ──
        private int _lastOffer = int.MinValue;   // OfferPacked we last built cards from
        private int _lastVotes = int.MinValue;   // VotesPacked we last painted badges from
        private int _myVote = -1;                // slot this client voted for (-1 = hasn't voted)
        private int _revealSlot = -1;            // winning slot once the vote resolves (-1 = still open)
        private Coroutine _reveal;

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
            Localization.OnLanguageChanged += InvalidateLabels;
            AugmentSystem.OnVoteResolved += HandleVoteResolved;
        }

        private void OnDisable()
        {
            RoundManager.OnPhaseChanged -= HandlePhase;
            Localization.OnLanguageChanged -= InvalidateLabels;
            AugmentSystem.OnVoteResolved -= HandleVoteResolved;
            StopReveal();
        }

        // RPC path: immediate roll on the phase-change cue.
        private void HandlePhase(RoundPhase phase) => SyncPhase(phase);

        // Single entry point for both the RPC and polling paths (see Update()). The _shown sentinel
        // guards against Roll() re-running twice for the same phase.
        private void SyncPhase(RoundPhase phase)
        {
            if (phase == _shown) return;
            _shown = phase;
            if (phase == RoundPhase.Payout) Roll();
        }

        private static PlayerInventory LocalInventory()
        {
            var p = PlayerController.LocalHuman;
            return p != null ? p.GetComponent<PlayerInventory>() : null;
        }

        private void Roll()
        {
            var aug = ServiceLocator.Get<AugmentSystem>();
            var inv = LocalInventory();

            // Online: the crew VOTES, so the three cards must be the server's offer — a locally rolled set
            // would mean "slot 1" named a different augment on every screen. Solo/AI keeps the local mixed
            // roll (augments + personal permanents) and buys on click, which is what the player asked for.
            if (AugmentSystem.VotingMode)
            {
                _myVote = -1;
                _revealSlot = -1;
                ApplyServerOffer(aug);
                return;
            }

            // Mixed pool of (isItem, id) — choices the buyer does not already own.
            var poolId = new System.Collections.Generic.List<int>();
            var poolItem = new System.Collections.Generic.List<bool>();
            for (int id = 0; id < AugmentSystem.CatalogCount; id++)
                if (aug == null || !aug.Owns(id)) { poolId.Add(id); poolItem.Add(false); }
            for (int id = 0; id < ItemCatalog.Count; id++)
                if (ItemCatalog.IsPermanent(id) && (inv == null || !InventoryLogic.OwnsPermanent(inv.PermanentMask.Value, id)))
                    { poolId.Add(id); poolItem.Add(true); }

            // _offered/_offeredIsItem/_costState are fixed-size, so clamp to their length too: wiring a
            // 9th card button in the scene should quietly offer 8, not throw IndexOutOfRange at runtime.
            int cards = cardButtons != null ? Mathf.Min(cardButtons.Length, _offered.Length) : 0;
            _offerCount = Mathf.Min(cards, Mathf.Min(GameConstants.AugmentShopChoices, poolId.Count));
            for (int i = 0; i < _offerCount; i++)
            {
                int r = Random.Range(i, poolId.Count);
                (poolId[i], poolId[r]) = (poolId[r], poolId[i]);
                (poolItem[i], poolItem[r]) = (poolItem[r], poolItem[i]);
                _offered[i] = poolId[i];
                _offeredIsItem[i] = poolItem[i];
            }

            InvalidateLabels();   // paints the new offer texts and re-arms the label caches
        }

        // Name/Desc/Cost text for the currently-offered cards — split out of Roll() so it can also be
        // used to re-paint (not re-roll) on a language switch via InvalidateLabels().
        private void ApplyOfferTexts()
        {
            if (titleText != null) titleText.text = Localization.Get("shop.title");  // Update() grows this to 2 lines next frame
            int cards = cardButtons != null ? cardButtons.Length : 0;
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

        // Forces every cached label (Name/Desc/Cost text + cost-state color/text + title cash line) to
        // recompute on the next Update() pass — used by a language switch, where the offered cards
        // themselves must NOT change (re-paint, not re-roll).
        private void InvalidateLabels()
        {
            ApplyOfferTexts();
            for (int i = 0; i < _costState.Length; i++) _costState[i] = -1;
            _lastCash = int.MinValue;
        }

        private void Update()
        {
            var rm = ServiceLocator.Get<RoundManager>();
            SyncPhase(rm != null ? rm.Phase.Value : RoundPhase.Lobby);

            if (panel == null || !panel.activeSelf) return;
            var aug = ServiceLocator.Get<AugmentSystem>();
            var quota = ServiceLocator.Get<QuotaSystem>();
            if (AugmentSystem.VotingMode) { UpdateVoting(aug, quota); return; }

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
                cardButtons[i].interactable = !owned && cash >= cost;   // ← unchanged

                int state = ShopCardView.State(owned, cash, cost);      // pure verdict (see below)
                if (_costState[i] != state && cardCostTexts != null && i < cardCostTexts.Length && cardCostTexts[i] != null)
                {
                    _costState[i] = state;
                    var t = cardCostTexts[i];
                    switch (state)
                    {
                        case 1: t.text = Localization.Get("shop.owned");
                                t.color = new Color(0.60f, 0.65f, 0.70f); break;          // grey — owned
                        case 2: t.text = "$" + cost + "  " + Localization.Get("shop.short");
                                t.color = new Color(1f, 0.45f, 0.40f); break;             // red — can't afford
                        default: t.text = "$" + cost;
                                 t.color = new Color(0.5f, 1f, 0.55f); break;             // scene-serialized green
                    }
                }
            }

            // Always-on "cash you're holding" line so unaffordable cards read as legitimate, not broken.
            if (titleText != null && cash != _lastCash)
            {
                _lastCash = cash;
                titleText.text = Localization.Get("shop.title")
                               + "\n<size=20>" + Localization.Get("shop.cash").Replace("{cash}", cash.ToString()) + "</size>";
            }
        }

        private void Buy(int slot)
        {
            if (slot < 0 || slot >= _offerCount) return;

            // Online: a click is a BALLOT, not a purchase — the server buys whatever the crew lands on.
            if (AugmentSystem.VotingMode)
            {
                if (_revealSlot >= 0) return;               // vote already decided
                _myVote = slot;
                _lastVotes = int.MinValue;                  // force the badges to repaint
                ServiceLocator.Get<AugmentSystem>()?.SubmitVoteRpc(slot);
                return;
            }

            if (_offeredIsItem[slot]) LocalInventory()?.RequestBuyPermanentRpc(_offered[slot]);
            else ServiceLocator.Get<AugmentSystem>()?.RequestBuyRpc(_offered[slot]);
        }

        // ── Crew vote mode ────────────────────────────────────────────────

        // Rebuild the cards from the server's offer. Called on the phase cue and again whenever OfferPacked
        // changes, because the replicated value can land a frame or two after the phase RPC.
        private void ApplyServerOffer(AugmentSystem aug)
        {
            int packed = aug != null ? aug.OfferPacked.Value : 0;
            _lastOffer = packed;
            int n = Mathf.Min(AugmentOffer.Count(packed), _offered.Length);
            if (cardButtons != null) n = Mathf.Min(n, cardButtons.Length);
            _offerCount = n;
            for (int i = 0; i < n; i++)
            {
                _offered[i] = AugmentOffer.Slot(packed, i);
                _offeredIsItem[i] = false;                  // the crew vote is augments only
            }
            InvalidateLabels();
            ResetCardScales();
        }

        private void UpdateVoting(AugmentSystem aug, QuotaSystem quota)
        {
            if (aug == null) return;
            if (aug.OfferPacked.Value != _lastOffer) ApplyServerOffer(aug);

            int votes = aug.VotesPacked.Value;
            int cash = quota != null ? quota.Cash.Value : 0;
            if (votes != _lastVotes || cash != _lastCash)
            {
                _lastVotes = votes;
                _lastCash = cash;
                PaintVoteLabels(votes, cash);
            }

            for (int i = 0; i < _offerCount; i++)
                if (cardButtons[i] != null) cardButtons[i].interactable = _revealSlot < 0;
        }

        private void PaintVoteLabels(int votes, int cash)
        {
            if (titleText != null)
                titleText.text = Localization.Get("vote.title")
                               + "\n<size=20>" + Localization.Get("shop.cash").Replace("{cash}", cash.ToString())
                               + "   ·   " + Localization.Get("vote.hint") + "</size>";

            for (int i = 0; i < _offerCount; i++)
            {
                if (cardCostTexts == null || i >= cardCostTexts.Length || cardCostTexts[i] == null) continue;
                var a = AugmentSystem.Get(_offered[i]);
                int n = Mathf.Max(0, AugmentOffer.Slot(votes, i));
                string line = "$" + a.cost + "   " + Localization.Get("vote.tally").Replace("{n}", n.ToString());
                if (i == _myVote) line += "   " + Localization.Get("vote.mine");
                cardCostTexts[i].text = line;
                cardCostTexts[i].color = i == _myVote ? new Color(1f, 0.9f, 0.35f)
                                       : cash >= a.cost ? new Color(0.5f, 1f, 0.55f)
                                       : new Color(1f, 0.45f, 0.40f);
            }
        }

        private void HandleVoteResolved(int slot, int augmentId, bool wasTie, bool bought)
        {
            if (!AugmentSystem.VotingMode) return;
            _revealSlot = slot;
            StopReveal();
            _reveal = StartCoroutine(RevealRoutine(slot, wasTie, bought));
        }

        // The decision animation: a highlight sweeps across the cards, slows down, and lands on the winner,
        // which then pops. A tie says so out loud — otherwise a random pick reads as the game misreading votes.
        private System.Collections.IEnumerator RevealRoutine(int slot, bool wasTie, bool bought)
        {
            if (_offerCount <= 0) yield break;

            if (slot < 0)   // nobody voted
            {
                if (titleText != null) titleText.text = Localization.Get("vote.title") + "\n<size=22>" + Localization.Get("vote.none") + "</size>";
                yield break;
            }

            float t = 0f, step = 0.06f;
            int cursor = 0;
            while (t < GameConstants.VoteSpinSeconds)
            {
                HighlightOnly(cursor % _offerCount);
                cursor++;
                yield return new WaitForSecondsRealtime(step);
                t += step;
                step *= 1.12f;                               // decelerate into the result
            }

            // Land on the winner and hold it there.
            HighlightOnly(slot);
            if (titleText != null)
            {
                string headline = wasTie ? Localization.Get("vote.tie") : Localization.Get("vote.won");
                if (!bought) headline += "  ·  " + Localization.Get("vote.broke");
                titleText.text = Localization.Get("vote.title") + "\n<size=24>" + headline + "</size>";
            }

            // A short scale pop on the winning card.
            var rt = cardButtons != null && slot < cardButtons.Length && cardButtons[slot] != null
                   ? cardButtons[slot].transform as RectTransform : null;
            for (float p = 0f; p < 0.45f && rt != null; p += Time.unscaledDeltaTime)
            {
                float k = 1f + 0.18f * Mathf.Sin(p / 0.45f * Mathf.PI);
                rt.localScale = Vector3.one * k;
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one * 1.08f;
        }

        private void HighlightOnly(int slot)
        {
            if (cardButtons == null) return;
            for (int i = 0; i < _offerCount && i < cardButtons.Length; i++)
            {
                if (cardButtons[i] == null) continue;
                var rt = cardButtons[i].transform as RectTransform;
                if (rt != null) rt.localScale = Vector3.one * (i == slot ? 1.08f : 0.94f);
            }
        }

        private void ResetCardScales()
        {
            if (cardButtons == null) return;
            foreach (var b in cardButtons)
                if (b != null) b.transform.localScale = Vector3.one;
        }

        private void StopReveal()
        {
            if (_reveal != null) { StopCoroutine(_reveal); _reveal = null; }
        }

        private static void Set(Text[] arr, int i, string s)
        {
            if (arr != null && i < arr.Length && arr[i] != null) arr[i].text = s;
        }
    }

    /// <summary>Shop card price-label display state (0=affordable, 1=owned, 2=insufficient funds) — pure verdict.</summary>
    public static class ShopCardView
    {
        public static int State(bool owned, int cash, int cost) => owned ? 1 : (cash >= cost ? 0 : 2);
    }
}
