namespace Simulation
{
    /// <summary>
    /// Standard mode: two blocks of the same colour and size merge into the next size up;
    /// two Large blocks clear each other. Indicators never merge.
    /// </summary>
    public struct StandardMergeRules : IMergeRules
    {
        public bool CanMerge(Block mover, Block target)
        {
            return mover.Color == target.Color &&
                   mover.Size == target.Size &&
                   !mover.IsIndicator &&
                   !target.IsIndicator;
        }

        public MergeResult Merge(Block mover, Block target)
        {
            switch (mover.Size)
            {
                case BlockSize.Large:
                    return new MergeResult(mover.Color, BlockSize.Large, ScoreTier.Tier3, destroysBoth: true);
                case BlockSize.Medium:
                    return new MergeResult(mover.Color, BlockSize.Large, ScoreTier.Tier2);
                default:
                    return new MergeResult(mover.Color, BlockSize.Medium, ScoreTier.Tier1);
            }
        }
    }
}
