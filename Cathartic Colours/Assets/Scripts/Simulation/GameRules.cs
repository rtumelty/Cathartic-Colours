namespace Simulation
{
    /// <summary>Which merge rule a game uses. Each value has an <see cref="IMergeRules"/> struct.</summary>
    public enum MergeRuleKind : byte
    {
        Standard = 0,
        ColorMerge = 1,
        AdvancedColorMerge = 2
    }

    /// <summary>
    /// A game mode as data: one rule from each family, plus the rules' settings (ADR-005). Unmanaged and
    /// Burst-compatible. Never saved: <see cref="For"/> rebuilds it from the saved mode and settings.
    /// </summary>
    public struct GameRules
    {
        public MergeRuleKind Merge;
        public SpawnRuleKind Spawn;
        public ScoringRuleKind Scoring;
        public EndConditionKind End;
        public GridMutationKind Grid;
        public TimerRuleKind Timer;

        /// <summary>Points per merge tier, used by <see cref="TierPointsScoring"/>.</summary>
        public TierPoints Points;

        /// <summary>
        /// The rules of one of the original modes. All three use the uniform primary spawn (no spawn when the toggle
        /// is off), tier-point scoring, the no-moves-left end, and no grid change or timer. Only the merge rule
        /// differs. Unknown modes use Standard.
        /// </summary>
        public static GameRules For(GameMode mode, in StepSettings settings)
        {
            return new GameRules
            {
                Merge = MergeRuleFor(mode),
                Spawn = settings.SpawnEnabled ? SpawnRuleKind.UniformPrimary : SpawnRuleKind.None,
                Scoring = ScoringRuleKind.TierPoints,
                End = EndConditionKind.NoMovesLeft,
                Grid = GridMutationKind.None,
                Timer = TimerRuleKind.None,
                Points = settings.Points
            };
        }

        private static MergeRuleKind MergeRuleFor(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.ColorMerge: return MergeRuleKind.ColorMerge;
                case GameMode.AdvancedColorMerge: return MergeRuleKind.AdvancedColorMerge;
                default: return MergeRuleKind.Standard;
            }
        }

        /// <summary>Whether <paramref name="mover"/> can merge into <paramref name="target"/> under this game's merge rule.</summary>
        public bool CanMerge(Block mover, Block target)
        {
            switch (Merge)
            {
                case MergeRuleKind.AdvancedColorMerge: return new AdvancedColorMergeRules().CanMerge(mover, target);
                case MergeRuleKind.ColorMerge: return new ColorMergeRules().CanMerge(mover, target);
                default: return new StandardMergeRules().CanMerge(mover, target);
            }
        }

        /// <summary>The result of a merge under this game's merge rule; only meaningful when <see cref="CanMerge"/> is true.</summary>
        public MergeResult MergeBlocks(Block mover, Block target)
        {
            switch (Merge)
            {
                case MergeRuleKind.AdvancedColorMerge: return new AdvancedColorMergeRules().Merge(mover, target);
                case MergeRuleKind.ColorMerge: return new ColorMergeRules().Merge(mover, target);
                default: return new StandardMergeRules().Merge(mover, target);
            }
        }
    }
}
