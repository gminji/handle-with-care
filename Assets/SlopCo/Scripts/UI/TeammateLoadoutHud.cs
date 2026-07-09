using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using SlopCo.Core;
using SlopCo.Items;
using SlopCo.Player;

namespace SlopCo.UI
{
    /// <summary>
    /// Always-on compact HUD listing each HUMAN teammate's chosen augment (their selected permanent item) and
    /// their held / most-recently-used item. Reads replicated <see cref="PlayerInventory"/> state
    /// (ReadPermission.Everyone) → no extra netcode. Humans are told apart from AI bots by the REPLICATED
    /// <see cref="NetworkObject.IsPlayerObject"/> (bots are <c>Spawn()</c>'d, humans <c>SpawnAsPlayerObject</c>),
    /// so the filter is correct on host AND remote clients. Mirrors <see cref="ItemHud"/> / <see cref="DashGauge"/>
    /// (guards on <see cref="PlayerController.LocalHuman"/>). Rows are pooled and refreshed on a low-frequency timer.
    /// </summary>
    public sealed class TeammateLoadoutHud : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform rowsParent;
        [Tooltip("Inactive template row. Children named 'Dot' (Image), 'Aug' (Text), 'Item' (Text).")]
        [SerializeField] private GameObject rowTemplate;
        [SerializeField] private int maxRows = 3;
        [SerializeField] private float refreshInterval = 0.25f;

        private sealed class Row { public GameObject go; public Image dot; public Text aug; public Text item; }
        private readonly List<Row> _pool = new();
        private readonly List<PlayerController> _scratch = new();
        private float _timer;

        private void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = refreshInterval;
            Refresh();
        }

        private void Refresh()
        {
            var local = PlayerController.LocalHuman;
            if (local == null || rowTemplate == null) { Hide(); return; }

            ulong localClient = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.LocalClientId
                : local.OwnerClientId;

            // Gather HUMAN teammates (exclude self + AI bots). ≤4 players → FindObjectsByType is bounded/cheap.
            _scratch.Clear();
            foreach (var pc in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (pc == null) continue;
                var no = pc.GetComponent<NetworkObject>();
                if (no == null || !no.IsPlayerObject) continue;   // humans only (bots aren't player-objects)
                if (no.OwnerClientId == localClient) continue;     // exclude self
                _scratch.Add(pc);
            }
            _scratch.Sort((a, b) =>
            {
                var na = a.GetComponent<NetworkObject>();
                var nb = b.GetComponent<NetworkObject>();
                return na.OwnerClientId.CompareTo(nb.OwnerClientId); // stable row order
            });

            if (_scratch.Count == 0) { Hide(); return; }
            if (root != null && !root.activeSelf) root.SetActive(true);

            int shown = Mathf.Min(_scratch.Count, maxRows);
            for (int i = 0; i < shown; i++) FillRow(GetRow(i), _scratch[i]);
            for (int i = shown; i < _pool.Count; i++)
                if (_pool[i].go != null && _pool[i].go.activeSelf) _pool[i].go.SetActive(false);
        }

        private void FillRow(Row row, PlayerController pc)
        {
            if (!row.go.activeSelf) row.go.SetActive(true);
            var inv = pc.GetComponent<PlayerInventory>();
            if (row.dot != null) row.dot.color = pc.CurrentColor;

            int augId = inv != null ? LoadoutView.AugmentDisplayId(inv.SelectedPermanent.Value) : InventoryLogic.Empty;
            if (row.aug != null)
                row.aug.text = Localization.Get("hud.loadout.aug") + ": " +
                    (augId == InventoryLogic.Empty
                        ? Localization.Get("item.slot.none")
                        : Localization.Get(ItemCatalog.Get(augId).nameKey));

            int itemId = inv != null
                ? LoadoutView.ItemDisplayId(inv.ConsumableId.Value, inv.LastUsedItemId.Value)
                : InventoryLogic.Empty;
            if (row.item != null)
            {
                if (itemId == InventoryLogic.Empty)
                    row.item.text = Localization.Get("item.slot.empty");
                else
                {
                    string prefix = LoadoutView.ItemIsHeld(inv.ConsumableId.Value) ? "hud.loadout.held" : "hud.loadout.used";
                    row.item.text = Localization.Get(prefix) + ": " + Localization.Get(ItemCatalog.Get(itemId).nameKey);
                }
            }
        }

        private Row GetRow(int index)
        {
            while (_pool.Count <= index)
            {
                var go = Instantiate(rowTemplate, rowsParent);
                var t = go.transform;
                _pool.Add(new Row
                {
                    go = go,
                    dot = t.Find("Dot") ? t.Find("Dot").GetComponent<Image>() : null,
                    aug = t.Find("Aug") ? t.Find("Aug").GetComponent<Text>() : null,
                    item = t.Find("Item") ? t.Find("Item").GetComponent<Text>() : null,
                });
            }
            return _pool[index];
        }

        private void Hide()
        {
            if (root != null && root.activeSelf) root.SetActive(false);
        }
    }
}
