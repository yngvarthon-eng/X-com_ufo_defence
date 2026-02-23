using UnityEngine;
using System.Collections;
using XCon.UI.Boxes;

public class UFOManager : MonoBehaviour
{
    [Header("Boxes")]
    [SerializeField] private bool publishUfoSpottedBoxes = true;
    [SerializeField] private float thinkingDelaySeconds = 0.25f;

    public void SpawnUFO(Vector3 position)
    {
        // TODO: Instantiate UFO prefab at position
        Debug.Log("UFO spawned at " + position);

        if (!publishUfoSpottedBoxes)
        {
            return;
        }

        var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : Object.FindAnyObjectByType<BoxMessageQueue>();
        if (queue == null)
        {
            Debug.LogWarning("[UFO] BoxMessageQueue not found; cannot publish UFO spotted messages.");
            return;
        }

        queue.Publish(new BoxMessage(
            triggerKey: "ufo/spotted",
            channel: BoxChannel.Info,
            severity: BoxSeverity.Warn,
            sourceTag: "Radar",
            title: "UFO Spotted",
            body: $"UFO sighted at {position}."));

        StartCoroutine(PublishThinking(queue));
    }

    public void MoveUFO(GameObject ufo, Vector3 targetPosition)
    {
        // TODO: Move UFO to target position
        ufo.transform.position = targetPosition;
        Debug.Log("UFO moved to " + targetPosition);
    }

    private IEnumerator PublishThinking(BoxMessageQueue queue)
    {
        if (thinkingDelaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(thinkingDelaySeconds);
        }

        queue.Publish(new BoxMessage(
            triggerKey: "thinking/pick_priority",
            channel: BoxChannel.Thinking,
            severity: BoxSeverity.Info,
            sourceTag: "Commander",
            title: "Pick a Priority",
            body: "Pick a priority: coverage, research, or response."));
    }

    [ContextMenu("Debug/Spot UFO")]
    private void DebugSpotUfo()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[UFO] Enter Play Mode to run the debug action.");
            return;
        }

        SpawnUFO(new Vector3(12f, 0f, 8f));
    }
}
