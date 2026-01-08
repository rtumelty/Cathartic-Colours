using System;
using ECS.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct BlockMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<MoveDirectionComponent>(out var moveDirection))
                return;

            var gridConfig = SystemAPI.GetSingleton<GridConfigComponent>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Build occupancy map
            var occupancyMap = new NativeHashMap<int2, Entity>(100, Allocator.Temp);
            foreach (var (block, entity) in SystemAPI.Query<RefRO<BlockComponent>>().WithEntityAccess())
            {
                occupancyMap[block.ValueRO.GridPosition] = entity;
            }

            // Process all moving blocks 
            foreach (var (block, entity) in 
                SystemAPI.Query<RefRW<BlockComponent>>()
                .WithAll<MovingBlockTag>() // Only queries ENABLED MovingBlockTag
                .WithEntityAccess())
            {
                int2 currentPos = block.ValueRO.GridPosition;
                int2 targetPos = currentPos + moveDirection.Direction;

                // Check bounds
                if (targetPos.x < 0 || targetPos.x >= gridConfig.Width ||
                    targetPos.y < 0 || targetPos.y >= gridConfig.Height)
                {
                    ecb.SetComponentEnabled<MovingBlockTag>(entity, false);
                    continue;
                }

                // Check if target cell is occupied
                if (occupancyMap.TryGetValue(targetPos, out Entity targetEntity))
                {
                    var targetBlock = state.EntityManager.GetComponentData<BlockComponent>(targetEntity);
                    
                    // Check if can merge
                    if (block.ValueRO.Color == targetBlock.Color && 
                        block.ValueRO.Size == targetBlock.Size &&
                        !block.ValueRO.IsNextColorIndicator &&
                        !targetBlock.IsNextColorIndicator)
                    {
                        // Enable merge tag and set data
                        var mergeTag = new PendingMergeTag { TargetBlock = targetEntity };
                        ecb.SetComponent(entity, mergeTag);
                        ecb.SetComponentEnabled<PendingMergeTag>(entity, true);
                    }
                    
                    ecb.SetComponentEnabled<MovingBlockTag>(entity, false);
                }
                else
                {
                    // Move to empty cell
                    block.ValueRW.GridPosition = targetPos;
                    occupancyMap[targetPos] = entity;
                    occupancyMap.Remove(currentPos);
                    ecb.SetComponentEnabled<MovingBlockTag>(entity, false);
                }
            }

            ecb.Playback(state.EntityManager);
            occupancyMap.Dispose();
        }
    }
}