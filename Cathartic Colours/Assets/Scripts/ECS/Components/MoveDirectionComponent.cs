using Unity.Entities;
using Unity.Mathematics;


namespace ECS.Components
{
    public struct MoveDirectionComponent : IComponentData
    {
        public int2 Direction;
    }
}
