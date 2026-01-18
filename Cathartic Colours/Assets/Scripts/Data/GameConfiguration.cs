using ECS.Components;
using UnityEngine;

namespace Data
{
    [CreateAssetMenu(fileName = "GameConfiguration", menuName = "Scriptable Objects/GameConfiguration")]
    public class GameConfiguration : ScriptableObject
    {
        [Header("Gameplay Settings")] 
        public ECS.Components.GameMode gameMode = ECS.Components.GameMode.Standard;

        [Header("Grid Settings")]
        [SerializeField] public int gridWidth = 6;
        [SerializeField] public int gridHeight = 6;

        [Header("Active systems")] 
        public bool spawnNextColourSystem = true;

        [Header("Scoring Configuration")]
        [SerializeField] public int tier1Points = 10;   // Basic merges
        [SerializeField] public int tier2Points = 50;   // Medium complexity merges  
        [SerializeField] public int tier3Points = 100;  // High complexity merges
        [SerializeField] public int tier4Points = 200;  // Special/rare merges

        public int GetPointsForTier(ScoreTier tier)
        {
            return tier switch
            {
                ScoreTier.Tier1 => tier1Points,
                ScoreTier.Tier2 => tier2Points,
                ScoreTier.Tier3 => tier3Points,
                ScoreTier.Tier4 => tier4Points,
                _ => 0
            };
        }
    }
}