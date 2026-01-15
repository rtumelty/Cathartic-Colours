using Unity.Entities;
using Unity.Mathematics;

namespace ECS.Components
{  
    public enum BlockColor : byte
    {
        None = 0,
        Red = 1,
        Green = 2,
        Blue = 4,
        Yellow = 3,
        Magenta = 5,
        Cyan = 6,
        White = 7  // Next color indicator
    }

    public enum BlockSize : byte
    {
        None = 0,
        Small = 1,
        Medium = 2,
        Large = 3
    }

    public struct BlockComponent : IComponentData
    {
        public BlockColor Color;
        public BlockSize Size;
        public int2 GridPosition;
        public bool IsNextColorIndicator; // White block that will become random color
    }
}

