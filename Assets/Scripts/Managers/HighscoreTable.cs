using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Local top-10 list per game mode and difficulty, kept in PlayerPrefs (browser storage on the web).
/// Every finished mission is offered; only the ten best per list are kept. Training has no list.
/// </summary>
public static class HighscoreTable
{
    public const int MaxEntries = 10;
    private const string PrefKeyPrefix = "xcon.highscores.";

    [Serializable]
    public struct Entry
    {
        public string name;
        public int score;
        public string medal;      // "Gold", "Silver", "Bronze" or "".
        public bool victory;      // false = the mission failed before the clock ran out.
        public int seconds;       // Time played. The headline number in Endurance.
        public string dateUtc;    // ISO 8601.
    }

    [Serializable]
    private struct Store
    {
        public List<Entry> entries;
    }

    public static List<Entry> Load(GameMode mode, DifficultyLevel level)
    {
        var json = PlayerPrefs.GetString(PrefKey(mode, level), string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<Entry>();
        }

        try
        {
            var store = JsonUtility.FromJson<Store>(json);
            return store.entries ?? new List<Entry>();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Highscores] Could not read {level} list, starting fresh: {e.Message}");
            return new List<Entry>();
        }
    }

    /// <summary>Add a result. Returns its place (1-based) if it made the list, otherwise 0.</summary>
    public static int Submit(GameMode mode, DifficultyLevel level, Entry entry)
    {
        var entries = Load(mode, level);
        entries.Add(entry);
        // Highest score first; on a tie the older entry keeps the higher place.
        var ordered = new List<Entry>(entries);
        ordered.Sort((a, b) =>
        {
            var byScore = b.score.CompareTo(a.score);
            return byScore != 0 ? byScore : entries.IndexOf(a).CompareTo(entries.IndexOf(b));
        });

        var place = ordered.IndexOf(entry) + 1;
        if (ordered.Count > MaxEntries)
        {
            ordered.RemoveRange(MaxEntries, ordered.Count - MaxEntries);
        }

        if (place > MaxEntries)
        {
            place = 0;
        }

        PlayerPrefs.SetString(PrefKey(mode, level), JsonUtility.ToJson(new Store { entries = ordered }));
        PlayerPrefs.Save();
        return place;
    }

    /// <summary>Delete every local highscore list (all difficulties).</summary>
    public static void ClearAll()
    {
        foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
        {
            foreach (DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel)))
            {
                PlayerPrefs.DeleteKey(PrefKey(mode, level));
            }
        }

        PlayerPrefs.Save();
    }

    // Mission keeps the original "xcon.highscores.normal" keys; other modes add ".endurance" etc. after the prefix.
    private static string PrefKey(GameMode mode, DifficultyLevel level) =>
        PrefKeyPrefix + (mode == GameMode.Mission ? string.Empty : mode.ToString().ToLowerInvariant() + ".") + level.ToString().ToLowerInvariant();
}
