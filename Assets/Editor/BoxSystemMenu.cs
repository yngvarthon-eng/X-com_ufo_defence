#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using XCon.UI.Boxes;

namespace XCon.EditorTools
{
    public static class BoxSystemMenu
    {
        private const string RootName = "BoxSystem (Runtime)";

        [MenuItem("Tools/XCon/Spawn BoxSystem", priority = 10)]
        private static void Spawn()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "BoxSystem",
                    "Enter Play Mode first, then run Tools → XCon → Spawn BoxSystem.",
                    "OK");
                return;
            }

            var existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            var root = new GameObject(RootName);

            // Ensure it lives in the active scene (visible in Hierarchy).
            if (activeScene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(root, activeScene);
            }

            root.AddComponent<BoxMessageQueue>();

            var view = root.AddComponent<BoxSystemView>();
            view.BuildUI();

            root.AddComponent<BoxDebugHotkeys>();

            var queue = root.GetComponent<BoxMessageQueue>();
            queue?.Publish(new BoxMessage(
                triggerKey: "debug/boxsystem/ready",
                channel: BoxChannel.Info,
                severity: BoxSeverity.Info,
                sourceTag: "System",
                title: "BoxSystem Ready",
                body: "Press 1/2/3 (or F1/F2/F3). Esc to dismiss."));

            Selection.activeObject = root;
            EditorGUIUtility.PingObject(root);
        }

        [MenuItem("Tools/XCon/Spawn BoxSystem", validate = true)]
        private static bool SpawnValidate()
        {
            return true;
        }
    }
}
#endif
