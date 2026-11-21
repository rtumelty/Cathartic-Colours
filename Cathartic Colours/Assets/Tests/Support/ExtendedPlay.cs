// Extended play for regression cases (CC-046.3): plays a game with a varied cycle of directions and, after every move,
// checks the rules that must hold whatever the random spawns do (the checks behind TP-STD-11, TP-CM-07 and TP-ACM-08).
// Runtime APIs only, so it runs in a player too.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Simulation;
using UnityEngine;

namespace TestSupport
{
    /// <summary>What an <see cref="ExtendedPlay"/> run saw.</summary>
    public sealed class ExtendedPlayReport
    {
        public int Moves;
        public bool ReachedGameOver;
        public int FinalScore;
        public readonly SortedSet<int> ScoreDeltas = new SortedSet<int>();
        public readonly SortedSet<string> BlocksSeen = new SortedSet<string>();

        /// <summary>A one-line summary for the test output: moves, game over, merge scores and block kinds seen.</summary>
        public string Summary()
        {
            var sb = new StringBuilder();
            sb.Append("moves=").Append(Moves).Append(" gameOver=").Append(ReachedGameOver).Append(" score=").Append(FinalScore);
            sb.Append(" scoreDeltasSeen=[").Append(string.Join(",", ScoreDeltas)).Append("]");
            sb.Append(" blocksSeen=[").Append(string.Join(" ", BlocksSeen)).Append("]");
            return sb.ToString();
        }
    }

    public static class ExtendedPlay
    {
        /// <summary>
        /// Plays up to <paramref name="maxMoves"/> moves (stopping at game over), cycling through the four directions in an
        /// order that is shuffled each round (fixed <paramref name="orderSeed"/>, so the same moves are tried every run;
        /// the game's own spawns are random). After every move it checks:
        /// the move was counted; the cell that held the indicator now holds a resolved Small primary; there is at most one
        /// indicator, and exactly one when a cell is empty; the score rose by a multiple of 10 (never fell); the game
        /// is over exactly when the board is full and no neighbouring pair can merge in <paramref name="mode"/>.
        /// A failed check names the move, the direction and the boards before and after.
        /// Start the game (spawn on) before calling.
        /// </summary>
        public static IEnumerator Run(GameDriver driver, GameMode mode, int maxMoves, ExtendedPlayReport report, int orderSeed = 12345)
        {
            var rules = GameRules.For(mode, StepSettings.Default);
            var random = new System.Random(orderSeed);
            var order = new[] { Direction.Up, Direction.Right, Direction.Down, Direction.Left };
            int index = order.Length; // forces a shuffle on the first move

            for (int move = 1; move <= maxMoves && !driver.IsGameOver; move++)
            {
                if (index >= order.Length)
                {
                    for (int i = order.Length - 1; i > 0; i--)
                    {
                        int j = random.Next(i + 1);
                        (order[i], order[j]) = (order[j], order[i]);
                    }
                    index = 0;
                }
                var direction = order[index++];

                string before = driver.Board();
                int scoreBefore = driver.Score, movesBefore = driver.MoveCount;
                yield return driver.Move(direction);
                string after = driver.Board();
                string context = $"move {move} ({direction})\n  before: {before}\n  after:  {after}";

                Assert.AreEqual(movesBefore + 1, driver.MoveCount, "The move was counted. " + context);
                CheckIndicator(before, after, context);
                int delta = driver.Score - scoreBefore;
                Assert.GreaterOrEqual(delta, 0, "The score never falls. " + context);
                Assert.AreEqual(0, delta % 10, "A move scores a sum of tier points, so a multiple of 10. " + context);
                bool expectedOver = IsGameOver(after, rules);
                Assert.AreEqual(expectedOver, driver.IsGameOver,
                    "The game is over exactly when the board is full with no merge possible. " + context);

                report.Moves = move;
                if (delta > 0) report.ScoreDeltas.Add(delta);
                foreach (var token in BoardNotation.Tokens(after))
                    if (token != "." && token != "i") report.BlocksSeen.Add(token);
            }
            report.ReachedGameOver = driver.IsGameOver;
            report.FinalScore = driver.Score;
        }

        static void CheckIndicator(string before, string after, string context)
        {
            var was = BoardNotation.Tokens(before);
            var now = BoardNotation.Tokens(after);
            int width = now.GetLength(0), height = now.GetLength(1);
            int indicators = 0, empty = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (was[x, y] == "i")
                        Assert.IsTrue(BoardNotation.IsResolvedPrimary(BoardNotation.ParseCell(now[x, y])),
                            $"The indicator at ({x},{y}) resolves in place to a Small Red, Green or Blue block, but the cell holds '{now[x, y]}'. " + context);
                    if (now[x, y] == "i") indicators++;
                    if (now[x, y] == ".") empty++;
                }
            Assert.LessOrEqual(indicators, 1, "At most one indicator. " + context);
            if (empty > 0)
                Assert.AreEqual(1, indicators, "A new indicator appears whenever a cell is empty. " + context);
        }

        /// <summary>True when the board has no empty cell and no block can merge into a neighbour under the rules.</summary>
        public static bool IsGameOver(string board, in GameRules rules)
        {
            var tokens = BoardNotation.Tokens(board);
            int width = tokens.GetLength(0), height = tokens.GetLength(1);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (tokens[x, y] == ".")
                        return false;

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var block = ToBlock(tokens[x, y]);
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        if (rules.CanMerge(block, ToBlock(tokens[nx, ny])))
                            return false;
                    }
                }
            return true;
        }

        static Simulation.Block ToBlock(string token)
        {
            var cell = BoardNotation.ParseCell(token);
            if (cell.IsIndicator) return Simulation.Block.Indicator;
            return Simulation.Block.Of(cell.Color, cell.Size);
        }
    }
}
