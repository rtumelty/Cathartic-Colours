using UnityEngine;

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
}