#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using XCon.UI.Boxes;

namespace XCon.EditorTools
{
    [InitializeOnLoad]
    public static class BoxSystemEnterPlayModeBootstrap
    {
        private const string RootName = "BoxSystem (Runtime)";

        static BoxSystemEnterPlayModeBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            // Delay one tick so the scene is fully ready.
            EditorApplication.delayCall += EnsureCreated;
        }

        private static void EnsureCreated()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            var activeScene = SceneManager.GetActiveScene();

            if (GameObject.Find(RootName) != null)
            {
                return;
            }

            var root = new GameObject(RootName);
            // Keep it visible in the active scene while running in the Editor.
            // (DontDestroyOnLoad moves it to a special scene that can be easy to miss.)

            root.AddComponent<BoxMessageQueue>();

            var view = root.AddComponent<BoxSystemView>();
            view.BuildUI();

            root.AddComponent<BoxDebugHotkeys>();

            Debug.Log($"[BoxSystem] (Editor) Bootstrapped on EnteredPlayMode in scene '{activeScene.name}'.");

            // Make the system visible even if Console logs are hidden/filtered.
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlaying)
                {
                    return;
                }

                var queue = UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
                const string readyKey = "debug/boxsystem/ready";

                queue?.Publish(new BoxMessage(
                    triggerKey: readyKey,
                    channel: BoxChannel.Info,
                    severity: BoxSeverity.Info,
                    sourceTag: "System",
                    title: "BoxSystem Ready",
                    body: "Press 1/2/3 (or F1/F2/F3). Esc to dismiss."));

                // Auto-dismiss Ready after a moment so it doesn't block other messages.
                var root = GameObject.Find(RootName);
                if (root != null && queue != null)
                {
                    var autoDismiss = root.AddComponent<BoxAutoDismissCurrentMessage>();
                    autoDismiss.Configure(queue, readyKey, delaySeconds: 2.0f);
                }
            };
        }

    }
}
#endif
