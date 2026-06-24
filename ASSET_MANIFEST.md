# Asset Manifest — Kenney CC0 packs

All packs below are **CC0 1.0 Universal** (public domain): free for commercial use, no attribution
required, modification & redistribution allowed. Download from kenney.nl, import the **FBX** versions
into `Assets/SlopCo/Art/<pack>/` (gitignored — not committed to the repo).

> Unity import tip: Kenney 3D packs ship FBX (Unity-native), GLB, and OBJ. Use **FBX**. For character
> packs, set the model's **Rig → Animation Type = Humanoid** (or Generic if retargeting fails) so the
> 32 bundled animations drive the Animator.

| Gameplay role | Kenney pack | URL | Used for |
|---------------|-------------|-----|----------|
| **Players** | Mini Characters | https://kenney.nl/assets/mini-characters | Rigged humanoid avatars (32 anims each); the `Player.prefab` mesh + Animator. Shared skeleton → one Animator Controller for all skins. |
| Players (alt/skins) | Blocky Characters | https://kenney.nl/assets/blocky-characters | Optional modular alternate avatars / customization. |
| **Cargo (fragile)** | Furniture Kit | https://kenney.nl/assets/furniture-kit | Sofas, pianos, beds, tables → the oversized `TwoPerson` cargo. |
| **Cargo (small)** | Food Kit | https://kenney.nl/assets/food-kit | 200 props → `OneHand` cargo + comedy throwables. |
| Cargo (tools/misc) | Survival Kit | https://kenney.nl/assets/survival-kit | Extra prop variety. |
| **Level (urban)** | City Kit (Suburban/Commercial/Roads) | https://kenney.nl/assets/city-kit-suburban | Modular streets/buildings for the haul course + depot. |
| **Level (obstacle)** | Platformer Kit | https://kenney.nl/assets/platformer-kit | 150 modular blocks → ramps, planks, ledges (the fumble hazards). |
| **Delivery van** | Car Kit | https://kenney.nl/assets/car-kit | The delivery truck at the `DeliveryZone`. |
| **Greybox** | Prototype Textures | https://kenney.nl/assets/prototype-textures | Block out & playtest the course before final art. |
| Arena (optional) | Mini Arena | https://kenney.nl/assets/mini-arena | Alternate competitive stage geometry. |
| Audio (optional) | Kenney Audio packs (Impact/UI) | https://kenney.nl/assets/category:Audio | Crunch/cha-ching SFX for damage & delivery FX. |

## Prefab mapping (build these in the Editor)
- `Player.prefab` ← **Mini Characters** mesh. Components per README.
- `Cargo_Piano.prefab`, `Cargo_Sofa.prefab` ← **Furniture Kit**, `MassClass = TwoPerson`, high `baseValue`.
- `Cargo_Crate.prefab`, `Cargo_Watermelon.prefab` ← **Food/Survival Kit**, `MassClass = OneHand`, lower `baseValue`.
- `Van` (DeliveryZone host) ← **Car Kit** van + a box trigger collider.
- Course geometry ← **Platformer Kit** + **Prototype Textures** (greybox first).

> The `AssetManifest` ScriptableObject mirrors this list as in-project data; keep both in sync.
