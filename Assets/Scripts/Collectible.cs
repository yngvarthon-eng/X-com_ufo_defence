using UnityEngine;
using XCon.UI.Boxes;

public class Collectible : MonoBehaviour
{
    [Header("Pickup")]
    [SerializeField] private bool destroyOnPickup = true;

    [Header("Research (optional)")]
    [SerializeField] private bool triggersResearchComplete = false;
    [SerializeField] private string researchProjectName = "Alien Alloys";
    [TextArea]
    [SerializeField] private string researchResultSummary = "New manufacturing options unlocked.";

    public void ConfigureResearchCompletePickup(string projectName, string resultSummary, bool destroyOnPickup = true)
    {
        this.destroyOnPickup = destroyOnPickup;
        triggersResearchComplete = true;
        researchProjectName = string.IsNullOrWhiteSpace(projectName) ? "Research" : projectName;
        researchResultSummary = resultSummary ?? string.Empty;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other.gameObject)) return;
        HandlePickup(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsPlayer(collision.collider.gameObject)) return;
        HandlePickup(collision.collider.gameObject);
    }

    private static bool IsPlayer(GameObject go)
    {
        if (go == null)
        {
            return false;
        }

        return go.CompareTag("Player") || go.GetComponent<PlayerController>() != null;
    }

    private void HandlePickup(GameObject player)
    {
        // Visible confirmation for debugging.
        var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : Object.FindAnyObjectByType<BoxMessageQueue>();
        queue?.Publish(new BoxMessage(
            triggerKey: $"debug/pickup/{gameObject.name}",
            channel: BoxChannel.Info,
            severity: BoxSeverity.Info,
            sourceTag: "Pickup",
            title: "Pickup Triggered",
            body: $"Picked up: {gameObject.name}"));

        if (triggersResearchComplete)
        {
            var research = Object.FindAnyObjectByType<ResearchManager>();
            if (research != null)
            {
                research.CompleteResearch(researchProjectName, researchResultSummary);
            }
            else
            {
                Debug.LogWarning("[Collectible] ResearchManager not found; cannot complete research.");
            }
        }

        // Add score or other effects here.
        if (destroyOnPickup)
        {
            Destroy(gameObject);
        }
    }
}