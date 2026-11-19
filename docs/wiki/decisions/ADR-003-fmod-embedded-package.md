# ADR-003 — FMOD for Unity as an embedded UPM package

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-11-18 |
| Roadmap id | [CC-033](https://github.com/rtumelty/Cathartic-Colours/issues/45) |
| Supersedes | — |

## Context
FMOD for Unity 2.03.11 was imported as a `.unitypackage` into `Assets/Plugins/FMOD/`, and all 449 files (297 MB, every platform) were committed. The 2.04 integration (`fmodstudio20400.tgz`) is distributed as a **UPM tarball** (`com.fmod.fmod-for-unity.v204`, version 2.4.0). Its code hard-codes `RuntimeUtils.PluginBasePath = "Packages/com.fmod.fmod-for-unity.v204"`, so it has to be installed as a package, not copied under `Assets/`. As a package it keeps its settings and event cache in `Assets/Resources/FMOD/` (`EditorSettings.CacheFolderFull`).

The repo has no other vendored packages yet. Every clone (feature sessions, other machines) needs a working FMOD without a separate download from fmod.com, which needs an account.

## Options

### Option A — Embedded package (committed)
- **Description:** extract the tarball to `Cathartic Colours/Packages/com.fmod.fmod-for-unity.v204/` and commit it. Unity treats the folder as an embedded package with no manifest entry, and the lock records `"source": "embedded"`.
- **Pros:** clones work anywhere; same footprint and workflow as the 2.03 commit; files can be patched if a newer Unity breaks the integration (as 6000.6 broke 2.03's `GetInstanceID`); updates are reviewable file diffs.
- **Cons:** 421 files, 257 MB in the working tree (about 90 MB packed); a local patch would be lost on the next update unless it's recorded.

### Option B — Commit the tarball, `file:` reference
- **Description:** keep `fmodstudio20400.tgz` in `Packages/` and reference it from the manifest.
- **Pros:** one immutable file; the manifest shows the version.
- **Cons:** an 89 MB single file is over GitHub's 50 MB warning threshold; every update adds another full blob; it can't be patched.

### Option C — Embedded, platforms trimmed
- **Description:** as A, but delete the unused `platforms/` folders and keep only Android and Windows.
- **Pros:** about 200 MB smaller.
- **Cons:** modifies the vendor package, and every update has to redo the trim. FMOD's settings asset already references every platform class, so missing classes would leave broken sub-assets.

## Decision
Option A (Ronan, 2026-11-18).

Rules:
- **The package folder is vendor code.** Don't edit it unless a Unity upgrade forces it. Any patch is recorded on the feature page and in `systems/audio-fmod.md`, so the next update can re-apply or drop it.
- **The settings asset lives in `Assets/Resources/FMOD/FMODStudioSettings.asset`.** It moved from `Assets/Plugins/FMOD/Resources/` with its `.meta`, so its GUID and the GUIDs of its platform sub-assets carry over.
- **The event cache `Assets/Resources/FMOD/FMODStudioCache.asset` is gitignored.** It's regenerated locally, as `Assets/Plugins/FMOD/Cache/` was before.
- **`.gitattributes` and `.gitignore` FMOD rules point at the package folder.** FMOD's setup wizard suggests stale `Assets/Plugins/FMOD` and `com.fmod.fmod-for-unity` paths, so don't apply its suggestions verbatim.
- **Update procedure:** delete the package folder, extract the new tarball into a folder named after its `package.json` `name` (a new minor gets a new name, for example `.v205`), carry the rules above over, and run a batchmode compile.

## Consequences
- `Assets/Plugins/` is gone. FMOD code is in `Packages/com.fmod.fmod-for-unity.v204/`, and `FMODUnity`/`FMODUnityEditor` asmdefs are unchanged in name, so game code (`using FMODUnity;`) is unaffected.
- The package name changes with each FMOD minor version (`.v204`), so an upgrade also changes the folder, the lock entry and the git rules.
- The FMOD Studio project stays on 2.03 until it's migrated ([CC-003](https://github.com/rtumelty/Cathartic-Colours/issues/9)). A 2.04 runtime loading 2.03 banks is checked in CC-033 MANUAL step 2. (Done 2026-11-19 in CC-003: the Studio project is saved in 2.04 and the banks are rebuilt with 2.04.)
