using ECS.Components;
using ECS.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BlockMovementSystem))]
    [UpdateAfter(typeof(ColorMergeBlockMovementSystem))]
    [UpdateAfter(typeof(AdvancedColorMergeBlockMovementSystem))]
    [UpdateAfter(typeof(SpawnColorSystem))]
    public partial struct GameEndConditionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var gameState = SystemAPI.GetSingletonRW<GameStateComponent>();
            
            if (gameState.ValueRW.GameOver) return;
            
            // Determine which merge mode is active
            bool isColorMergeMode = SystemAPI.HasSingleton<ColorMergeSystemTag>();
            bool isStandardMode = SystemAPI.HasSingleton<StandardMergeSystemTag>();
            bool isAdvancedColorMergeMode = SystemAPI.HasSingleton<AdvancedColorMergeSystemTag>();
            
            // If no mode is active, skip (shouldn't happen, but safety check)
            if (!isColorMergeMode && !isStandardMode && !isAdvancedColorMergeMode)
            {
                return;
            }
            
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

            // Check if there are valid merges based on active game mode
            bool hasValidMerge = CheckForValidMerges(
                ref state, 
                ref occupancyMap, 
                gridConfig, 
                isColorMergeMode,
                isAdvancedColorMergeMode
            );

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

        private bool CheckForValidMerges(
            ref SystemState state,
            ref NativeHashMap<int2, Entity> occupancyMap,
            GridConfigComponent gridConfig,
            bool isColorMergeMode,
            bool isAdvancedColorMergeMode)
        {
            // Reusable directions array
            var directions = new NativeArray<int2>(4, Allocator.Temp);
            directions[0] = new int2(1, 0);   // Right
            directions[1] = new int2(-1, 0);  // Left
            directions[2] = new int2(0, 1);   // Up
            directions[3] = new int2(0, -1);  // Down

            bool hasValidMerge = false;

            foreach (var (block, entity) in SystemAPI.Query<RefRO<BlockComponent>>().WithEntityAccess())
            {
                int2 currentPos = block.ValueRO.GridPosition;

                // Check adjacent cells for potential merges
                for (int i = 0; i < directions.Length; i++)
                {
                    int2 neighborPos = currentPos + directions[i];
                    
                    // Check bounds
                    if (neighborPos.x < 0 || neighborPos.x >= gridConfig.Width ||
                        neighborPos.y < 0 || neighborPos.y >= gridConfig.Height)
                        continue;

                    if (!occupancyMap.TryGetValue(neighborPos, out Entity neighborEntity))
                        continue;

                    var neighborBlock = state.EntityManager.GetComponentData<BlockComponent>(neighborEntity);

                    // Skip indicator blocks
                    if (block.ValueRO.IsNextColorIndicator || neighborBlock.IsNextColorIndicator)
                        continue;

                    // Check if blocks can merge based on game mode
                    bool canMerge;
                    if (isAdvancedColorMergeMode)
                    {
                        canMerge = AdvancedColorMergeUtility.CanMerge(block.ValueRO, neighborBlock);
                    }
                    else if (isColorMergeMode)
                    {
                        canMerge = ColorMergeUtility.CanMerge(block.ValueRO.Color, neighborBlock.Color);
                    }
                    else // Standard mode
                    {
                        canMerge = CanMergeStandard(block.ValueRO, neighborBlock);
                    }

                    if (canMerge)
                    {
                        hasValidMerge = true;
                        break;
                    }
                }

                if (hasValidMerge)
                    break;
            }

            directions.Dispose();
            return hasValidMerge;
        }

        // Standard merge logic: same color and same size
        private bool CanMergeStandard(BlockComponent block1, BlockComponent block2)
        {
            return block1.Color == block2.Color && 
                   block1.Size == block2.Size;
        }
    }
}
