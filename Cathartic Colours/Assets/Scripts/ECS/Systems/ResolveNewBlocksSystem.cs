using ECS.Components;
using Unity.Collections;
using Unity.Entities;

namespace ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BlockMovementSystem))]
    [UpdateBefore(typeof(SpawnNextColorSystem))]
    public partial struct ResolveNewBlocksSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameStateComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var gameStateEntity = SystemAPI.GetSingletonEntity<GameStateComponent>();
            var gameState = state.EntityManager.GetComponentData<GameStateComponent>(gameStateEntity);
        
            if (gameState.WaitingForInput || gameState.GameOver)
                return;
            
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            // Convert white blocks to random colored blocks - new blocks should not move in same frame
            foreach (var (block, entity) in SystemAPI.Query<RefRW<BlockComponent>>().WithEntityAccess())
            {
                if (block.ValueRO.IsNextColorIndicator)
                {
                    block.ValueRW.IsNextColorIndicator = false;
                    block.ValueRW.Color = GetRandomColor(ref gameState.Random);
                    block.ValueRW.Size = BlockSize.Small;
                    
                    ecb.AddComponent<MovingBlockTag>(entity);
                    ecb.SetComponentEnabled<MovingBlockTag>(entity, false);
                    
                    ecb.AddComponent<PendingMergeTag>(entity);
                    ecb.SetComponentEnabled<PendingMergeTag>(entity, false);
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private BlockColor GetRandomColor(ref Unity.Mathematics.Random random)
        {
            BlockColor[] colors = new[] { BlockColor.Red, BlockColor.Green, BlockColor.Blue };
            int colorIndex = random.NextInt(0, 3);
            return colors[colorIndex];
        }
    }
}
