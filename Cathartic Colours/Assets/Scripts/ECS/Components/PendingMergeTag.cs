using Unity.Entities;

namespace ECS.Components
{
    public struct PendingMergeTag : IComponentData, IEnableableComponent
    {
        public Entity TargetBlock;
    }
}
