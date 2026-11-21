// CC-008.3: GameRulesComponent.From is the one place a game's rules are built, for new and resumed games alike
// (GameRenderer.InitializeGame calls it with GameManager.ActiveConfiguration). GameRenderer itself is a
// MonoBehaviour and is not exercised here; these tests cover the builder and the configuration a resume produces.
// The resume tests save to a temp folder through SaveGameManager.SaveFilePathOverride, never the real save.
using System;
using System.IO;
using Data;
using ECS.Components;
using NUnit.Framework;
using SaveSystem;
using Simulation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ECS.Tests
{
    public class GameRulesComponentTests
    {
        string folder;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "cc-rules-tests", Guid.NewGuid().ToString("N"));
            SaveGameManager.SaveFilePathOverride = Path.Combine(folder, "savegame.json");
        }

        [TearDown]
        public void TearDown()
        {
            SaveGameManager.SaveFilePathOverride = null;
            foreach (var o in Resources.FindObjectsOfTypeAll<GameConfiguration>())
                if (!UnityEditor.EditorUtility.IsPersistent(o)) Object.DestroyImmediate(o);
            foreach (var o in Resources.FindObjectsOfTypeAll<ColorProfile>())
                if (!UnityEditor.EditorUtility.IsPersistent(o)) Object.DestroyImmediate(o);
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        static GameConfiguration NewConfig(GameMode mode, bool spawn, int t1 = 10, int t2 = 50, int t3 = 100, int t4 = 200)
        {
            var config = ScriptableObject.CreateInstance<GameConfiguration>();
            config.gameMode = mode;
            config.spawnNextColourSystem = spawn;
            config.tier1Points = t1;
            config.tier2Points = t2;
            config.tier3Points = t3;
            config.tier4Points = t4;
            return config;
        }

        [TestCase(GameMode.Standard, MergeRuleKind.Standard)]
        [TestCase(GameMode.ColorMerge, MergeRuleKind.ColorMerge)]
        [TestCase(GameMode.AdvancedColorMerge, MergeRuleKind.AdvancedColorMerge)]
        public void From_MapsTheModeToItsMergeRule(GameMode mode, MergeRuleKind expected)
        {
            var rules = GameRulesComponent.From(NewConfig(mode, true)).Rules;

            Assert.AreEqual(expected, rules.Merge);
        }

        [TestCase(true, SpawnRuleKind.UniformPrimary)]
        [TestCase(false, SpawnRuleKind.None)]
        public void From_MapsTheSpawnToggleToTheSpawnRule(bool spawn, SpawnRuleKind expected)
        {
            var rules = GameRulesComponent.From(NewConfig(GameMode.Standard, spawn)).Rules;

            Assert.AreEqual(expected, rules.Spawn);
        }

        [Test]
        public void From_CopiesTheTierPoints()
        {
            var rules = GameRulesComponent.From(NewConfig(GameMode.Standard, true, 11, 22, 33, 44)).Rules;

            Assert.AreEqual(11, rules.Points.Tier1);
            Assert.AreEqual(22, rules.Points.Tier2);
            Assert.AreEqual(33, rules.Points.Tier3);
            Assert.AreEqual(44, rules.Points.Tier4);
        }

        [Test]
        public void From_MatchesGameRulesForWithTheSameSettings()
        {
            var settings = new StepSettings { Points = new TierPoints { Tier1 = 1, Tier2 = 2, Tier3 = 3, Tier4 = 4 }, SpawnEnabled = false };

            var rules = GameRulesComponent.From(NewConfig(GameMode.ColorMerge, false, 1, 2, 3, 4)).Rules;

            AssertSameRules(GameRules.For(GameMode.ColorMerge, settings), rules);
        }

        [TestCase(GameMode.Standard, true)]
        [TestCase(GameMode.Standard, false)]
        [TestCase(GameMode.ColorMerge, true)]
        [TestCase(GameMode.ColorMerge, false)]
        [TestCase(GameMode.AdvancedColorMerge, true)]
        [TestCase(GameMode.AdvancedColorMerge, false)]
        public void ResumedGame_GetsTheSameRulesAsANewGameWithTheSameModeAndToggle(GameMode mode, bool spawn)
        {
            // A saved game: the save holds the mode and the toggle, the tier points come from the template.
            GameManager.Initialize(NewConfig(mode, spawn, 7, 14, 21, 28), NewNamedProfile("ProfileA"));
            var saveData = new SaveGameData { gameMode = mode, gridWidth = 4, gridHeight = 4, spawnNextColourSystem = spawn };
            var template = NewConfig(GameMode.Standard, !spawn, 7, 14, 21, 28);

            SaveGameManager.InitializeFromSave(saveData, template, NewNamedProfile("ProfileA"));
            var resumed = GameRulesComponent.From(GameManager.ActiveConfiguration).Rules;

            var newGame = GameRulesComponent.From(NewConfig(mode, spawn, 7, 14, 21, 28)).Rules;
            AssertSameRules(newGame, resumed);
        }

        static ColorProfile NewNamedProfile(string name)
        {
            var profile = ScriptableObject.CreateInstance<ColorProfile>();
            profile.name = name;
            return profile;
        }

        static void AssertSameRules(GameRules expected, GameRules actual)
        {
            Assert.AreEqual(expected.Merge, actual.Merge, "Merge");
            Assert.AreEqual(expected.Spawn, actual.Spawn, "Spawn");
            Assert.AreEqual(expected.Scoring, actual.Scoring, "Scoring");
            Assert.AreEqual(expected.End, actual.End, "End");
            Assert.AreEqual(expected.Grid, actual.Grid, "Grid");
            Assert.AreEqual(expected.Timer, actual.Timer, "Timer");
            Assert.AreEqual(expected.Points.Tier1, actual.Points.Tier1, "Tier1");
            Assert.AreEqual(expected.Points.Tier2, actual.Points.Tier2, "Tier2");
            Assert.AreEqual(expected.Points.Tier3, actual.Points.Tier3, "Tier3");
            Assert.AreEqual(expected.Points.Tier4, actual.Points.Tier4, "Tier4");
        }
    }
}
