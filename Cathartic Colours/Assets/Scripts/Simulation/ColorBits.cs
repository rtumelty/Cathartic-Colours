namespace Simulation
{
    /// <summary>Colour helpers. Primaries are single bits (Red 1, Green 2, Blue 4); secondaries are two bits; White is all three.</summary>
    public static class ColorBits
    {
        public static bool IsPrimary(BlockColor color)
        {
            byte c = (byte)color;
            return c == 1 || c == 2 || c == 4;
        }

        public static bool IsSecondary(BlockColor color)
        {
            byte c = (byte)color;
            return c == 3 || c == 5 || c == 6;
        }

        /// <summary>True when <paramref name="primary"/> is the primary that <paramref name="secondary"/> lacks.</summary>
        public static bool IsMissingPrimary(BlockColor secondary, BlockColor primary)
        {
            byte s = (byte)secondary;
            byte p = (byte)primary;
            return (s & p) == 0 && (s | p) == 7;
        }

        public static BlockColor Combine(BlockColor a, BlockColor b) => (BlockColor)((byte)a | (byte)b);
    }
}
