using System;
using ECS.Components;
using UnityEngine;

public class PointsParticleSpawner : MonoBehaviour
{
    enum SpawnMode
    {
        Text,
        ParticleCount
    }
    
    [Header("Particle System(s)")]
    [SerializeField]  ParticleSystem blobParticleSystem;
    public ParticleSystem[] digitParticleSystems = new ParticleSystem[10];
    [SerializeField] SpawnMode spawnMode = SpawnMode.Text;
    [SerializeField] int pointsPerBlobParticle = 10;
    
    [Header("Text Settings")]
    [SerializeField]  float particleWidth = .1f; // Adjust based on your particle size/font
    [SerializeField]  float spacingMultiplier = 1.2f; // 1.2 = 20% wider than particle width
    
    [Header("Positioning")]
    public bool centerAlignment = true; // Whether to center the group of numbers

    private void Awake()
    {
        GameManager.ActivePointsParticleSpawner = this;
    }

    private void OnDestroy()
    {
        GameManager.ActivePointsParticleSpawner = null;
    }

    private void SpawnParticles(Vector3 worldPosition, BlockColor blockColor, int count)
    {
        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
        emitParams.position = worldPosition;
        emitParams.startColor = GameManager.ActiveColorProfile.GetColor(blockColor);
        
        blobParticleSystem.Emit(emitParams, count);
    }

    public void SpawnNumberParticle(int value, Vector3 worldPosition)
    {
        if (value > digitParticleSystems.Length || value < 0) 
            return;
        
        // Create emission parameters with minimal overrides
        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
        emitParams.position = worldPosition;
        
        var digitParticleSystem = digitParticleSystems[value];
        
        // Emit a single particle
        digitParticleSystem.Emit(emitParams, 1);
    }

    public void Emit(int pointValue, BlockColor mergeColor, Vector3 worldPosition)
    {
        Debug.Log(pointValue);
        if (spawnMode == SpawnMode.Text)
        {
            SpawnPoints(pointValue, worldPosition);
        }
        else if (spawnMode == SpawnMode.ParticleCount) 
        {
            SpawnBlobParticles(pointValue, worldPosition, mergeColor);
        }
    }
    
    private void SpawnPoints(int points, Vector3 position)
    {
        if (points <= 0) return;

        // Calculate digit count using integer operations
        int digitCount = Mathf.FloorToInt(Mathf.Log10(points)) + 1;
    
        // Calculate spacing between particles
        float spacing = particleWidth * spacingMultiplier;
        float totalWidth = (digitCount - 1) * spacing;
    
        // Calculate starting position (leftmost particle position)
        Vector3 startPosition = position;
        if (centerAlignment)
        {
            startPosition.x -= totalWidth * 0.5f;
        }

        // Spawn a particle for each digit
        for (int i = 0; i < digitCount; i++)
        {
            Vector3 digitPosition = startPosition + Vector3.right * (spacing * i);
        
            // Calculate the divisor for this digit position (leftmost first)
            int divisor = (int)Mathf.Pow(10, digitCount - 1 - i);
            int digit = (points / divisor) % 10;
        
            SpawnNumberParticle(digit, digitPosition);
        }
    }
    
    private void SpawnBlobParticles(int points, Vector3 position, BlockColor mergeColor)
    {
        // Determine how many particles to spawn based on point value
        int particleCount = Mathf.Clamp(points / pointsPerBlobParticle, 1, int.MaxValue); // 1-5 particles based on points
        SpawnParticles(position, mergeColor, particleCount);
    }
}
