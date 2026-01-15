using ECS.Components;

namespace ECS.Utilities
{
    public static class ColorMergeUtility
    {
        // Check if two colors can merge
        public static bool CanMerge(BlockColor color1, BlockColor color2)
        {
            // Can't merge with self
            if (color1 == color2) return false;
            
            // Can't merge white or none
            if (color1 == BlockColor.White || color2 == BlockColor.White) return false;
            if (color1 == BlockColor.None || color2 == BlockColor.None) return false;
            
            byte c1 = (byte)color1;
            byte c2 = (byte)color2;
            
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
        
        // Get the result of merging two colors
        public static BlockColor MergeColors(BlockColor color1, BlockColor color2)
        {
            byte result = (byte)((byte)color1 | (byte)color2);
            
            // If all three primary colors are present, it becomes white
            if (result == 7) // Red | Green | Blue = 1 | 2 | 4 = 7
                return BlockColor.White;
            
            return (BlockColor)result;
        }
        
        // Check if a color is a primary color (single bit set)
        private static bool IsPrimaryColor(byte color)
        {
            // Primary colors have only one bit set: 1, 2, or 4
            return color == 1 || color == 2 || color == 4;
        }
        
        // Check if primaryColor is the missing component of secondaryColor
        private static bool IsMissingPrimary(byte secondaryColor, byte primaryColor)
        {
            // The primary color should NOT be present in the secondary color
            // and when combined, should make all three primaries (white = 7)
            return ((secondaryColor & primaryColor) == 0) && 
                   ((secondaryColor | primaryColor) == 7);
        }
    }
}
