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
            if (block1.Size == BlockSize.Large)
            {
                return new MergeResult(block1.Color, BlockSize.Large, ScoreTier.Tier3, shouldDestroy: true);
            }
            else if (block1.Size == BlockSize.Medium)
            {
                return new MergeResult(block1.Color, BlockSize.Large, ScoreTier.Tier2);
            }
            else
            {
                return new MergeResult(block1.Color, BlockSize.Medium, ScoreTier.Tier1);
            }
        }
    }
}