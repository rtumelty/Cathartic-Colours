// Replays the recorded games (CC-008.1) and compares them with RecordedGames/recorded-games.txt.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Simulation.Tests
{
    public class RecordedGamesTests
    {
        const string RecordEnvironmentVariable = "CC_RECORD_GAMES";

        static readonly GameMode[] Modes = { GameMode.Standard, GameMode.ColorMerge, GameMode.AdvancedColorMerge };

        static string FilePath => Path.Combine(Application.dataPath, RecordedGames.RelativePath);

        static string Key(GameMode mode, bool spawn, uint seed) => $"{mode} {(spawn ? "spawnOn" : "spawnOff")} {seed}";

        static IEnumerable<TestCaseData> ModeAndSpawn()
        {
            foreach (var mode in Modes)
            foreach (var spawn in new[] { true, false })
                yield return new TestCaseData(mode, spawn).SetName($"Replay_{mode}_{(spawn ? "SpawnOn" : "SpawnOff")}");
        }

        static Dictionary<string, (int moves, ulong hash)> ReadFile()
        {
            Assert.IsTrue(File.Exists(FilePath), "Recorded games file is missing: " + FilePath);
            var map = new Dictionary<string, (int, ulong)>();
            foreach (var raw in File.ReadAllLines(FilePath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                var p = line.Split(' ');
                Assert.AreEqual(5, p.Length, "Malformed line: " + line);
                map[$"{p[0]} {p[1]} {p[2]}"] = (int.Parse(p[3]), Convert.ToUInt64(p[4], 16));
            }
            return map;
        }

        [TestCaseSource(nameof(ModeAndSpawn))]
        public void Replay_MatchesRecordedGames(GameMode mode, bool spawn)
        {
            var recorded = ReadFile();
            var failures = new StringBuilder();
            for (uint seed = 1; seed <= RecordedGames.SeedCount; seed++)
            {
                var key = Key(mode, spawn, seed);
                var actual = RecordedGames.PlayGame(mode, spawn, seed);
                if (!recorded.TryGetValue(key, out var expected))
                {
                    failures.AppendLine($"{key}: no recorded line");
                    continue;
                }
                if (expected.moves != actual.Moves || expected.hash != actual.Hash)
                    failures.AppendLine($"{key}: recorded {expected.moves} moves, {expected.hash:x16}; actual {actual.Moves} moves, {actual.Hash:x16}");
            }
            Assert.IsEmpty(failures.ToString(), "Behaviour differs from the recorded games:\n" + failures);
        }

        [Test]
        public void RecordedFile_CoversExactlyTheExpectedGames()
        {
            var recorded = ReadFile();
            var expected = new HashSet<string>();
            foreach (var mode in Modes)
            foreach (var spawn in new[] { true, false })
            for (uint seed = 1; seed <= RecordedGames.SeedCount; seed++)
                expected.Add(Key(mode, spawn, seed));

            Assert.AreEqual(300, expected.Count);
            Assert.AreEqual(300, recorded.Count, "number of recorded games");
            foreach (var key in expected)
                Assert.IsTrue(recorded.ContainsKey(key), "missing " + key);
        }

        [Test]
        public void Games_AreNonTrivial()
        {
            int games = 0, totalMoves = 0, endedEarly = 0;
            foreach (var mode in Modes)
            for (uint seed = 1; seed <= RecordedGames.SeedCount; seed++)
            {
                var g = RecordedGames.PlayGame(mode, true, seed);
                games++;
                totalMoves += g.Moves;
                if (g.Moves < RecordedGames.MaxMoves)
                    endedEarly++;
            }
            Assert.Greater(totalMoves / (double)games, 10.0, "average moves in spawn-on games");
            Assert.Greater(endedEarly, 0, "some spawn-on games should end before the move cap");
        }

        [Test]
        public void SeamStep_ProducesMergeEvents()
        {
            // Proves the seam reaches the real step code: two small reds side by side, pushed right, merge.
            var random = new Random(1u);
            var events = new NativeList<SimEvent>(16, Allocator.Temp);
            var board = new BoardState(RecordedGames.BoardSize, RecordedGames.BoardSize, Allocator.Temp);
            try
            {
                board[4, 0] = Block.Of(BlockColor.Red, BlockSize.Small);
                board[5, 0] = Block.Of(BlockColor.Red, BlockSize.Small);
                var result = RecordedGames.StepOnce(GameMode.Standard, ref board, new int2(1, 0), false, ref random, events);
                Assert.IsTrue(result.Accepted);
                int merges = 0;
                for (int i = 0; i < events.Length; i++)
                    if (events[i].Type == SimEventType.Merged)
                        merges++;
                Assert.AreEqual(1, merges);
            }
            finally
            {
                events.Dispose();
                board.Dispose();
            }
        }

        [Test]
        public void PlayGame_IsDeterministic()
        {
            foreach (var mode in Modes)
            {
                var a = RecordedGames.PlayGame(mode, true, 7u);
                var b = RecordedGames.PlayGame(mode, true, 7u);
                Assert.AreEqual(a.Moves, b.Moves);
                Assert.AreEqual(a.Hash, b.Hash);
            }
        }

        [Test, Explicit("Re-records the file; run only when behaviour is meant to change")]
        public void Record_WritesRecordedGamesFile()
        {
            // A class-wide -testFilter also runs Explicit tests, which would silently re-record the hashes a
            // refactor must match. Require a deliberate opt-in as well.
            if (Environment.GetEnvironmentVariable(RecordEnvironmentVariable) != "1")
                Assert.Ignore($"Set {RecordEnvironmentVariable}=1 to re-record the recorded games.");

            var sb = new StringBuilder();
            sb.Append("# Recorded games (CC-008.1): pins the simulation behaviour. Do not edit by hand.\n");
            sb.Append("# Each line: <mode> <spawnOn|spawnOff> <seed> <moves played> <FNV-1a 64-bit hash of the whole game>.\n");
            sb.Append("# The hash covers the start board and, every move, the result, board, random state and events.\n");
            sb.Append("# A mismatch means behaviour changed. If that is deliberate, re-record by running the explicit test\n");
            sb.Append("# Simulation.Tests.RecordedGamesTests.Record_WritesRecordedGamesFile by name with CC_RECORD_GAMES=1 (see docs/wiki/systems/merge-rules.md).\n");
            foreach (var mode in Modes)
            foreach (var spawn in new[] { true, false })
            for (uint seed = 1; seed <= RecordedGames.SeedCount; seed++)
            {
                var g = RecordedGames.PlayGame(mode, spawn, seed);
                sb.Append($"{Key(mode, spawn, seed)} {g.Moves} {g.Hash:x16}\n");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, sb.ToString());
        }
    }
}
