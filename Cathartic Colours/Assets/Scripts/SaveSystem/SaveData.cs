using Simulation;
using System;
using ECS.Components;
using UnityEngine;

namespace SaveSystem
{
    [Serializable]
    public class SaveGameData
    {
        [Header("Metadata")]
        public string version = "1.0";
        public string saveTimestamp;
        
        [Header("Game Configuration")]
        public GameMode gameMode;
        public int gridWidth;
        public int gridHeight;
        public bool spawnNextColourSystem;
        public string colorProfileName;
        
        [Header("Game State")]
        public bool waitingForInput;
        public bool gameOver;
        public bool levelComplete;
        public int moveCount;
        public uint randomState; 
        
        [Header("Score")]
        public int totalScore;
        public int currentFrameScore;
        
        [Header("Grid Contents")]
        public SavedBlock[] blocks;
        
        public SaveGameData()
        {
            saveTimestamp = DateTime.Now.ToBinary().ToString();
        }
    }
    
    [Serializable]
    public struct SavedBlock
    {
        public BlockColor color;
        public BlockSize size;
        public int gridX;
        public int gridY;
        public bool isNextColorIndicator;
    }
}