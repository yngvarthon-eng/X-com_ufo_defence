using UnityEngine;

public class Collectible : MonoBehaviour
{
    [Header("Pickup")]
    [SerializeField] private bool destroyOnPickup = true;

    [Header("Research (optional)")]
    [SerializeField] private bool triggersResearchComplete = false;
    [SerializeField] private string researchProjectName = "Alien Alloys";
    [TextArea]
    [SerializeField] private string researchResultSummary = "New manufacturing options unlocked.";

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        HandlePickup(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag("Player")) return;
        HandlePickup(collision.collider.gameObject);
    }

    private void HandlePickup(GameObject player)
    {
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