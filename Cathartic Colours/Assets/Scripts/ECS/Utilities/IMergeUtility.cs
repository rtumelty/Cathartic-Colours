using ECS.Components;

namespace ECS.Utilities
{
    public struct MergeResult
    {
        public BlockColor Color;
        public BlockSize Size;
        public bool ShouldDestroy;
        
        public MergeResult(BlockColor color, BlockSize size, bool shouldDestroy = false)
        {
            Color = color;
            Size = size;
            ShouldDestroy = shouldDestroy;
        }
    }

    public interface IMergeUtility
    {
        /// <summary>
        /// Check if two blocks can merge
        /// </summary>
        bool CanMerge(BlockComponent block1, BlockComponent block2);
        
        /// <summary>
        /// Get the result of merging two blocks
        /// </summary>
        MergeResult MergeBlocks(BlockComponent block1, BlockComponent block2);
    }
}