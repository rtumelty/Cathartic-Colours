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

        /// <summary>The save file path. The single owner of the path: SaveGameSystem uses it too.</summary>
        internal static string SaveFilePath => SaveFilePathOverride ?? Path.Combine(Application.persistentDataPath, "savegame.json");

        /// <summary>Where the save is written before it is moved over the real file, so a crash mid-write never leaves
        /// a truncated save.</summary>
        internal static string TempFilePath => SaveFilePath + ".tmp";

        /// <summary>Where an unusable save (unreadable, or from an unknown version) is moved, so it stops offering Resume
        /// but is kept for inspection. Replaced by the next unusable save.</summary>
        internal static string BadFilePath => Path.Combine(Path.GetDirectoryName(SaveFilePath) ?? "", "savegame.bad.json");

        /// <summary>True when there is a save that can be resumed. An unusable file is moved aside here, so the main menu
        /// shows Resume only for a save that will load.</summary>
        public static bool HasSaveGame()
        {
            return TryReadSave(out _);
        }

        public static SaveGameData LoadSaveGame()
        {
            if (!File.Exists(SaveFilePath))
            {
                Debug.LogWarning("No save game file found");
                return null;
            }

            return TryReadSave(out var saveData) ? saveData : null;
        }

        /// <summary>Writes the save atomically: the JSON goes to a temp file, which then replaces the real one.</summary>
        internal static void WriteSave(SaveGameData saveData)
        {
            string savePath = SaveFilePath;
            string tempPath = TempFilePath;

            string directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            saveData.version = CurrentVersion;
            File.WriteAllText(tempPath, JsonUtility.ToJson(saveData, true));

            if (File.Exists(savePath))
            {
                try
                {
                    File.Replace(tempPath, savePath, null);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                    // Fall through: delete and move. A crash between the two leaves no save, never a truncated one.
                }
                File.Delete(savePath);
            }
            File.Move(tempPath, savePath);
        }

        public static void DeleteSaveGame()
        {
            if (File.Exists(SaveFilePath))
                File.Delete(SaveFilePath);
            if (File.Exists(TempFilePath))
                File.Delete(TempFilePath);
        }

        /// <summary>Reads and checks the save. Returns false when there is none, or when it is unusable, in which case
        /// the file is moved to <see cref="BadFilePath"/> and a warning is logged.</summary>
        static bool TryReadSave(out SaveGameData saveData)
        {
            saveData = null;
            if (!File.Exists(SaveFilePath))
                return false;

            string problem;
            try
            {
                saveData = JsonUtility.FromJson<SaveGameData>(File.ReadAllText(SaveFilePath));
                problem = Validate(saveData);
            }
            catch (Exception e)
            {
                problem = $"unreadable ({e.Message})";
            }

            if (problem == null)
                return true;

            saveData = null;
            SetAside(problem);
            return false;
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

        static void SetAside(string problem)
        {
            try
            {
                if (File.Exists(BadFilePath))
                    File.Delete(BadFilePath);
                File.Move(SaveFilePath, BadFilePath);
                Debug.LogWarning($"Save game is unusable ({problem}); moved to {BadFilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Save game is unusable ({problem}) and could not be moved aside: {e.Message}");
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

            // Initialize GameManager with loaded configuration
            GameManager.Initialize(runtimeConfig, FindProfile(saveData.colorProfileName, knownColorProfiles) ?? colorProfileTemplate);
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
