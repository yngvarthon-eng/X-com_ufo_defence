#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class InputActionsImportDiagnostics
{
    private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

    static InputActionsImportDiagnostics()
    {
        EditorApplication.delayCall += RunOnce;
    }

    private static void RunOnce()
    {
        try
        {
            Debug.Log($"[InputActionsImportDiagnostics] Forcing import: {InputActionsPath}");
            AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceUpdate);

            var mainType = AssetDatabase.GetMainAssetTypeAtPath(InputActionsPath);
            Debug.Log($"[InputActionsImportDiagnostics] Main asset type: {mainType}");

            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (asset == null)
            {
                Debug.LogError($"[InputActionsImportDiagnostics] LoadAssetAtPath<InputActionAsset> returned null for {InputActionsPath}");
                var all = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath);
                Debug.Log($"[InputActionsImportDiagnostics] LoadAllAssetsAtPath count: {all?.Length ?? 0}");
                if (all != null)
                {
                    for (var i = 0; i < all.Length; i++)
                        Debug.Log($"[InputActionsImportDiagnostics]  - [{i}] {all[i]?.GetType()} name={all[i]?.name}");
                }

                return;
            }

            Debug.Log($"[InputActionsImportDiagnostics] Loaded OK: name={asset.name} maps={asset.actionMaps.Count}");

            RepairPlayerInputReferences(asset);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private static void RepairPlayerInputReferences(InputActionAsset asset)
    {
        var playerInputs = Resources.FindObjectsOfTypeAll<PlayerInput>();
        var repairedCount = 0;

        foreach (var playerInput in playerInputs)
        {
            if (playerInput == null)
                continue;

            // Only touch scene objects (not assets/prefabs in Project view).
            if (EditorUtility.IsPersistent(playerInput))
                continue;

            if (playerInput.actions != null)
                continue;

            Undo.RecordObject(playerInput, "Repair PlayerInput Actions");
            playerInput.actions = asset;
            EditorUtility.SetDirty(playerInput);

            var scene = playerInput.gameObject.scene;
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);

            repairedCount++;
        }

        if (repairedCount > 0)
            Debug.Log($"[InputActionsImportDiagnostics] Repaired PlayerInput.Actions on {repairedCount} scene object(s)");
    }
}
#endif