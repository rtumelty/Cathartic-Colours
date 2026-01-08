using Unity.Entities;
using Unity.Mathematics;


namespace ECS.Components
{
    public struct GridCellComponent : IComponentData
    {
        public int2 Position;
        public bool IsOccupied;
    }
}
