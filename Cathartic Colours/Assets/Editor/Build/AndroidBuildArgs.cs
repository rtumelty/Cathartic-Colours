using System;
using System.Globalization;
using System.IO;

namespace CatharticColours.Build
{
    /// <summary>
    /// Pure helpers for the command-line Android build: argument parsing and output naming.
    /// Kept free of UnityEditor calls so they can be unit-tested.
    /// </summary>
    public static class AndroidBuildArgs
    {
        public const string VersionCodeFlag = "-versionCode";
        public const string VersionBaseFlag = "-versionBase";
        public const string VersionAheadFlag = "-versionAhead";
        public const string BuildProfileFlag = "-buildProfile";

        /// <summary>The build profile used when <see cref="BuildProfileFlag"/> is not passed.</summary>
        public const string DefaultProfile = "Development";

        const string DevelopmentProfileAssetName = "Android\u2122 Development";
        const string DebuggableProfileAssetName = "Android\u2122 Development (Debuggable)";
        const string ReleaseProfileAssetName = "Android\u2122";

        /// <summary>Google Play's maximum versionCode.</summary>
        public const int MaxVersionCode = 2100000000;

        /// <summary>Most commits a branch can be ahead of develop and still get a version code.</summary>
        public const int MaxVersionAhead = 99;

        /// <summary>Multiplier applied to the develop base commit count; leaves room for <see cref="MaxVersionAhead"/>.</summary>
        public const int VersionBaseMultiplier = 100;

        /// <summary>
        /// Reads the Android version code from the command line. An explicit <see cref="VersionCodeFlag"/> wins
        /// and is used as given. Otherwise <see cref="VersionBaseFlag"/> and <see cref="VersionAheadFlag"/> must
        /// both be present and are combined by <see cref="TryComposeVersionCode"/>.
        /// </summary>
        public static bool TryParseVersionCode(string[] args, out int versionCode, out string error)
        {
            versionCode = 0;
            error = null;

            int codeIndex = FindFlag(args, VersionCodeFlag);
            if (codeIndex >= 0)
            {
                if (!TryReadValue(args, codeIndex, VersionCodeFlag, "42", out string codeText, out error))
                    return false;

                if (!int.TryParse(codeText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int parsed))
                {
                    error = $"{VersionCodeFlag} must be a whole number between 1 and {MaxVersionCode}, but got '{codeText}'.";
                    return false;
                }

                if (parsed < 1 || parsed > MaxVersionCode)
                {
                    error = $"{VersionCodeFlag} must be between 1 and {MaxVersionCode}, but got {parsed}.";
                    return false;
                }

                versionCode = parsed;
                return true;
            }

            int baseIndex = FindFlag(args, VersionBaseFlag);
            int aheadIndex = FindFlag(args, VersionAheadFlag);
            if (baseIndex < 0 && aheadIndex < 0)
            {
                error = $"Missing required argument {VersionCodeFlag} <number> (or {VersionBaseFlag} <number> {VersionAheadFlag} <number>).";
                return false;
            }

            if (baseIndex < 0)
            {
                error = $"Missing required argument {VersionBaseFlag} <number> (needed together with {VersionAheadFlag}).";
                return false;
            }

            if (aheadIndex < 0)
            {
                error = $"Missing required argument {VersionAheadFlag} <number> (needed together with {VersionBaseFlag}).";
                return false;
            }

            if (!TryReadWholeNumber(args, baseIndex, VersionBaseFlag, "110", out long versionBase, out error))
                return false;
            if (!TryReadWholeNumber(args, aheadIndex, VersionAheadFlag, "7", out long ahead, out error))
                return false;

            return TryComposeVersionCode(versionBase, ahead, out versionCode, out error);
        }

