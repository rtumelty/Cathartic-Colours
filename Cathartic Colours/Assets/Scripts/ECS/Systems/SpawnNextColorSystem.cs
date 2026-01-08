using ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BlockMovementSystem))]
    public partial struct SpawnNextColorSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var gameState = SystemAPI.GetSingletonRW<GameStateComponent>();

            if (gameState.ValueRO.WaitingForInput)
                return;

            var gridConfig = SystemAPI.GetSingleton<GridConfigComponent>();

            // Build occupancy map
            var occupancyMap = new NativeHashSet<int2>(100, Allocator.Temp);
            foreach (var block in SystemAPI.Query<RefRO<BlockComponent>>())
            {
                occupancyMap.Add(block.ValueRO.GridPosition);
            }

            // Find random empty position
            int2 spawnPos = new int2(-1, -1);
            int maxAttempts = gridConfig.Width * gridConfig.Height * 2;
            
            for (int i = 0; i < maxAttempts; i++)
            {
                int2 testPos = new int2(
                    gameState.ValueRW.Random.NextInt(0, gridConfig.Width),
                    gameState.ValueRW.Random.NextInt(0, gridConfig.Height)
                );

                if (!occupancyMap.Contains(testPos))
                {
                    spawnPos = testPos;
                    break;
                }
            }

            occupancyMap.Dispose();

            if (spawnPos.x == -1)
            {
                // Grid is full
                gameState.ValueRW.WaitingForInput = true;
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new BlockComponent
            {
                Color = BlockColor.White,
                Size = BlockSize.Small,
                GridPosition = spawnPos,
                IsNextColorIndicator = true
            });

            gameState.ValueRW.WaitingForInput = true;

            // Check win condition
            bool hasColoredBlocks = false;
            foreach (var block in SystemAPI.Query<RefRO<BlockComponent>>())
            {
                if (!block.ValueRO.IsNextColorIndicator)
                {
                    hasColoredBlocks = true;
                    break;
                }
            }

            if (!hasColoredBlocks)
            {
                gameState.ValueRW.LevelComplete = true;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}