using UnityEngine;
using UnityEngine.UI;
using SlopCo.Core;
using SlopCo.Items;
using SlopCo.Player;

namespace SlopCo.UI
{
    /// <summary>
    /// HUD for the local player's two item slots — the held CONSUMABLE (Q use / G discard) and the selected
    /// PERMANENT (F use / R cycle). Reads <see cref="PlayerController.LocalHuman"/>'s <see cref="PlayerInventory"/>
    /// (replicated state). Mirrors <see cref="DashGauge"/>; hidden until a local human + inventory exist.
    /// </summary>
    public sealed class ItemHud : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text consumableText;
        [SerializeField] private Text permanentText;

        private void Update()
        {
            var owner = PlayerController.LocalHuman;
            var inv = owner != null ? owner.GetComponent<PlayerInventory>() : null;
            if (inv == null) { if (root != null && root.activeSelf) root.SetActive(false); return; }
            if (root != null && !root.activeSelf) root.SetActive(true);

            if (consumableText != null)
            {
                int c = inv.ConsumableId.Value;
                consumableText.text = c == InventoryLogic.Empty
                    ? Localization.Get("item.slot.empty")
                    : "[Q] " + Localization.Get(ItemCatalog.Get(c).nameKey);
            }
            if (permanentText != null)
            {
                int p = inv.SelectedPermanent.Value;
                permanentText.text = (p == InventoryLogic.Empty || !ItemCatalog.IsValid(p))
                    ? Localization.Get("item.slot.none")
                    : "[F] " + Localization.Get(ItemCatalog.Get(p).nameKey);
            }
        }
    }
}
