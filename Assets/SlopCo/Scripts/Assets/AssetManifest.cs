using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlopCo.GameAssets
{
    /// <summary>
    /// In-project mirror of ASSET_MANIFEST.md — every CC0 source the game's art and audio come from.
    /// Pure data; carried so tooling/credits screens can read the list. One entry per distinct source;
    /// the markdown groups them by asset folder, so its table has fewer rows than this list has entries.
    /// Keep the two in sync — the art itself is committed, so this is provenance, not a download list.
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
            public string license; // "CC0 1.0" for every source below
        }

        [Tooltip("CC0 sources used by the game. Every entry is CC0 1.0 — commercial use and redistribution allowed, attribution not required.")]
        public List<PackEntry> packs = new();

        private void Reset()
        {
            packs = new List<PackEntry>
            {
                new() { packName = "Mini Characters", url = "https://kenney.nl/assets/mini-characters", usage = "Player avatars (rigged) — Player.prefab mesh + Animator", license = "CC0 1.0" },
                new() { packName = "Furniture Kit", url = "https://kenney.nl/assets/furniture-kit", usage = "TwoPerson cargo (fridge, sofa, bed) + OneHand TV/lamp", license = "CC0 1.0" },
                new() { packName = "Food Kit", url = "https://kenney.nl/assets/food-kit", usage = "OneHand cargo (cake, watermelon) + throwables", license = "CC0 1.0" },
                new() { packName = "Car Kit", url = "https://kenney.nl/assets/car-kit", usage = "Delivery van (DeliveryZone)", license = "CC0 1.0" },
                new() { packName = "Prototype Textures", url = "https://kenney.nl/assets/prototype-textures", usage = "Greybox course blockout materials", license = "CC0 1.0" },
                new() { packName = "Kenney Impact Sounds", url = "https://kenney.nl/assets/impact-sounds", usage = "Smash SFX (light/heavy variety)", license = "CC0 1.0" },
                new() { packName = "Kenney Interface Sounds", url = "https://kenney.nl/assets/interface-sounds", usage = "UI click + cargo grab cue", license = "CC0 1.0" },
                new() { packName = "PolyHaven", url = "https://polyhaven.com", usage = "PBR surface textures (ground, path, depot, rails) + skybox HDRI", license = "CC0 1.0" },
                new() { packName = "AmbientCG", url = "https://ambientcg.com", usage = "Cardboard PBR material for cargo crates", license = "CC0 1.0" },
            };
        }
    }
}
