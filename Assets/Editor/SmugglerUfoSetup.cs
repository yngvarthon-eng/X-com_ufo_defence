using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SmugglerUfoSetup
{
    private const string ModelAssetPath = "Assets/Art/Models/UFO/UFODemo/UFODemo.fbx";
    private const string PrefabOutputPath = "Assets/Prefabs/UFO/SmugglerUFO.prefab";
    private const string MainScenePath = "Assets/x-con_ufo_defence.unity";

    [MenuItem("Tools/X-Con/Setup Smuggler UFO Prefab")]
    public static void SetupSmugglerUfoPrefabAndAssignFromMenu()
    {
        SetupSmugglerUfoPrefabAndAssign();
    }

    public static void SetupSmugglerUfoPrefabAndAssign()
    {
        if (!File.Exists(ModelAssetPath))
        {
            throw new FileNotFoundException($"Smuggler FBX not found at expected path: {ModelAssetPath}");
        }

        EnsurePrefabFolderExists();

        var modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ModelAssetPath);
        if (modelRoot == null)
        {
            throw new InvalidOperationException($"Failed to load FBX model asset: {ModelAssetPath}");
        }

        var tempInstance = PrefabUtility.InstantiatePrefab(modelRoot) as GameObject;
        if (tempInstance == null)
        {
            throw new InvalidOperationException("Failed to instantiate smuggler model for prefab creation.");
        }

        tempInstance.name = "SmugglerUFO";

        var prefab = PrefabUtility.SaveAsPrefabAsset(tempInstance, PrefabOutputPath);
        UnityEngine.Object.DestroyImmediate(tempInstance);

        if (prefab == null)
        {
            throw new InvalidOperationException($"Failed to create prefab at: {PrefabOutputPath}");
        }

        var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

        var ufoManager = UnityEngine.Object.FindAnyObjectByType<UFOManager>();
        if (ufoManager == null)
        {
            var gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            if (gameManager == null)
            {
                throw new InvalidOperationException("Could not find UFOManager or GameManager in the main scene.");
            }

            ufoManager = gameManager.gameObject.GetComponent<UFOManager>();
            if (ufoManager == null)
            {
                ufoManager = gameManager.gameObject.AddComponent<UFOManager>();
            }
        }

        var serialized = new SerializedObject(ufoManager);
        var smugglerPrefabProperty = serialized.FindProperty("smugglerUfoPrefab");
        var useSmugglerAsDefaultProperty = serialized.FindProperty("useSmugglerPrefabAsDefaultVisual");

        if (smugglerPrefabProperty == null || useSmugglerAsDefaultProperty == null)
        {
            throw new InvalidOperationException("UFOManager is missing expected smuggler prefab serialized fields.");
        }

        smugglerPrefabProperty.objectReferenceValue = prefab;
        useSmugglerAsDefaultProperty.boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(ufoManager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Smuggler UFO setup complete. Prefab: {PrefabOutputPath}, assigned in scene: {MainScenePath}");
    }

    private static void EnsurePrefabFolderExists()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UFO"))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "UFO");
        }
    }
}
