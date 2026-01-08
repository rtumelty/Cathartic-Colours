using ECS.Components;
using Unity.Collections;
using Unity.Entities;

namespace ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(BlockMovementSystem))]
    public partial struct InputProcessingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameStateComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var gameStateEntity = SystemAPI.GetSingletonEntity<GameStateComponent>();
            var gameState = state.EntityManager.GetComponentData<GameStateComponent>(gameStateEntity);
        
            if (!gameState.WaitingForInput || gameState.GameOver)
                return;

            if (!SystemAPI.TryGetSingleton<MoveDirectionComponent>(out var moveDirection))
                return;

            if (moveDirection.Direction.x == 0 && moveDirection.Direction.y == 0)
                return;
            
            // Enable MovingBlockTag on all blocks
            foreach (var entity in SystemAPI.QueryBuilder()
                         .WithAll<BlockComponent>()
                         .WithDisabled<MovingBlockTag>()
                         .Build()
                         .ToEntityArray(Allocator.Temp))
            {
                state.EntityManager.SetComponentEnabled<MovingBlockTag>(entity, true);
            }
            
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

            gameState.WaitingForInput = false;
            gameState.MoveCount++;
            state.EntityManager.SetComponentData(gameStateEntity, gameState);
        }

        private BlockColor GetRandomColor(ref Unity.Mathematics.Random random)
        {
            int colorIndex = random.NextInt(1, 4);
            return (BlockColor)colorIndex;
        }
    }
}
