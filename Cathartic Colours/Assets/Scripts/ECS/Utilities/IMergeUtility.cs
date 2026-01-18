using ECS.Components;

namespace ECS.Utilities
{
    public struct MergeResult
    {
        public BlockColor Color;
        public BlockSize Size;
        public bool ShouldDestroy;
        public ScoreTier ScoreTier;
        
        public MergeResult(BlockColor color, BlockSize size, ScoreTier scoreTier = ScoreTier.Tier1, bool shouldDestroy = false)
        {
            Color = color;
            Size = size;
            ShouldDestroy = shouldDestroy;
            ScoreTier = scoreTier;
        }
    }

    public interface IMergeUtility
    {
        bool CanMerge(BlockComponent block1, BlockComponent block2);
        MergeResult MergeBlocks(BlockComponent block1, BlockComponent block2);
    }
}