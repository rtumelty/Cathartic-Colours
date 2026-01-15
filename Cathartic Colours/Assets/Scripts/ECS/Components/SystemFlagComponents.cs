using Unity.Entities;

namespace ECS.Components
{
    public struct StandardMergeSystemTag : IComponentData { }
    
    public struct ColorMergeSystemTag : IComponentData { }
    
    public struct AdvancedColorMergeSystemTag : IComponentData { }
    
    public struct SpawnColorSystemTag : IComponentData { }
}