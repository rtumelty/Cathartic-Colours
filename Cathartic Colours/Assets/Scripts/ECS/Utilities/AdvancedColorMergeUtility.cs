using ECS.Components;

namespace ECS.Utilities
{
    public static class AdvancedColorMergeUtility
    {
        // Check if two blocks can merge in advanced mode
        public static bool CanMerge(BlockComponent block1, BlockComponent block2)
        {
            // Can't merge with self
            if (block1.Color == block2.Color && block1.Size == block2.Size)
            {
                // Exception: small blocks of same color can merge
                if (block1.Size == BlockSize.Small && block1.Color == block2.Color)
                    return IsPrimaryColor(block1.Color);
                
                // Exception: large white blocks can merge
                if (block1.Size == BlockSize.Large && block1.Color == BlockColor.White)
                    return true;
                    
                return false;
            }
            
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

        // Get the result of merging two blocks
        public static (BlockColor color, BlockSize size, bool destroy) MergeBlocks(
            BlockComponent block1, 
            BlockComponent block2)
        {
            // Rule 1: Small + Small (same color) → Medium (same color)
            if (block1.Size == BlockSize.Small && block2.Size == BlockSize.Small &&
                block1.Color == block2.Color)
            {
                return (block1.Color, BlockSize.Medium, false);
            }

            // Rule 2: Medium + Medium (different primaries) → Large secondary
            if (block1.Size == BlockSize.Medium && block2.Size == BlockSize.Medium)
            {
                BlockColor mergedColor = (BlockColor)((byte)block1.Color | (byte)block2.Color);
                return (mergedColor, BlockSize.Large, false);
            }

            // Rule 3: Large secondary + Medium primary → Large white
            if ((block1.Size == BlockSize.Large && block2.Size == BlockSize.Medium) ||
                (block1.Size == BlockSize.Medium && block2.Size == BlockSize.Large))
            {
                return (BlockColor.White, BlockSize.Large, false);
            }

            // Rule 4: Large white + Large white → Destroyed
            if (block1.Size == BlockSize.Large && block2.Size == BlockSize.Large &&
                block1.Color == BlockColor.White && block2.Color == BlockColor.White)
            {
                return (BlockColor.None, BlockSize.None, true);
            }

            // Should never reach here if CanMerge was called first
            return (BlockColor.None, BlockSize.None, false);
        }

        public static bool IsPrimaryColor(BlockColor color)
        {
            byte c = (byte)color;
            return c == 1 || c == 2 || c == 4; // Red, Green, Blue
        }

        public static bool IsSecondaryColor(BlockColor color)
        {
            byte c = (byte)color;
            return c == 3 || c == 5 || c == 6; // Yellow, Magenta, Cyan
        }

        private static bool IsMissingPrimary(BlockColor secondaryColor, BlockColor primaryColor)
        {
            byte secondary = (byte)secondaryColor;
            byte primary = (byte)primaryColor;
            
            // The primary color should NOT be present in the secondary color
            // and when combined, should make white (7)
            return ((secondary & primary) == 0) && ((secondary | primary) == 7);
        }
    }
}
