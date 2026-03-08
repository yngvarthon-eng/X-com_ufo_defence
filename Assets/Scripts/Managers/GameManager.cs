using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private const string RootName = "GameManager";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureExists()
    {
#if UNITY_EDITOR
        // In the Editor we ensure existence AfterSceneLoad so the object ends up
        // in the active scene Hierarchy (and isn't lost during the first load).
        return;
#else
        if (Instance != null)
        {
            return;
        }

        var existing = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject(RootName);
        go.AddComponent<GameManager>();
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExistsAfterSceneLoad()
    {
        if (Instance != null)
        {
            return;
        }

        var existing = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject(RootName);
        go.AddComponent<GameManager>();
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            // Keep it visible in the active scene while running in the Editor.
            // In builds, persist across scene loads.
#if !UNITY_EDITOR
            DontDestroyOnLoad(gameObject);
#endif
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Debug.Log("GameManager started. Welcome, General Yngvar Thon!");

        // Ensure the research system exists so other systems can call it.
        if (GetComponent<ResearchManager>() == null)
        {
            gameObject.AddComponent<ResearchManager>();
        }

        // Ensure UFO manager exists so UFO events can publish box messages.
        if (GetComponent<UFOManager>() == null)
        {
            gameObject.AddComponent<UFOManager>();
        }
    }
}
