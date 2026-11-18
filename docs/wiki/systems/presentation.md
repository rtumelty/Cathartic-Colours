# Presentation

## Purpose
Describes the MonoBehaviours that turn ECS state into what the player sees: `GameRenderer` (grid, block visuals, camera, and the creation of the ECS singletons), `Block` (per-block animation), `JellyManager` (shader wobble on a move) and `PointsParticleSpawner` (score particles on a merge). For each, which ECS state it reads and what it writes. UI Toolkit layout is in [ui-layout.md](ui-layout.md).

All paths are relative to the Unity project folder (`Cathartic Colours/`).

## Key files
| File | Role |
|---|---|
| `Assets/Scripts/Gameplay/GameRenderer.cs` | Grid, block visuals, camera, singleton creation |
| `Assets/Scripts/Gameplay/Block.cs` | Wrapper over the block `Animator` (global namespace) |
| `Assets/Scripts/Gameplay/JellyManager.cs` | Drives the jelly shader properties after a move |
| `Assets/Scripts/Gameplay/JellyController.cs` | Per-sprite wobble helper (not used by any prefab or scene) |
| `Assets/Scripts/Gameplay/PointsParticleSpawner.cs` | Emits score particles (global namespace) |
| `Assets/Prefabs/GameManager.prefab` | Root object: `GameRenderer`, `GameInputHandler`, `SwipeDetector`; children `JellyManager` and `GameUI` |
| `Assets/Prefabs/Block.prefab`, `Assets/Prefabs/Cell.prefab`, `Assets/Prefabs/ScoreParticles.prefab` | Instantiated or placed visuals |
| `Assets/Materials/BlockMaterial.mat`, `Assets/Materials/CellMaterial.mat` | Materials |
| `Assets/Shaders/JellyShader.shadergraph` | Block shader |
| `Assets/AnimationClips/Block.controller` and `Appear/Small_to_Medium/Medium_to_Large/Disappear.anim` | Block animation |

## How it works

### `GameRenderer`
`Assets/Scripts/Gameplay/GameRenderer.cs`. Serialised fields (prefab values, `Assets/Prefabs/GameManager.prefab`): `fallbackConfiguration` = `Default.asset`, `fallbackColorProfile` = `DefaultColorProfile.asset`, `cellPrefab` = `Cell.prefab`, `blockPrefab` = `Block.prefab`, `cellSize` = 1, `cellSpacing` = 0.1.

**Lifecycle.** `Awake` registers `GameManager.ActiveGameRenderer`, caches `Camera.main` and loads fallback configuration if none ([configuration.md](configuration.md)). `Start` runs `SetupUI` (camera background colour from the colour profile, then `GameUIManager.SetupUI`) and `InitializeGame`. `OnEnable` subscribes to the UI events ([ui-layout.md](ui-layout.md)). `Update` runs `UpdateVisuals` then `UpdateUI` every frame. `OnDestroy` clears the static reference and runs `CleanUp`.

