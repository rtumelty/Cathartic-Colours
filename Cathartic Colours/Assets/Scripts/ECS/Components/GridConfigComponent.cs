using Unity.Entities;

namespace ECS.Components
{
    public struct GridConfigComponent : IComponentData
    {
        public int Width;
        public int Height;
    }
}
