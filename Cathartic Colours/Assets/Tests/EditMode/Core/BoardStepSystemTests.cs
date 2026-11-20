// CC-002.2: EditMode tests for the ECS wrapper around the board step (BoardStepSystem + ClearMoveDirectionSystem).
// Each test builds its own World (BoardStepTestWorld), sends a move command as GameInputHandler does, updates the
// systems once and checks the entities, singletons and event entities. What a move does to the board is already
// pinned by the Simulation tests; these tests check what the wrapper adds: entity identity, game state, score,
// event entities and the move command.
using System.Collections.Generic;
using ECS.Components;
using ECS.Utilities;
using NUnit.Framework;
using Simulation;
using Unity.Burst;
using Unity.Entities;

namespace ECS.Tests
{
    public class BoardStepSystemTests
    {
        static readonly string MovePath = FMODEventPaths.BlockMove.ToString();
        static readonly string MergeSmallPath = FMODEventPaths.BlockMergeSmall.ToString();
        static readonly string MergeMediumPath = FMODEventPaths.BlockMergeMedium.ToString();
        static readonly string MergeLargePath = FMODEventPaths.BlockMergeLarge.ToString();
        static readonly string GameOverPath = FMODEventPaths.GameOver.ToString();

        bool previousSynchronousBurst;
        readonly List<BoardStepTestWorld> worlds = new List<BoardStepTestWorld>();

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // BoardStepSystem and ClearMoveDirectionSystem are Burst-compiled: compile them before the first update
            // so the tests run the Burst code, and an error in it fails the test run.
            previousSynchronousBurst = BurstCompiler.Options.EnableBurstCompileSynchronously;
            BurstCompiler.Options.EnableBurstCompileSynchronously = true;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            BurstCompiler.Options.EnableBurstCompileSynchronously = previousSynchronousBurst;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var world in worlds)
                world.Dispose();
            worlds.Clear();
        }

        BoardStepTestWorld Create(GameMode mode, string board, bool spawnEnabled = false,
            uint seed = BoardStepTestWorld.DefaultSeed, TierPoints? points = null)
        {
            var world = new BoardStepTestWorld(mode, board, spawnEnabled, seed, points);
            worlds.Add(world);
            return world;
        }

        // ---- Entity identity ---------------------------------------------------------------------------------

        [Test]
        public void Slide_KeepsTheEntityOfTheSlidingBlock()
        {
            var w = Create(GameMode.Standard, ". Rs . .");
            var block = w.InitialEntityAt(1, 0);

            w.Move(BoardStepTestWorld.Right);

            Assert.AreEqual(1, w.BlockCount);
            Assert.AreEqual(block, w.EntityAt(2, 0), "the block keeps its entity when it slides");
            Assert.AreEqual(new Unity.Mathematics.int2(2, 0), w.BlockOf(block).GridPosition);
            Assert.AreEqual(BlockColor.Red, w.BlockOf(block).Color);
            Assert.AreEqual(BlockSize.Small, w.BlockOf(block).Size);
        }

        [Test]
        public void Merge_KeepsTheEntityMergedIntoAndDestroysTheMergedAwayBlock()
        {
            // Move right: Rs at x3 is blocked by the wall, Rs at x2 merges into it, Gs at x1 slides into the gap.
            var w = Create(GameMode.Standard, ". Gs Rs Rs");
            var slid = w.InitialEntityAt(1, 0);
            var mergedAway = w.InitialEntityAt(2, 0);
            var mergedInto = w.InitialEntityAt(3, 0);

            w.Move(BoardStepTestWorld.Right);

            Assert.IsFalse(w.Exists(mergedAway), "the merged-away block's entity is destroyed");
            Assert.IsTrue(w.Exists(mergedInto));
            Assert.IsTrue(w.Exists(slid));
            Assert.AreEqual(2, w.BlockCount);

            Assert.AreEqual(mergedInto, w.EntityAt(3, 0), "the block merged into keeps its entity");
            Assert.AreEqual(BlockSize.Medium, w.BlockOf(mergedInto).Size, "and takes the merge result");
            Assert.AreEqual(BlockColor.Red, w.BlockOf(mergedInto).Color);

            Assert.AreEqual(slid, w.EntityAt(2, 0), "the block that slid into the gap keeps its entity");
            Assert.AreEqual(BlockColor.Green, w.BlockOf(slid).Color);
            Assert.AreEqual(Entity.Null, w.EntityAt(1, 0));
        }

        [Test]
        public void MergeThatDestroysBoth_DestroysBothEntities()
        {
            var w = Create(GameMode.Standard, ". . Rl Rl");
            var first = w.InitialEntityAt(2, 0);
            var second = w.InitialEntityAt(3, 0);

            w.Move(BoardStepTestWorld.Right);

            Assert.IsFalse(w.Exists(first));
            Assert.IsFalse(w.Exists(second));
            Assert.AreEqual(0, w.BlockCount);
        }

        [Test]
        public void SpawnedIndicator_GetsANewEntity_AndResolvedIndicatorKeepsItsEntity()
        {
            // Indicator at (0,2), a block at (0,0) that slides right; spawning on.
            var w = Create(GameMode.Standard, "i . . | . . . | Rs . .", spawnEnabled: true);
            var oldIndicator = w.InitialEntityAt(0, 2);
            var slider = w.InitialEntityAt(0, 0);

            w.Move(BoardStepTestWorld.Right);

            Assert.AreEqual(3, w.BlockCount, "slider, resolved indicator, new indicator");

            // The old indicator keeps its entity and becomes a Small primary block.
            Assert.AreEqual(oldIndicator, w.EntityAt(0, 2));
            var resolved = w.BlockOf(oldIndicator);
            Assert.IsFalse(resolved.IsNextColorIndicator, "the resolved indicator is a normal block");
            Assert.AreEqual(BlockSize.Small, resolved.Size);
            Assert.That(resolved.Color, Is.EqualTo(BlockColor.Red).Or.EqualTo(BlockColor.Green).Or.EqualTo(BlockColor.Blue));

            // The slider keeps its entity.
            Assert.AreEqual(slider, w.EntityAt(1, 0));

            // The new indicator is a new entity, White and Small, on a cell that was free.
            Assert.AreEqual(1, w.IndicatorCount);
            foreach (var (entity, block) in w.Blocks())
            {
                if (!block.IsNextColorIndicator)
                    continue;
                Assert.AreNotEqual(oldIndicator, entity, "the new indicator is a new entity");
                Assert.AreNotEqual(slider, entity);
                Assert.AreEqual(BlockColor.White, block.Color);
                Assert.AreEqual(BlockSize.Small, block.Size);
                Assert.AreNotEqual(new Unity.Mathematics.int2(0, 2), block.GridPosition);
                Assert.AreNotEqual(new Unity.Mathematics.int2(1, 0), block.GridPosition);
            }
        }

        [Test]
        public void SpawnDisabled_ResolvesTheIndicatorAndCreatesNoNewBlock()
        {
            var w = Create(GameMode.Standard, "i . . | . . . | . . .", spawnEnabled: false);
            var indicator = w.InitialEntityAt(0, 2);

            w.Move(BoardStepTestWorld.Right);

            Assert.AreEqual(1, w.BlockCount, "no new indicator entity");
            Assert.AreEqual(0, w.IndicatorCount);
            Assert.AreEqual(indicator, w.EntityAt(0, 2));
            Assert.IsFalse(w.BlockOf(indicator).IsNextColorIndicator);
        }

        // ---- The resolved and the new indicator, in every mode ----------------------------------------------

        [TestCase(GameMode.Standard)]
        [TestCase(GameMode.ColorMerge)]
        [TestCase(GameMode.AdvancedColorMerge)]
        public void Indicator_IsResolvedToASmallPrimary_AndANewIndicatorIsSpawned(GameMode mode)
        {
            var w = Create(mode, ". . . | . i . | . . .", spawnEnabled: true);
            var oldIndicator = w.InitialEntityAt(1, 1);

            w.Move(BoardStepTestWorld.Up);

            Assert.AreEqual(2, w.BlockCount);
            Assert.AreEqual(oldIndicator, w.EntityAt(1, 1), "an indicator never moves; it is resolved in place");

            var resolved = w.BlockOf(oldIndicator);
            Assert.IsFalse(resolved.IsNextColorIndicator);
            Assert.AreEqual(BlockSize.Small, resolved.Size);
            Assert.That(resolved.Color, Is.EqualTo(BlockColor.Red).Or.EqualTo(BlockColor.Green).Or.EqualTo(BlockColor.Blue));

            Assert.AreEqual(1, w.IndicatorCount);
            foreach (var (entity, block) in w.Blocks())
                if (block.IsNextColorIndicator)
                    Assert.AreNotEqual(oldIndicator, entity, "the new indicator is a new entity");
        }

        // ---- Score, move count and game state ----------------------------------------------------------------

        [Test]
        public void ScoringMove_AddsTheTierPointsToTotalAndCurrentFrameScore()
        {
            var w = Create(GameMode.Standard, ". . Rs Rs");

            w.Move(BoardStepTestWorld.Right);

            Assert.AreEqual(10, w.Score.TotalScore);
            Assert.AreEqual(10, w.Score.CurrentFrameScore);
        }

        [Test]
        public void ScoringMove_UsesThePointsFromTheScoringConfig()
        {
            var points = new TierPoints { Tier1 = 25, Tier2 = 60, Tier3 = 125, Tier4 = 250 };
            var w = Create(GameMode.Standard, ". . Rs Rs", points: points);

            w.Move(BoardStepTestWorld.Right);

            Assert.AreEqual(25, w.Score.TotalScore);
            Assert.AreEqual(25, w.Score.CurrentFrameScore);
            Assert.AreEqual(25, w.MergeEvents()[0].Points);
        }

        [Test]
        public void TwoMergesInOneMove_ScoreTheirSum()
        {
            var w = Create(GameMode.Standard, ". . Rs Rs | . . Gs Gs");

            w.Move(BoardStepTestWorld.Right);

            Assert.AreEqual(20, w.Score.TotalScore);
            Assert.AreEqual(20, w.Score.CurrentFrameScore);
            Assert.AreEqual(2, w.MergeEvents().Count);
        }

        [Test]
        public void CurrentFrameScore_ResetsOnTheNextUpdateWithNoMove_TotalScoreStays()
        {
            var w = Create(GameMode.Standard, ". . Rs Rs");
            w.Move(BoardStepTestWorld.Right);
            Assert.AreEqual(10, w.Score.CurrentFrameScore);
            w.EndFrame();

            w.Update();

            Assert.AreEqual(0, w.Score.CurrentFrameScore);
            Assert.AreEqual(10, w.Score.TotalScore);
        }

        [Test]
        public void TotalScore_AccumulatesAcrossMoves()
        {
            // Two merges on two different moves, in two rows.
            var w = Create(GameMode.Standard, ". . Rs Rs | . Gs Gs .");

            w.Move(BoardStepTestWorld.Right);   // top row merges (10); Gs Gs slide right without merging
            Assert.AreEqual(10, w.Score.TotalScore);
            w.EndFrame();

            w.Move(BoardStepTestWorld.Right);   // Gs Gs are now adjacent against the wall and merge (10)
            Assert.AreEqual(20, w.Score.TotalScore);
            Assert.AreEqual(10, w.Score.CurrentFrameScore);
        }

        [Test]
        public void AcceptedMove_IncrementsMoveCountAndRecordsDirectionAndRandomState()
        {
            // Spawn off, one indicator: it is resolved with a random draw, so the random state advances.
            var w = Create(GameMode.Standard, ". . . | . i . | . . .");
            var seedState = new Unity.Mathematics.Random(BoardStepTestWorld.DefaultSeed).state;

            w.Move(BoardStepTestWorld.Left);

            var state = w.GameState;
            Assert.AreEqual(1, state.MoveCount);
            Assert.AreEqual(BoardStepTestWorld.Left, state.LastMoveDirection);
            Assert.IsFalse(state.WaitingForInput, "false for the rest of the frame; EndFrameSystem sets it back");
            Assert.IsFalse(state.GameOver);
            Assert.AreNotEqual(seedState, state.Random.state, "the step's random draws are stored back");
        }

        [Test]
        public void MoveCount_CountsEachAcceptedMove()
        {
            var w = Create(GameMode.Standard, ". Rs . .");

            w.Move(BoardStepTestWorld.Right);
            w.EndFrame();
            w.Move(BoardStepTestWorld.Left);

            Assert.AreEqual(2, w.GameState.MoveCount);
        }

        [Test]
        public void MoveWhileNotWaitingForInput_IsRejected()
        {
            // An accepted move leaves WaitingForInput false until EndFrameSystem runs, so a second command in the
            // same frame cycle is ignored.
            var w = Create(GameMode.Standard, ". Rs . .");
            w.Move(BoardStepTestWorld.Right);
            var audioAfterFirstMove = w.AudioPaths().Count;
            var blockAfterFirstMove = w.EntityAt(2, 0);

            w.Move(BoardStepTestWorld.Right);

            Assert.AreEqual(1, w.GameState.MoveCount);
            Assert.AreEqual(audioAfterFirstMove, w.AudioPaths().Count, "no events for a rejected move");
            Assert.AreEqual(blockAfterFirstMove, w.EntityAt(2, 0), "the board did not change");
        }

        // ---- Audio events ------------------------------------------------------------------------------------

        [Test]
        public void MoveWithoutMerge_CreatesOnlyAMoveSound()
        {
            var w = Create(GameMode.Standard, ". Rs . .");

            w.Move(BoardStepTestWorld.Right);

            CollectionAssert.AreEqual(new[] { MovePath }, w.AudioPaths());
            Assert.AreEqual(0, w.MergeEvents().Count);
            Assert.AreEqual(0, w.Score.CurrentFrameScore);
        }

        [Test]
        public void SmallMerge_CreatesAMoveSoundAndASmallMergeSound()
        {
            var w = Create(GameMode.Standard, ". . Rs Rs");

            w.Move(BoardStepTestWorld.Right);

            CollectionAssert.AreEquivalent(new[] { MovePath, MergeSmallPath }, w.AudioPaths());
        }

        [Test]
        public void TwoMerges_CreateOneMoveSoundAndTwoMergeSounds()
        {
            var w = Create(GameMode.Standard, ". . Rs Rs | . . Gs Gs");

            w.Move(BoardStepTestWorld.Right);

            CollectionAssert.AreEquivalent(new[] { MovePath, MergeSmallPath, MergeSmallPath }, w.AudioPaths());
        }

        [Test]
        public void MoveThatEndsTheGame_CreatesAGameOverSound_AndSetsGameOver()
        {
            // One free cell and nothing can merge: Bs slides into the gap on the right, the new indicator fills the last
            // free cell, and the full board has no merge left. (A board that is already full and stuck before the
            // move is rejected by the step, so it cannot be used here.)
            var w = Create(GameMode.Standard, "Rs Gs | Bs .", spawnEnabled: true);

            w.Move(BoardStepTestWorld.Right);

            CollectionAssert.AreEquivalent(new[] { MovePath, GameOverPath }, w.AudioPaths());
            var state = w.GameState;
            Assert.IsTrue(state.GameOver);
            Assert.IsTrue(state.WaitingForInput, "a finished game goes straight back to waiting");
            Assert.AreEqual(1, state.MoveCount);
        }

        // ---- Merge events ------------------------------------------------------------------------------------

        [Test]
        public void Merge_CreatesAMergeEventWithPositionTierColorAndPoints()
        {
            var w = Create(GameMode.Standard, ". . Rs Rs");

            w.Move(BoardStepTestWorld.Right);

            var events = w.MergeEvents();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(new Unity.Mathematics.int2(3, 0), events[0].Position);
            Assert.AreEqual(ScoreTier.Tier1, events[0].Tier);
            Assert.AreEqual(BlockColor.Red, events[0].Color);
            Assert.AreEqual(10, events[0].Points);
        }

        // ---- The move command --------------------------------------------------------------------------------

        [Test]
        public void MoveCommand_IsRemovedAfterAnAcceptedMove()
        {
            var w = Create(GameMode.Standard, ". Rs . .");
            w.SendMove(BoardStepTestWorld.Right);
            Assert.IsTrue(w.HasMoveCommand, "precondition: the command exists");

            w.Update();

            Assert.IsFalse(w.HasMoveCommand);
            Assert.AreEqual(1, w.GameState.MoveCount, "and the move was applied once");
        }

        [Test]
        public void MoveCommand_IsRemovedAfterARejectedMove()
        {
            var w = Create(GameMode.Standard, ". Rs . .");
            var state = w.GameState;
            state.GameOver = true;
            w.GameState = state;

            w.Move(BoardStepTestWorld.Right);

            Assert.IsFalse(w.HasMoveCommand);
        }

        [Test]
        public void SecondUpdateWithoutANewCommand_DoesNotMoveAgain()
        {
            var w = Create(GameMode.Standard, ". Rs . .");
            w.Move(BoardStepTestWorld.Right);
            w.EndFrame();

            w.Update();

            Assert.AreEqual(1, w.GameState.MoveCount);
            Assert.AreEqual(1, w.AudioPaths().Count, "still only the first move's sound");
        }

        // ---- A move after game over (CC-023) -----------------------------------------------------------------

        [Test]
        public void MoveAfterGameOver_CreatesNoAudioEventsAndDoesNotCountAMove()
        {
            var w = Create(GameMode.Standard, "Rs Gs | Gs Rs");
            var state = w.GameState;
            state.GameOver = true;
            w.GameState = state;
            var randomBefore = w.GameState.Random.state;

            w.Move(BoardStepTestWorld.Right);

            Assert.AreEqual(0, w.AudioPaths().Count, "no move sound for a rejected move (CC-023)");
            Assert.AreEqual(0, w.MergeEvents().Count);
            Assert.AreEqual(0, w.GameState.MoveCount);
            Assert.AreEqual(randomBefore, w.GameState.Random.state);
            Assert.AreEqual(0, w.Score.TotalScore);
            Assert.AreEqual(4, w.BlockCount);
        }

        [Test]
        public void MoveAfterTheGameWasEndedByAMove_CreatesNoFurtherEvents()
        {
            var w = Create(GameMode.Standard, "Rs Gs | Bs .", spawnEnabled: true);
            w.Move(BoardStepTestWorld.Right);          // ends the game
            w.EndFrame();
            DestroyAudioEvents(w);

            w.Move(BoardStepTestWorld.Left);

            Assert.AreEqual(0, w.AudioPaths().Count);
            Assert.AreEqual(1, w.GameState.MoveCount);
            Assert.IsTrue(w.GameState.GameOver);
        }

        // ---- One case per game mode --------------------------------------------------------------------------

        [Test]
        public void Standard_MediumPair_MergesIntoLarge_Tier2()
        {
            var w = Create(GameMode.Standard, ". . Rm Rm");
            var target = w.InitialEntityAt(3, 0);

            w.Move(BoardStepTestWorld.Right);

            Assert.AreEqual(target, w.EntityAt(3, 0));
            Assert.AreEqual(BlockSize.Large, w.BlockOf(target).Size);
            Assert.AreEqual(50, w.Score.TotalScore);
            Assert.AreEqual(ScoreTier.Tier2, w.MergeEvents()[0].Tier);
            CollectionAssert.AreEquivalent(new[] { MovePath, MergeMediumPath }, w.AudioPaths());
        }

        [Test]
        public void Standard_LargePair_DestroysBoth_Tier3()
        {
            var w = Create(GameMode.Standard, ". . Rl Rl");

            w.Move(BoardStepTestWorld.Right);

            Assert.AreEqual(0, w.BlockCount);
            Assert.AreEqual(100, w.Score.TotalScore);
            var events = w.MergeEvents();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ScoreTier.Tier3, events[0].Tier);
            Assert.AreEqual(100, events[0].Points);
            CollectionAssert.AreEquivalent(new[] { MovePath, MergeLargePath }, w.AudioPaths());
        }

        [Test]
        public void ColorMerge_RedAndGreen_MergeIntoYellow_Tier1()
        {
            // Move left: Rs at x0 is blocked by the wall; Gs at x1 merges into it.
            var w = Create(GameMode.ColorMerge, "Rs Gs . .");
            var target = w.InitialEntityAt(0, 0);
            var mergedAway = w.InitialEntityAt(1, 0);

            w.Move(BoardStepTestWorld.Left);

            Assert.AreEqual(target, w.EntityAt(0, 0));
            Assert.AreEqual(BlockColor.Yellow, w.BlockOf(target).Color);
            Assert.AreEqual(BlockSize.Small, w.BlockOf(target).Size);
            Assert.IsFalse(w.Exists(mergedAway));
            Assert.AreEqual(10, w.Score.TotalScore);
            var events = w.MergeEvents();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(BlockColor.Yellow, events[0].Color);
            Assert.AreEqual(ScoreTier.Tier1, events[0].Tier);
            CollectionAssert.AreEquivalent(new[] { MovePath, MergeSmallPath }, w.AudioPaths());
        }

        [Test]
        public void ColorMerge_SecondaryAndItsMissingPrimary_DestroyBoth_Tier3()
        {
            var w = Create(GameMode.ColorMerge, "Ys Bs . .");

            w.Move(BoardStepTestWorld.Left);

            Assert.AreEqual(0, w.BlockCount);
            Assert.AreEqual(100, w.Score.TotalScore);
            var events = w.MergeEvents();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(BlockColor.White, events[0].Color, "the effect colour of a clear is white");
            Assert.AreEqual(ScoreTier.Tier3, events[0].Tier);
            CollectionAssert.AreEquivalent(new[] { MovePath, MergeLargePath }, w.AudioPaths());
        }

        [Test]
        public void AdvancedColorMerge_TwoMediumPrimaries_MergeIntoALargeSecondary_Tier2()
        {
            var w = Create(GameMode.AdvancedColorMerge, ". . Rm Bm");
            var target = w.InitialEntityAt(3, 0);

            w.Move(BoardStepTestWorld.Right);

            Assert.AreEqual(target, w.EntityAt(3, 0));
            Assert.AreEqual(BlockColor.Magenta, w.BlockOf(target).Color);
            Assert.AreEqual(BlockSize.Large, w.BlockOf(target).Size);
            Assert.AreEqual(50, w.Score.TotalScore);
            Assert.AreEqual(ScoreTier.Tier2, w.MergeEvents()[0].Tier);
            CollectionAssert.AreEquivalent(new[] { MovePath, MergeMediumPath }, w.AudioPaths());
        }

        [Test]
        public void AdvancedColorMerge_TwoWhiteLarge_DestroyBoth_Tier4()
        {
            var w = Create(GameMode.AdvancedColorMerge, ". . Wl Wl");

            w.Move(BoardStepTestWorld.Right);

            Assert.AreEqual(0, w.BlockCount);
            Assert.AreEqual(200, w.Score.TotalScore);
            var events = w.MergeEvents();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ScoreTier.Tier4, events[0].Tier);
            Assert.AreEqual(200, events[0].Points);
            CollectionAssert.AreEquivalent(new[] { MovePath, MergeLargePath }, w.AudioPaths());
        }

        // ---- Helpers -----------------------------------------------------------------------------------------

        static void DestroyAudioEvents(BoardStepTestWorld w)
        {
            using var query = w.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<AudioEventComponent>());
            w.EntityManager.DestroyEntity(query);
        }
    }
}
