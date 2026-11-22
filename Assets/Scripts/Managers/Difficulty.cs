using System;
using UnityEngine;

public enum DifficultyLevel
{
    Easy = 0,
    Normal = 1,
    Hard = 2,
}

/// <summary>Tuning values for one difficulty level. All numbers live in <see cref="Difficulty.For"/>.</summary>
public readonly struct DifficultyPreset
{
    public DifficultyPreset(
        string label,
        float missionDurationSeconds,
        float responseTimeoutMultiplier,
        float spawnRateMultiplier,
        int consecutiveExpireFailThreshold,
        float permitRiskGainMultiplier,
        float softStartDurationSeconds,
        float scoreMultiplier)
    {
        Label = label;
        MissionDurationSeconds = missionDurationSeconds;
        ResponseTimeoutMultiplier = responseTimeoutMultiplier;
        SpawnRateMultiplier = spawnRateMultiplier;
        ConsecutiveExpireFailThreshold = consecutiveExpireFailThreshold;
        PermitRiskGainMultiplier = permitRiskGainMultiplier;
        SoftStartDurationSeconds = softStartDurationSeconds;
        ScoreMultiplier = scoreMultiplier;
    }

    public string Label { get; }
    public float MissionDurationSeconds { get; }
    public float ResponseTimeoutMultiplier { get; }
    public float SpawnRateMultiplier { get; }
    public int ConsecutiveExpireFailThreshold { get; }
    public float PermitRiskGainMultiplier { get; }
    public float SoftStartDurationSeconds { get; }
    public float ScoreMultiplier { get; }
}

/// <summary>
/// Easy / Normal / Hard.
/// <see cref="Selected"/> is the player's choice (persisted). <see cref="Active"/> is locked at mission
/// start, so changing the setting mid-mission only applies after a restart.
/// </summary>
public static class Difficulty
{
    private const string SelectedPrefKey = "xcon.difficulty.selected";
    private const string NormalBestScorePrefKey = "xcon.mission.bestScore"; // Pre-difficulty key, kept for Normal.

    public static event Action ActiveChanged;

    public static DifficultyLevel Active { get; private set; } = DifficultyLevel.Normal;
    public static DifficultyPreset ActivePreset => For(Active);

    public static DifficultyLevel Selected
    {
        get
        {
            var raw = PlayerPrefs.GetInt(SelectedPrefKey, (int)DifficultyLevel.Normal);
            return Enum.IsDefined(typeof(DifficultyLevel), raw) ? (DifficultyLevel)raw : DifficultyLevel.Normal;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Active = DifficultyLevel.Normal;
        ActiveChanged = null;
    }

    public static DifficultyPreset For(DifficultyLevel level) => level switch
    {
        //                                       mission  timeout  spawn  missFail  risk   softStart  score
        DifficultyLevel.Easy => new DifficultyPreset("Easy", 20f * 60f, 1.5f, 0.75f, 5, 0.75f, 8f * 60f, 0.8f),
        DifficultyLevel.Hard => new DifficultyPreset("Hard", 15f * 60f, 0.7f, 1.3f, 2, 1.25f, 3f * 60f, 1.3f),
        _ => new DifficultyPreset("Normal", 15f * 60f, 1.0f, 1.0f, 3, 1.0f, 6f * 60f, 1.0f),
    };

    public static void SetSelected(DifficultyLevel level)
    {
        PlayerPrefs.SetInt(SelectedPrefKey, (int)level);
        PlayerPrefs.Save();
    }

    /// <summary>Make the selected difficulty the active one. Call only when a mission starts.</summary>
    public static void LockForMission()
    {
        var next = Selected;
        if (next == Active)
        {
            return;
        }

        Active = next;
        ActiveChanged?.Invoke();
    }

    public static DifficultyLevel Next(DifficultyLevel level) => (DifficultyLevel)(((int)level + 1) % 3);

    public static string BestScorePrefKey(DifficultyLevel level) => level == DifficultyLevel.Normal
        ? NormalBestScorePrefKey
        : $"{NormalBestScorePrefKey}.{level.ToString().ToLowerInvariant()}";

    public static bool TryParse(string value, out DifficultyLevel level)
    {
        level = DifficultyLevel.Normal;
        return !string.IsNullOrWhiteSpace(value)
               && Enum.TryParse(value, ignoreCase: true, out level)
               && Enum.IsDefined(typeof(DifficultyLevel), level);
    }
}