        /// <summary>
        /// Builds a version code that never goes backwards across squash merges:
        /// <c>versionBase * 100 + ahead</c>, where <paramref name="versionBase"/> is the number of commits up to
        /// the point where the build's branch left develop and <paramref name="ahead"/> is the number of commits
        /// on the branch since then (0 on develop). The result must be between 1 and <see cref="MaxVersionCode"/>.
        /// </summary>
        public static bool TryComposeVersionCode(long versionBase, long ahead, out int versionCode, out string error)
        {
            versionCode = 0;
            error = null;

            if (versionBase < 0)
            {
                error = $"{VersionBaseFlag} must not be negative, but got {versionBase}.";
                return false;
            }

            if (ahead < 0)
            {
                error = $"{VersionAheadFlag} must not be negative, but got {ahead}.";
                return false;
            }

            if (ahead > MaxVersionAhead)
            {
                error = $"The branch is too far ahead of develop: {ahead} commits, but at most {MaxVersionAhead} fit in the version code. Rebase onto or merge develop first.";
                return false;
            }

            // Check the base before multiplying so a huge value can't overflow long.
            if (versionBase > MaxVersionCode / VersionBaseMultiplier)
            {
                error = $"The version code from {VersionBaseFlag} {versionBase} would exceed {MaxVersionCode}.";
                return false;
            }

            long composed = versionBase * VersionBaseMultiplier + ahead;
            if (composed < 1 || composed > MaxVersionCode)
            {
                error = $"The version code {composed} (from {VersionBaseFlag} {versionBase} and {VersionAheadFlag} {ahead}) must be between 1 and {MaxVersionCode}.";
                return false;
            }

            versionCode = (int)composed;
            return true;
        }

        /// <summary>Index of the first argument equal to <paramref name="flag"/> (ordinal), or -1.</summary>
        static int FindFlag(string[] args, string flag)
        {
            if (args == null)
                return -1;

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        static bool TryReadValue(string[] args, int flagIndex, string flag, string example, out string value, out string error)
        {
            value = null;
            error = null;

            if (flagIndex + 1 >= args.Length)
            {
                error = $"Argument {flag} needs a value, for example {flag} {example}.";
                return false;
            }

            value = args[flagIndex + 1];
            return true;
        }

        static bool TryReadWholeNumber(string[] args, int flagIndex, string flag, string example, out long number, out string error)
        {
            number = 0;
            if (!TryReadValue(args, flagIndex, flag, example, out string text, out error))
                return false;

            if (!long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number))
            {
                error = $"{flag} must be a whole number, but got '{text}'.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Reads the optional <see cref="BuildProfileFlag"/> argument. When the flag is absent the profile is
        /// <see cref="DefaultProfile"/>. A flag without a value, or with an empty one, is an error.
        /// </summary>
        public static bool TryParseProfile(string[] args, out string profileName, out string error)
        {
            profileName = DefaultProfile;
            error = null;

            int index = FindFlag(args, BuildProfileFlag);
            if (index < 0)
                return true;

            if (!TryReadValue(args, index, BuildProfileFlag, "Release", out string text, out error))
                return false;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = $"Argument {BuildProfileFlag} needs a non-empty profile name, for example {BuildProfileFlag} Release.";
                return false;
            }

            profileName = text.Trim();
            return true;
        }

        /// <summary>
        /// Maps a profile name to its asset path. "Development", "Debuggable" and "Release" (any case) are aliases
        /// for the project's Android build profiles; any other name is used as the asset name.
        /// </summary>
        public static string ProfileAssetPath(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
                throw new ArgumentException("The profile name must not be null or empty.", nameof(profileName));

            string assetName;
            if (string.Equals(profileName, "Development", StringComparison.OrdinalIgnoreCase))
                assetName = DevelopmentProfileAssetName;
            else if (string.Equals(profileName, "Debuggable", StringComparison.OrdinalIgnoreCase))
                assetName = DebuggableProfileAssetName;
            else if (string.Equals(profileName, "Release", StringComparison.OrdinalIgnoreCase))
                assetName = ReleaseProfileAssetName;
            else
                assetName = profileName;

            if (assetName.IndexOfAny(new[] { '/', '\\' }) >= 0 || assetName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException(
                    $"The profile name '{profileName}' contains characters that are not valid in a file name.",
                    nameof(profileName));

            return $"Assets/Settings/Build Profiles/{assetName}.asset";
        }

        public static string OutputFileName(string versionName, int versionCode, bool development, bool appBundle)
        {
            if (string.IsNullOrWhiteSpace(versionName))
                throw new ArgumentException("The version name must not be null or empty.", nameof(versionName));

            if (versionName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException(
                    $"The version name '{versionName}' contains characters that are not valid in a file name.",
                    nameof(versionName));

            return $"CatharticColours-{versionName}-{versionCode}-{(development ? "dev" : "release")}.{(appBundle ? "aab" : "apk")}";
        }
    }
}
