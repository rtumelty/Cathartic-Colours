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
        public void ProfileDefaultsWhenFlagMissing()
        {
            Assert.IsTrue(AndroidBuildArgs.TryParseProfile(new[] { "-batchmode" }, out string name, out string error));
            Assert.AreEqual(AndroidBuildArgs.DefaultProfile, name);
            Assert.IsNull(error);
        }

        [Test]
        public void ProfileDefaultsOnNullArgs()
        {
            Assert.IsTrue(AndroidBuildArgs.TryParseProfile(null, out string name, out _));
            Assert.AreEqual(AndroidBuildArgs.DefaultProfile, name);
        }

        [Test]
        public void ParsesProfileValue()
        {
            Assert.IsTrue(AndroidBuildArgs.TryParseProfile(new[] { "-buildProfile", "Release" }, out string name, out string error));
            Assert.AreEqual("Release", name);
            Assert.IsNull(error);
        }

        [Test]
        public void TrimsProfileValue()
        {
            Assert.IsTrue(AndroidBuildArgs.TryParseProfile(new[] { "-buildProfile", "  Release " }, out string name, out _));
            Assert.AreEqual("Release", name);
        }

        [Test]
        public void ParsesProfileAmongOtherArgs()
        {
            var args = new[] { "Unity.exe", "-batchmode", "-versionCode", "7", "-buildProfile", "Release", "-quit" };
            Assert.IsTrue(AndroidBuildArgs.TryParseProfile(args, out string name, out _));
            Assert.AreEqual("Release", name);
        }

        [Test]
        public void ProfileFailsWhenFlagHasNoValue()
        {
            Assert.IsFalse(AndroidBuildArgs.TryParseProfile(new[] { "-batchmode", "-buildProfile" }, out _, out string error));
            StringAssert.Contains("needs a value", error);
        }

        [TestCase("")]
        [TestCase("   ")]
        public void ProfileFailsWhenValueEmpty(string value)
        {
            Assert.IsFalse(AndroidBuildArgs.TryParseProfile(new[] { "-buildProfile", value }, out _, out string error));
            StringAssert.Contains("non-empty", error);
        }

        [Test]
        public void ProfileAssetPathMapsDevelopmentAlias()
        {
            Assert.AreEqual("Assets/Settings/Build Profiles/Android\u2122 Development.asset", AndroidBuildArgs.ProfileAssetPath("Development"));
        }

        [Test]
        public void ProfileAssetPathMapsReleaseAlias()
        {
            Assert.AreEqual("Assets/Settings/Build Profiles/Android\u2122.asset", AndroidBuildArgs.ProfileAssetPath("Release"));
        }

        [TestCase("release")]
        [TestCase("RELEASE")]
        public void ProfileAssetPathAliasIsCaseInsensitive(string alias)
        {
            Assert.AreEqual("Assets/Settings/Build Profiles/Android\u2122.asset", AndroidBuildArgs.ProfileAssetPath(alias));
            Assert.AreEqual("Assets/Settings/Build Profiles/Android\u2122 Development.asset", AndroidBuildArgs.ProfileAssetPath(alias == "release" ? "development" : "DEVELOPMENT"));
        }

        [Test]
        public void ProfileAssetPathPassesCustomNameThrough()
        {
            Assert.AreEqual("Assets/Settings/Build Profiles/My Profile.asset", AndroidBuildArgs.ProfileAssetPath("My Profile"));
        }

        [TestCase("a/b")]
        [TestCase("..\\x")]
        public void ProfileAssetPathRejectsPathSeparators(string name)
        {
            Assert.Throws<ArgumentException>(() => AndroidBuildArgs.ProfileAssetPath(name));
        }

        [Test]
        public void OutputFileNameHasExpectedFormatForDevelopmentApk()
        {
            Assert.AreEqual("CatharticColours-0.1.0-42-dev.apk", AndroidBuildArgs.OutputFileName("0.1.0", 42, true, false));
        }

        [Test]
        public void OutputFileNameHasExpectedFormatForReleaseBundle()
        {
            Assert.AreEqual("CatharticColours-0.1.0-42-release.aab", AndroidBuildArgs.OutputFileName("0.1.0", 42, false, true));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("1/2")]
        [TestCase("a:b")]
        public void OutputFileNameRejectsBadVersionName(string versionName)
        {
            Assert.Throws<ArgumentException>(() => AndroidBuildArgs.OutputFileName(versionName, 1, true, false));
        }
    }
}
