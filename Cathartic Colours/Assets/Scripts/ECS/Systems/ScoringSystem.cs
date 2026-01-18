using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using ECS.Components;

namespace ECS.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct ScoringSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameStateComponent>();
            state.RequireForUpdate<ScoreComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<ScoreComponent>(out var scoreComponent))
                return;

            // Get active configuration
            var gameConfig = GameConfigurationManager.ActiveConfiguration;
            if (gameConfig == null)
            {
                Debug.LogWarning("No active GameConfiguration found!");
                return;
            }

            // Find particle spawner in scene
            var particleSpawner = GameObject.FindObjectOfType<PointsParticleSpawner>();
            if (particleSpawner == null)
            {
                Debug.LogWarning("PointsParticleSpawner not found in scene!");
                return;
            }

            // Find GameRenderer for world position conversion
            var gameRenderer = GameObject.FindObjectOfType<Gameplay.GameRenderer>();
            if (gameRenderer == null)
            {
                Debug.LogWarning("GameRenderer not found in scene!");
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            int frameScore = 0;

            // Process all merge events from this frame
            foreach (var (mergeEvent, entity) in SystemAPI.Query<RefRW<MergeEventComponent>>().WithEntityAccess())
            {
                // Calculate points based on tier
                int points = gameConfig.GetPointsForTier(mergeEvent.ValueRO.Tier);

                // Update merge event with calculated points
                var updatedEvent = mergeEvent.ValueRO;
                updatedEvent.Points = points;
                mergeEvent.ValueRW = updatedEvent;

                // Add to frame score
                frameScore += points;

                // Convert grid position to world position
                Vector3 worldPosition = GridToWorldPosition(mergeEvent.ValueRO.Position, gameRenderer);

                // Spawn particles at merge location
                particleSpawner.Emit(points, worldPosition);

                // Clean up the merge event
                ecb.DestroyEntity(entity);
            }

            // Update score component
            scoreComponent.TotalScore += frameScore;
            scoreComponent.CurrentFrameScore = frameScore;
            SystemAPI.SetSingleton(scoreComponent);

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private Vector3 GridToWorldPosition(Unity.Mathematics.int2 gridPos, Gameplay.GameRenderer renderer)
        {
            return new Vector3(
                gridPos.x * (renderer.cellSize + renderer.cellSpacing),
                gridPos.y * (renderer.cellSize + renderer.cellSpacing),
                -1
            );
        }
    }
}
