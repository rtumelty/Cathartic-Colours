using System;
using UnityEngine;

public class PointsParticleSpawner : MonoBehaviour
{
    enum SpawnMode
    {
        Text,
        ParticleCount
    }
    
    [Header("Particle System(s)")]
    [SerializeField]  ParticleSystem particleSystem;
    public ParticleSystem[] digitParticleSystems = new ParticleSystem[10];
    [SerializeField] SpawnMode spawnMode = SpawnMode.Text;
    
    [Header("Text Settings")]
    [SerializeField]  float particleWidth = .1f; // Adjust based on your particle size/font
    [SerializeField]  float spacingMultiplier = 1.2f; // 1.2 = 20% wider than particle width
    
    [Header("Positioning")]
    public bool centerAlignment = true; // Whether to center the group of numbers

    private void SpawnParticles(Vector3 worldPosition, int count)
    {
        // Create emission parameters with minimal overrides
        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
        emitParams.position = worldPosition;
        
        // Emit a single particle
        particleSystem.Emit(emitParams, count);
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

    public void Emit(int pointValue, Vector3 worldPosition)
    {
        if (spawnMode == SpawnMode.Text)
        {
            SpawnPoints(pointValue, worldPosition);
        }
        else if (spawnMode == SpawnMode.ParticleCount) 
        {
            SpawnPointsScaled(pointValue, worldPosition);
        }
    }

    private void SpawnPoints(int points, Vector3 position)
    {
        // Convert points to string to get digit count
        string pointsText = points.ToString();
        int digitCount = pointsText.Length;
        
        if (digitCount == 0) return;

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
            SpawnNumberParticle(points / (10 * digitCount), digitPosition);
        }
    }
    
    // Alternative method if you want to spawn different amounts based on point value
    private void SpawnPointsScaled(int points, Vector3 position)
    {
        // Determine how many particles to spawn based on point value
        int particleCount = Mathf.Clamp(points / 100, 1, 5); // 1-5 particles based on points
        SpawnParticles(position, particleCount);
    }
}
