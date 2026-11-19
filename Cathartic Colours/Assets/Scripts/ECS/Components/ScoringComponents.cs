using Simulation;
using Unity.Entities;
using Unity.Mathematics;

namespace ECS.Components
{
    public struct MergeEventComponent : IComponentData
    {
        public int2 Position;       // Grid position where merge occurred
        public ScoreTier Tier;      // Scoring tier for this merge
        public BlockColor Color;    // Colour of resulting block
    }

    public struct ScoreComponent : IComponentData
    {
        public int TotalScore;
        public int CurrentFrameScore;
    }
}