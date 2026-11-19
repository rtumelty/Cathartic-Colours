namespace Simulation
{
    /// <summary>
    /// Advanced Color Merge mode, by size pair:
    /// Small + Small of the same primary make a Medium; two different Medium primaries make a Large secondary;
    /// a Large secondary and the Medium primary it lacks (either way round) make a Large White;
    /// two Large Whites clear each other. Everything else does not merge.
    /// </summary>
    public struct AdvancedColorMergeRules : IMergeRules
    {
        public bool CanMerge(Block mover, Block target)
        {
            if (mover.IsIndicator || target.IsIndicator)
                return false;

            if (mover.Color == BlockColor.None || target.Color == BlockColor.None)
                return false;

            if (mover.Size == BlockSize.Small && target.Size == BlockSize.Small)
                return mover.Color == target.Color && ColorBits.IsPrimary(mover.Color);

            if (mover.Size == BlockSize.Medium && target.Size == BlockSize.Medium)
                return ColorBits.IsPrimary(mover.Color) &&
                       ColorBits.IsPrimary(target.Color) &&
                       mover.Color != target.Color;

            if (mover.Size == BlockSize.Large && target.Size == BlockSize.Medium)
                return ColorBits.IsSecondary(mover.Color) &&
                       ColorBits.IsPrimary(target.Color) &&
                       ColorBits.IsMissingPrimary(mover.Color, target.Color);

            if (mover.Size == BlockSize.Medium && target.Size == BlockSize.Large)
                return ColorBits.IsSecondary(target.Color) &&
                       ColorBits.IsPrimary(mover.Color) &&
                       ColorBits.IsMissingPrimary(target.Color, mover.Color);

            if (mover.Size == BlockSize.Large && target.Size == BlockSize.Large)
                return mover.Color == BlockColor.White && target.Color == BlockColor.White;

            return false;
        }

        public MergeResult Merge(Block mover, Block target)
        {
            if (mover.Size == BlockSize.Small && target.Size == BlockSize.Small && mover.Color == target.Color)
                return new MergeResult(mover.Color, BlockSize.Medium, ScoreTier.Tier1);

            if (mover.Size == BlockSize.Medium && target.Size == BlockSize.Medium)
                return new MergeResult(ColorBits.Combine(mover.Color, target.Color), BlockSize.Large, ScoreTier.Tier2);

            if ((mover.Size == BlockSize.Large && target.Size == BlockSize.Medium) ||
                (mover.Size == BlockSize.Medium && target.Size == BlockSize.Large))
                return new MergeResult(BlockColor.White, BlockSize.Large, ScoreTier.Tier3);

            if (mover.Size == BlockSize.Large && target.Size == BlockSize.Large &&
                mover.Color == BlockColor.White && target.Color == BlockColor.White)
                return new MergeResult(BlockColor.None, BlockSize.None, ScoreTier.Tier4, destroysBoth: true);

            // Not reached when CanMerge is true; kept to match the previous rules.
            return new MergeResult(mover.Color, mover.Size, ScoreTier.Tier1);
        }
    }
}
