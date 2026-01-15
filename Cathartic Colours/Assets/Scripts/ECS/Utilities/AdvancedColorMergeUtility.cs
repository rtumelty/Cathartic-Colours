using ECS.Components;

namespace ECS.Utilities
{
    public struct AdvancedColorMergeUtility : IMergeUtility
    {
        public bool CanMerge(BlockComponent block1, BlockComponent block2)
        {
            // Can't merge indicators
            if (block1.IsNextColorIndicator || block2.IsNextColorIndicator)
                return false;

            // Can't merge None
            if (block1.Color == BlockColor.None || block2.Color == BlockColor.None)
                return false;

            // Rule 1: Small + Small (same primary color) → Medium
            if (block1.Size == BlockSize.Small && block2.Size == BlockSize.Small)
            {
                return block1.Color == block2.Color && IsPrimaryColor(block1.Color);
            }

            // Rule 2: Medium + Medium (different primary colors) → Large secondary
            if (block1.Size == BlockSize.Medium && block2.Size == BlockSize.Medium)
            {
                return IsPrimaryColor(block1.Color) && 
                       IsPrimaryColor(block2.Color) && 
                       block1.Color != block2.Color;
            }

            // Rule 3: Large secondary + Medium (missing primary) → Large white
            if (block1.Size == BlockSize.Large && block2.Size == BlockSize.Medium)
            {
                return IsSecondaryColor(block1.Color) && 
                       IsPrimaryColor(block2.Color) &&
                       IsMissingPrimary(block1.Color, block2.Color);
            }
            
            if (block1.Size == BlockSize.Medium && block2.Size == BlockSize.Large)
            {
                return IsSecondaryColor(block2.Color) && 
                       IsPrimaryColor(block1.Color) &&
                       IsMissingPrimary(block2.Color, block1.Color);
            }

            // Rule 4: Large white + Large white → Destroyed
            if (block1.Size == BlockSize.Large && block2.Size == BlockSize.Large)
            {
                return block1.Color == BlockColor.White && block2.Color == BlockColor.White;
            }

            return false;
        }

        public MergeResult MergeBlocks(BlockComponent block1, BlockComponent block2)
        {
            // Rule 1: Small + Small (same color) → Medium (same color)
            if (block1.Size == BlockSize.Small && block2.Size == BlockSize.Small &&
                block1.Color == block2.Color)
            {
                return new MergeResult(block1.Color, BlockSize.Medium);
            }

            // Rule 2: Medium + Medium (different primaries) → Large secondary
            if (block1.Size == BlockSize.Medium && block2.Size == BlockSize.Medium)
            {
                BlockColor mergedColor = (BlockColor)((byte)block1.Color | (byte)block2.Color);
                return new MergeResult(mergedColor, BlockSize.Large);
            }

            // Rule 3: Large secondary + Medium primary → Large white
            if ((block1.Size == BlockSize.Large && block2.Size == BlockSize.Medium) ||
                (block1.Size == BlockSize.Medium && block2.Size == BlockSize.Large))
            {
                return new MergeResult(BlockColor.White, BlockSize.Large);
            }

            // Rule 4: Large white + Large white → Destroyed
            if (block1.Size == BlockSize.Large && block2.Size == BlockSize.Large &&
                block1.Color == BlockColor.White && block2.Color == BlockColor.White)
            {
                return new MergeResult(BlockColor.None, BlockSize.None, shouldDestroy: true);
            }

            // Fallback (should never reach here)
            return new MergeResult(block1.Color, block1.Size);
        }

        private static bool IsPrimaryColor(BlockColor color)
        {
            byte c = (byte)color;
            return c == 1 || c == 2 || c == 4;
        }

        private static bool IsSecondaryColor(BlockColor color)
        {
            byte c = (byte)color;
            return c == 3 || c == 5 || c == 6;
        }

        private static bool IsMissingPrimary(BlockColor secondaryColor, BlockColor primaryColor)
        {
            byte secondary = (byte)secondaryColor;
            byte primary = (byte)primaryColor;
            
            return ((secondary & primary) == 0) && ((secondary | primary) == 7);
        }
    }
}
