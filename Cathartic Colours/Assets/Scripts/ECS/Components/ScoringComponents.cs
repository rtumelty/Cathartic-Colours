using Unity.Entities;
using Unity.Mathematics;

namespace ECS.Components
{
    public enum ScoreTier
    {
        None = 0,
        Tier1 = 1,  
        Tier2 = 2,   
        Tier3 = 3, 
        Tier4 = 4  
    }

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