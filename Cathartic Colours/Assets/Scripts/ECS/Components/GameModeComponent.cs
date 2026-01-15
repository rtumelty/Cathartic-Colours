using Unity.Entities;

namespace ECS.Components
{
    public struct GameModeComponent : IComponentData
    {
        public GameMode Mode;
    }
    
    public enum GameMode : byte
    {
        Standard = 0,
        ColorMerge = 1,
        AdvancedColorMerge = 2
    }
}