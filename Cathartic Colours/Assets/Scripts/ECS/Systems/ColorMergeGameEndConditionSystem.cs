/*
using ECS.Utilities;
using ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ColorMergeBlockMovementSystem))]
    [UpdateAfter(typeof(SpawnColorSystem))]
    public partial struct ColorMergeGameEndConditionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ColorMergeSystemTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var gameState = SystemAPI.GetSingletonRW<GameStateComponent>();
            
            if (gameState.ValueRW.GameOver) return;
            
            var gridConfig = SystemAPI.GetSingleton<GridConfigComponent>();

            // Build occupancy map
            var occupancyMap = new NativeHashMap<int2, Entity>(100, Allocator.Temp);
            foreach (var (block, entity) in SystemAPI.Query<RefRO<BlockComponent>>().WithEntityAccess())
            {
                occupancyMap[block.ValueRO.GridPosition] = entity;
            }

            // Check if there is room to place a new block
            bool hasEmptyCell = false;
            for (int x = 0; x < gridConfig.Width; x++)
            {
                for (int y = 0; y < gridConfig.Height; y++)
                {
                    int2 cellPos = new int2(x, y);
                    if (!occupancyMap.ContainsKey(cellPos))
                    {
                        hasEmptyCell = true;
                        break;
                    }
                }
                if (hasEmptyCell)
                    break;
            }

            // Check if there are valid merges
            bool hasValidMerge = false;
            foreach (var (block, entity) in SystemAPI.Query<RefRO<BlockComponent>>().WithEntityAccess())
            {
                int2 currentPos = block.ValueRO.GridPosition;

                // Check adjacent cells for potential merges
                int2[] directions = new int2[]
                {
                    new int2(1, 0),  // Right
                    new int2(-1, 0), // Left
                    new int2(0, 1),  // Up
                    new int2(0, -1)  // Down
                };

                foreach (var direction in directions)
                {
                    int2 neighborPos = currentPos + direction;
                    if (neighborPos.x >= 0 && neighborPos.x < gridConfig.Width &&
                        neighborPos.y >= 0 && neighborPos.y < gridConfig.Height &&
                        occupancyMap.TryGetValue(neighborPos, out Entity neighborEntity))
                    {
                        var neighborBlock = state.EntityManager.GetComponentData<BlockComponent>(neighborEntity);

                        // Check if blocks can merge using color combination rules
                        if (!block.ValueRO.IsNextColorIndicator &&
                            !neighborBlock.IsNextColorIndicator &&
                            ColorMergeUtility.CanMerge(block.ValueRO.Color, neighborBlock.Color))
                        {
                            hasValidMerge = true;
                            break;
                        }
                    }
                }

                if (hasValidMerge)
                    break;
            }

            occupancyMap.Dispose();

            // Update game state based on conditions
            if (!hasEmptyCell && !hasValidMerge)
            {
                gameState.ValueRW.GameOver = true;
                gameState.ValueRW.WaitingForInput = true;

                var audioEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(audioEntity, new AudioEventComponent
                {
                    EventPath = FMODEventPaths.GameOver
                });
            }
            else
            {
                gameState.ValueRW.GameOver = false;
            }
        }
    }
}
*/
