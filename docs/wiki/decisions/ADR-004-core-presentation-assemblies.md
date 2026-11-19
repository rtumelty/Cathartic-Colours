# ADR-004 — Game code in Core and Presentation assemblies

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-11-19 |
| Roadmap id | [CC-002](https://github.com/rtumelty/Cathartic-Colours/issues/8) |
| Supersedes | — |

## Context
Apart from the pure `CatharticColours.Simulation` assembly (ADR-001) and the editor build assembly from CC-004, all game code in `Assets/Scripts/` compiles into Unity's default `Assembly-CSharp`. Test assemblies can't reference `Assembly-CSharp`, so ECS systems, save code and presentation can't be tested automatically.

The folders depend on each other in a loop:
- ECS and save code read the static `GameManager` (global namespace, `Assets/Scripts/Gameplay/GameManager.cs`) for the runtime `GameConfiguration` and `ColorProfile` (`SaveGameSystem`, `SaveGameManager`).
- `GameManager` also holds the presentation objects (`ActiveGameRenderer`, `ActiveGameUIManager`, `ActivePointsParticleSpawner`, `JellyManager`), so it references `Gameplay` types.
- `MergeEffectsSystem` (`Assets/Scripts/ECS/Systems/`) calls `PointsParticleSpawner` and `GameRenderer`.
- `Gameplay` reads ECS components and singletons.

## Options

### Option A — One game assembly
- **Description:** put everything in `Assets/Scripts/` except Simulation into a single `CatharticColours.Game` assembly.
- **Pros:** almost no code changes; testable straight away.
- **Cons:** nothing stops core logic from depending on MonoBehaviours, so ECS and save tests may need scene objects; the loop stays.

### Option B — Core and Presentation assemblies
- **Description:** `CatharticColours.Core` holds `Data/`, `ECS/`, `SaveSystem/` and the configuration half of `GameManager`. `CatharticColours.Presentation` holds `Gameplay/`, `MergeEffectsSystem`, and a new static for the presentation references. Presentation references Core; Core never references Presentation.
- **Pros:** the compiler enforces the layering; Core tests need no scenes or MonoBehaviours; Burst and ECS code stays free of managed presentation types, in line with project `CLAUDE.md` §3.
- **Cons:** callers of the presentation references change names, and one system moves folder.

## Decision
Option B (Ronan, 2026-11-19). It makes the core testable without scenes and keeps the dependency direction one way.

## Consequences
- Namespaces and class names stay the same. Files move with their `.meta` files, so scene and prefab script references keep their GUIDs.
- Unity's Entities package finds systems in every assembly, so `MergeEffectsSystem` keeps running from Presentation with the same `[UpdateInGroup]`/`[UpdateAfter]` ordering.
- New test assemblies: `CatharticColours.Core.Tests` (EditMode) and `CatharticColours.PlayMode.Tests`.
- New code goes in Core unless it needs MonoBehaviours, UI Toolkit, input or scene objects. Documented in [systems/assemblies](../systems/assemblies.md) (added by CC-002.4) and project `CLAUDE.md` §3.

## Status
Accepted, 2026-11-19.
