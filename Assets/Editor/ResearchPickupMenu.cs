#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XCon.EditorTools
{
    public static class ResearchPickupMenu
    {
        [MenuItem("Tools/XCon/Spawn Research Pickup", priority = 20)]
        private static void SpawnResearchPickup()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                EditorUtility.DisplayDialog("Spawn Research Pickup", "No valid active scene.", "OK");
                return;
            }

            var pickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pickup.name = "ResearchPickup (Debug)";
            SceneManager.MoveGameObjectToScene(pickup, activeScene);

            // Place it in front of the SceneView camera if possible.
            var pos = new Vector3(0f, 1f, 0f);
            if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
            {
                var cam = SceneView.lastActiveSceneView.camera.transform;
                pos = cam.position + cam.forward * 3.0f;
                pos.y = Mathf.Max(pos.y, 0.5f);
            }

            pickup.transform.position = pos;
            pickup.transform.localScale = Vector3.one * 0.5f;

            // Collider + Rigidbody setup.
            // Triggers require at least one Rigidbody in the interaction.
            var collider = pickup.GetComponent<Collider>();
            if (collider == null)
            {
                collider = pickup.AddComponent<BoxCollider>();
            }

            collider.isTrigger = true;

            var rb = pickup.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = pickup.AddComponent<Rigidbody>();
            }

            rb.useGravity = false;
            rb.isKinematic = true;

            // Make it visually distinct.
            var renderer = pickup.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
                renderer.sharedMaterial.color = new Color(0.2f, 0.9f, 1.0f, 1.0f);
            }

            // Add Collectible and configure it to trigger research.
            var collectible = pickup.AddComponent<Collectible>();
            collectible.ConfigureResearchCompletePickup(
                projectName: "Alien Alloys",
                resultSummary: "New manufacturing options unlocked.");

            Selection.activeObject = pickup;
            EditorGUIUtility.PingObject(pickup);

            Debug.Log("[Tools/XCon] Spawned ResearchPickup (Debug). Ensure Player has tag 'Player' and a Rigidbody for triggers to fire.");
        }

        [MenuItem("Tools/XCon/Spawn Research Pickup", validate = true)]
        private static bool SpawnResearchPickupValidate()
        {
            return true;
        }
    }
}
#endif
