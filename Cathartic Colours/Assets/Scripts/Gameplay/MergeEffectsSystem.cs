using ECS.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ECS.Systems
{
    /// <summary>
    /// Turns merge events into points particles and destroys them. Score is already applied by
    /// <see cref="BoardStepSystem"/>, so a missing particle spawner only loses the effect.
    /// Managed (not Burst): it calls the <c>PointsParticleSpawner</c> MonoBehaviour.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(SaveGameSystem))] // was UpdateAfter(MergeEffectsSystem) on SaveGameSystem before CC-002
    public partial struct MergeEffectsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MergeEventComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var particleSpawner = PresentationRegistry.ActivePointsParticleSpawner;
            var gameRenderer = PresentationRegistry.ActiveGameRenderer;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (mergeEvent, entity) in SystemAPI.Query<RefRO<MergeEventComponent>>().WithEntityAccess())
            {
                if (particleSpawner != null && gameRenderer != null)
                {
                    Vector3 worldPosition = GridToWorldPosition(mergeEvent.ValueRO.Position, gameRenderer) + new Vector3(0, 0, -5);
                    particleSpawner.Emit(mergeEvent.ValueRO.Points, mergeEvent.ValueRO.Color, worldPosition);
                }

                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static Vector3 GridToWorldPosition(Unity.Mathematics.int2 gridPos, Gameplay.GameRenderer renderer)
        {
            return new Vector3(
                gridPos.x * (renderer.cellSize + renderer.cellSpacing),
                gridPos.y * (renderer.cellSize + renderer.cellSpacing),
                -1
            );
        }
    }
}
