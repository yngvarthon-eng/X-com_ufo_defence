#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace XCon.EditorTools
{
    public static class TempUfoPrefabCreator
    {
        private const string DefaultPrefabPath = "Assets/Prefabs/TempUFO.prefab";

        [MenuItem("Tools/X-CON/Create Temp UFO Prefab")]
        public static void CreateTempUfoPrefab()
        {
            var directory = System.IO.Path.GetDirectoryName(DefaultPrefabPath);
            if (!string.IsNullOrWhiteSpace(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                CreateFoldersRecursively(directory);
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "TempUFO";
            go.transform.localScale = Vector3.one * 1.5f;

            // Avoid interfering with gameplay physics.
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            PrefabUtility.SaveAsPrefabAsset(go, DefaultPrefabPath);
            Object.DestroyImmediate(go);

            AssetDatabase.Refresh();
            Debug.Log($"[X-CON] Created temp UFO prefab at '{DefaultPrefabPath}'.");
        }

        private static void CreateFoldersRecursively(string assetPath)
        {
            assetPath = assetPath.Replace('\\', '/');
            if (!assetPath.StartsWith("Assets"))
            {
                return;
            }

            var parts = assetPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
#endif
