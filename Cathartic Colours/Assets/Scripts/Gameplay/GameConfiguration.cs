using UnityEngine;

[CreateAssetMenu(fileName = "GameConfiguration", menuName = "Scriptable Objects/GameConfiguration")]
public class GameConfiguration : ScriptableObject
{
    public enum GameMode : byte
    {
        Standard,
        ColorMerge,
        AdvancedColorMerge
    }

    [Header("Gameplay Settings")] public GameMode gameMode = GameMode.Standard;

    [Header("Grid Settings")]
    [SerializeField] public int gridWidth = 6;
    [SerializeField] public int gridHeight = 6;

    [Header("Active systems")] 
    public bool spawnNextColourSystem = true;

}
