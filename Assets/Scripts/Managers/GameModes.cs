using System;
using UnityEngine;

public enum GameMode
{
    Mission = 0,    // Hold out until the clock runs out (15 / 20 min).
    Endurance = 1,  // No clock: contacts keep getting faster until you fail. Score = how long and how well.
    Training = 2,   // No fail, no highscores. U spawns a contact on demand.
}

/// <summary>
/// The game mode. Works like <see cref="Difficulty"/>: <see cref="Selected"/> is the player's choice
/// (persisted) and <see cref="Active"/> is locked when a mission starts.
/// </summary>
public static class GameModes
{
    private const string SelectedPrefKey = "xcon.mode.selected";

    // Endurance ramp, applied on top of the difficulty preset.
    private const float EnduranceSpawnGainPerMinute = 0.06f;   // +6% contacts per minute survived...
    private const float EnduranceMaxSpawnMultiplier = 3.0f;    // ...up to 3x.
    private const float EnduranceTimeoutLossPerMinute = 0.015f; // -1.5% response time per minute...
    private const float EnduranceMinTimeoutMultiplier = 0.55f;  // ...down to 55%.

    public static GameMode Active { get; private set; } = GameMode.Mission;

    public static GameMode Selected
    {
        get
        {
            var raw = PlayerPrefs.GetInt(SelectedPrefKey, (int)GameMode.Mission);
            return Enum.IsDefined(typeof(GameMode), raw) ? (GameMode)raw : GameMode.Mission;
        }
    }

    /// <summary>Mission mode ends in victory when the clock runs out; the other modes have no clock.</summary>
    public static bool HasTimeLimit => Active == GameMode.Mission;

    /// <summary>Training never fails and never writes highscores.</summary>
    public static bool IsTraining => Active == GameMode.Training;

    /// <summary>
    /// The debug "spawn a contact" key (U / F8). Always on in the editor and in development builds,
    /// otherwise only in Training, so it can't be used to farm highscores.
    /// </summary>
    public static bool SpawnHotkeyAllowed => Application.isEditor || Debug.isDebugBuild || IsTraining;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Active = GameMode.Mission;
    }

    public static void SetSelected(GameMode mode)
    {
        PlayerPrefs.SetInt(SelectedPrefKey, (int)mode);
        PlayerPrefs.Save();
    }

    /// <summary>Make the selected mode the active one. Call only when a mission starts.</summary>
    public static void LockForMission() => Active = Selected;

    public static GameMode Next(GameMode mode) => (GameMode)(((int)mode + 1) % 3);

    public static string Label(GameMode mode) => mode switch
    {
        GameMode.Endurance => "Endurance",
        GameMode.Training => "Training",
        _ => "Mission",
    };

    public static string ShortDescription(GameMode mode) => mode switch
    {
        GameMode.Endurance => "no clock, it keeps getting harder",
        GameMode.Training => "no fail, U spawns a contact",
        _ => "survive until the clock runs out",
    };

    public static float EnduranceSpawnMultiplier(float elapsedSeconds) =>
        Active != GameMode.Endurance
            ? 1f
            : Mathf.Min(EnduranceMaxSpawnMultiplier, 1f + EnduranceSpawnGainPerMinute * (elapsedSeconds / 60f));

    public static float EnduranceTimeoutMultiplier(float elapsedSeconds) =>
        Active != GameMode.Endurance
            ? 1f
            : Mathf.Max(EnduranceMinTimeoutMultiplier, 1f - EnduranceTimeoutLossPerMinute * (elapsedSeconds / 60f));

    /// <summary>Highscore / best-score key suffix: Mission keeps the original keys.</summary>
    public static string KeySuffix(GameMode mode) => mode == GameMode.Mission ? string.Empty : "." + mode.ToString().ToLowerInvariant();
}
