using System.Collections.Generic;
using ECS.Components;
using ECS.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BlockMovementSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameStateComponent>();
            state.RequireForUpdate<GameModeComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<MoveDirectionComponent>(out var moveDirection))
                return;

            // Get the game mode and appropriate merge utility
            var gameMode = SystemAPI.GetSingleton<GameModeComponent>();
            IMergeUtility mergeUtility = MergeUtilityFactory.GetMergeUtility(gameMode.Mode);

            var gridConfig = SystemAPI.GetSingleton<GridConfigComponent>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            CreateAudioEventEntity(ecb, FMODEventPaths.BlockMove);

            // Build occupancy map
            var occupancyMap = new NativeHashMap<int2, Entity>(100, Allocator.Temp);
            foreach (var (block, entity) in SystemAPI.Query<RefRO<BlockComponent>>().WithEntityAccess())
            {
                occupancyMap[block.ValueRO.GridPosition] = entity;
            }

            // Track blocks that have been merged into (can't merge again)
            var mergedIntoBlocks = new NativeHashSet<Entity>(100, Allocator.Temp);
            
            // Track blocks that are being destroyed this pass
            var destroyedBlocks = new NativeHashSet<Entity>(100, Allocator.Temp);

            // Collect blocks into a NativeList
            var blocks = new NativeList<(BlockComponent, Entity)>(Allocator.Temp);
            foreach (var (block, entity) in SystemAPI.Query<RefRO<BlockComponent>>()
                .WithAll<MovingBlockTag>()
                .WithEntityAccess())
            {
                blocks.Add((block.ValueRO, entity));
            }

            // Sort blocks based on move direction
            blocks.Sort(new BlockComparer(moveDirection.Direction));

            // Process all moving blocks
            for (int i = 0; i < blocks.Length; i++)
            {
                var (blockData, entity) = blocks[i];
                
                // Skip if this block was destroyed in a merge
                if (destroyedBlocks.Contains(entity))
                {
                    continue;
                }

                int2 currentPos = blockData.GridPosition;
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
                    // Skip if target was destroyed
                    if (destroyedBlocks.Contains(targetEntity))
                    {
                        // Target was destroyed, move into the space
                        occupancyMap.Remove(currentPos);
                        occupancyMap[targetPos] = entity;
                        
                        var updatedBlock = blockData;
                        updatedBlock.GridPosition = targetPos;
                        ecb.SetComponent(entity, updatedBlock);
                        ecb.SetComponentEnabled<MovingBlockTag>(entity, false);
                        continue;
                    }

                    var targetBlock = state.EntityManager.GetComponentData<BlockComponent>(targetEntity);

                    // Check if can merge using the appropriate utility
                    if (!mergedIntoBlocks.Contains(targetEntity) &&
                        mergeUtility.CanMerge(blockData, targetBlock))
                    {
                        // Perform merge using utility
                        MergeResult result = mergeUtility.MergeBlocks(blockData, targetBlock);
                        
                        if (result.ShouldDestroy)
                        {
                            // Both blocks destroyed
                            ecb.DestroyEntity(entity);
                            ecb.DestroyEntity(targetEntity);
                            
                            destroyedBlocks.Add(entity);
                            destroyedBlocks.Add(targetEntity);
                            
                            // Remove from occupancy map
                            occupancyMap.Remove(currentPos);
                            occupancyMap.Remove(targetPos);
                            
                            CreateAudioEventEntity(ecb, FMODEventPaths.BlockMergeLarge);
                            CreateMergeEventEntity(ecb, targetPos, result);
                        }
                        else
                        {
                            // Update target block with merge result
                            var newBlock = targetBlock;
                            newBlock.Color = result.Color;
                            newBlock.Size = result.Size;
                            
                            // Audio based on size change
                            FixedString64Bytes audioPath = GetMergeAudioPath(blockData.Size, result.Size);
                            CreateAudioEventEntity(ecb, audioPath);
                            CreateMergeEventEntity(ecb, targetPos, result);
                            
                            ecb.SetComponent(targetEntity, newBlock);
                            
                            // Destroy moving block
                            ecb.DestroyEntity(entity);
                            destroyedBlocks.Add(entity);
                            
                            // Remove moving block from occupancy
                            occupancyMap.Remove(currentPos);
                            
                            // Mark target as merged into
                            mergedIntoBlocks.Add(targetEntity);
                        }
                    }
                    else
                    {
                        // Can't merge, can't move
                        ecb.SetComponentEnabled<MovingBlockTag>(entity, false);
                    }
                }
                else
                {
                    // Move to empty cell
                    occupancyMap.Remove(currentPos);
                    occupancyMap[targetPos] = entity;
                    
                    var updatedBlock = blockData;
                    updatedBlock.GridPosition = targetPos;
                    ecb.SetComponent(entity, updatedBlock);
                    ecb.SetComponentEnabled<MovingBlockTag>(entity, false);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            occupancyMap.Dispose();
            mergedIntoBlocks.Dispose();
            destroyedBlocks.Dispose();
            blocks.Dispose();
        }

        private FixedString64Bytes GetMergeAudioPath(BlockSize originalSize, BlockSize resultSize)
        {
            // If size increased or stayed same but merged
            if (resultSize == BlockSize.Small || originalSize == BlockSize.Small)
                return FMODEventPaths.BlockMergeSmall;
            else if (resultSize == BlockSize.Medium || originalSize == BlockSize.Medium)
                return FMODEventPaths.BlockMergeMedium;
            else
                return FMODEventPaths.BlockMergeLarge;
        }
        
        private void CreateAudioEventEntity(EntityCommandBuffer ecb, FixedString64Bytes eventPath)
        {
            var audioEntity = ecb.CreateEntity();
            ecb.AddComponent(audioEntity, new AudioEventComponent
            {
                EventPath = eventPath
            });
        }
        
        private void CreateMergeEventEntity(EntityCommandBuffer ecb, int2 position, MergeResult result)
        {
            var mergeEventEntity = ecb.CreateEntity();
            ecb.AddComponent(mergeEventEntity, new MergeEventComponent
            {
                Position = position,
                Tier = result.ScoreTier,
                Color = result.Color
            });
        }

        // Custom comparer for sorting blocks based on move direction
        private struct BlockComparer : IComparer<(BlockComponent, Entity)>
        {
            private int2 moveDirection;

            public BlockComparer(int2 moveDirection)
            {
                this.moveDirection = moveDirection;
            }

            public int Compare((BlockComponent, Entity) a, (BlockComponent, Entity) b)
            {
                int2 posA = a.Item1.GridPosition;
                int2 posB = b.Item1.GridPosition;
                return math.dot(posB - posA, moveDirection);
            }
        }
    }
}
