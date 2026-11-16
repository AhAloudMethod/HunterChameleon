using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class InputLoggingReleaseBuild
{
    public static void Build()
    {
        InputRecordingChecks.Run();
        string path = Environment.GetEnvironmentVariable("OVGL_INPUT_BUILD_PATH");
        if (string.IsNullOrEmpty(path)) throw new BuildFailedException("Set OVGL_INPUT_BUILD_PATH.");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = path, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
        if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Windows build failed.");
        Debug.Log("INPUT_LOGGING_BUILD_SUCCEEDED " + path);
    }
}
