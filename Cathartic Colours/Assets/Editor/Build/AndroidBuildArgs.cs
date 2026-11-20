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
        public const string BuildProfileFlag = "-buildProfile";

        /// <summary>The build profile used when <see cref="BuildProfileFlag"/> is not passed.</summary>
        public const string DefaultProfile = "Development";

        const string DevelopmentProfileAssetName = "Android\u2122 Development";
        const string ReleaseProfileAssetName = "Android\u2122";

        /// <summary>Google Play's maximum versionCode.</summary>
        public const int MaxVersionCode = 2100000000;

        public static bool TryParseVersionCode(string[] args, out int versionCode, out string error)
        {
            versionCode = 0;
            error = null;

            int index = -1;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (string.Equals(args[i], VersionCodeFlag, StringComparison.Ordinal))
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index < 0)
            {
                error = $"Missing required argument {VersionCodeFlag} <number>.";
                return false;
            }

            if (index + 1 >= args.Length)
            {
                error = $"Argument {VersionCodeFlag} needs a value, for example {VersionCodeFlag} 42.";
                return false;
            }

            string text = args[index + 1];
            if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int parsed))
            {
                error = $"{VersionCodeFlag} must be a whole number between 1 and {MaxVersionCode}, but got '{text}'.";
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

        /// <summary>
        /// Reads the optional <see cref="BuildProfileFlag"/> argument. When the flag is absent the profile is
        /// <see cref="DefaultProfile"/>. A flag without a value, or with an empty one, is an error.
        /// </summary>
        public static bool TryParseProfile(string[] args, out string profileName, out string error)
        {
            profileName = DefaultProfile;
            error = null;

            int index = -1;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (string.Equals(args[i], BuildProfileFlag, StringComparison.Ordinal))
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index < 0)
                return true;

            if (index + 1 >= args.Length)
            {
                error = $"Argument {BuildProfileFlag} needs a value, for example {BuildProfileFlag} Release.";
                return false;
            }

            string text = args[index + 1];
            if (string.IsNullOrWhiteSpace(text))
            {
                error = $"Argument {BuildProfileFlag} needs a non-empty profile name, for example {BuildProfileFlag} Release.";
                return false;
            }

            profileName = text.Trim();
            return true;
        }

        /// <summary>
        /// Maps a profile name to its asset path. "Development" and "Release" (any case) are aliases for the
        /// project's Android build profiles; any other name is used as the asset name.
        /// </summary>
        public static string ProfileAssetPath(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
                throw new ArgumentException("The profile name must not be null or empty.", nameof(profileName));

            string assetName;
            if (string.Equals(profileName, "Development", StringComparison.OrdinalIgnoreCase))
                assetName = DevelopmentProfileAssetName;
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
