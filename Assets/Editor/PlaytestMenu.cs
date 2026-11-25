using UnityEditor;
using UnityEngine;

/// <summary>Playtest shortcuts under Tools > X-Con Playtest. Only enabled in Play mode.</summary>
public static class PlaytestMenu
{
    private const string Root = "Tools/X-Con Playtest/";

    [MenuItem(Root + "End Mission Now (Victory)")]
    private static void EndMissionVictory() => Send("DebugEndMissionVictory");

    [MenuItem(Root + "End Mission Now (Permit Risk Failure)")]
    private static void EndMissionFailure() => Send("DebugEndMissionFailure");

    [MenuItem(Root + "End Mission Now (Victory)", true)]
    [MenuItem(Root + "End Mission Now (Permit Risk Failure)", true)]
    private static bool CanRun() => Application.isPlaying && GameManager.Instance != null;

    [MenuItem(Root + "Clear Highscores")]
    private static void ClearHighscores()
    {
        if (EditorUtility.DisplayDialog("Clear highscores", "Delete the local highscore lists for Easy, Normal and Hard?", "Clear", "Cancel"))
        {
            HighscoreTable.ClearAll();
            Debug.Log("[Highscores] Cleared.");
        }
    }

    private static void Send(string method)
    {
        GameManager.Instance.SendMessage(method, SendMessageOptions.RequireReceiver);
    }
}
