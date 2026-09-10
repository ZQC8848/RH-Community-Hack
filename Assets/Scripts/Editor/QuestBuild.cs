using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace RHCommunityHack.EditorTools
{
    // One-click Quest 3 APK. Exists so the build is reproducible from a menu (and from a script)
    // rather than from whatever the Build Settings window happened to hold last time.
    //
    // It builds the scenes in Build Settings for Android into Builds/, development build so the
    // player log reaches `adb logcat -s Unity` - which is how the video and XR start-up get
    // checked on the headset. Installing is `adb install -r <apk>`; it is deliberately not done
    // here, since a build with no headset attached is still a useful build.
    public static class QuestBuild
    {
        const string Output = "Builds/RHCommunityHack-quest3-dev.apk";

        [MenuItem("RH Community Hack/Build/Quest 3 APK (Development)")]
        public static void BuildDevelopment() => Build(BuildOptions.Development);

        [MenuItem("RH Community Hack/Build/Quest 3 APK (Release)")]
        public static void BuildRelease() => Build(BuildOptions.None);

        static void Build(BuildOptions options)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError("[QuestBuild] Active build target is not Android - switch platform first.");
                return;
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) { Debug.LogError("[QuestBuild] No scenes enabled in Build Settings."); return; }

            EditorUserBuildSettings.buildAppBundle = false;   // an .apk, not an .aab - sideloading wants the apk
            Directory.CreateDirectory("Builds");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Output,
                target = BuildTarget.Android,
                options = options,
            });

            var s = report.summary;
            if (s.result == BuildResult.Succeeded)
                Debug.Log($"[QuestBuild] OK {Output} - {s.totalSize / (1024f * 1024f):F0} MB in {s.totalTime.TotalMinutes:F1} min, " +
                          $"{s.totalWarnings} warnings. Install: adb install -r \"{Path.GetFullPath(Output)}\"");
            else
                Debug.LogError($"[QuestBuild] {s.result}: {s.totalErrors} errors - see the Editor log.");
        }
    }
}
