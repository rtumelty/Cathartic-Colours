using ECS.Components;
using ECS.Utilities;
using Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ECS.Systems
{
    /// <summary>
    /// Runs one player move through the pure simulation (<see cref="BoardSimulation"/>, ADR-001).
    /// Copies the block entities into a <see cref="BoardState"/>, calls the step, writes the board back while
    /// keeping each surviving block's entity, updates game state and score, and turns the step's events into
    /// audio and merge event entities. A rejected move (game over, or not waiting for input) changes nothing
    /// and creates no events.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct BoardStepSystem : ISystem
    {
        private EntityQuery blockQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameStateComponent>();
            state.RequireForUpdate<GameModeComponent>();
            state.RequireForUpdate<GridConfigComponent>();
            state.RequireForUpdate<ScoringConfigComponent>();
            state.RequireForUpdate<ScoreComponent>();

            blockQuery = SystemAPI.QueryBuilder().WithAll<BlockComponent>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // CurrentFrameScore holds only this frame's points: zero unless a move scores below.
            var score = SystemAPI.GetSingletonRW<ScoreComponent>();
            score.ValueRW.CurrentFrameScore = 0;

            if (!SystemAPI.TryGetSingleton<MoveDirectionComponent>(out var move))
                return;

            var gameState = SystemAPI.GetSingletonRW<GameStateComponent>();
            if (!gameState.ValueRO.WaitingForInput || gameState.ValueRO.GameOver)
                return;

            var grid = SystemAPI.GetSingleton<GridConfigComponent>();
            var mode = SystemAPI.GetSingleton<GameModeComponent>().Mode;
            var settings = SystemAPI.GetSingleton<ScoringConfigComponent>().Settings;

            var entities = blockQuery.ToEntityArray(Allocator.Temp);
            var blocks = blockQuery.ToComponentDataArray<BlockComponent>(Allocator.Temp);
            var board = new BoardState(grid.Width, grid.Height, Allocator.Temp);
            var occupants = new NativeArray<int>(grid.Width * grid.Height, Allocator.Temp);
            for (int i = 0; i < occupants.Length; i++)
                occupants[i] = OccupantTracking.None;

            for (int i = 0; i < blocks.Length; i++)
            {
                int2 position = blocks[i].GridPosition;
                if (!board.InBounds(position))
                    continue;

                board[position] = blocks[i].ToSimulationBlock();
                occupants[board.IndexOf(position)] = i;
            }

            var events = new NativeList<SimEvent>(32, Allocator.Temp);
            var random = gameState.ValueRO.Random;
            var result = MergeRulesFactory.Step(mode, ref board, move.Direction, settings, ref random, events);

            if (result.Accepted)
            {
                ApplyGameState(ref gameState.ValueRW, ref score.ValueRW, result, random, move.Direction);
                WriteBack(ref state, board, entities, occupants, events);
            }

            events.Dispose();
            occupants.Dispose();
            board.Dispose();
            blocks.Dispose();
            entities.Dispose();
        }

        private static void ApplyGameState(ref GameStateComponent gameState, ref ScoreComponent score,
            in StepResult result, Random random, int2 direction)
        {
            gameState.Random = random;
            gameState.MoveCount++;
            gameState.LastMoveDirection = direction;

            // False for the rest of this frame marks "a move happened" for SaveGameSystem; EndFrameSystem sets it back.
            gameState.WaitingForInput = false;

            if (result.GameOver)
            {
                gameState.GameOver = true;
                gameState.WaitingForInput = true;
            }

            score.TotalScore += result.ScoreDelta;
            score.CurrentFrameScore = result.ScoreDelta;
        }

        private static void WriteBack(ref SystemState state, in BoardState board, NativeArray<Entity> entities,
            NativeArray<int> occupants, NativeList<SimEvent> events)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            AddAudioEvent(ref ecb, FMODEventPaths.BlockMove);
            for (int i = 0; i < events.Length; i++)
            {
                var e = events[i];
                if (e.Type == SimEventType.Merged)
                {
                    AddAudioEvent(ref ecb, e.DestroysBoth ? FMODEventPaths.BlockMergeLarge : MergeAudioPath(e.MoverSize, e.Size));
                    var mergeEvent = ecb.CreateEntity();
                    ecb.AddComponent(mergeEvent, new MergeEventComponent
                    {
                        Position = e.Position,
                        Tier = e.Tier,
                        Color = e.Color,
                        Points = e.Points
                    });
                }
                else if (e.Type == SimEventType.GameOver)
                {
                    AddAudioEvent(ref ecb, FMODEventPaths.GameOver);
                }
            }

            // Keep entity identity: slid and merged-into blocks keep their entity, so their visuals persist.
            var removed = new NativeList<int>(8, Allocator.Temp);
            var spawned = new NativeList<int2>(1, Allocator.Temp);
            OccupantTracking.Apply(occupants, board.Width, events, 0, removed, spawned);

            for (int i = 0; i < removed.Length; i++)
                ecb.DestroyEntity(entities[removed[i]]);

            for (int index = 0; index < occupants.Length; index++)
            {
                int occupant = occupants[index];
                if (occupant == OccupantTracking.None)
                    continue;

                var position = new int2(index % board.Width, index / board.Width);
                ecb.SetComponent(entities[occupant], ToComponent(board[position], position));
            }

            for (int i = 0; i < spawned.Length; i++)
            {
                var entity = ecb.CreateEntity();
                ecb.AddComponent(entity, ToComponent(board[spawned[i]], spawned[i]));
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            spawned.Dispose();
            removed.Dispose();
        }

        private static BlockComponent ToComponent(Simulation.Block block, int2 position) => new BlockComponent
        {
            Color = block.Color,
            Size = block.Size,
            GridPosition = position,
            IsNextColorIndicator = block.IsIndicator
        };

        private static void AddAudioEvent(ref EntityCommandBuffer ecb, in FixedString64Bytes eventPath)
        {
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new AudioEventComponent { EventPath = eventPath });
        }

        /// <summary>Small if either size is Small, else Medium if either is Medium, else Large (as before CC-006).</summary>
        private static FixedString64Bytes MergeAudioPath(BlockSize moverSize, BlockSize resultSize)
        {
            if (resultSize == BlockSize.Small || moverSize == BlockSize.Small)
                return FMODEventPaths.BlockMergeSmall;
            if (resultSize == BlockSize.Medium || moverSize == BlockSize.Medium)
                return FMODEventPaths.BlockMergeMedium;
            return FMODEventPaths.BlockMergeLarge;
        }
    }
}
