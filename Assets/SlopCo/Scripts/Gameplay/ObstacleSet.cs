using System;
using UnityEngine;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// A map's own junk: the props its obstacle slots are filled with instead of the four default primitives.
    /// Pure data; <see cref="MapManager.BuildObstacleField"/> picks an entry per slot from the layout seed
    /// (<see cref="MapLayout.EntryFor"/>), sizes it to the entry's footprint, and makes sure it has a solid collider.
    /// </summary>
    [CreateAssetMenu(fileName = "ObstacleSet", menuName = "SlopCo/Obstacle Set", order = 2)]
    public sealed class ObstacleSet : ScriptableObject
    {
        /// <summary>Widest a prop may be at base scale, so no single one can plug the 1.5 m bridge.</summary>
        public const float MaxFootprint = 1.1f;

        [Serializable]
        public sealed class Entry
        {
            public GameObject prefab;
            [Tooltip("Longest horizontal side at base scale, in metres (max 1.1). The per-run size roll scales this.")]
            public float footprint = 1f;
        }

        [SerializeField] private Entry[] entries;

        public int Count => entries != null ? entries.Length : 0;
        public Entry Get(int i) => entries[i];

        /// <summary>False for an empty list — the map then gets the default primitives.</summary>
        public bool HasEntries => Count > 0;

        /// <summary>Replaces the list (used by the tests, which build sets in code).</summary>
        public void SetEntries(Entry[] value) => entries = value;

        private void OnValidate()
        {
            if (entries == null) return;
            foreach (var e in entries)
                if (e != null) e.footprint = Mathf.Clamp(e.footprint, 0.05f, MaxFootprint);
        }
    }
}
