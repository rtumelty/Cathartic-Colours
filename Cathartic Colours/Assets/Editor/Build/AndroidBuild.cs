using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CatharticColours.Build
{
    /// <summary>
    /// Command-line entry point for Android builds. Run it through tools/build-android.ps1, or with
    /// <c>-executeMethod CatharticColours.Build.AndroidBuild.CommandLine -versionCode &lt;n&gt; [-buildProfile &lt;name&gt;]</c>.
    /// The profile defaults to Development; "Release" selects the release profile, and any other name is the asset
    /// name of a build profile in Assets/Settings/Build Profiles. Whether the output is an APK or an AAB, and whether
    /// it is named "dev" or "release", comes from the profile's own settings.
    /// Exit codes: 0 success, 1 build failed, 2 bad arguments, 3 build profile missing.
    /// </summary>
    public static class AndroidBuild
    {
        /// <summary>Back-compat entry point. Does the same as <see cref="CommandLine"/>.</summary>
        public static void Development()
        {
            CommandLine();
        }

        public static void CommandLine()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                if (!AndroidBuildArgs.TryParseVersionCode(args, out int versionCode, out string error)
                    || !AndroidBuildArgs.TryParseProfile(args, out string profileName, out error))
                {
                    Debug.LogError(error);
                    EditorApplication.Exit(2);
                    return;
                }

                string profilePath = AndroidBuildArgs.ProfileAssetPath(profileName);
                var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
                if (profile == null)
                {
                    Debug.LogError($"Build profile '{profileName}' not found at '{profilePath}'.");
                    EditorApplication.Exit(3);
                    return;
                }

                EditorApplication.Exit(Build(profile, profileName, versionCode));
            }
            catch (ArgumentException e)
            {
                Debug.LogError(e.Message);
                EditorApplication.Exit(2);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static int Build(BuildProfile profile, string profileName, int versionCode)
        {
            ReadProfileSettings(profile, profileName, out bool development, out bool appBundle);

            // Setting the version code rewrites ProjectSettings.asset, so the old value is put back afterwards
            // to leave the working tree clean.
            int previousVersionCode = PlayerSettings.Android.bundleVersionCode;
            try
            {
                PlayerSettings.Android.bundleVersionCode = versionCode;

                string projectDir = Directory.GetParent(Application.dataPath).FullName;
                string outputDir = Path.Combine(projectDir, "Builds", "Android");
                Directory.CreateDirectory(outputDir);
                string outputPath = Path.Combine(outputDir, AndroidBuildArgs.OutputFileName(PlayerSettings.bundleVersion, versionCode, development, appBundle));

                var options = new BuildPlayerWithProfileOptions
                {
                    buildProfile = profile,
                    locationPathName = outputPath,
                    options = BuildOptions.None,
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                Debug.Log($"Android build {summary.result}: {summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalSize} bytes, {summary.totalTime}.");

                if (summary.result != BuildResult.Succeeded)
                {
                    foreach (var step in report.steps)
                    {
                        foreach (var message in step.messages)
                        {
                            if (message.type == LogType.Error || message.type == LogType.Exception)
                                Debug.LogError(message.content);
                        }
                    }
                    return 1;
                }

                Debug.Log($"{(appBundle ? "AAB" : "APK")}: {outputPath}");
                return 0;
            }
            finally
            {
                PlayerSettings.Android.bundleVersionCode = previousVersionCode;
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// Reads the development and app bundle flags from the profile asset without changing it. In the asset
        /// they are the nested integers m_Development and m_BuildAppBundle. If one is missing, development falls
        /// back to whether this is the default profile, and app bundle to false.
        /// </summary>
        static void ReadProfileSettings(BuildProfile profile, string profileName, out bool development, out bool appBundle)
        {
            bool? foundDevelopment = null;
            bool? foundAppBundle = null;

            using (var serialized = new SerializedObject(profile))
            {
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (foundDevelopment == null && property.name == "m_Development" && IsBoolOrInt(property))
                        foundDevelopment = ReadFlag(property);
                    else if (foundAppBundle == null && property.name == "m_BuildAppBundle" && IsBoolOrInt(property))
                        foundAppBundle = ReadFlag(property);

                    if (foundDevelopment != null && foundAppBundle != null)
                        break;
                }
            }

            if (foundDevelopment == null)
                Debug.LogWarning($"m_Development was not found in build profile '{profileName}'; assuming it is {(IsDefaultProfile(profileName) ? "a" : "not a")} development build.");
            if (foundAppBundle == null)
                Debug.LogWarning($"m_BuildAppBundle was not found in build profile '{profileName}'; assuming an APK.");

            development = foundDevelopment ?? IsDefaultProfile(profileName);
            appBundle = foundAppBundle ?? false;
        }

        static bool IsDefaultProfile(string profileName)
        {
            return string.Equals(profileName, AndroidBuildArgs.DefaultProfile, StringComparison.OrdinalIgnoreCase);
        }

        static bool IsBoolOrInt(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.Boolean
                || property.propertyType == SerializedPropertyType.Integer;
        }

        static bool ReadFlag(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.Boolean
                ? property.boolValue
                : property.intValue != 0;
        }
    }
}
