using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlopCo.GameAssets
{
    /// <summary>
    /// In-project mirror of ASSET_MANIFEST.md — the list of Kenney CC0 packs the game draws from.
    /// Pure data; carried so tooling/credits screens can read the download list. Keep in sync with
    /// the markdown manifest at the repo root.
    /// </summary>
    [CreateAssetMenu(fileName = "AssetManifest", menuName = "SlopCo/Asset Manifest", order = 0)]
    public sealed class AssetManifest : ScriptableObject
    {
        [Serializable]
        public struct PackEntry
        {
            public string packName;
            public string url;
            [TextArea] public string usage;
            public string license; // always "CC0 1.0" for Kenney
        }

        [Tooltip("Kenney CC0 packs used by the game. License is CC0 1.0 for all Kenney assets.")]
        public List<PackEntry> packs = new();

        private void Reset()
        {
            packs = new List<PackEntry>
            {
                new() { packName = "Mini Characters", url = "https://kenney.nl/assets/mini-characters", usage = "Player avatars (rigged, 32 anims)", license = "CC0 1.0" },
                new() { packName = "Furniture Kit", url = "https://kenney.nl/assets/furniture-kit", usage = "TwoPerson fragile cargo (pianos, sofas)", license = "CC0 1.0" },
                new() { packName = "Food Kit", url = "https://kenney.nl/assets/food-kit", usage = "OneHand cargo + throwables", license = "CC0 1.0" },
                new() { packName = "City Kit", url = "https://kenney.nl/assets/city-kit-suburban", usage = "Course + depot environment", license = "CC0 1.0" },
                new() { packName = "Platformer Kit", url = "https://kenney.nl/assets/platformer-kit", usage = "Ramps/planks/ledges (hazards)", license = "CC0 1.0" },
                new() { packName = "Car Kit", url = "https://kenney.nl/assets/car-kit", usage = "Delivery van (DeliveryZone)", license = "CC0 1.0" },
                new() { packName = "Prototype Textures", url = "https://kenney.nl/assets/prototype-textures", usage = "Greybox blockout", license = "CC0 1.0" },
            };
        }
    }
}
