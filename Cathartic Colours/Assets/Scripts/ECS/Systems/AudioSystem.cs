using ECS.Components;
using FMODUnity;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ECS.Systems
{
    // Plays audio events created by BoardStepSystem in the same frame. Managed (not Burst): FMOD RuntimeManager.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BoardStepSystem))]
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