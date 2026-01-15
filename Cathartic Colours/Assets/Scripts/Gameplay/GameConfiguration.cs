using UnityEngine;

[CreateAssetMenu(fileName = "GameConfiguration", menuName = "Scriptable Objects/GameConfiguration")]
public class GameConfiguration : ScriptableObject
{

    [Header("Grid Settings")]
    [SerializeField] public int gridWidth = 6;
    [SerializeField] public int gridHeight = 6;
    
}
