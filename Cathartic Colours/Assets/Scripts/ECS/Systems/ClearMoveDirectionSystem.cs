using ECS.Components;
using Unity.Entities;

namespace ECS.Systems
{
    // System to clear move direction after processing
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SpawnNextColorSystem))]
    public partial struct ClearMoveDirectionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<MoveDirectionComponent>(out var entity))
            {
                state.EntityManager.DestroyEntity(entity);
            }
        }
    }
}