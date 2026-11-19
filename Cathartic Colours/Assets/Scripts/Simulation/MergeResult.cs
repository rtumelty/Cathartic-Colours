namespace Simulation
{
    /// <summary>What a merge produces. When <see cref="DestroysBoth"/> is true both blocks are removed and
    /// <see cref="Color"/> is only the colour of the merge effect.</summary>
    public struct MergeResult
    {
        public BlockColor Color;
        public BlockSize Size;
        public ScoreTier Tier;
        public bool DestroysBoth;

        public MergeResult(BlockColor color, BlockSize size, ScoreTier tier, bool destroysBoth = false)
        {
            Color = color;
            Size = size;
            Tier = tier;
            DestroysBoth = destroysBoth;
        }
    }
}
