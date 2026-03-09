using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WebGLBuildRunner
{
    private const string DevelopmentOutputPath = "Builds/WebGL/dev";
    private const string ReleaseCandidateOutputPath = "Builds/WebGL/rc";
    private const string PreferredMainScene = "Assets/x-con_ufo_defence.unity";

    public static void BuildDevelopmentWebGL()
    {
        BuildWebGL(DevelopmentOutputPath, BuildOptions.Development | BuildOptions.AllowDebugging, "dev");
    }

    public static void BuildReleaseCandidateWebGL()
    {
        BuildWebGL(ReleaseCandidateOutputPath, BuildOptions.None, "release-candidate");
    }

    private static void BuildWebGL(string outputPath, BuildOptions buildOptions, string buildLabel)
    {
        var scenes = ResolveScenesForBuild();

        Directory.CreateDirectory(outputPath);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            options = buildOptions
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        Debug.Log($"WebGL {buildLabel} build result: {summary.result}, warnings: {summary.totalWarnings}, errors: {summary.totalErrors}, output: {summary.outputPath}");

        if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new Exception($"WebGL {buildLabel} build failed with result: {summary.result}");
        }
    }

    private static string[] ResolveScenesForBuild()
    {
        var enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();

        if (enabledScenes.Length == 0)
        {
            if (File.Exists(PreferredMainScene))
            {
                Debug.LogWarning($"No enabled scenes found in Build Settings. Falling back to {PreferredMainScene}.");
                return new[] { PreferredMainScene };
            }

            var fallbackScene = Directory
                .GetFiles("Assets", "*.unity", SearchOption.AllDirectories)
                .OrderBy(path => path)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(fallbackScene))
            {
                Debug.LogWarning($"No enabled scenes found in Build Settings. Falling back to discovered scene {fallbackScene}.");
                return new[] { fallbackScene };
            }

            throw new InvalidOperationException("No enabled scenes found in Build Settings and no fallback scene discovered under Assets.");
        }

        return enabledScenes;
    }
}
