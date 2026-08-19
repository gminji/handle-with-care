# Asset Manifest — CC0 art & audio

Every source below is **CC0 1.0 Universal** (public domain): free for commercial use, no attribution
required, modification & redistribution allowed. Because CC0 permits redistribution, the art is
**committed to this repo** — clone it and the project runs, with no separate asset download step.
Sources are credited here as good practice, not because the licence demands it.

> Unity import note: Kenney 3D packs ship FBX (Unity-native), GLB, and OBJ. This project uses the
> **FBX** versions. The character pack is imported with **Rig → Animation Type = Humanoid** so the
> bundled animations drive the Animator; one Animator Controller covers every skin because they share
> a skeleton.

## In the repo

Everything below lives under `Assets/SlopCo/Art/` and is referenced by the scene and prefabs by GUID.

| Gameplay role | Source pack | Folder | Files | Used for |
|---|---|---|---|---|
| **Players** | [Kenney Mini Characters](https://kenney.nl/assets/mini-characters) | `mini-characters/` | 27 | Rigged humanoid avatars → the `Player.prefab` mesh + Animator |
| **Cargo (two-person)** | [Kenney Furniture Kit](https://kenney.nl/assets/furniture-kit) | `furniture-kit/` | 140 | Piano, sofa, bed, fridge, TV, lamp → the oversized `TwoPerson` cargo |
| **Cargo (one-hand)** | [Kenney Food Kit](https://kenney.nl/assets/food-kit) | `food-kit/` | 201 | Crate, cake and comedy throwables → `OneHand` cargo |
| **Delivery van** | [Kenney Car Kit](https://kenney.nl/assets/car-kit) | `car-kit/` | 51 | The truck at the `DeliveryZone` |
| **Greybox** | [Kenney Prototype Textures](https://kenney.nl/assets/prototype-textures) | `prototype-textures/` | 8 | Course blockout materials (path / rail readability) |
| **Audio** | [Kenney Impact](https://kenney.nl/assets/impact-sounds) + [Interface Sounds](https://kenney.nl/assets/interface-sounds) | `audio/kenney/` | 20 | UI click, grab cue, smash variety — see `audio/kenney/CREDITS.md` |
| **Surfaces & sky** | [PolyHaven](https://polyhaven.com) + [AmbientCG](https://ambientcg.com) | `cc0/` | 12 | PBR ground/brick/wood/plaster textures, cargo cardboard, skybox HDRI — see `cc0/CREDITS.md` |

## Prefab mapping

Cargo prefabs live in `Assets/SlopCo/Prefabs/`. `MassClass` is `OneHand = 0` / `TwoPerson = 1`
(see `Scripts/Cargo/CargoItem.cs`); `TwoPerson` cargo needs two players on the handles.

| Prefab | Mesh | Pack | MassClass | `baseValue` |
|---|---|---|---|---|
| `Player` | character mesh + Animator | Mini Characters | — | — |
| `Cargo_Fridge` | `kitchenFridgeLarge` | Furniture Kit | TwoPerson | 320 |
| `Cargo_Piano` | `loungeSofaLong` | Furniture Kit | TwoPerson | 250 |
| `Cargo_Bed` | `bedDouble` | Furniture Kit | TwoPerson | 200 |
| `Cargo_TV` | `televisionVintage` | Furniture Kit | OneHand | 180 |
| `Cargo_Lamp` | `lampRoundFloor` | Furniture Kit | OneHand | 60 |
| `Cargo_Cake` | `cake-birthday` | Food Kit | OneHand | 90 |
| `Cargo_Crate` | `watermelon` | Food Kit | OneHand | 80 |
| `Cargo_Bomb` | primitive + `BombMat` (emissive fuse) | — | TwoPerson | 500 |
| `ItemCapsule`, `Rat`, `Thief`, `Ufo` | primitives + materials | — | — | — |

Van (the `DeliveryZone` host) and the course geometry are authored in `Scenes/Bootstrap.unity`:
the van from the Car Kit plus a box trigger collider, the course from primitives textured with the
Prototype Textures set.

> `Cargo_Piano` and `Cargo_Crate` are named for their intended props but currently use the sofa and
> watermelon meshes. Harmless placeholders — rename or reskin when the final art pass happens.

## Considered, not imported

These were scoped during planning but are **not** in the repo. Listed so the shortlist is not lost:
Kenney [Blocky Characters](https://kenney.nl/assets/blocky-characters) (alternate skins),
[Survival Kit](https://kenney.nl/assets/survival-kit) (prop variety),
[City Kit](https://kenney.nl/assets/city-kit-suburban) (modular streets),
[Platformer Kit](https://kenney.nl/assets/platformer-kit) (modular ramps/ledges),
[Mini Arena](https://kenney.nl/assets/mini-arena) (alternate stage), and
[Quaternius](https://quaternius.com) low-poly prop packs (decor scatter).

---
The `AssetManifest` ScriptableObject (`Assets/SlopCo/Settings/AssetManifest.asset`) mirrors this list
as in-project data; keep both in sync.
