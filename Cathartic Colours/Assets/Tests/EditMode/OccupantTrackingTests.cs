using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Simulation.Tests
{
    /// <summary>
    /// The ECS wrapper keeps one entity per block by replaying a step's events over per-cell ids
    /// (OccupantTracking), so visuals keyed by entity survive slides and merges. Also covers CC-023:
    /// a rejected move produces no events, so the wrapper plays no move sound.
    /// </summary>
    public class OccupantTrackingTests
    {
        private const int Width = 4;

        private static NativeArray<int> Occupants(params int[] ids)
        {
            var array = new NativeArray<int>(ids.Length, Allocator.Persistent);
            array.CopyFrom(ids);
            return array;
        }

        private static (int[] occupants, int[] removed, int2[] spawned) Run(int[] start, params SimEvent[] simEvents)
        {
            var occupants = Occupants(start);
            var events = new NativeList<SimEvent>(Allocator.Persistent);
            var removed = new NativeList<int>(Allocator.Persistent);
            var spawned = new NativeList<int2>(Allocator.Persistent);
            try
            {
                foreach (var e in simEvents)
                    events.Add(e);
                OccupantTracking.Apply(occupants, Width, events, 0, removed, spawned);
                return (occupants.ToArray(), removed.AsArray().ToArray(), spawned.AsArray().ToArray());
            }
            finally
            {
                occupants.Dispose();
                events.Dispose();
                removed.Dispose();
                spawned.Dispose();
            }
        }

        private static SimEvent Moved(int fromX, int toX) =>
            new SimEvent { Type = SimEventType.Moved, From = new int2(fromX, 0), Position = new int2(toX, 0) };

        private static SimEvent Merged(int fromX, int toX, bool destroysBoth = false) =>
            new SimEvent { Type = SimEventType.Merged, From = new int2(fromX, 0), Position = new int2(toX, 0), DestroysBoth = destroysBoth };

        [Test]
        public void Moved_BlockKeepsItsIdInTheNewCell()
        {
            var (occupants, removed, spawned) = Run(new[] { 7, -1, -1, -1 }, Moved(0, 1));
            CollectionAssert.AreEqual(new[] { -1, 7, -1, -1 }, occupants);
            Assert.IsEmpty(removed);
            Assert.IsEmpty(spawned);
        }

        [Test]
        public void ChainOfMoves_FurthestFirst_EachBlockKeepsItsId()
        {
            // [A B C .] moving right: C, then B, then A each move one cell.
            var (occupants, _, _) = Run(new[] { 1, 2, 3, -1 }, Moved(2, 3), Moved(1, 2), Moved(0, 1));
            CollectionAssert.AreEqual(new[] { -1, 1, 2, 3 }, occupants);
        }

        [Test]
        public void Merged_MoverIsRemoved_TargetKeepsItsId()
        {
            var (occupants, removed, _) = Run(new[] { -1, -1, 5, 6 }, Merged(2, 3));
            CollectionAssert.AreEqual(new[] { -1, -1, -1, 6 }, occupants);
            CollectionAssert.AreEqual(new[] { 5 }, removed);
        }

        [Test]
        public void MergedDestroyingBoth_BothIdsAreRemoved()
        {
            var (occupants, removed, _) = Run(new[] { -1, -1, 5, 6 }, Merged(2, 3, destroysBoth: true));
            CollectionAssert.AreEqual(new[] { -1, -1, -1, -1 }, occupants);
            CollectionAssert.AreEquivalent(new[] { 5, 6 }, removed);
        }

        [Test]
        public void MergeThenMoveIntoTheVacatedCell_KeepsIds()
        {
            // [. Rs Rs Rs] right: x2 merges into x3, then x1 moves into x2.
            var (occupants, removed, _) = Run(new[] { -1, 1, 2, 3 }, Merged(2, 3), Moved(1, 2));
            CollectionAssert.AreEqual(new[] { -1, -1, 1, 3 }, occupants);
            CollectionAssert.AreEqual(new[] { 2 }, removed);
        }

        [Test]
        public void ResolvedIndicator_KeepsItsId_SpawnedIndicatorNeedsANewObject()
        {
            var resolved = new SimEvent { Type = SimEventType.IndicatorResolved, Position = new int2(1, 0) };
            var spawn = new SimEvent { Type = SimEventType.IndicatorSpawned, Position = new int2(3, 0) };
            var (occupants, removed, spawned) = Run(new[] { -1, 4, -1, -1 }, resolved, spawn);
            CollectionAssert.AreEqual(new[] { -1, 4, -1, -1 }, occupants);
            Assert.IsEmpty(removed);
            CollectionAssert.AreEqual(new[] { new int2(3, 0) }, spawned);
        }

        [Test]
        public void FullStep_ReplayedOverIds_MatchesTheResultingBoard()
        {
            // Every occupied cell after the step has an id or is a new spawn; every empty cell has none.
            var board = new BoardState(4, 4, Allocator.Persistent);
            var events = new NativeList<SimEvent>(Allocator.Persistent);
            var occupants = new NativeArray<int>(16, Allocator.Persistent);
            var removed = new NativeList<int>(Allocator.Persistent);
            var spawned = new NativeList<int2>(Allocator.Persistent);
            try
            {
                // Row 0: [. Rs Rs Rs] -> one merge at the wall, then a slide. Row 2: [. . Bl Bl] -> both cleared.
                board[1, 0] = Block.Of(BlockColor.Red, BlockSize.Small);
                board[2, 0] = Block.Of(BlockColor.Red, BlockSize.Small);
                board[3, 0] = Block.Of(BlockColor.Red, BlockSize.Small);
                board[2, 2] = Block.Of(BlockColor.Blue, BlockSize.Large);
                board[3, 2] = Block.Of(BlockColor.Blue, BlockSize.Large);
                board[3, 3] = Block.Indicator;

                int next = 0;
                for (int i = 0; i < 16; i++)
                    occupants[i] = board.Cells[i].IsEmpty ? OccupantTracking.None : next++;

                var random = new Random(987654321u);
                var result = BoardSimulation.Step(ref board, new int2(1, 0), GameRules.For(GameMode.Standard, StepSettings.Default), ref random, events);
                Assert.IsTrue(result.Accepted);

                OccupantTracking.Apply(occupants, 4, events, 0, removed, spawned);

                for (int i = 0; i < 16; i++)
                {
                    var cell = new int2(i % 4, i / 4);
                    bool isSpawn = false;
                    for (int s = 0; s < spawned.Length; s++)
                        isSpawn |= spawned[s].Equals(cell);

                    if (board.Cells[i].IsEmpty)
                        Assert.AreEqual(OccupantTracking.None, occupants[i], "empty cell " + cell + " has an id");
                    else
                        Assert.IsTrue(occupants[i] != OccupantTracking.None || isSpawn, "occupied cell " + cell + " has no id and is not a spawn");
                }

                Assert.AreEqual(3, removed.Length, "one Small merge (mover) and one Large clear (both)");
            }
            finally
            {
                board.Dispose();
                events.Dispose();
                occupants.Dispose();
                removed.Dispose();
                spawned.Dispose();
            }
        }

        [Test]
        public void Fixes_CC023_RejectedMoveOnAGameOverBoard_ReturnsNoEventsAndChangesNothing()
        {
            // A full Standard board with no equal neighbours: game over. Before CC-006 a swipe here still
            // played the move sound; the wrapper now makes sounds only from the events of an accepted step.
            var board = new BoardState(2, 2, Allocator.Persistent);
            var events = new NativeList<SimEvent>(Allocator.Persistent);
            try
            {
                board[0, 0] = Block.Of(BlockColor.Red, BlockSize.Small);
                board[1, 0] = Block.Of(BlockColor.Green, BlockSize.Small);
                board[0, 1] = Block.Of(BlockColor.Blue, BlockSize.Small);
                board[1, 1] = Block.Of(BlockColor.Red, BlockSize.Medium);
                Assert.IsTrue(BoardSimulation.IsGameOver(board, GameRules.For(GameMode.Standard, StepSettings.Default)), "test setup: board is game over");

                var random = new Random(42u);
                uint stateBefore = random.state;
                var result = BoardSimulation.Step(ref board, new int2(-1, 0), GameRules.For(GameMode.Standard, StepSettings.Default), ref random, events);

                Assert.IsFalse(result.Accepted);
                Assert.AreEqual(0, events.Length);
                Assert.AreEqual(stateBefore, random.state);
                Assert.AreEqual(Block.Of(BlockColor.Red, BlockSize.Medium), board[1, 1]);
            }
            finally
            {
                board.Dispose();
                events.Dispose();
            }
        }
    }
}
