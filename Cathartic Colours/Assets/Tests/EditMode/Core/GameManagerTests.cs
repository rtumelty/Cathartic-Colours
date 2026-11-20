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
    }
}
