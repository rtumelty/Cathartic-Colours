using ECS.Components;

namespace ECS.Utilities
{
    public struct ColorMergeUtility : IMergeUtility
    {
        public bool CanMerge(BlockComponent block1, BlockComponent block2)
        {
            // Can't merge with self
            if (block1.Color == block2.Color) return false;
            
            // Can't merge white, none, or indicators
            if (block1.Color == BlockColor.White || block2.Color == BlockColor.White) return false;
            if (block1.Color == BlockColor.None || block2.Color == BlockColor.None) return false;
            if (block1.IsNextColorIndicator || block2.IsNextColorIndicator) return false;
            
            byte c1 = (byte)block1.Color;
            byte c2 = (byte)block2.Color;
            
            // Check if both are primary colors (single bit set)
            bool c1IsPrimary = IsPrimaryColor(c1);
            bool c2IsPrimary = IsPrimaryColor(c2);
            
            // Two different primary colors can always merge
            if (c1IsPrimary && c2IsPrimary && c1 != c2)
                return true;
            
            // If one is secondary, the other must be the missing primary
            if (!c1IsPrimary && c2IsPrimary)
                return IsMissingPrimary(c1, c2);
            
            if (c1IsPrimary && !c2IsPrimary)
                return IsMissingPrimary(c2, c1);
            
            // Two secondary colors cannot merge
            return false;
        }

        public MergeResult MergeBlocks(BlockComponent block1, BlockComponent block2)
        {
            byte result = (byte)((byte)block1.Color | (byte)block2.Color);
            
            // If all three primary colors are present, it becomes white (and destroys)
            if (result == 7) // Red | Green | Blue = 1 | 2 | 4 = 7
            {
                return new MergeResult(BlockColor.White, block1.Size, ScoreTier.Tier3, shouldDestroy: true);
            }
            
            return new MergeResult((BlockColor)result, block1.Size, ScoreTier.Tier1, shouldDestroy: false);
        }

        private static bool IsPrimaryColor(byte color)
        {
            return color == 1 || color == 2 || color == 4;
        }
        
        private static bool IsMissingPrimary(byte secondaryColor, byte primaryColor)
        {
            return ((secondaryColor & primaryColor) == 0) && 
                   ((secondaryColor | primaryColor) == 7);
        }
    }
}
