using UnityEngine;
using XCon.UI.Boxes;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private const string RootName = "GameManager";

    public enum StrategicPriority
    {
        Coverage = 1,
        Research = 2,
        Response = 3,
    }

    [Header("Priority")]
    [SerializeField] private StrategicPriority currentPriority = StrategicPriority.Coverage;

    public StrategicPriority CurrentPriority => currentPriority;

    private const string PickPriorityTriggerKey = "thinking/pick_priority";

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

        // Ensure response-related managers exist.
        if (GetComponent<SquadManager>() == null)
        {
            gameObject.AddComponent<SquadManager>();
        }

        if (GetComponent<BaseManager>() == null)
        {
            gameObject.AddComponent<BaseManager>();
        }
    }

    private void Update()
    {
        var queue = BoxMessageQueue.Instance;
        if (queue == null || !queue.Current.HasValue)
        {
            return;
        }

        var current = queue.Current.Value;
        if (current.TriggerKey != PickPriorityTriggerKey)
        {
            return;
        }

        if (!TryGetPriorityChoice(out var choice))
        {
            return;
        }

        currentPriority = choice;

        // Keep the prompt visible and update it so you can re-choose.
        queue.UpdateBody(PickPriorityTriggerKey,
            $"Pick a priority (press 1-3 anytime):\n1) Coverage\n2) Research\n3) Response\n\nCurrent: {choice}");
    }

    private static bool TryGetPriorityChoice(out StrategicPriority choice)
    {
        choice = default;

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) { choice = StrategicPriority.Coverage; return true; }
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) { choice = StrategicPriority.Research; return true; }
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) { choice = StrategicPriority.Response; return true; }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { choice = StrategicPriority.Coverage; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { choice = StrategicPriority.Research; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) { choice = StrategicPriority.Response; return true; }
#endif

        return false;
    }
}
