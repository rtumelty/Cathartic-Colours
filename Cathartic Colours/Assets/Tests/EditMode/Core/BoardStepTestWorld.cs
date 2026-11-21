// Test helper for the CC-002.2 ECS board-step tests: a fresh ECS World that contains only BoardStepSystem and
// ClearMoveDirectionSystem, the singletons GameRenderer.InitializeGame creates, and block entities built from a
// board written in the same notation as the Simulation tests (cells separated by spaces, rows by '|', the FIRST
// row listed is the TOP row, "." empty, "i" the white indicator, "Rs" = colour letter R G B Y M C W + size
// letter s m l).
using System;
using System.Collections.Generic;
using ECS.Components;
using ECS.Systems;
using Simulation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ECS.Tests
{
    internal sealed class BoardStepTestWorld : IDisposable
    {
        public static readonly int2 Right = new int2(1, 0);
        public static readonly int2 Left = new int2(-1, 0);
        public static readonly int2 Up = new int2(0, 1);
        public static readonly int2 Down = new int2(0, -1);

        public const uint DefaultSeed = 12345u;

        public readonly World World;
        public readonly EntityManager EntityManager;
        public readonly int Width;
        public readonly int Height;
        public readonly Entity GameStateEntity;
        public readonly Entity ScoreEntity;

        readonly SimulationSystemGroup group;
        readonly Entity[] initialEntities;

        public BoardStepTestWorld(GameMode mode, string board, bool spawnEnabled = false, uint seed = DefaultSeed,
            TierPoints? points = null)
        {
            World = new World("CC-002.2 board step test world");
            EntityManager = World.EntityManager;

            // The two systems under test, updated in a SimulationSystemGroup so their [UpdateAfter] attribute decides
            // the order (BoardStepSystem, then ClearMoveDirectionSystem), as in the game.
            group = World.GetOrCreateSystemManaged<SimulationSystemGroup>();
            group.AddSystemToUpdateList(World.CreateSystem<BoardStepSystem>());
            group.AddSystemToUpdateList(World.CreateSystem<ClearMoveDirectionSystem>());
            group.SortSystems();

            var rows = board.Split('|');
            Height = rows.Length;
            var parsed = new string[Height][];
            for (int r = 0; r < Height; r++)
                parsed[r] = rows[r].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Width = parsed[0].Length;

            // Singletons, as GameRenderer.InitializeGame creates them.
            var grid = EntityManager.CreateEntity();
            EntityManager.AddComponentData(grid, new GridConfigComponent { Width = Width, Height = Height });

            GameStateEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(GameStateEntity, new GameStateComponent
            {
                WaitingForInput = true,
                GameOver = false,
                LevelComplete = false,
                MoveCount = 0,
                Random = new Unity.Mathematics.Random(seed)
            });

            ScoreEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(ScoreEntity, new ScoreComponent());

            var modeEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(modeEntity, new GameModeComponent { Mode = mode });

            var settings = new StepSettings
            {
                Points = points ?? TierPoints.Default,
                SpawnEnabled = spawnEnabled
            };
            var rulesEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(rulesEntity, new GameRulesComponent { Rules = GameRules.For(mode, settings) });

            // Block entities.
            initialEntities = new Entity[Width * Height];
            for (int r = 0; r < Height; r++)
            {
                int y = Height - 1 - r;
                if (parsed[r].Length != Width)
                    throw new ArgumentException("Ragged board: " + board);
                for (int x = 0; x < Width; x++)
                {
                    var token = parsed[r][x];
                    if (token == ".")
                        continue;

                    var block = ParseBlock(token);
                    var entity = EntityManager.CreateEntity();
                    EntityManager.AddComponentData(entity, new BlockComponent
                    {
                        Color = block.Color,
                        Size = block.Size,
                        GridPosition = new int2(x, y),
                        IsNextColorIndicator = block.IsIndicator
                    });
                    initialEntities[x + y * Width] = entity;
                }
            }
        }

        // ---- Driving the world -------------------------------------------------------------------------------

        /// <summary>Creates a move command entity, as GameInputHandler.SendMoveCommand does.</summary>
        public void SendMove(int2 direction)
        {
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new MoveDirectionComponent { Direction = direction });
        }

        /// <summary>Runs one frame of the two systems.</summary>
        public void Update() => group.Update();

        /// <summary>Sends a move and runs one frame.</summary>
        public void Move(int2 direction)
        {
            SendMove(direction);
            Update();
        }

        /// <summary>
        /// Stands in for EndFrameSystem (not part of this world): an accepted move leaves WaitingForInput false for the
        /// rest of the frame and EndFrameSystem sets it back to true.
        /// </summary>
        public void EndFrame()
        {
            var state = GameState;
            state.WaitingForInput = true;
            GameState = state;
        }

        // ---- SaveGameSystem (CC-002.3) -----------------------------------------------------------------------

        PresentationSystemGroup presentationGroup;

        /// <summary>
        /// Adds SaveGameSystem in a PresentationSystemGroup, as in the game. Only <see cref="UpdatePresentation"/>
        /// runs it, so tests that don't call it are unaffected.
        /// </summary>
        public void AddSaveGameSystem()
        {
            presentationGroup = World.GetOrCreateSystemManaged<PresentationSystemGroup>();
            presentationGroup.AddSystemToUpdateList(World.CreateSystem<SaveGameSystem>());
            presentationGroup.SortSystems();
        }

        /// <summary>Runs one frame of the presentation group (SaveGameSystem only).</summary>
        public void UpdatePresentation() => presentationGroup.Update();

        /// <summary>
        /// One frame of an accepted move as the game runs it: simulation systems, SaveGameSystem, then EndFrameSystem's
        /// reset of WaitingForInput.
        /// </summary>
        public void MoveAndSave(int2 direction)
        {
            Move(direction);
            UpdatePresentation();
            EndFrame();
        }

        // ---- Reading the world -------------------------------------------------------------------------------

        public GameStateComponent GameState
        {
            get => EntityManager.GetComponentData<GameStateComponent>(GameStateEntity);
            set => EntityManager.SetComponentData(GameStateEntity, value);
        }

        public ScoreComponent Score => EntityManager.GetComponentData<ScoreComponent>(ScoreEntity);

        /// <summary>The entity that was created for the block at (x, y) when the world was built.</summary>
        public Entity InitialEntityAt(int x, int y) => initialEntities[x + y * Width];

        public bool Exists(Entity entity) => EntityManager.Exists(entity);

        public BlockComponent BlockOf(Entity entity) => EntityManager.GetComponentData<BlockComponent>(entity);

        /// <summary>All current block entities with their data.</summary>
        public List<(Entity Entity, BlockComponent Block)> Blocks()
        {
            var list = new List<(Entity, BlockComponent)>();
            using var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<BlockComponent>());
            using var entities = query.ToEntityArray(Allocator.TempJob);
            using var blocks = query.ToComponentDataArray<BlockComponent>(Allocator.TempJob);
            for (int i = 0; i < entities.Length; i++)
                list.Add((entities[i], blocks[i]));
            return list;
        }

        /// <summary>The block entity at (x, y), or Entity.Null when the cell is empty.</summary>
        public Entity EntityAt(int x, int y)
        {
            foreach (var (entity, block) in Blocks())
                if (block.GridPosition.x == x && block.GridPosition.y == y)
                    return entity;
            return Entity.Null;
        }

        public int BlockCount => Blocks().Count;

        public int IndicatorCount
        {
            get
            {
                int n = 0;
                foreach (var (_, block) in Blocks())
                    if (block.IsNextColorIndicator) n++;
                return n;
            }
        }

        /// <summary>The audio event paths currently waiting for AudioSystem (it is not in this world), as strings.</summary>
        public List<string> AudioPaths()
        {
            var list = new List<string>();
            using var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<AudioEventComponent>());
            using var events = query.ToComponentDataArray<AudioEventComponent>(Allocator.TempJob);
            for (int i = 0; i < events.Length; i++)
                list.Add(events[i].EventPath.ToString());
            return list;
        }

        public List<MergeEventComponent> MergeEvents()
        {
            var list = new List<MergeEventComponent>();
            using var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<MergeEventComponent>());
            using var events = query.ToComponentDataArray<MergeEventComponent>(Allocator.TempJob);
            for (int i = 0; i < events.Length; i++)
                list.Add(events[i]);
            return list;
        }

        public bool HasMoveCommand
        {
            get
            {
                using var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<MoveDirectionComponent>());
                return !query.IsEmpty;
            }
        }

        public void Dispose()
        {
            if (World != null && World.IsCreated)
                World.Dispose();
        }

        // ---- Board notation ----------------------------------------------------------------------------------

        static Simulation.Block ParseBlock(string token)
        {
            if (token == "i") return Simulation.Block.Indicator;
            if (token.Length != 2) throw new ArgumentException("Bad block token '" + token + "'");
            return Simulation.Block.Of(ParseColor(token[0]), ParseSize(token[1]));
        }

        static BlockColor ParseColor(char c)
        {
            switch (c)
            {
                case 'R': return BlockColor.Red;
                case 'G': return BlockColor.Green;
                case 'B': return BlockColor.Blue;
                case 'Y': return BlockColor.Yellow;
                case 'M': return BlockColor.Magenta;
                case 'C': return BlockColor.Cyan;
                case 'W': return BlockColor.White;
                default: throw new ArgumentException("Unknown colour letter '" + c + "'");
            }
        }

        static BlockSize ParseSize(char c)
        {
            switch (c)
            {
                case 's': return BlockSize.Small;
                case 'm': return BlockSize.Medium;
                case 'l': return BlockSize.Large;
                default: throw new ArgumentException("Unknown size letter '" + c + "'");
            }
        }
    }
}
