#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace XCon.EditorTools
{
    public static class SceneManagersCreator
    {
        private const string RootName = "GameManager";
        private const string TempUfoPrefabPath = "Assets/Prefabs/TempUFO.prefab";

        [MenuItem("Tools/X-CON/Create/Ensure Managers In Scene")]
        public static void EnsureManagersInScene()
        {
            var root = FindSceneObject<GameManager>()?.gameObject;
            if (root == null)
            {
                root = new GameObject(RootName);
                root.AddComponent<GameManager>();
                MarkSceneDirty(root);
                Debug.Log("[X-CON] Created GameManager root in scene.");
            }

            EnsureComponent<UFOManager>(root);
            EnsureComponent<ResearchManager>(root);
            EnsureComponent<SquadManager>(root);
            EnsureComponent<BaseManager>(root);

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
        }

        [MenuItem("Tools/X-CON/Create/Assign Temp UFO Prefab")]
        public static void AssignTempUfoPrefab()
        {
            EnsureManagersInScene();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TempUfoPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[X-CON] Temp UFO prefab not found at '{TempUfoPrefabPath}'. Run Tools → X-CON → Create Temp UFO Prefab first.");
                return;
            }

            var manager = FindSceneObject<UFOManager>();
            if (manager == null)
            {
                Debug.LogWarning("[X-CON] UFOManager not found in scene.");
                return;
            }

            Undo.RecordObject(manager, "Assign Temp UFO Prefab");
            var so = new SerializedObject(manager);
            var ufoPrefabProp = so.FindProperty("ufoPrefab");
            var spawnProp = so.FindProperty("spawnUfoVisuals");
            if (ufoPrefabProp != null)
            {
                ufoPrefabProp.objectReferenceValue = prefab;
            }

            if (spawnProp != null)
            {
                spawnProp.boolValue = true;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            MarkSceneDirty(manager.gameObject);

            Selection.activeObject = manager;
            EditorGUIUtility.PingObject(manager);
            Debug.Log($"[X-CON] Assigned '{TempUfoPrefabPath}' to UFOManager.ufoPrefab.");
        }

        [MenuItem("Tools/X-CON/Create/Assign Temp UFO Prefab", true)]
        private static bool AssignTempUfoPrefab_Validate()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TempUfoPrefabPath) != null;
        }

        private static void EnsureComponent<T>(GameObject go) where T : Component
        {
            if (go.GetComponent<T>() != null)
            {
                return;
            }

            go.AddComponent<T>();
            MarkSceneDirty(go);
        }

        private static T FindSceneObject<T>() where T : Object
        {
            var all = Resources.FindObjectsOfTypeAll<T>();
            return all.FirstOrDefault(obj =>
            {
                if (obj == null)
                {
                    return false;
                }

                if (EditorUtility.IsPersistent(obj))
                {
                    return false;
                }

                if (obj is Component c)
                {
                    return c.gameObject.scene.IsValid();
                }

                if (obj is GameObject go)
                {
                    return go.scene.IsValid();
                }

                return false;
            });
        }

        private static void MarkSceneDirty(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            EditorUtility.SetDirty(go);
            if (go.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(go.scene);
            }
        }
    }
}
#endif
