using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Creates the original X-Con saucer (mesh asset, materials, prefab) from <see cref="SaucerMeshBuilder"/>
/// and makes it the UFO visual in the main scene. Safe to run again: it overwrites its own assets.
/// Batch: Unity -batchmode -quit -projectPath . -executeMethod XConSaucerSetup.BuildAndAssign
/// </summary>
public static class XConSaucerSetup
{
    private const string ArtFolder = "Assets/Art/Models/UFO/XConSaucer";
    private const string MeshPath = ArtFolder + "/XConSaucer_Mesh.asset";
    private const string HullMaterialPath = ArtFolder + "/XConSaucer_Hull.mat";
    private const string DomeMaterialPath = ArtFolder + "/XConSaucer_Dome.mat";
    private const string LightsMaterialPath = ArtFolder + "/XConSaucer_Lights.mat";
    private const string PrefabPath = "Assets/Prefabs/UFO/XConSaucer.prefab";
    private const string MainScenePath = "Assets/x-con_ufo_defence.unity";

    [MenuItem("Tools/X-Con/Build Original Saucer And Assign")]
    public static void BuildAndAssign()
    {
        EnsureFolder("Assets/Art/Models/UFO", "XConSaucer");
        EnsureFolder("Assets/Prefabs", "UFO");

        var mesh = SaveMesh();
        var hull = SaveMaterial(HullMaterialPath, new Color(0.56f, 0.60f, 0.66f), metallic: 0.8f, smoothness: 0.6f, emission: null);
        var dome = SaveMaterial(DomeMaterialPath, new Color(0.30f, 0.75f, 0.95f), metallic: 0.1f, smoothness: 0.95f, emission: new Color(0.06f, 0.22f, 0.32f));
        var lights = SaveMaterial(LightsMaterialPath, new Color(1f, 0.75f, 0.4f), metallic: 0f, smoothness: 0.3f, emission: new Color(1f, 0.62f, 0.22f) * 1.8f);

        var prefab = SavePrefab(mesh, hull, dome, lights);
        AssignInScene(prefab);

        AssetDatabase.SaveAssets();
        Debug.Log($"[XConSaucer] Built {PrefabPath} and assigned it as the UFO visual in {MainScenePath}.");
    }

    private static Mesh SaveMesh()
    {
        var fresh = SaucerMeshBuilder.Build();
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(fresh, MeshPath);
            return fresh;
        }

        // Keep the asset (and its GUID) so references stay valid.
        EditorUtility.CopySerialized(fresh, existing);
        UnityEngine.Object.DestroyImmediate(fresh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static Material SaveMaterial(string path, Color color, float metallic, float smoothness, Color? emission)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("Standard") ?? throw new InvalidOperationException("Standard shader not found.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        if (emission.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission.Value);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject SavePrefab(Mesh mesh, Material hull, Material dome, Material lights)
    {
        var root = new GameObject("XConSaucer");
        try
        {
            var body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(root.transform, false);
            body.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = body.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { hull, dome, lights };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            root.AddComponent<SaucerIdle>();

            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath)
                   ?? throw new InvalidOperationException($"Could not save prefab {PrefabPath}.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void AssignInScene(GameObject prefab)
    {
        var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        var ufoManager = UnityEngine.Object.FindAnyObjectByType<UFOManager>()
                         ?? throw new InvalidOperationException("No UFOManager in the main scene.");

        var serialized = new SerializedObject(ufoManager);
        serialized.FindProperty("ufoPrefab").objectReferenceValue = prefab;
        // The old smuggler visual was a third-party model: drop the reference so it never ships.
        serialized.FindProperty("smugglerUfoPrefab").objectReferenceValue = null;
        serialized.FindProperty("useSmugglerPrefabAsDefaultVisual").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
