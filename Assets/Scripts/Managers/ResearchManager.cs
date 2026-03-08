using System.Collections;
using UnityEngine;
using XCon.UI.Boxes;

public sealed class ResearchManager : MonoBehaviour
{
    [Header("Prototype")]
    [SerializeField] private bool enableThinkingReflection = false;
    [SerializeField] private float thinkingDelaySeconds = 0.25f;

    public void CompleteResearch(string projectName, string resultSummary)
    {
        var queue = GetQueue();
        if (queue == null)
        {
            Debug.LogWarning("[Research] BoxMessageQueue not found; cannot publish research completion.");
            return;
        }

        queue.Publish(new BoxMessage(
            triggerKey: $"research/complete/{SanitizeKey(projectName)}",
            channel: BoxChannel.Info,
            severity: BoxSeverity.Info,
            sourceTag: "R&D",
            title: "Research Complete",
            body: string.IsNullOrWhiteSpace(resultSummary)
                ? projectName
                : $"{projectName}\n{resultSummary}"));

        StartCoroutine(PublishThinkingFollowups(queue));
    }

    private IEnumerator PublishThinkingFollowups(BoxMessageQueue queue)
    {
        if (thinkingDelaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(thinkingDelaySeconds);
        }

        if (enableThinkingReflection)
        {
            queue.Publish(new BoxMessage(
                triggerKey: "thinking/post_event/reflection",
                channel: BoxChannel.Thinking,
                severity: BoxSeverity.Info,
                sourceTag: "Commander",
                title: "Post-Event Reflection",
                body: "What did we learn, and what are we still blind to?"));

            if (thinkingDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(thinkingDelaySeconds);
            }
        }

        queue.Publish(new BoxMessage(
            triggerKey: "thinking/pick_priority",
            channel: BoxChannel.Thinking,
            severity: BoxSeverity.Info,
            sourceTag: "Commander",
            title: "Pick a Priority",
            body: "Pick a priority: coverage, research, or response."));
    }

    [ContextMenu("Debug/Complete Sample Research")]
    private void DebugCompleteSampleResearch()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Research] Enter Play Mode to run the debug action.");
            return;
        }

        CompleteResearch(
            projectName: "Alien Alloys",
            resultSummary: "New manufacturing options unlocked.");
    }

    private static BoxMessageQueue GetQueue()
    {
        var queue = BoxMessageQueue.Instance;
        if (queue != null)
        {
            return queue;
        }

        return UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
    }

    private static string SanitizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        // Minimal key sanitization for trigger keys.
        return value.Trim().ToLowerInvariant().Replace(' ', '_');
    }
}
