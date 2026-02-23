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
            if (activeScene.name == "MainMenu")
            {
                return;
            }

            if (GameObject.Find(RootName) != null)
            {
                return;
            }

            var root = new GameObject(RootName);
            Object.DontDestroyOnLoad(root);

            root.AddComponent<BoxMessageQueue>();

            var view = root.AddComponent<BoxSystemView>();
            view.BuildUI();

            root.AddComponent<BoxDebugHotkeys>();

            Debug.Log($"[BoxSystem] (Editor) Bootstrapped on EnteredPlayMode in scene '{activeScene.name}'.");
        }

    }
}
#endif
