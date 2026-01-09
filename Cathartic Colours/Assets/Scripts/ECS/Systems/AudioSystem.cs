using ECS.Components;
using FMODUnity;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct AudioSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (audioEvent, entity) in SystemAPI.Query<RefRO<AudioEventComponent>>().WithEntityAccess())
            {
                // Play the FMOD event
                RuntimeManager.PlayOneShot(audioEvent.ValueRO.EventPath.ToString(), Vector3.zero);
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}