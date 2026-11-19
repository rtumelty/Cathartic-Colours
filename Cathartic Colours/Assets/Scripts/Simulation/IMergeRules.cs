namespace Simulation
{
    /// <summary>
    /// A game mode's merge rules. Implemented by structs and used as a generic constraint
    /// (<c>where TRules : struct, IMergeRules</c>), so calls are not boxed and stay Burst-compatible.
    /// </summary>
    public interface IMergeRules
    {
        /// <summary>Whether <paramref name="mover"/> can merge into <paramref name="target"/>, the block in the cell it moves into.</summary>
        bool CanMerge(Block mover, Block target);

        /// <summary>The result of merging; only meaningful when <see cref="CanMerge"/> is true.</summary>
        MergeResult Merge(Block mover, Block target);
    }
}
