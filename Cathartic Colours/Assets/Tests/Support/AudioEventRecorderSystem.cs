// Records the FMOD event paths the board raises, for tests (CC-046.1, question 4 in ADR-006).
//
// BoardStepSystem creates AudioEventComponent entities and AudioSystem plays and destroys them in the same frame, so a
// test can't see them by polling. This system runs between the two and copies each event path into a list. It is
// test-only and never auto-created ([DisableAutoCreation]): the GameDriver adds it to the default world's
// SimulationSystemGroup at the start of a test. The game itself is unchanged, and FMOD still plays (or skips) the
// event exactly as it would without the recorder.
using System.Collections.Generic;
using ECS.Components;
using ECS.Systems;
using Unity.Collections;
using Unity.Entities;

namespace TestSupport
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BoardStepSystem))]
    [UpdateBefore(typeof(AudioSystem))]
    public partial class AudioEventRecorderSystem : SystemBase
    {
        static readonly List<string> recorded = new List<string>();
        EntityQuery query;

        /// <summary>Event paths recorded since the last <see cref="Take"/>, oldest first.</summary>
        public static IReadOnlyList<string> Recorded => recorded;

        /// <summary>Returns the event paths recorded since the last call and clears the list.</summary>
        public static List<string> Take()
        {
            var copy = new List<string>(recorded);
            recorded.Clear();
            return copy;
        }

        public static void Clear() => recorded.Clear();

        /// <summary>Adds the recorder to the default world's simulation group once; later calls do nothing.</summary>
        public static void EnsureInDefaultWorld()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated || world.GetExistingSystemManaged<AudioEventRecorderSystem>() != null)
                return;
            var system = world.CreateSystemManaged<AudioEventRecorderSystem>();
            var group = world.GetExistingSystemManaged<SimulationSystemGroup>();
            group.AddSystemToUpdateList(system);
            group.SortSystems();
        }

        protected override void OnCreate()
        {
            query = GetEntityQuery(ComponentType.ReadOnly<AudioEventComponent>());
        }

        protected override void OnUpdate()
        {
            if (query.IsEmpty) return;
            using var events = query.ToComponentDataArray<AudioEventComponent>(Allocator.Temp);
            foreach (var e in events)
                recorded.Add(e.EventPath.ToString());
        }
    }
}
