#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace XCon.EditorTools
{
    [InitializeOnLoad]
    public static class InputActionsSelfHeal
    {
        private const string ActionsPath = "Assets/InputSystem_Actions.inputactions";

        static InputActionsSelfHeal()
        {
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            EditorApplication.delayCall -= Run;

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (actions == null)
            {
                AssetDatabase.ImportAsset(
                    ActionsPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            }

            if (actions == null)
            {
                Debug.LogWarning($"[InputActionsSelfHeal] Could not load InputActionAsset at '{ActionsPath}'.");
                return;
            }

            var anyAssigned = false;

            foreach (var playerInput in Resources.FindObjectsOfTypeAll<PlayerInput>())
            {
                if (playerInput == null)
                {
                    continue;
                }

                // Avoid touching assets/prefabs; only fix open scene instances.
                if (EditorUtility.IsPersistent(playerInput))
                {
                    continue;
                }

                if (playerInput.actions != null)
                {
                    continue;
                }

                Undo.RecordObject(playerInput, "Assign Input Actions");
                playerInput.actions = actions;
                EditorUtility.SetDirty(playerInput);
                anyAssigned = true;
            }

            if (anyAssigned)
            {
                // Helps Unity serialize the fixed reference.
                AssetDatabase.SaveAssets();
            }
        }
    }
}
#endif
