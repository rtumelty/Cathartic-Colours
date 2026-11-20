using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using ECS.Components;
using Data;
using SaveSystem;

namespace ECS.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    // Runs after MergeEffectsSystem; that system (Presentation assembly) declares the order, since Core can't reference it.
    public partial struct SaveGameSystem : ISystem
    {
        // True once the save has been deleted for the current game over, so the file check runs once, not every frame.
        private bool endedLastFrame;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameStateComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<GameStateComponent>(out var gameState))
                return;

            // BoardStepSystem sets WaitingForInput to false only in the frame of an accepted move
            // (EndFrameSystem sets it back), so this is true exactly once per move.
            bool inputProcessedThisFrame = !gameState.WaitingForInput;

            bool ended = gameState.GameOver || gameState.LevelComplete;

            if (inputProcessedThisFrame && !ended)
            {
                SaveGame(ref state);
            }
            else if (ended && !endedLastFrame)
            {
                DeleteSaveGame();
            }

            endedLastFrame = ended;
        }

        private void SaveGame(ref SystemState state)
        {
            try
            {
                // Written to a temp file and moved into place, so a crash mid-write can't truncate the save (CC-031).
                // The two saves before it are kept as backups for a fallback load (CC-042).
                SaveGameManager.WriteSave(CreateSaveData(ref state));
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
                // The asset's name, without the runtime copy's "(Clone)" suffix, so a resume can find the profile.
                saveData.colorProfileName = SaveGameManager.ProfileAssetName(colorProfile);
            }
            
            // Get game state
            if (SystemAPI.TryGetSingleton<GameStateComponent>(out var gameState))
            {
                // The save describes the idle state after the move, when the game waits for the next input.
                // (WaitingForInput is false right now only because this is the move's frame.)
                saveData.waitingForInput = true;
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
                SaveGameManager.DeleteSaveGame();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to delete save game: {e.Message}");
            }
        }
    }
}
