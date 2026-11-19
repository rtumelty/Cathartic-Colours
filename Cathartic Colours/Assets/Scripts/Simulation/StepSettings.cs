namespace Simulation
{
    /// <summary>Points awarded per merge tier (copied from <c>GameConfiguration</c>).</summary>
    public struct TierPoints
    {
        public int Tier1;
        public int Tier2;
        public int Tier3;
        public int Tier4;

        public static TierPoints Default => new TierPoints { Tier1 = 10, Tier2 = 50, Tier3 = 100, Tier4 = 200 };

        public int For(ScoreTier tier)
        {
            switch (tier)
            {
                case ScoreTier.Tier1: return Tier1;
                case ScoreTier.Tier2: return Tier2;
                case ScoreTier.Tier3: return Tier3;
                case ScoreTier.Tier4: return Tier4;
                default: return 0;
            }
        }
    }

    /// <summary>Per-game settings a step needs besides the board and the rules.</summary>
    public struct StepSettings
    {
        public TierPoints Points;

        /// <summary>Whether a new indicator is spawned after each move (<c>GameConfiguration.spawnNextColourSystem</c>).</summary>
        public bool SpawnEnabled;

        public static StepSettings Default => new StepSettings { Points = TierPoints.Default, SpawnEnabled = true };
    }
}
