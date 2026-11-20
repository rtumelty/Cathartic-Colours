// CC-025: GameManager.Initialize rejects a missing configuration or colour profile without replacing the current state.
using System.Text.RegularExpressions;
using Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ECS.Tests
{
    public class GameManagerTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var o in Resources.FindObjectsOfTypeAll<GameConfiguration>())
                if (!UnityEditor.EditorUtility.IsPersistent(o)) Object.DestroyImmediate(o);
            foreach (var o in Resources.FindObjectsOfTypeAll<ColorProfile>())
                if (!UnityEditor.EditorUtility.IsPersistent(o)) Object.DestroyImmediate(o);
        }

        static GameConfiguration NewConfig(int width)
        {
            var config = ScriptableObject.CreateInstance<GameConfiguration>();
            config.gridWidth = width;
            return config;
        }

        static ColorProfile NewProfile(string name)
        {
            var profile = ScriptableObject.CreateInstance<ColorProfile>();
            profile.name = name;
            return profile;
        }

        [Test]
        public void Initialize_WithANullColorProfile_LogsAnErrorAndKeepsTheCurrentState()
        {
            GameManager.Initialize(NewConfig(4), NewProfile("Before"));
            var configBefore = GameManager.ActiveConfiguration;
            var profileBefore = GameManager.ActiveColorProfile;

            LogAssert.Expect(LogType.Error, new Regex("null color profile"));
            Assert.DoesNotThrow(() => GameManager.Initialize(NewConfig(7), null));

            Assert.AreSame(configBefore, GameManager.ActiveConfiguration, "the configuration is not half-replaced");
            Assert.AreSame(profileBefore, GameManager.ActiveColorProfile);
        }

        [Test]
        public void Initialize_WithANullConfiguration_LogsAnErrorAndKeepsTheCurrentState()
        {
            GameManager.Initialize(NewConfig(4), NewProfile("Before"));
            var configBefore = GameManager.ActiveConfiguration;
            var profileBefore = GameManager.ActiveColorProfile;

            LogAssert.Expect(LogType.Error, new Regex("null config"));
            GameManager.Initialize(null, NewProfile("After"));

            Assert.AreSame(configBefore, GameManager.ActiveConfiguration);
            Assert.AreSame(profileBefore, GameManager.ActiveColorProfile);
        }

        [Test]
        public void Initialize_UsesRuntimeCopies()
        {
            var config = NewConfig(5);
            var profile = NewProfile("Profile");

            GameManager.Initialize(config, profile);

            Assert.AreNotSame(config, GameManager.ActiveConfiguration);
            Assert.AreEqual(5, GameManager.ActiveConfiguration.gridWidth);
            Assert.AreNotSame(profile, GameManager.ActiveColorProfile);
        }

        [Test]
        public void Initialize_SetsTheMenuAndActiveConfigurationToTheSameInstance()
        {
            GameManager.Initialize(NewConfig(5), NewProfile("Profile"));

            Assert.AreSame(GameManager.MenuConfiguration, GameManager.ActiveConfiguration);
            Assert.IsTrue(GameManager.IsInitialized);
        }

        [Test]
        public void StartResumedGame_ChangesTheActiveConfigurationAndProfileButNotTheMenuConfiguration()
        {
            GameManager.Initialize(NewConfig(4), NewProfile("Menu"));
            var menu = GameManager.MenuConfiguration;
            var profileBefore = GameManager.ActiveColorProfile;
            var resumedProfile = NewProfile("Resumed");

            GameManager.StartResumedGame(NewConfig(8), resumedProfile);

            Assert.AreSame(menu, GameManager.MenuConfiguration);
            Assert.AreEqual(4, GameManager.MenuConfiguration.gridWidth);
            Assert.AreNotSame(menu, GameManager.ActiveConfiguration);
            Assert.AreEqual(8, GameManager.ActiveConfiguration.gridWidth);
            Assert.AreNotSame(profileBefore, GameManager.ActiveColorProfile);
            Assert.AreNotSame(resumedProfile, GameManager.ActiveColorProfile, "a runtime copy");
            StringAssert.StartsWith("Resumed", GameManager.ActiveColorProfile.name);
        }

        [Test]
        public void UseMenuConfiguration_SwitchesTheActiveConfigurationBackToTheMenuConfiguration()
        {
            GameManager.Initialize(NewConfig(4), NewProfile("Menu"));
            GameManager.StartResumedGame(NewConfig(8), NewProfile("Resumed"));

            GameManager.UseMenuConfiguration();

            Assert.AreSame(GameManager.MenuConfiguration, GameManager.ActiveConfiguration);
            Assert.AreEqual(4, GameManager.ActiveConfiguration.gridWidth);
        }

        [Test]
        public void StartResumedGame_WithANullConfiguration_LogsAnErrorAndChangesNothing()
        {
            GameManager.Initialize(NewConfig(4), NewProfile("Menu"));
            var menu = GameManager.MenuConfiguration;
            var active = GameManager.ActiveConfiguration;
            var profileBefore = GameManager.ActiveColorProfile;

            LogAssert.Expect(LogType.Error, new Regex("null config"));
            GameManager.StartResumedGame(null, NewProfile("Resumed"));

            Assert.AreSame(menu, GameManager.MenuConfiguration);
            Assert.AreSame(active, GameManager.ActiveConfiguration);
            Assert.AreSame(profileBefore, GameManager.ActiveColorProfile);
        }

        [Test]
        public void StartResumedGame_WithANullColorProfile_LogsAnErrorAndChangesNothing()
        {
            GameManager.Initialize(NewConfig(4), NewProfile("Menu"));
            var menu = GameManager.MenuConfiguration;
            var active = GameManager.ActiveConfiguration;
            var profileBefore = GameManager.ActiveColorProfile;

            LogAssert.Expect(LogType.Error, new Regex("null color profile"));
            GameManager.StartResumedGame(NewConfig(8), null);

            Assert.AreSame(menu, GameManager.MenuConfiguration);
            Assert.AreSame(active, GameManager.ActiveConfiguration);
            Assert.AreSame(profileBefore, GameManager.ActiveColorProfile);
        }
    }
}
