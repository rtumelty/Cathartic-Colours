using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CatharticColours.Build
{
    /// <summary>
    /// Command-line entry point for Android development builds. Run it through tools/build-android.ps1, or with
    /// <c>-executeMethod CatharticColours.Build.AndroidBuild.Development -versionCode &lt;n&gt;</c>.
    /// Exit codes: 0 success, 1 build failed, 2 bad arguments, 3 build profile missing.
    /// </summary>
    public static class AndroidBuild
    {
        const string ProfilePath = "Assets/Settings/Build Profiles/Android™ Development.asset";

        public static void Development()
        {
            try
            {
                if (!AndroidBuildArgs.TryParseVersionCode(Environment.GetCommandLineArgs(), out int versionCode, out string error))
                {
                    Debug.LogError(error);
                    EditorApplication.Exit(2);
                    return;
                }

                var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(ProfilePath);
                if (profile == null)
                {
                    Debug.LogError($"Build profile not found at '{ProfilePath}'.");
                    EditorApplication.Exit(3);
                    return;
                }

                EditorApplication.Exit(Build(profile, versionCode));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static int Build(BuildProfile profile, int versionCode)
        {
            // Setting the version code rewrites ProjectSettings.asset, so the old value is put back afterwards
            // to leave the working tree clean.
            int previousVersionCode = PlayerSettings.Android.bundleVersionCode;
            try
            {
                PlayerSettings.Android.bundleVersionCode = versionCode;

                string projectDir = Directory.GetParent(Application.dataPath).FullName;
                string outputDir = Path.Combine(projectDir, "Builds", "Android");
                Directory.CreateDirectory(outputDir);
                string outputPath = Path.Combine(outputDir, AndroidBuildArgs.OutputFileName(PlayerSettings.bundleVersion, versionCode));

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

                Debug.Log($"APK: {outputPath}");
                return 0;
            }
            finally
            {
                PlayerSettings.Android.bundleVersionCode = previousVersionCode;
                AssetDatabase.SaveAssets();
            }
        }
    }
}
