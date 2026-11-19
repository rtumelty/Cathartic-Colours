using UnityEngine;
using System.IO;
using SaveSystem;
using Data;
using ECS.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace SaveSystem
{
    public static class SaveGameManager
    {
        private static string SaveFilePath => Path.Combine(Application.persistentDataPath, "savegame.json");

        public static bool HasSaveGame()
        {
            return File.Exists(SaveFilePath);
        }

        public static SaveGameData LoadSaveGame()
        {
            if (!HasSaveGame())
            {
                Debug.LogWarning("No save game file found");
                return null;
            }

            try
            {
                string json = File.ReadAllText(SaveFilePath);
                var saveData = JsonUtility.FromJson<SaveGameData>(json);
                
                #if UNITY_EDITOR
                Debug.Log($"Save game loaded from: {SaveFilePath}");
                #endif
                
                return saveData;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load save game: {e.Message}");
                return null;
            }
        }

        public static void DeleteSaveGame()
        {
            if (File.Exists(SaveFilePath))
            {
                File.Delete(SaveFilePath);
                Debug.Log("Save game deleted");
            }
        }

        /// <summary>
        /// Initialize game from save data instead of default configuration
        /// </summary>
        public static void InitializeFromSave(SaveGameData saveData, GameConfiguration configTemplate, ColorProfile colorProfileTemplate)
        {
            if (saveData == null)
            {
                Debug.LogError("Cannot initialize from null save data");
                return;
            }

            // Create runtime configuration from save data
            var runtimeConfig = Object.Instantiate(configTemplate);
            runtimeConfig.gameMode = saveData.gameMode;
            runtimeConfig.gridWidth = saveData.gridWidth;
            runtimeConfig.gridHeight = saveData.gridHeight;
            runtimeConfig.spawnNextColourSystem = saveData.spawnNextColourSystem;

            // Initialize GameManager with loaded configuration
            GameManager.Initialize(runtimeConfig, colorProfileTemplate);
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
                gameState.Random = new Unity.Mathematics.Random(saveData.randomState);
                
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
