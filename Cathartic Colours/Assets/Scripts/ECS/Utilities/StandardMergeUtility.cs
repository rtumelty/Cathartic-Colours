using ECS.Components;

namespace ECS.Utilities
{
    public struct StandardMergeUtility : IMergeUtility
    {
        public bool CanMerge(BlockComponent block1, BlockComponent block2)
        {
            return block1.Color == block2.Color && 
                   block1.Size == block2.Size &&
                   !block1.IsNextColorIndicator &&
                   !block2.IsNextColorIndicator;
        }

        public MergeResult MergeBlocks(BlockComponent block1, BlockComponent block2)
        {
            // Same color and size, upgrade to next size
            if (block1.Size == BlockSize.Large)
            {
                // Large blocks are destroyed when merged
                return new MergeResult(block1.Color, BlockSize.Large, shouldDestroy: true);
            }
            else
            {
                // Upgrade to next size
                BlockSize newSize = (BlockSize)((int)block1.Size + 1);
                return new MergeResult(block1.Color, newSize, shouldDestroy: false);
            }
        }
    }
}