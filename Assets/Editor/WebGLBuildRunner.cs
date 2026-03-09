using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WebGLBuildRunner
{
    private const string DevelopmentOutputPath = "Builds/WebGL/dev";

    public static void BuildDevelopmentWebGL()
    {
        var enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (enabledScenes.Length == 0)
        {
            throw new InvalidOperationException("No enabled scenes found in Build Settings.");
        }

        Directory.CreateDirectory(DevelopmentOutputPath);

        var options = new BuildPlayerOptions
        {
            scenes = enabledScenes,
            locationPathName = DevelopmentOutputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.Development | BuildOptions.AllowDebugging
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        Debug.Log($"WebGL dev build result: {summary.result}, warnings: {summary.totalWarnings}, errors: {summary.totalErrors}, output: {summary.outputPath}");

        if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new Exception($"WebGL development build failed with result: {summary.result}");
        }
    }
}
