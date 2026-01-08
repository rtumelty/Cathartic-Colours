using ECS.Components;
using Unity.Collections;
using Unity.Entities;

namespace ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BlockMovementSystem))]
    public partial struct BlockMergeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (mergeTag, block, entity) in 
                     SystemAPI.Query<RefRO<PendingMergeTag>, RefRW<BlockComponent>>()
                         .WithAll<PendingMergeTag>() 
                         .WithEntityAccess())
            {
                if (!state.EntityManager.Exists(mergeTag.ValueRO.TargetBlock))
                {
                    ecb.SetComponentEnabled<PendingMergeTag>(entity, false);
                    continue;
                }

                var targetBlock = state.EntityManager.GetComponentData<BlockComponent>(mergeTag.ValueRO.TargetBlock);

                if (block.ValueRO.Size == BlockSize.Large)
                {
                    ecb.DestroyEntity(entity);
                    ecb.DestroyEntity(mergeTag.ValueRO.TargetBlock);
                }
                else
                {
                    var newBlock = targetBlock;
                    newBlock.Size = (BlockSize)((int)targetBlock.Size + 1);
                    ecb.SetComponent(mergeTag.ValueRO.TargetBlock, newBlock);
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}