# Assemblies

## Purpose
Describes how the game's code is split into assemblies, which folder feeds each one, what may reference what, and where new code goes. The split (CC-002, [ADR-004](../decisions/ADR-004-core-presentation-assemblies.md)) lets test assemblies reference the ECS, save and configuration code, which used to compile into Unity's default `Assembly-CSharp`.

All paths are relative to the Unity project folder (`Cathartic Colours/`).

## Key files
| File | Role |
|---|---|
| `Assets/Scripts/Simulation/CatharticColours.Simulation.asmdef` | Pure board rules ([ADR-001](../decisions/ADR-001-pure-board-simulation.md)) |
| `Assets/Scripts/Core/CatharticColours.Core.asmdef` | Core assembly definition; the folder also holds `GameManager.cs` and `AssemblyInfo.cs` |
| `Assets/Scripts/{Data,ECS,SaveSystem}/CatharticColours.Core.asmref` | Assembly definition references that add those folders to Core |
| `Assets/Scripts/Gameplay/CatharticColours.Presentation.asmdef` | Presentation assembly (MonoBehaviours, UI, input) |
| `Assets/Editor/Build/CatharticColours.Build.Editor.asmdef` | Editor-only build tooling ([build-android](build-android.md)) |
| `Assets/Tests/EditMode/**/*.asmdef`, `Assets/Tests/PlayMode/*.asmdef` | The four test assemblies |

## How it works

### The assemblies
| Assembly | Source folders | References | Notes |
|---|---|---|---|
| `CatharticColours.Simulation` | `Scripts/Simulation/` | `Unity.Mathematics`, `Unity.Collections` (no engine objects) | Block enums, board state, merge rules, `BoardSimulation` step |
| `CatharticColours.Core` | `Scripts/Core/` plus `Scripts/Data/`, `Scripts/ECS/`, `Scripts/SaveSystem/` through `.asmref` files | Simulation, `Unity.Entities`, `Unity.Mathematics`, `Unity.Collections`, `Unity.Burst`, `FMODUnity` | `GameManager` (runtime configuration), `GameConfiguration`, `ColorProfile`, ECS components and systems, `SaveGameManager`. Nothing that needs a scene |
| `CatharticColours.Presentation` | `Scripts/Gameplay/` | Core, Simulation, `Unity.Entities`, `Unity.Mathematics`, `Unity.Collections`, `Unity.InputSystem` | `GameRenderer`, `GameUIManager`, `MainMenuController`, `GameInputHandler`, `SwipeDetector`, jelly and particle classes, `PresentationRegistry`, `MergeEffectsSystem` |
| `CatharticColours.Build.Editor` | `Editor/Build/` | none | Editor only; the Android build entry point |
| `CatharticColours.Simulation.Tests` | `Tests/EditMode/` | Simulation, Unity.Mathematics/Collections/Burst, test runner | EditMode, editor only |
| `CatharticColours.Core.Tests` | `Tests/EditMode/Core/` | Core, Simulation, Unity.Entities/Mathematics/Collections/Burst, test runner | EditMode, editor only: board-step system and save/restore tests |
| `CatharticColours.Build.Editor.Tests` | `Tests/EditMode/Build/` | Build.Editor, test runner | EditMode, editor only |
| `CatharticColours.PlayMode.Tests` | `Tests/PlayMode/` | Core, Presentation, Simulation, Unity.Entities/Mathematics/Collections, test runner | PlayMode smoke tests that run the real scenes |

Runtime assemblies have `autoReferenced` true; test assemblies have it false and a `UNITY_INCLUDE_TESTS` define constraint.

### Reference rules
- **Core never references Presentation.** The compiler enforces the layering: Core types can't name `GameRenderer`, `PointsParticleSpawner` or any other Presentation type. Presentation references Core.
- Simulation references nothing of the game; Core and Presentation reference Simulation.
- Core reaches presentation objects only through ECS data (components and event entities). Presentation reads that data from `Update` loops, or runs systems of its own.
- `PresentationRegistry` (`Assets/Scripts/Gameplay/PresentationRegistry.cs`) holds the live renderer, UI manager, particle spawner and jelly manager. `GameManager` (Core) holds only the runtime configuration and colour profile.

### What goes where
**New code goes in Core unless it needs MonoBehaviours, UI Toolkit, input or scene objects**; those go in Presentation. Rules that need no Unity types at all go in Simulation ([merge-rules](merge-rules.md)).

A new folder under `Assets/Scripts/` that should be Core needs a `CatharticColours.Core.asmref` (in the editor: **Create > Assembly Definition Reference**, pointing at `CatharticColours.Core`). A folder with neither an asmdef nor an asmref falls into `Assembly-CSharp`, which no test assembly can reference.

### System order across the assembly boundary
`SaveGameSystem` (Core) must run after `MergeEffectsSystem` (Presentation) in `PresentationSystemGroup`. Core can't name a Presentation type, so the constraint is declared from the Presentation side: `[UpdateBefore(typeof(SaveGameSystem))]` on `MergeEffectsSystem` (`Assets/Scripts/Gameplay/MergeEffectsSystem.cs`). The resolved order is unchanged: `MergeEffectsSystem`, `SaveGameSystem`, `EndFrameSystem` ([ecs-frame-order](ecs-frame-order.md)). Entities finds systems in every assembly, so a Presentation system runs like any other.

### Test hooks: InternalsVisibleTo and the save path override
`Assets/Scripts/Core/AssemblyInfo.cs` makes Core's internals visible to `CatharticColours.Core.Tests` and `CatharticColours.PlayMode.Tests`. Tests use that to set `SaveGameManager.SaveFilePathOverride` (`Assets/Scripts/SaveSystem/SaveGameManager.cs`), which redirects the single save path (`SaveGameManager.SaveFilePath`, also used by `SaveGameSystem`) to a temp folder. The default path (`Application.persistentDataPath/savegame.json`) is unchanged, and tests set the override back to `null` in their teardown. A new test assembly that needs it adds its own `InternalsVisibleTo` line.

### Running the tests
Commands are in the project `CLAUDE.md` §4 (editor closed, no `-quit`):
- **EditMode** runs `CatharticColours.Simulation.Tests`, `CatharticColours.Core.Tests` and `CatharticColours.Build.Editor.Tests`.
- **PlayMode** runs `CatharticColours.PlayMode.Tests` (`Assets/Tests/PlayMode/`): a gameplay scene test (one move changes the move count and block positions, and is saved to the temp folder) and a main menu test (the Start button loads a new game with the chosen mode and size). Both set the save override, so the real save is never touched and no game resumes. A logged error fails a test.

## Interactions with other systems
- [ecs-frame-order](ecs-frame-order.md): which systems run in which group, and the `MergeEffectsSystem` to `SaveGameSystem` constraint above.
- [save-system](save-system.md): the save path owner and the override.
- [configuration](configuration.md): `GameManager` and the start-up path the PlayMode tests exercise.
- [presentation](presentation.md): the Presentation assembly's classes.

## Known issues
None for the split itself. The PlayMode smoke tests load the configuration assets through `AssetDatabase`, so they run in the editor only (not in a player build).

## Related test cases
[TP-TOOL-02](../test-plan.md) (command-line EditMode and PlayMode runs).
