using Unity.Mathematics;

namespace Simulation
{
    public enum SimEventType : byte
    {
        Moved,
        Merged,
        IndicatorResolved,
        IndicatorSpawned,
        GameOver
    }

    /// <summary>Something that happened during a step, for presentation (sound, particles, animation).</summary>
    public struct SimEvent
    {
        public SimEventType Type;

        /// <summary>Moved, Merged: the mover's cell before the move.</summary>
        public int2 From;

        /// <summary>Moved: the new cell. Merged: the target cell. IndicatorResolved, IndicatorSpawned: the cell.</summary>
        public int2 Position;

        /// <summary>Merged: result colour. IndicatorResolved: the colour rolled. IndicatorSpawned: White.</summary>
        public BlockColor Color;

        /// <summary>Merged: result size.</summary>
        public BlockSize Size;

        /// <summary>Merged: the mover's size before the merge.</summary>
        public BlockSize MoverSize;

        public ScoreTier Tier;

        /// <summary>Merged: points for this merge.</summary>
        public int Points;

        /// <summary>Merged: both blocks were removed.</summary>
        public bool DestroysBoth;
    }

    public struct StepResult
    {
        /// <summary>False when the move was rejected; the board, random state and events are then unchanged.</summary>
        public bool Accepted;

        /// <summary>Sum of merge points in this step.</summary>
        public int ScoreDelta;

        /// <summary>The board is in game over after this step.</summary>
        public bool GameOver;

        /// <summary>Spawning was enabled but the board had no empty cell, so no indicator was placed.</summary>
        public bool SpawnFailed;
    }
}
