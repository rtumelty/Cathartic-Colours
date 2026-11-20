// CC-030: one direction rule for keyboard, gamepad and swipe input.
using ECS.Utilities;
using NUnit.Framework;
using Unity.Mathematics;

namespace ECS.Tests
{
    public class InputDirectionTests
    {
        [Test]
        public void BelowDeadZone_GivesNoDirection()
        {
            Assert.AreEqual(int2.zero, InputDirection.Resolve(float2.zero));
            Assert.AreEqual(int2.zero, InputDirection.Resolve(new float2(0.4f, 0f)));
            Assert.AreEqual(int2.zero, InputDirection.Resolve(new float2(0.1f, -0.3f)));
        }

        [Test]
        public void AtDeadZone_GivesDirection()
        {
            Assert.AreEqual(new int2(1, 0), InputDirection.Resolve(new float2(InputDirection.DeadZone, 0f)));
        }

        [Test]
        public void DominantAxisWins()
        {
            Assert.AreEqual(new int2(1, 0), InputDirection.Resolve(new float2(0.9f, 0.4f)));
            Assert.AreEqual(new int2(-1, 0), InputDirection.Resolve(new float2(-0.9f, 0.4f)));
            Assert.AreEqual(new int2(0, 1), InputDirection.Resolve(new float2(0.4f, 0.9f)));
            Assert.AreEqual(new int2(0, -1), InputDirection.Resolve(new float2(0.4f, -0.9f)));
        }

        [Test]
        public void DiagonalTie_ResolvesToVertical()
        {
            Assert.AreEqual(new int2(0, 1), InputDirection.Resolve(new float2(0.707f, 0.707f)));
            Assert.AreEqual(new int2(0, -1), InputDirection.Resolve(new float2(-0.707f, -0.707f)));
        }

        [Test]
        public void NormalisedSwipe_AlwaysGivesDirection()
        {
            for (int deg = 0; deg < 360; deg += 5)
            {
                float rad = math.radians(deg);
                var dir = InputDirection.Resolve(new float2(math.cos(rad), math.sin(rad)));
                Assert.AreEqual(1, math.abs(dir.x) + math.abs(dir.y), "angle " + deg);
            }
        }
    }
}
