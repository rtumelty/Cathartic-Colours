using Unity.Collections;
using Unity.Mathematics;

namespace Simulation
{
    /// <summary>
    /// Follows the identity of each block through a step, so a wrapper (the ECS world) can keep one object per
    /// block instead of recreating them: a block that slides or is merged into keeps its id, a merged-away block
    /// is removed, and each spawned indicator needs a new object.
    /// </summary>
    public static class OccupantTracking
    {
        public const int None = -1;

        /// <summary>
        /// Replays <paramref name="events"/> (from <paramref name="firstEvent"/> on) over <paramref name="occupants"/>,
        /// one id per cell (index <c>x + y * width</c>, <see cref="None"/> for empty). Ids that no longer exist are
        /// appended to <paramref name="removed"/>; cells that need a new object to <paramref name="spawned"/>.
        /// Spawned cells are left as <see cref="None"/> in <paramref name="occupants"/>.
        /// </summary>
        public static void Apply(NativeArray<int> occupants, int width, NativeList<SimEvent> events, int firstEvent,
            NativeList<int> removed, NativeList<int2> spawned)
        {
            for (int i = firstEvent; i < events.Length; i++)
            {
                var e = events[i];
                int from = e.From.x + e.From.y * width;
                int at = e.Position.x + e.Position.y * width;

                switch (e.Type)
                {
                    case SimEventType.Moved:
                        occupants[at] = occupants[from];
                        occupants[from] = None;
                        break;

                    case SimEventType.Merged:
                        AddIfPresent(removed, occupants[from]);
                        occupants[from] = None;
                        if (e.DestroysBoth)
                        {
                            AddIfPresent(removed, occupants[at]);
                            occupants[at] = None;
                        }
                        break;

                    case SimEventType.IndicatorSpawned:
                        spawned.Add(e.Position);
                        break;
                }
            }
        }

        private static void AddIfPresent(NativeList<int> list, int id)
        {
            if (id != None)
                list.Add(id);
        }
    }
}
