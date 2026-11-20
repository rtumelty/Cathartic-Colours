using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Data;
using ECS.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace SaveSystem
{
    public static class SaveGameManager
    {
        /// <summary>The save format this build writes and the formats it can read. A save with any other version is
        /// set aside as unusable (CC-031). Add an entry, with a migration if the fields change, when the format changes.</summary>
        internal const string CurrentVersion = "1.0";
        static readonly string[] ReadableVersions = { CurrentVersion };

        const string CloneSuffix = "(Clone)";

        /// <summary>Test-only: when set, the save file lives at this path instead of persistentDataPath. Null = default.</summary>
        internal static string SaveFilePathOverride { get; set; }

        /// <summary>Test-only: when true, <see cref="WriteSave"/> takes the path used where File.Replace is not supported.</summary>
        internal static bool SimulateNoFileReplace { get; set; }

        /// <summary>The save file path. The single owner of the path: SaveGameSystem uses it too.</summary>
        internal static string SaveFilePath => SaveFilePathOverride ?? Path.Combine(Application.persistentDataPath, "savegame.json");

        /// <summary>Where the save is written before it is moved over the real file, so a crash mid-write never leaves
        /// a truncated save.</summary>
        internal static string TempFilePath => SaveFilePath + ".tmp";

        /// <summary>How many earlier saves are kept beside the newest one (CC-042): savegame.1.json is the move before
        /// the newest, savegame.2.json the move before that.</summary>
        internal const int BackupCount = 2;

        /// <summary>The save files, newest first: slot 0 is <see cref="SaveFilePath"/>, then the backups
        /// (savegame.1.json, savegame.2.json), all in the save's folder.</summary>
        internal static string SaveSlotPath(int slot)
        {
            string path = SaveFilePath;
            if (slot == 0) return path;
            string name = $"{Path.GetFileNameWithoutExtension(path)}.{slot}{Path.GetExtension(path)}";
            return Path.Combine(Path.GetDirectoryName(path) ?? "", name);
        }

        /// <summary>Where an unusable save (unreadable, or from an unknown version) is moved, so it stops offering Resume
        /// but is kept for inspection. Replaced by the next unusable save.</summary>
        internal static string BadFilePath => Path.Combine(Path.GetDirectoryName(SaveFilePath) ?? "", "savegame.bad.json");

        /// <summary>True when there is a save that can be resumed: the newest save, or else the newest backup that passes
        /// the checks (CC-042). Unusable files are moved aside here, so the main menu shows Resume only for a save that
        /// will load.</summary>
        public static bool HasSaveGame()
        {
            return TryReadSave(out _);
        }

        /// <summary>Loads the newest usable save, falling back to the backups (CC-042). Null when none is usable.</summary>
        public static SaveGameData LoadSaveGame()
        {
            if (!AnySaveSlotExists())
            {
                Debug.LogWarning("No save game file found");
                return null;
            }

            return TryReadSave(out var saveData) ? saveData : null;
        }

        /// <summary>
        /// Writes the save atomically and keeps the two saves before it (CC-042). The JSON goes to a temp file. Then
        /// savegame.2.json is dropped, savegame.1.json becomes .2, and the temp file replaces savegame.json, whose old
        /// content becomes .1 in the same call. A crash at any step leaves at least one complete earlier save.
        /// The first save of a game (move 1) drops the earlier files instead, so a fallback never loads another game.
        /// </summary>
        internal static void WriteSave(SaveGameData saveData)
        {
            string savePath = SaveFilePath;
            string tempPath = TempFilePath;

            string directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            saveData.version = CurrentVersion;
            File.WriteAllText(tempPath, JsonUtility.ToJson(saveData, true));

            // Saves happen only after a move, so move 1 is a new game's first save: the files on disk belong to an
            // earlier game and are not kept as its backups (CC-042, decision D5).
            bool firstSaveOfGame = saveData.moveCount <= 1;

            // Oldest first, so each move has a free target: .2 is dropped, then .1 moves to .2.
            for (int slot = BackupCount; slot >= 1; slot--)
            {
                string path = SaveSlotPath(slot);
                if (!File.Exists(path)) continue;
                if (slot == BackupCount || firstSaveOfGame)
                    File.Delete(path);
                else
                    File.Move(path, SaveSlotPath(slot + 1));
            }

            string newestBackup = firstSaveOfGame ? null : SaveSlotPath(1);

            if (File.Exists(savePath))
            {
                try
                {
                    if (SimulateNoFileReplace)
                        throw new PlatformNotSupportedException();
                    File.Replace(tempPath, savePath, newestBackup);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                    // Fall through: the same steps as two moves. A crash between them leaves the old save as .1.
                }
                if (newestBackup != null)
                    File.Move(savePath, newestBackup);
                else
                    File.Delete(savePath);
            }
            File.Move(tempPath, savePath);
        }

        /// <summary>Deletes the save, its backups and any leftover temp file. SaveGameSystem calls it when the game
        /// ends. Set-aside bad files are kept.</summary>
        public static void DeleteSaveGame()
        {
            for (int slot = 0; slot <= BackupCount; slot++)
            {
                string path = SaveSlotPath(slot);
                if (File.Exists(path))
                    File.Delete(path);
            }
            if (File.Exists(TempFilePath))
                File.Delete(TempFilePath);
        }

        static bool AnySaveSlotExists()
        {
            for (int slot = 0; slot <= BackupCount; slot++)
                if (File.Exists(SaveSlotPath(slot)))
                    return true;
            return false;
        }

        /// <summary>
        /// Reads the save files newest first and returns the first that passes the checks. Each unusable file on the
        /// way is moved aside with a warning. When a backup is used, the files are renumbered so it becomes
        /// savegame.json and the next save rotates normally (CC-042). False when no file is usable.
        /// </summary>
        static bool TryReadSave(out SaveGameData saveData)
        {
            for (int slot = 0; slot <= BackupCount; slot++)
            {
                string path = SaveSlotPath(slot);
                if (!File.Exists(path))
                    continue;

                if (TryReadFile(path, out saveData))
                {
                    if (slot > 0)
                        PromoteSlot(slot);
                    return true;
                }
            }

            saveData = null;
            return false;
        }

        /// <summary>Reads and checks one save file. An unusable file is moved to <see cref="BadFilePath"/> with a
        /// warning, and false is returned.</summary>
        static bool TryReadFile(string path, out SaveGameData saveData)
        {
            string problem;
            try
            {
                saveData = JsonUtility.FromJson<SaveGameData>(File.ReadAllText(path));
                problem = Validate(saveData);
            }
            catch (Exception e)
            {
                saveData = null;
                problem = $"unreadable ({e.Message})";
            }

            if (problem == null)
                return true;

            saveData = null;
            SetAside(path, problem);
            return false;
        }

        /// <summary>Renumbers the files so <paramref name="slot"/> becomes savegame.json and the older ones follow it.
        /// The newer slots were all rejected, so a file still in one (it could not be moved aside) is dropped.</summary>
        static void PromoteSlot(int slot)
        {
            try
            {
                for (int from = slot; from <= BackupCount; from++)
                {
                    string source = SaveSlotPath(from);
                    string target = SaveSlotPath(from - slot);
                    if (File.Exists(target))
                        File.Delete(target);
                    if (File.Exists(source))
                        File.Move(source, target);
                }
            }
            catch (Exception e)
            {
                // The usable file stays in a slot the next load reads, so it is found again then.
                Debug.LogError($"Could not renumber the save files after a fallback: {e.Message}");
            }
        }

        /// <summary>Null when the save can be resumed, otherwise what is wrong with it.</summary>
        static string Validate(SaveGameData saveData)
        {
            if (saveData == null)
                return "empty";
            if (Array.IndexOf(ReadableVersions, saveData.version) < 0)
                return $"unknown version \"{saveData.version}\"";
            if (saveData.gridWidth < 1 || saveData.gridHeight < 1)
                return $"invalid grid size {saveData.gridWidth}x{saveData.gridHeight}";
            return null;
        }

        static void SetAside(string path, string problem)
        {
            string file = Path.GetFileName(path);
            try
            {
                if (File.Exists(BadFilePath))
                    File.Delete(BadFilePath);
                File.Move(path, BadFilePath);
                Debug.LogWarning($"Save game is unusable ({file}: {problem}); moved to {BadFilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Save game is unusable ({file}: {problem}) and could not be moved aside: {e.Message}");
            }
        }

        /// <summary>The asset name of a profile: a runtime copy is named "&lt;asset&gt;(Clone)".</summary>
        internal static string ProfileAssetName(ColorProfile profile)
        {
            if (profile == null) return null;
            string name = profile.name;
            return name.EndsWith(CloneSuffix, StringComparison.Ordinal) ? name.Substring(0, name.Length - CloneSuffix.Length).TrimEnd() : name;
        }

        /// <summary>
        /// Initialize game from save data instead of default configuration. The saved colour profile is used when one of
        /// <paramref name="knownColorProfiles"/> has its name; otherwise <paramref name="colorProfileTemplate"/>.
        /// </summary>
        public static void InitializeFromSave(SaveGameData saveData, GameConfiguration configTemplate, ColorProfile colorProfileTemplate,
            IReadOnlyList<ColorProfile> knownColorProfiles = null)
        {
            if (saveData == null)
            {
                Debug.LogError("Cannot initialize from null save data");
                return;
            }

            // Create runtime configuration from save data
            var runtimeConfig = UnityEngine.Object.Instantiate(configTemplate);
            runtimeConfig.gameMode = saveData.gameMode;
            runtimeConfig.gridWidth = saveData.gridWidth;
            runtimeConfig.gridHeight = saveData.gridHeight;
            runtimeConfig.spawnNextColourSystem = saveData.spawnNextColourSystem;

            // Start the resumed game without touching the menu's configuration (CC-027)
            GameManager.StartResumedGame(runtimeConfig, FindProfile(saveData.colorProfileName, knownColorProfiles) ?? colorProfileTemplate);
        }

        static ColorProfile FindProfile(string savedName, IReadOnlyList<ColorProfile> profiles)
        {
            if (string.IsNullOrEmpty(savedName) || profiles == null) return null;
            // Saves before CC-031 stored the runtime copy's name, with the "(Clone)" suffix.
            string wanted = savedName.EndsWith(CloneSuffix, StringComparison.Ordinal)
                ? savedName.Substring(0, savedName.Length - CloneSuffix.Length).TrimEnd()
                : savedName;
            foreach (var profile in profiles)
                if (profile != null && ProfileAssetName(profile) == wanted)
                    return profile;
            return null;
        }

        /// <summary>
        /// Restore ECS world state from save data
        /// Call this after the ECS world has been set up but before spawning initial blocks
        /// </summary>
        public static void RestoreWorldState(SaveGameData saveData)
        {
            if (saveData == null) return;

            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;

            // Restore game state
            var gameStateQuery = entityManager.CreateEntityQuery(typeof(GameStateComponent));
            if (!gameStateQuery.IsEmpty)
            {
                var gameStateEntity = gameStateQuery.GetSingletonEntity();
                var gameState = entityManager.GetComponentData<GameStateComponent>(gameStateEntity);
                
                // Always resume waiting for input. Saves from builds before CC-006.5 stored false (the move
                // frame's value), which made the first frame after resume look like a move.
                gameState.WaitingForInput = true;
                gameState.GameOver = saveData.gameOver;
                gameState.LevelComplete = saveData.levelComplete;
                gameState.MoveCount = saveData.moveCount;
                gameState.Random = RestoreRandom(saveData.randomState);
                
                entityManager.SetComponentData(gameStateEntity, gameState);
            }

            // Restore score
            var scoreQuery = entityManager.CreateEntityQuery(typeof(ScoreComponent));
            if (!scoreQuery.IsEmpty)
            {
                var scoreEntity = scoreQuery.GetSingletonEntity();
                var score = new ScoreComponent
                {
                    TotalScore = saveData.totalScore,
                    CurrentFrameScore = saveData.currentFrameScore
                };
                entityManager.SetComponentData(scoreEntity, score);
            }

            RestoreBlocks(entityManager, saveData.blocks);
        }

        /// <summary>
        /// Rebuilds the generator from its saved STATE (not a seed: Random(uint) would hash it), so a resumed game
        /// continues the saved sequence. CC-044.
        /// </summary>
        internal static Unity.Mathematics.Random RestoreRandom(uint savedState)
        {
            // State 0 is invalid for Random's xorshift; only a hand-edited or broken file can hold it.
            if (savedState == 0u) return new Unity.Mathematics.Random(1u);
            return new Unity.Mathematics.Random { state = savedState };
        }

        private static void RestoreBlocks(EntityManager entityManager, SavedBlock[] savedBlocks)
        {
            if (savedBlocks == null || savedBlocks.Length == 0) return;
            
            var blockArchetype = entityManager.CreateArchetype(typeof(BlockComponent));

            foreach (var savedBlock in savedBlocks)
            {
                var blockEntity = entityManager.CreateEntity(blockArchetype);
                
                // Set the block component data
                entityManager.SetComponentData(blockEntity, new BlockComponent
                {
                    Color = savedBlock.color,
                    Size = savedBlock.size,
                    GridPosition = new int2(savedBlock.gridX, savedBlock.gridY),
                    IsNextColorIndicator = savedBlock.isNextColorIndicator
                });
            }
        }
    }
}
