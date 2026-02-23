#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

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
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}
#endif