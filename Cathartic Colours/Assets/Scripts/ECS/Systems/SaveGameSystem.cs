using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using System.IO;
using ECS.Components;
using Data;
using SaveSystem;

namespace ECS.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ScoringSystem))]
    public partial struct SaveGameSystem : ISystem
    {
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameStateComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<GameStateComponent>(out var gameState))
                return;

            // Detect if input was processed this frame
            // Input is processed when we transition from WaitingForInput=false to WaitingForInput=true
            bool inputProcessedThisFrame = !gameState.WaitingForInput;

            if (inputProcessedThisFrame && !gameState.GameOver && !gameState.LevelComplete)
            {
                SaveGame(ref state);
            }
            else if (gameState.GameOver || gameState.LevelComplete)
            {
                DeleteSaveGame();
            }
        }

        private void SaveGame(ref SystemState state)
        {
            try
            {
                var saveData = CreateSaveData(ref state);
                string json = JsonUtility.ToJson(saveData, true);
                string savePath = GetSaveFilePath();
                
                // Ensure directory exists
                string directory = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                File.WriteAllText(savePath, json);
                
                #if UNITY_EDITOR
                Debug.Log($"Game saved to: {savePath}");
                #endif
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to save game: {e.Message}");
            }
        }

        private SaveGameData CreateSaveData(ref SystemState state)
        {
            var saveData = new SaveGameData();
            
            // Get game configuration
            var config = GameManager.ActiveConfiguration;
            if (config != null)
            {
                saveData.gameMode = config.gameMode;
                saveData.gridWidth = config.gridWidth;
                saveData.gridHeight = config.gridHeight;
                saveData.spawnNextColourSystem = config.spawnNextColourSystem;
            }
            
            // Get color profile name
            var colorProfile = GameManager.ActiveColorProfile;
            if (colorProfile != null)
            {
                saveData.colorProfileName = colorProfile.name;
            }
            
            // Get game state
            if (SystemAPI.TryGetSingleton<GameStateComponent>(out var gameState))
            {
                saveData.waitingForInput = gameState.WaitingForInput;
                saveData.gameOver = gameState.GameOver;
                saveData.levelComplete = gameState.LevelComplete;
                saveData.moveCount = gameState.MoveCount;
                saveData.randomState = gameState.Random.state;
            }
            
            // Get score
            if (SystemAPI.TryGetSingleton<ScoreComponent>(out var score))
            {
                saveData.totalScore = score.TotalScore;
                saveData.currentFrameScore = score.CurrentFrameScore;
            }
            
            // Get all blocks
            var blockQuery = SystemAPI.QueryBuilder().WithAll<BlockComponent>().Build();
            var blocks = blockQuery.ToComponentDataArray<BlockComponent>(Allocator.Temp);
            
            saveData.blocks = new SavedBlock[blocks.Length];
            for (int i = 0; i < blocks.Length; i++)
            {
                var block = blocks[i];
                saveData.blocks[i] = new SavedBlock
                {
                    color = block.Color,
                    size = block.Size,
                    gridX = block.GridPosition.x,
                    gridY = block.GridPosition.y,
                    isNextColorIndicator = block.IsNextColorIndicator
                };
            }
            
            blocks.Dispose();
            return saveData;
        }

        private void DeleteSaveGame()
        {
            try
            {
                string savePath = GetSaveFilePath();
                if (File.Exists(savePath))
                {
                    File.Delete(savePath);
                    #if UNITY_EDITOR
                    Debug.Log("Save game deleted");
                    #endif
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to delete save game: {e.Message}");
            }
        }

        private string GetSaveFilePath()
        {
            return Path.Combine(Application.persistentDataPath, "savegame.json");
        }
    }
}
