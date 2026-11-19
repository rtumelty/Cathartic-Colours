using Simulation;
using Unity.Entities;
using Unity.Mathematics;

namespace ECS.Components
{
    public struct BlockComponent : IComponentData
    {
        public BlockColor Color;
        public BlockSize Size;
        public int2 GridPosition;
        public bool IsNextColorIndicator; // White block that will become random color

        public Simulation.Block ToSimulationBlock() => new Simulation.Block
        {
            Color = Color,
            Size = Size,
            IsIndicator = IsNextColorIndicator
        };
    }
}

