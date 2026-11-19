using System;
using NUnit.Framework;

namespace CatharticColours.Build.Tests
{
    public class AndroidBuildArgsTests
    {
        [Test]
        public void ParsesValidVersionCode()
        {
            bool ok = AndroidBuildArgs.TryParseVersionCode(new[] { "-versionCode", "42" }, out int code, out string error);
            Assert.IsTrue(ok);
            Assert.AreEqual(42, code);
            Assert.IsNull(error);
        }

        [Test]
        public void ParsesVersionCodeAmongOtherArgs()
        {
            var args = new[] { "Unity.exe", "-batchmode", "-versionCode", "7", "-quit" };
            Assert.IsTrue(AndroidBuildArgs.TryParseVersionCode(args, out int code, out _));
            Assert.AreEqual(7, code);
        }

        [Test]
        public void AcceptsBoundaryValues()
        {
            Assert.IsTrue(AndroidBuildArgs.TryParseVersionCode(new[] { "-versionCode", "1" }, out int low, out _));
            Assert.AreEqual(1, low);
            Assert.IsTrue(AndroidBuildArgs.TryParseVersionCode(new[] { "-versionCode", "2100000000" }, out int high, out _));
            Assert.AreEqual(2100000000, high);
        }

        [Test]
        public void FailsWhenFlagMissing()
        {
            Assert.IsFalse(AndroidBuildArgs.TryParseVersionCode(new[] { "-batchmode" }, out _, out string error));
            StringAssert.Contains("Missing", error);
        }

        [Test]
        public void FailsOnNullArgs()
        {
            Assert.IsFalse(AndroidBuildArgs.TryParseVersionCode(null, out _, out string error));
            StringAssert.Contains("Missing", error);
        }

        [Test]
        public void FailsWhenFlagHasNoValue()
        {
            Assert.IsFalse(AndroidBuildArgs.TryParseVersionCode(new[] { "-batchmode", "-versionCode" }, out _, out string error));
            StringAssert.Contains("needs a value", error);
        }

        [TestCase("abc")]
        [TestCase("1.5")]
        [TestCase("")]
        [TestCase("99999999999")]
        public void FailsWhenNotAnInteger(string value)
        {
            Assert.IsFalse(AndroidBuildArgs.TryParseVersionCode(new[] { "-versionCode", value }, out _, out string error));
            StringAssert.Contains("whole number", error);
        }

        [TestCase("0")]
        [TestCase("-5")]
        public void FailsWhenBelowOne(string value)
        {
            Assert.IsFalse(AndroidBuildArgs.TryParseVersionCode(new[] { "-versionCode", value }, out _, out string error));
            StringAssert.Contains("between 1 and", error);
        }

        [Test]
        public void FailsWhenAboveMax()
        {
            Assert.IsFalse(AndroidBuildArgs.TryParseVersionCode(new[] { "-versionCode", "2100000001" }, out _, out string error));
            StringAssert.Contains("between 1 and", error);
        }

        [Test]
        public void OutputFileNameHasExpectedFormat()
        {
            Assert.AreEqual("CatharticColours-0.1.0-42-dev.apk", AndroidBuildArgs.OutputFileName("0.1.0", 42));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("1/2")]
        [TestCase("a:b")]
        public void OutputFileNameRejectsBadVersionName(string versionName)
        {
            Assert.Throws<ArgumentException>(() => AndroidBuildArgs.OutputFileName(versionName, 1));
        }
    }
}
