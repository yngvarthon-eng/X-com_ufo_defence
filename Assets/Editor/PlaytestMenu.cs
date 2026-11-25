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

    private static void Send(string method)
    {
        GameManager.Instance.SendMessage(method, SendMessageOptions.RequireReceiver);
    }
}
