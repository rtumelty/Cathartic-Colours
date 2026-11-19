using NUnit.Framework;

namespace Simulation.Tests
{
    // Guards the enum values that save files and ScriptableObject assets serialize as integers.
    public class SimulationAssemblySmokeTests
    {
        [Test]
        public void BlockColor_ValuesAreStable()
        {
            Assert.AreEqual(typeof(byte), System.Enum.GetUnderlyingType(typeof(BlockColor)));
            Assert.AreEqual(0, (int)BlockColor.None);
            Assert.AreEqual(1, (int)BlockColor.Red);
            Assert.AreEqual(2, (int)BlockColor.Green);
            Assert.AreEqual(3, (int)BlockColor.Yellow);
            Assert.AreEqual(4, (int)BlockColor.Blue);
            Assert.AreEqual(5, (int)BlockColor.Magenta);
            Assert.AreEqual(6, (int)BlockColor.Cyan);
            Assert.AreEqual(7, (int)BlockColor.White);
        }

        [Test]
        public void BlockSize_ValuesAreStable()
        {
            Assert.AreEqual(typeof(byte), System.Enum.GetUnderlyingType(typeof(BlockSize)));
            Assert.AreEqual(0, (int)BlockSize.None);
            Assert.AreEqual(1, (int)BlockSize.Small);
            Assert.AreEqual(2, (int)BlockSize.Medium);
            Assert.AreEqual(3, (int)BlockSize.Large);
        }

        [Test]
        public void GameMode_ValuesAreStable()
        {
            Assert.AreEqual(typeof(byte), System.Enum.GetUnderlyingType(typeof(GameMode)));
            Assert.AreEqual(0, (int)GameMode.Standard);
            Assert.AreEqual(1, (int)GameMode.ColorMerge);
            Assert.AreEqual(2, (int)GameMode.AdvancedColorMerge);
        }

        [Test]
        public void ScoreTier_ValuesAreStable()
        {
            Assert.AreEqual(typeof(int), System.Enum.GetUnderlyingType(typeof(ScoreTier)));
            Assert.AreEqual(0, (int)ScoreTier.None);
            Assert.AreEqual(1, (int)ScoreTier.Tier1);
            Assert.AreEqual(2, (int)ScoreTier.Tier2);
            Assert.AreEqual(3, (int)ScoreTier.Tier3);
            Assert.AreEqual(4, (int)ScoreTier.Tier4);
        }
    }
}
