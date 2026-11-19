using Simulation;
using Unity.Entities;

namespace ECS.Components
{
    public struct GameModeComponent : IComponentData
    {
        public GameMode Mode;
    }
}