**Singletons.** `InitializeGame` creates the grid, game-state, score and game-mode entities and the mode and spawn tags, then the visual grid, then either `SpawnInitialBlock` or `SaveGameManager.RestoreWorldState`. This is documented in [configuration.md](configuration.md#starting-a-game) and [ecs-frame-order.md](ecs-frame-order.md#singletons) and is not repeated here. `RestartGame` = `CleanUp` + `InitializeGame`.

**Visual grid.** `CreateVisualGrid` instantiates `gridWidth * gridHeight` `Cell` prefabs as children, named `Cell_{x}_{y}`, at world position `(x * (cellSize + cellSpacing), y * (cellSize + cellSpacing), 0)`. It tries to tint each via `GetComponent<SpriteRenderer>()` with `ColorProfile.GridBackgroundColor`; the `Cell` prefab has a `MeshFilter`, `MeshRenderer` and `BoxCollider` and no `SpriteRenderer`, so the tint is skipped (see Known issues).

**Block visuals (`UpdateVisuals`, every frame).**
1. Builds an `EntityQuery` for `BlockComponent` and copies the entity and component arrays.
2. For each block entity, looks it up in the `Dictionary<Entity, BlockVisual>` (`BlockVisual` = `GameObject`, `Block`, `Renderer`). If absent it instantiates `blockPrefab` under the renderer's transform, reads its `Block` and `Renderer` components, and logs an error and destroys the object if either is missing. There is no pooling: visuals are created and destroyed.
3. Sets the visual's world position to `(gridX * (cellSize + cellSpacing), gridY * (cellSize + cellSpacing), -1)` immediately. There is no slide animation: a block jumps to its new cell in the frame the ECS state changes.
4. `UpdateBlockVisual`: creates a `MaterialPropertyBlock`, sets `_MainColor` to `ColorProfile.GetColor(block.Color)` (or a hard-coded fallback colour table if there is no profile), applies it to the `Renderer`, and sets `Block.Size = (int)BlockSize` (`None` 0, `Small` 1, `Medium` 2, `Large` 3), which sets the animator's `Size` integer.
5. Any dictionary entry whose entity no longer has a block gets `Block.Disappear()` (an animator trigger) and is removed from the dictionary; the prefab's disappear animation ends with an animation event calling `Block.Dispose()` which destroys the `GameObject` (`Assets/AnimationClips/Disappear.anim`, function `Dispose`). The disappearing visual stays at its last drawn position: a block that merges into another vanishes in place rather than sliding into the merge cell (derived from code reading; not run).

**The indicator look.** `IsNextColorIndicator` is not read by `GameRenderer`. The indicator is an ordinary `Small`, `White` block, drawn with the profile's White colour ([ecs-frame-order.md](ecs-frame-order.md)); only its behaviour differs, not its appearance. A real White block in the Color Merge modes looks identical.

**Sizes.** World size comes from the prefab and from the `Small_to_Medium` and `Medium_to_Large` animation clips driven by the animator `Size` parameter; `GameRenderer` does not set a scale. The exact scale values in the clips were not examined.

**Camera.** `SetupUI` sets `mainCamera.backgroundColor` once. The scene camera is orthographic, clear flags solid colour, size 5 (`Assets/Scenes/Gameplay scene.unity`). `UpdateCamera(headerHeight, footerHeight)` re-fits size and position when the UI reports a layout change; the maths is described in [ui-layout.md](ui-layout.md#layoutobserver-and-the-camera).

**Cleanup.** `CleanUp` destroys all block entities, the cached game-state and score entities, and the `GridConfigComponent` and `GameModeComponent` entities, then destroys every visual object and clears the dictionary. It does not destroy the `Cell` objects, which are left in place and then re-created by the next `InitializeGame` (see Known issues).

### `Block`
`Assets/Scripts/Gameplay/Block.cs` (global namespace). Serialised `animator`; property `Size` (setter calls `animator.SetInteger("Size", value)`), `Disappear()` (`animator.SetTrigger("Disappear")`), `Dispose()` (`Destroy(gameObject)`, called from the animation event). The controller `Assets/AnimationClips/Block.controller` has parameters `Size` (int) and `Disappear` (trigger), and states `Appear` (default), `Small_to_Medium`, `Medium_to_Large` and `Disappear`; transitions use `Size` thresholds (greater than 1 and greater than 2) and the trigger. The exact transition wiring was not traced.

### `JellyManager` and `JellyController`
**What the wobble is.** `JellyManager` sets shader properties on one shared material, `BlockMaterial.mat`, for `wobblePeriod` seconds after a move; the blocks' `JellyShader` (Shader Graph, `Assets/Shaders/JellyShader.shadergraph`) uses them to wobble the mesh (properties in the graph: `_MainColor`, `_WobbleStrength`, `_DirectionInfluence`, `_DirectionWobbleFrequency`, `_MoveDirection`, `_RandomWobbleFrequency`, `_MinimumOpacity`, `_VolumeIntensity`, `_VolumePower`, `_Texture2D`).

**Trigger.** `InputProcessingSystem.OnUpdate` calls `GameManager.JellyManager.StartWobble(new Vector2(dir.x, dir.y))` for each accepted move, with no null check ([CC-006](https://github.com/rtumelty/Cathartic-Colours/issues/12)). `JellyManager.OnEnable` registers itself in `GameManager.JellyManager`; `OnDisable` clears it.

**`StartWobble(Vector2 target)`.** Stores the target direction and resets the timer; starts `WobbleCoroutine` if none is running (a move during a running wobble restarts the timer and re-targets, without starting a second coroutine).

**`WobbleCoroutine`.** While `timeSinceEvent < wobblePeriod`, each frame: `currentWobbleDirection = Lerp(current, target, directionSmoothing)` (this is a fixed fraction per frame, so it is frame-rate dependent); sets `_MoveDirection`; with `t = timeSinceEvent / wobblePeriod`, sets `_WobbleStrength` = `wobbleStrengthCurve(t) * wobbleScale`, `_DirectionInfluence` = `directionInfluenceCurve(t) * directionInfluenceScale`, `_DirectionWobbleFrequency` = `directionWobbleFrequencyCurve(t) * directionFrequencyScale`. When the period ends it clears its flags; it does not write final values, so the properties keep the last evaluated values.

| Parameter | Prefab value | Scene override (`Gameplay scene.unity`) |
|---|---|---|
| `jellyMaterial` | `BlockMaterial.mat` | n/a |
| `wobblePeriod` | 0.6 | n/a |
| `directionSmoothing` | 0.7 | n/a |
| `wobbleScale` | 0.1 | 0.05 |
| `directionInfluenceScale` | 0.5 | n/a |
| `directionFrequencyScale` | 0.3 | 0.5 |
| the three curves | set in the prefab (values not transcribed here) | n/a |

`JellyController` (`RequireComponent(SpriteRenderer)`) animates `_WobbleStrength` and `_DistortionStrength` on `spriteRenderer.material` and has a `Jiggle(intensity)` coroutine. It is not attached to any prefab or scene (no reference found in `Assets/Prefabs` or `Assets/Scenes`), and its sprite-material properties do not match the mesh-based block shader.

### `PointsParticleSpawner`
`Assets/Scripts/Gameplay/PointsParticleSpawner.cs` (global namespace). `Awake` registers `GameManager.ActivePointsParticleSpawner`; `OnDestroy` clears it. `ScoringSystem` calls `Emit(points, mergeColor, worldPosition)` once per `MergeEventComponent`, with the world position computed from the grid position and `GameRenderer.cellSize`/`cellSpacing`, pushed back in z by 5 (`Assets/Scripts/ECS/Systems/ScoringSystem.cs`).

`Emit` branches on `spawnMode`:
- `Text`: `SpawnPoints` splits the points into digits and emits one particle from `digitParticleSystems[digit]` per digit, spaced `particleWidth * spacingMultiplier` apart and centred if `centerAlignment`. Skips if `points <= 0`.
- `ParticleCount`: `SpawnBlobParticles` emits `max(1, points / pointsPerBlobParticle)` particles from `blobParticleSystem`, with start colour from `ColorProfile.GetColor(mergeColor)`.

Current wiring: `ScoreParticles.prefab` has `spawnMode: 1` (`ParticleCount`); the scene instance (named `BlobParticles`) overrides `blobParticleSystem` and `pointsPerBlobParticle` = 2 and `InitialModule.startLifetime` = 0.6 (`Assets/Scenes/Gameplay scene.unity`). All ten `digitParticleSystems` slots in the prefab are empty (`fileID: 0`), so `Text` mode would throw a null reference.

### ECS state read and written, per component
| Component | Reads | Writes |
|---|---|---|
| `GameRenderer` | `BlockComponent` (all, per frame); `GameStateComponent` and `ScoreComponent` (via cached entities, per frame, for the UI); `GameManager.ActiveConfiguration`, `ActiveColorProfile` | Creates `GridConfigComponent`, `GameStateComponent`, `ScoreComponent`, `GameModeComponent`, the mode tag and `SpawnColorSystemTag` (`InitializeGame`); creates the initial indicator `BlockComponent` (`SpawnInitialBlock`); destroys blocks and singletons (`CleanUp`); `PlayerPrefs` (`LoadFromSave`) |
| `Block` | none (receives `Size` from the renderer) | none (animator parameters only) |
| `JellyManager` | none (receives a direction from `InputProcessingSystem`) | shared material `BlockMaterial.mat` properties |
| `PointsParticleSpawner` | none (receives points, colour and position from `ScoringSystem`); `ColorProfile` colours | none |

### Prefabs, materials and shaders
| Item | Path | Notes |
|---|---|---|
| Block prefab | `Assets/Prefabs/Block.prefab` | `MeshFilter` (built-in primitive mesh, fileID 10207), `MeshRenderer` using `BlockMaterial.mat`, `Animator` using `Block.controller`, `Block` script |
| Cell prefab | `Assets/Prefabs/Cell.prefab` | `MeshFilter` (built-in primitive mesh, fileID 10202), `MeshRenderer` using `CellMaterial.mat`, `BoxCollider` |
| Score particles | `Assets/Prefabs/ScoreParticles.prefab` | `PointsParticleSpawner` plus a `ParticleSystem` (default particle material) |
| Game manager | `Assets/Prefabs/GameManager.prefab` | See Key files |
| Block material | `Assets/Materials/BlockMaterial.mat` | Shader `JellyShader` (Shader Graph); `_MainColor` overridden per block by `MaterialPropertyBlock` |
| Cell material | `Assets/Materials/CellMaterial.mat` | A built-in or package shader (guid `650dd952...`, not resolved here) |
| Block shader | `Assets/Shaders/JellyShader.shadergraph` | Jelly vertex wobble |
| Animation | `Assets/AnimationClips/` | `Block.controller`, `Appear`, `Small_to_Medium`, `Medium_to_Large`, `Disappear` |

## Interactions with other systems
- Reads ECS state and creates singletons: [ecs-frame-order.md](ecs-frame-order.md), [configuration.md](configuration.md).
- Called from simulation systems: `InputProcessingSystem` (jelly), `ScoringSystem` (particles, `GameRenderer.cellSize`); see [input.md](input.md) and [merge-rules.md](merge-rules.md).
- UI text, panels and camera fitting: [ui-layout.md](ui-layout.md).
- Resume restores block entities that this system then draws: [save-system.md](save-system.md).

## Known issues
- Simulation systems call presentation directly through `GameManager`: `InputProcessingSystem` -> `JellyManager`; `ScoringSystem` -> `PointsParticleSpawner`/`GameRenderer`. [CC-006](https://github.com/rtumelty/Cathartic-Colours/issues/12)
- The indicator's colour is chosen on the following move. [CC-007](https://github.com/rtumelty/Cathartic-Colours/issues/19)
- Visuals are an aesthetic prototype (instant moves, scale-only animation); rework planned. [CC-018](https://github.com/rtumelty/Cathartic-Colours/issues/30)
- `GameRenderer` allocates every frame (a new `EntityQuery`, arrays, a `HashSet`, and a `MaterialPropertyBlock` per block); profiling is planned. [CC-017](https://github.com/rtumelty/Cathartic-Colours/issues/29)
- Guards: `CleanUp` (also called from `OnDestroy`) uses `entityManager` even if `InitializeGame` never ran; `GameManager.JellyManager` is dereferenced without a check. [CC-025](https://github.com/rtumelty/Cathartic-Colours/issues/37)
- Dead code in the ECS layer is tracked in [CC-024](https://github.com/rtumelty/Cathartic-Colours/issues/36); presentation-layer cleanup (including the unused `JellyController`) is [CC-032](https://github.com/rtumelty/Cathartic-Colours/issues/44).
- `GameRenderer.CreateVisualGrid` looks for a `SpriteRenderer` on the `Cell` prefab, which has only a `MeshRenderer`, so `ColorProfile.gridBackgroundColor` is never applied (`Assets/Scripts/Gameplay/GameRenderer.cs`, `Assets/Prefabs/Cell.prefab`). [CC-029](https://github.com/rtumelty/Cathartic-Colours/issues/41)
- `RestartGame` calls `CleanUp`, which does not destroy the existing `Cell` objects, and `InitializeGame` then creates a full new grid on top, so each restart adds a duplicate set of cells at the same positions (`Assets/Scripts/Gameplay/GameRenderer.cs`, `CleanUp` and `CreateVisualGrid`). Derived from code reading; not run. [CC-029](https://github.com/rtumelty/Cathartic-Colours/issues/41)
- `JellyManager` writes wobble properties on the shared `BlockMaterial.mat` asset (not an instance), so in the editor play sessions change the asset's saved values (the committed material has non-default `_WobbleStrength` etc.) (`Assets/Scripts/Gameplay/JellyManager.cs`, `Assets/Materials/BlockMaterial.mat`). [CC-032](https://github.com/rtumelty/Cathartic-Colours/issues/44)
- `JellyController` is not used by any prefab or scene (`Assets/Scripts/Gameplay/JellyController.cs`). [CC-032](https://github.com/rtumelty/Cathartic-Colours/issues/44)
- `ScoreParticles.prefab` serialises fields that no longer exist on `PointsParticleSpawner` (`particleSystem`, `minAngle`, `maxAngle`) and has no `digitParticleSystems` assigned; only the scene override supplies `blobParticleSystem`, so the prefab on its own does not work (`Assets/Prefabs/ScoreParticles.prefab`, `Assets/Scripts/Gameplay/PointsParticleSpawner.cs`). [CC-032](https://github.com/rtumelty/Cathartic-Colours/issues/44)
- the wobble smoothing `Lerp(current, target, 0.7)` is applied per frame rather than per second, so it is frame-rate dependent (`Assets/Scripts/Gameplay/JellyManager.cs`, `WobbleCoroutine`). [CC-032](https://github.com/rtumelty/Cathartic-Colours/issues/44)
- The Advanced Color Merge White Large clear has result colour `None`, which `DefaultColorProfile` maps to `(0,0,0,0)`, so its particles are fully transparent (`Assets/Scripts/ECS/Utilities/AdvancedColorMergeUtility.cs`, `Assets/Data/Color Profiles/DefaultColorProfile.asset`). Found by code reading; not run. [CC-032](https://github.com/rtumelty/Cathartic-Colours/issues/44)

## Related test cases
- [HUD](../test-plan.md#gameplay-hud-hud): TP-HUD-02 (camera fit), TP-HUD-06 (wobble, particles, instant moves).
- [Color Merge](../test-plan.md#color-merge-mode-cm): TP-CM-04 (white clear particles). [Advanced Color Merge](../test-plan.md#advanced-color-merge-mode-acm): TP-ACM-07 (transparent clear particles, CC-032). [Game over](../test-plan.md#game-over-over): TP-OVER-04 (restart), TP-OVER-05 (duplicate grid cells, CC-029).
