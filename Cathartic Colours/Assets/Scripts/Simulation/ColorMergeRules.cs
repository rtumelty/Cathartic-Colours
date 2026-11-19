namespace Simulation
{
    /// <summary>
    /// Color Merge mode: two different primaries make a secondary; a secondary and the primary it lacks
    /// clear each other. Size is ignored and the result keeps the mover's size.
    /// </summary>
    public struct ColorMergeRules : IMergeRules
    {
        public bool CanMerge(Block mover, Block target)
        {
            if (mover.Color == target.Color) return false;
            if (mover.Color == BlockColor.White || target.Color == BlockColor.White) return false;
            if (mover.Color == BlockColor.None || target.Color == BlockColor.None) return false;
            if (mover.IsIndicator || target.IsIndicator) return false;

            bool moverIsPrimary = ColorBits.IsPrimary(mover.Color);
            bool targetIsPrimary = ColorBits.IsPrimary(target.Color);

            if (moverIsPrimary && targetIsPrimary)
                return true;

            if (!moverIsPrimary && targetIsPrimary)
                return ColorBits.IsMissingPrimary(mover.Color, target.Color);

            if (moverIsPrimary && !targetIsPrimary)
                return ColorBits.IsMissingPrimary(target.Color, mover.Color);

            // Two secondaries never merge.
            return false;
        }

        public MergeResult Merge(Block mover, Block target)
        {
            BlockColor combined = ColorBits.Combine(mover.Color, target.Color);

            if (combined == BlockColor.White)
                return new MergeResult(BlockColor.White, mover.Size, ScoreTier.Tier3, destroysBoth: true);

            return new MergeResult(combined, mover.Size, ScoreTier.Tier1);
        }
    }
}
