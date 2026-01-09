using Unity.Collections;
using Unity.Entities;

namespace ECS.Components
{
    public struct AudioEventComponent : IComponentData
    {
        public FixedString64Bytes EventPath; // FMOD event path
    }
}