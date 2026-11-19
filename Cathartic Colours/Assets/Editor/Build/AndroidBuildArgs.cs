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

        public static string OutputFileName(string versionName, int versionCode)
        {
            if (string.IsNullOrWhiteSpace(versionName))
                throw new ArgumentException("The version name must not be null or empty.", nameof(versionName));

            if (versionName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException(
                    $"The version name '{versionName}' contains characters that are not valid in a file name.",
                    nameof(versionName));

            return $"CatharticColours-{versionName}-{versionCode}-dev.apk";
        }
    }
}
