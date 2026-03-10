using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameMenuController : MonoBehaviour
{
    private const string SaveSlotKey = "xcon.save.slot1";
    private const string AutoLoadPriorityPrefKey = "xcon.save.autoload.priority";
    private const string MapStylePrefKey = "xcon.worldmap.styleIndex";

    [Serializable]
    private struct SaveData
    {
        public int priority;
        public int mapStyleIndex;
        public float missionElapsedSeconds;
        public int bestScore;
        public float education;
        public float permitRisk;
        public int totalContactsSpawned;
        public int respondedContactsCount;
        public int expiredResponsesCount;
        public string commanderName;
        public string savedAtUtc;
    }

    private GameObject menuPanel;
    public static bool IsPaused { get; private set; }
    public static event Action<bool> PauseStateChanged;

    private bool menuForcedPause;
    private GameObject statusToast;
    private Text statusToastLabel;
    private Coroutine statusToastCoroutine;
    private RectTransform menuPanelRect;
    private RectTransform menuToggleRect;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPauseStateOnSubsystemRegistration()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        PauseStateChanged = null;
    }

    private void Start()
    {
        BuildMenuUI();
    }

    private void Update()
    {
        if (!IsEscapePressedThisFrame())
        {
            TryCloseMenuFromOutsidePointerPress();
            return;
        }

        ToggleMenuPanel();
    }

    private void OnDestroy()
    {
        if (statusToastCoroutine != null)
        {
            StopCoroutine(statusToastCoroutine);
            statusToastCoroutine = null;
        }
    }

    private void BuildMenuUI()
    {
        var canvasGo = new GameObject("GameMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var menuToggle = CreateButton(canvasGo.transform, "MenuToggleButton", "Menu", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -28f), new Vector2(120f, 40f));
        menuToggleRect = menuToggle.GetComponent<RectTransform>();
        menuToggleRect.pivot = new Vector2(1f, 1f);
        menuToggle.onClick.AddListener(ToggleMenuPanel);

        CreateStatusToast(canvasGo.transform);

        menuPanel = new GameObject("GameMenuPanel", typeof(Image));
        menuPanel.transform.SetParent(canvasGo.transform, false);
        var panelImage = menuPanel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);

        menuPanelRect = menuPanel.GetComponent<RectTransform>();
        menuPanelRect.anchorMin = new Vector2(1f, 1f);
        menuPanelRect.anchorMax = new Vector2(1f, 1f);
        menuPanelRect.pivot = new Vector2(1f, 1f);
        menuPanelRect.anchoredPosition = new Vector2(-20f, -70f);
        menuPanelRect.sizeDelta = new Vector2(280f, 250f);

        var newGameButton = CreateButton(menuPanel.transform, "NewGameButton", "New Game", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(240f, 42f));
        newGameButton.onClick.AddListener(NewGame);

        var loadButton = CreateButton(menuPanel.transform, "LoadButton", "Load", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(240f, 42f));
        loadButton.onClick.AddListener(LoadGame);

        var saveButton = CreateButton(menuPanel.transform, "SaveButton", "Save", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -134f), new Vector2(240f, 42f));
        saveButton.onClick.AddListener(SaveGame);

        var quitButton = CreateButton(menuPanel.transform, "QuitButton", "Quit", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(240f, 42f));
        quitButton.onClick.AddListener(QuitGame);

        menuPanel.SetActive(false);
    }

    private static Button CreateButton(Transform parent, string objectName, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
    {
        var go = new GameObject(objectName, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.92f);

        var button = go.GetComponent<Button>();

        var textGo = new GameObject("Label", typeof(Text));
        textGo.transform.SetParent(go.transform, false);

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textGo.GetComponent<Text>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.font = font;
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        text.text = label;

        return button;
    }

    private void ToggleMenuPanel()
    {
        if (menuPanel == null)
        {
            return;
        }

        SetMenuVisible(!menuPanel.activeSelf);
    }

    private void SaveGame()
    {
        var manager = GameManager.Instance != null ? GameManager.Instance : UnityEngine.Object.FindAnyObjectByType<GameManager>();
        var ufo = manager != null ? manager.GetComponent<UFOManager>() : UnityEngine.Object.FindAnyObjectByType<UFOManager>();

        var priority = manager != null ? (int)manager.CurrentPriority : (int)GameManager.StrategicPriority.Coverage;
        var mapStyle = PlayerPrefs.GetInt(MapStylePrefKey, 0);

        var data = new SaveData
        {
            priority = priority,
            mapStyleIndex = mapStyle,
            missionElapsedSeconds = manager != null ? manager.MissionElapsedSeconds : 0f,
            bestScore = manager != null ? manager.BestScore : PlayerPrefs.GetInt("xcon.mission.bestScore", 0),
            education = ufo != null ? ufo.Education : 0f,
            permitRisk = ufo != null ? ufo.PermitRisk : 0f,
            totalContactsSpawned = ufo != null ? ufo.TotalContactsSpawned : 0,
            respondedContactsCount = ufo != null ? ufo.RespondedContactsCount : 0,
            expiredResponsesCount = ufo != null ? ufo.ExpiredResponsesCount : 0,
            commanderName = manager != null ? manager.CommanderName : string.Empty,
            savedAtUtc = DateTime.UtcNow.ToString("O")
        };

        var json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SaveSlotKey, json);
        PlayerPrefs.Save();

        Debug.Log($"[Menu] Game saved. {json}");
        ShowStatusToast("Game saved");
    }

    private void LoadGame()
    {
        if (!PlayerPrefs.HasKey(SaveSlotKey))
        {
            Debug.LogWarning("[Menu] No save game found.");
            ShowStatusToast("No save found");
            return;
        }

        var json = PlayerPrefs.GetString(SaveSlotKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[Menu] Save slot is empty.");
            ShowStatusToast("Save slot empty");
            return;
        }

        var data = JsonUtility.FromJson<SaveData>(json);
        PlayerPrefs.SetInt(AutoLoadPriorityPrefKey, data.priority);
        PlayerPrefs.SetInt(MapStylePrefKey, data.mapStyleIndex);

        var state = new GameManager.AutoLoadState
        {
            priority = data.priority,
            missionElapsedSeconds = data.missionElapsedSeconds,
            bestScore = data.bestScore,
            education = data.education,
            permitRisk = data.permitRisk,
            totalContactsSpawned = data.totalContactsSpawned,
            respondedContactsCount = data.respondedContactsCount,
            expiredResponsesCount = data.expiredResponsesCount,
            commanderName = data.commanderName,
        };

        PlayerPrefs.SetString(GameManager.AutoLoadStatePrefKey, JsonUtility.ToJson(state));
        PlayerPrefs.Save();

        SetPaused(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void NewGame()
    {
        PlayerPrefs.DeleteKey(SaveSlotKey);
        PlayerPrefs.DeleteKey(AutoLoadPriorityPrefKey);
        PlayerPrefs.DeleteKey(MapStylePrefKey);
        PlayerPrefs.Save();

        SetPaused(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public static void TogglePause()
    {
        SetPaused(!IsPaused);
    }

    public static void SetPaused(bool paused)
    {
        if (IsPaused == paused)
        {
            return;
        }

        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
        PauseStateChanged?.Invoke(IsPaused);
    }

    private void SetMenuVisible(bool visible)
    {
        if (menuPanel == null || menuPanel.activeSelf == visible)
        {
            return;
        }

        menuPanel.SetActive(visible);

        if (visible)
        {
            menuForcedPause = !IsPaused;
            if (menuForcedPause)
            {
                SetPaused(true);
            }
        }
        else
        {
            if (menuForcedPause)
            {
                SetPaused(false);
            }

            menuForcedPause = false;
        }
    }

    private void CreateStatusToast(Transform parent)
    {
        statusToast = new GameObject("MenuStatusToast", typeof(Image));
        statusToast.transform.SetParent(parent, false);

        var image = statusToast.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.76f);

        var rect = statusToast.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-32f, -78f);
        rect.sizeDelta = new Vector2(220f, 32f);

        var labelGo = new GameObject("Label", typeof(Text));
        labelGo.transform.SetParent(statusToast.transform, false);

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 2f);
        labelRect.offsetMax = new Vector2(-8f, -2f);

        statusToastLabel = labelGo.GetComponent<Text>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        statusToastLabel.font = font;
        statusToastLabel.fontSize = 16;
        statusToastLabel.alignment = TextAnchor.MiddleCenter;
        statusToastLabel.color = new Color(1f, 1f, 1f, 1f);
        statusToastLabel.text = string.Empty;

        statusToast.SetActive(false);
    }

    private void ShowStatusToast(string message, float seconds = 1.6f)
    {
        if (statusToast == null || statusToastLabel == null)
        {
            return;
        }

        statusToastLabel.text = message;
        statusToast.SetActive(true);

        if (statusToastCoroutine != null)
        {
            StopCoroutine(statusToastCoroutine);
        }

        statusToastCoroutine = StartCoroutine(HideToastAfterDelay(seconds));
    }

    private IEnumerator HideToastAfterDelay(float seconds)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, seconds));

        if (statusToast != null)
        {
            statusToast.SetActive(false);
        }

        statusToastCoroutine = null;
    }

    private static bool IsEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            return true;
        }
#endif

        return false;
    }

    private void TryCloseMenuFromOutsidePointerPress()
    {
        if (menuPanel == null || !menuPanel.activeSelf)
        {
            return;
        }

        if (!TryGetPointerPressedScreenPosition(out var screenPosition))
        {
            return;
        }

        if (IsScreenPointInside(menuPanelRect, screenPosition) || IsScreenPointInside(menuToggleRect, screenPosition))
        {
            return;
        }

        SetMenuVisible(false);
    }

    private static bool IsScreenPointInside(RectTransform rect, Vector2 screenPosition)
    {
        return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, null);
    }

    private static bool TryGetPointerPressedScreenPosition(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        var touchScreen = UnityEngine.InputSystem.Touchscreen.current;
        if (touchScreen != null && touchScreen.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = touchScreen.primaryTouch.position.ReadValue();
            return true;
        }

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPosition = mouse.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0)
        {
            for (var i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began)
                {
                    screenPosition = touch.position;
                    return true;
                }
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        screenPosition = default;
        return false;
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
