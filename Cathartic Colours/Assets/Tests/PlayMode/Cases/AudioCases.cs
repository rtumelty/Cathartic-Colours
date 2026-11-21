// CC-046.3: the Audio (AUDIO) cases of docs/wiki/test-plan.md, TP-AUDIO-01 to TP-AUDIO-04 and TP-AUDIO-06.
// A test sees which FMOD events the game raised (GameDriver.AudioEvents), not how they sound. So these cases check the
// event level of the plan: "this event was raised, at the right time". Whether the sound is audible and distinct stays a
// manual check. TP-AUDIO-05 (the FMOD Studio project check, no game running) stays manual.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Simulation;
using TestSupport;
using UnityEngine;
using UnityEngine.TestTools;

namespace PlayModeTests
{
    [Category("TP_AUDIO")]
    public class AudioCases : PlaythroughTest
    {
        const GameMode Mode = GameMode.Standard;
        const string Move = "event:/Block_Move";
        const string MergeSmall = "event:/Block_Merge_Small";
        const string MergeMedium = "event:/Block_Merge_Medium";
        const string MergeLarge = "event:/Block_Merge_Large";
        const string GameOver = "event:/GameOver";

        [UnityTest, Category("TP_AUDIO_01")]
        public IEnumerator TP_AUDIO_01_MoveSoundOnEachMove()
        {
            yield return Driver.StartFromMenu(Mode, 4, 4, true);
            Driver.AudioEvents();

            for (int press = 1; press <= 5; press++)
            {
                yield return Driver.Move(press % 2 == 0 ? Direction.Right : Direction.Left);
                CollectionAssert.AreEqual(new[] { Move }, Driver.AudioEvents(),
                    $"Press {press} raised one move event, in the frame of its move (checked right after the press)");
            }
        }

        [UnityTest, Category("TP_AUDIO_02")]
        public IEnumerator TP_AUDIO_02_MergeSoundFollowsTheMovingBlocksSize()
        {
            yield return MergeRaises(BoardNotation.BottomRow(". . Rs Rs"), MergeSmall, "Small + Small");
            yield return MergeRaises(BoardNotation.BottomRow(". . Rm Rm"), MergeMedium, "Medium + Medium (a Large block results)");
            yield return MergeRaises(BoardNotation.BottomRow(". . Rl Rl"), MergeLarge, "Large + Large");
        }

        IEnumerator MergeRaises(string board, string mergeEvent, string what)
        {
            yield return Driver.ResumeWith(board, Mode);
            Driver.AudioEvents();
            yield return Driver.Move(Direction.Right);
            CollectionAssert.AreEqual(new[] { Move, mergeEvent }, Driver.AudioEvents(), what + ": the move sound, then that merge sound and no other");
        }

        [UnityTest, Category("TP_AUDIO_03")]
        public IEnumerator TP_AUDIO_03_GameOverSoundPlaysOnceAndDoesNotRepeat()
        {
            yield return Driver.ResumeWith(CaseChecks.AlmostFullCheckerboard, Mode, spawnNextColour: true);
            Driver.AudioEvents();

            yield return Driver.Move(Direction.Right);

            var events = Driver.AudioEvents();
            Assert.IsTrue(Driver.IsGameOver);
            Assert.AreEqual(1, events.Count(e => e == GameOver), "The game-over sound is raised once, with the move. Events: " + string.Join(", ", events));

            yield return GameDriver.Frames(60);
            CollectionAssert.IsEmpty(Driver.AudioEvents(), "Nothing, the game-over sound included, is raised over the next 60 frames.");
        }

        [UnityTest, Category("TP_AUDIO_04")]
        public IEnumerator TP_AUDIO_04_NoSoundForARejectedMoveAfterGameOver()
        {
            yield return CaseChecks.ReachGameOver(Driver);
            yield return GameDriver.Frames(2);
            Driver.AudioEvents();

            yield return Driver.Move(Direction.Left);
            yield return Driver.Move(Direction.Left);

            CollectionAssert.IsEmpty(Driver.AudioEvents(), "Moves are ignored after game over, so no sound is raised.");
        }

        // Covers the event check of TP-AUDIO-06 only: all five events are raised in the editor with no error and no FMOD or
        // "Event not found" problem logged. That the sounds are audible stays a manual check.
        [UnityTest, Category("TP_AUDIO_06")]
        public IEnumerator TP_AUDIO_06_AllFiveEventsAreRaisedWithNoErrorOrMissingEvent()
        {
            var problems = new List<string>();
            void Listen(string message, string stackTrace, LogType type)
            {
                bool fmodProblem = type != LogType.Log && message.IndexOf("FMOD", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (fmodProblem || message.Contains("Event not found"))
                    problems.Add($"{type}: {message}");
            }

            var raised = new List<string>();
            Application.logMessageReceived += Listen;
            try
            {
                yield return Driver.ResumeWith(BoardNotation.BottomRow(". . Rs Rs"), Mode);
                Driver.AudioEvents();
                yield return Driver.Move(Direction.Right);
                raised.AddRange(Driver.AudioEvents());

                yield return Driver.ResumeWith(BoardNotation.BottomRow(". . Rm Rm"), Mode);
                Driver.AudioEvents();
                yield return Driver.Move(Direction.Right);
                raised.AddRange(Driver.AudioEvents());

                yield return Driver.ResumeWith(BoardNotation.BottomRow(". . Rl Rl"), Mode);
                Driver.AudioEvents();
                yield return Driver.Move(Direction.Right);
                raised.AddRange(Driver.AudioEvents());

                yield return Driver.ResumeWith(CaseChecks.AlmostFullCheckerboard, Mode, spawnNextColour: true);
                Driver.AudioEvents();
                yield return Driver.Move(Direction.Right);
                raised.AddRange(Driver.AudioEvents());
                yield return GameDriver.Frames(10);
            }
            finally
            {
                Application.logMessageReceived -= Listen;
            }

            foreach (var expected in new[] { Move, MergeSmall, MergeMedium, MergeLarge, GameOver })
                CollectionAssert.Contains(raised, expected, "Raised events: " + string.Join(", ", raised));
            CollectionAssert.IsEmpty(problems, "No FMOD error and no 'Event not found' message was logged.");
        }
    }
}
