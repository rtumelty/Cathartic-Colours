using ECS.Components;
using Unity.Burst;
using Unity.Entities;

namespace ECS.Systems
{
    // Removes the move command after BoardStepSystem has used (or rejected) it, so each input is one move.
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BoardStepSystem))]
    public partial struct ClearMoveDirectionSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<MoveDirectionComponent>(out var entity))
            {
                state.EntityManager.DestroyEntity(entity);
            }
        }
    }
}