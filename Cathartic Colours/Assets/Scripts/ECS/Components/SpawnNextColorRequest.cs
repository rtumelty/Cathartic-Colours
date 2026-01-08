using Unity.Entities;
using Unity.Mathematics;

namespace ECS.Components
{
    public struct SpawnNextColorRequest : IComponentData
    {
        public int2 Position;
    }
}
