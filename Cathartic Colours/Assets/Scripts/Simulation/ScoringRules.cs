namespace Simulation
{
    /// <summary>Which scoring rule a game uses.</summary>
    public enum ScoringRuleKind : byte
    {
        /// <summary>Each merge scores the points for its tier.</summary>
        TierPoints = 0
    }

    /// <summary>A scoring rule: the points one merge is worth, asked for every merge during the slide (ADR-005).</summary>
    public interface IScoringRule
    {
        int PointsFor(in MergeResult merge);
    }

    /// <summary>Each merge scores <see cref="TierPoints.For"/> its tier (default 10/50/100/200, None 0).</summary>
    public struct TierPointsScoring : IScoringRule
    {
        public TierPoints Points;

        public TierPointsScoring(TierPoints points) => Points = points;

        public int PointsFor(in MergeResult merge) => Points.For(merge.Tier);
    }

    /// <summary>Calls the scoring rule a <see cref="GameRules"/> names. Unknown kinds use <see cref="TierPointsScoring"/>.</summary>
    public static class ScoringRules
    {
        public static int PointsFor(in GameRules rules, in MergeResult merge)
        {
            switch (rules.Scoring)
            {
                default: return new TierPointsScoring(rules.Points).PointsFor(merge);
            }
        }
    }
}
