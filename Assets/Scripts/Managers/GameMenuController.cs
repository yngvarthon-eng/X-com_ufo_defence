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
    private const string UfoSpawnRatePrefKey = "xcon.ufo.spawnRatePerMinute";
    private const float UfoSpawnRateMin = 0f;
    private const float UfoSpawnRateMax = 1.2f;

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
        public int consecutiveExpiredResponsesCount;
        public int muleCargoSpills;
        public int warheadsSeized;
        public int warheadsDelivered;
        public string difficulty; // Enum name; missing in older saves, which keeps the current choice.
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
    private InputField commanderNameInput;
    private Slider sfxVolumeSlider;
    private Text sfxVolumeValueLabel;
    private Slider ufoSpawnRateSlider;
    private Text ufoSpawnRateValueLabel;
    private Button mapStyleButton;
    private Button difficultyButton;

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
        menuPanelRect.sizeDelta = new Vector2(320f, 550f);

        CreateTextLabel(menuPanel.transform, "CommanderLabel", "Commander Name", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(260f, 24f), 16);

        commanderNameInput = CreateInputField(menuPanel.transform, "CommanderNameInput", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(260f, 36f), "Enter commander name");
        SyncCommanderInputFromState();

        var applyNameButton = CreateButton(menuPanel.transform, "ApplyCommanderNameButton", "Apply Name", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(260f, 36f));
        applyNameButton.onClick.AddListener(ApplyCommanderNameFromInput);

        CreateTextLabel(menuPanel.transform, "SfxLabel", "SFX Volume", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(260f, 22f), 16);
        sfxVolumeSlider = CreateSlider(menuPanel.transform, "SfxVolumeSlider", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -154f), new Vector2(190f, 22f));
        sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);

        sfxVolumeValueLabel = CreateTextLabel(menuPanel.transform, "SfxValueLabel", "100%", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(112f, -154f), new Vector2(66f, 22f), 14);
        sfxVolumeValueLabel.alignment = TextAnchor.MiddleRight;

        SyncAudioControlsFromState();

        CreateTextLabel(menuPanel.transform, "UfoSpawnRateLabel", "UFO Spawning", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -184f), new Vector2(260f, 22f), 16);
        ufoSpawnRateSlider = CreateSlider(menuPanel.transform, "UfoSpawnRateSlider", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -208f), new Vector2(190f, 22f));
        ufoSpawnRateSlider.minValue = UfoSpawnRateMin;
        ufoSpawnRateSlider.maxValue = UfoSpawnRateMax;
        ufoSpawnRateSlider.onValueChanged.AddListener(OnUfoSpawnRateChanged);

        ufoSpawnRateValueLabel = CreateTextLabel(menuPanel.transform, "UfoSpawnRateValueLabel", "0.24/min", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(112f, -208f), new Vector2(66f, 22f), 14);
        ufoSpawnRateValueLabel.alignment = TextAnchor.MiddleRight;

        SyncUfoSpawningControlsFromState();

        CreateTextLabel(menuPanel.transform, "MapStyleMenuLabel", "Map Style", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -238f), new Vector2(260f, 22f), 16);
        mapStyleButton = CreateButton(menuPanel.transform, "MapStyleMenuButton", "Map Style: Round Sphere", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -266f), new Vector2(260f, 36f));
        mapStyleButton.onClick.AddListener(OnMapStyleButtonClicked);
        SyncMapStyleControlFromState();

        difficultyButton = CreateButton(menuPanel.transform, "DifficultyMenuButton", "Difficulty: Normal", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -306f), new Vector2(260f, 36f));
        difficultyButton.onClick.AddListener(OnDifficultyButtonClicked);
        UpdateDifficultyButtonLabel();

        var newGameButton = CreateButton(menuPanel.transform, "NewGameButton", "New Game", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -346f), new Vector2(260f, 40f));
        newGameButton.onClick.AddListener(NewGame);

        var loadButton = CreateButton(menuPanel.transform, "LoadButton", "Load", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -392f), new Vector2(260f, 40f));
        loadButton.onClick.AddListener(LoadGame);

        var saveButton = CreateButton(menuPanel.transform, "SaveButton", "Save", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -438f), new Vector2(260f, 40f));
        saveButton.onClick.AddListener(SaveGame);

        var quitButton = CreateButton(menuPanel.transform, "QuitButton", "Quit", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -484f), new Vector2(260f, 40f));
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

    private static Text CreateTextLabel(Transform parent, string objectName, string textValue, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, int fontSize)
    {
        var go = new GameObject(objectName, typeof(Text));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var label = go.GetComponent<Text>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.font = font;
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        label.text = textValue;
        return label;
    }

    private static InputField CreateInputField(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, string placeholderText)
    {
        var go = new GameObject(objectName, typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var bg = go.GetComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.96f);

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        var placeholderGo = new GameObject("Placeholder", typeof(Text));
        placeholderGo.transform.SetParent(go.transform, false);
        var placeholderRect = placeholderGo.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(10f, 6f);
        placeholderRect.offsetMax = new Vector2(-10f, -6f);
        var placeholder = placeholderGo.GetComponent<Text>();
        placeholder.font = font;
        placeholder.fontSize = 18;
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.color = new Color(0.35f, 0.35f, 0.35f, 0.8f);
        placeholder.text = placeholderText;

        var textGo = new GameObject("Text", typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 6f);
        textRect.offsetMax = new Vector2(-10f, -6f);
        var inputText = textGo.GetComponent<Text>();
        inputText.font = font;
        inputText.fontSize = 18;
        inputText.alignment = TextAnchor.MiddleLeft;
        inputText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        inputText.supportRichText = false;

        var input = go.GetComponent<InputField>();
        input.textComponent = inputText;
        input.placeholder = placeholder;
        input.lineType = InputField.LineType.SingleLine;

        return input;
    }

    private static Slider CreateSlider(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var backgroundGo = new GameObject("Background", typeof(Image));
        backgroundGo.transform.SetParent(go.transform, false);
        var backgroundRect = backgroundGo.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.25f);
        backgroundRect.anchorMax = new Vector2(1f, 0.75f);
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        var backgroundImage = backgroundGo.GetComponent<Image>();
        backgroundImage.color = new Color(0.18f, 0.18f, 0.18f, 0.95f);

        var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGo.transform.SetParent(go.transform, false);
        var fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0f);
        fillAreaRect.anchorMax = new Vector2(1f, 1f);
        fillAreaRect.offsetMin = new Vector2(8f, 0f);
        fillAreaRect.offsetMax = new Vector2(-14f, 0f);

        var fillGo = new GameObject("Fill", typeof(Image));
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        var fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImage = fillGo.GetComponent<Image>();
        fillImage.color = new Color(0.43f, 0.79f, 1f, 1f);

        var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGo.transform.SetParent(go.transform, false);
        var handleAreaRect = handleAreaGo.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = new Vector2(0f, 0f);
        handleAreaRect.anchorMax = new Vector2(1f, 1f);
        handleAreaRect.offsetMin = new Vector2(8f, 0f);
        handleAreaRect.offsetMax = new Vector2(-8f, 0f);

        var handleGo = new GameObject("Handle", typeof(Image));
        handleGo.transform.SetParent(handleAreaGo.transform, false);
        var handleRect = handleGo.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(12f, 24f);
        var handleImage = handleGo.GetComponent<Image>();
        handleImage.color = new Color(0.96f, 0.96f, 0.96f, 1f);

        var slider = go.GetComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        return slider;
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
            consecutiveExpiredResponsesCount = ufo != null ? ufo.ConsecutiveExpiredResponsesCount : 0,
            muleCargoSpills = ufo != null ? ufo.MuleCargoSpills : 0,
            warheadsSeized = ufo != null ? ufo.WarheadsSeized : 0,
            warheadsDelivered = ufo != null ? ufo.WarheadsDelivered : 0,
            difficulty = Difficulty.Active.ToString(),
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
        if (Difficulty.TryParse(data.difficulty, out var savedDifficulty))
        {
            // Applied when the reloaded scene starts the mission.
            Difficulty.SetSelected(savedDifficulty);
        }

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
            consecutiveExpiredResponsesCount = data.consecutiveExpiredResponsesCount,
            muleCargoSpills = data.muleCargoSpills,
            warheadsSeized = data.warheadsSeized,
            warheadsDelivered = data.warheadsDelivered,
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
            SyncCommanderInputFromState();
            SyncAudioControlsFromState();
            UpdateDifficultyButtonLabel();
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

    private void SyncCommanderInputFromState()
    {
        if (commanderNameInput == null)
        {
            return;
        }

        var manager = GameManager.Instance != null ? GameManager.Instance : UnityEngine.Object.FindAnyObjectByType<GameManager>();
        commanderNameInput.SetTextWithoutNotify(manager != null ? manager.CommanderName : string.Empty);
    }

    private void ApplyCommanderNameFromInput()
    {
        var manager = GameManager.Instance != null ? GameManager.Instance : UnityEngine.Object.FindAnyObjectByType<GameManager>();
        if (manager == null)
        {
            ShowStatusToast("Game manager missing");
            return;
        }

        var nextName = commanderNameInput != null ? commanderNameInput.text : string.Empty;
        manager.SetCommanderName(nextName, persist: true);
        SyncCommanderInputFromState();
        ShowStatusToast($"Commander: {manager.CommanderName}");
    }

    private void SyncAudioControlsFromState()
    {
        if (sfxVolumeSlider == null)
        {
            return;
        }

        var cuePlayer = UnityEngine.Object.FindAnyObjectByType<ProceduralAudioCuePlayer>();
        var volume = cuePlayer != null ? cuePlayer.GetSfxVolume() : PlayerPrefs.GetFloat("xcon.audio.sfxVolume", 0.22f);
        sfxVolumeSlider.SetValueWithoutNotify(Mathf.Clamp01(volume));
        UpdateSfxVolumeLabel(volume);
    }

    private void OnSfxVolumeChanged(float value)
    {
        var cuePlayer = UnityEngine.Object.FindAnyObjectByType<ProceduralAudioCuePlayer>();
        if (cuePlayer != null)
        {
            cuePlayer.SetSfxVolume(value, persist: true);
        }
        else
        {
            PlayerPrefs.SetFloat("xcon.audio.sfxVolume", Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }

        UpdateSfxVolumeLabel(value);
    }

    private void UpdateSfxVolumeLabel(float value)
    {
        if (sfxVolumeValueLabel == null)
        {
            return;
        }

        sfxVolumeValueLabel.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
    }

    private void SyncUfoSpawningControlsFromState()
    {
        if (ufoSpawnRateSlider == null)
        {
            return;
        }

        var ufoManager = UnityEngine.Object.FindAnyObjectByType<UFOManager>();
        var storedRate = PlayerPrefs.GetFloat(UfoSpawnRatePrefKey, -1f);
        var rate = storedRate >= 0f
            ? storedRate
            : (ufoManager != null ? ufoManager.BaseContactsPerMinute : 0.24f);

        var clamped = Mathf.Clamp(rate, UfoSpawnRateMin, UfoSpawnRateMax);
        if (ufoManager != null)
        {
            ufoManager.SetBaseContactsPerMinute(clamped);
        }
        ufoSpawnRateSlider.SetValueWithoutNotify(clamped);
        UpdateUfoSpawnRateLabel(clamped);
    }

    private void OnUfoSpawnRateChanged(float value)
    {
        var clamped = Mathf.Clamp(value, UfoSpawnRateMin, UfoSpawnRateMax);
        var ufoManager = UnityEngine.Object.FindAnyObjectByType<UFOManager>();
        if (ufoManager != null)
        {
            ufoManager.SetBaseContactsPerMinute(clamped);
        }

        PlayerPrefs.SetFloat(UfoSpawnRatePrefKey, clamped);
        PlayerPrefs.Save();
        UpdateUfoSpawnRateLabel(clamped);
    }

    private void UpdateUfoSpawnRateLabel(float value)
    {
        if (ufoSpawnRateValueLabel == null)
        {
            return;
        }

        ufoSpawnRateValueLabel.text = $"{Mathf.Clamp(value, UfoSpawnRateMin, UfoSpawnRateMax):0.00}/m";
    }

    private void OnDifficultyButtonClicked()
    {
        Difficulty.SetSelected(Difficulty.Next(Difficulty.Selected));
        UpdateDifficultyButtonLabel();

        if (Difficulty.Selected != Difficulty.Active)
        {
            ShowStatusToast($"{Difficulty.For(Difficulty.Selected).Label} applies from New Game");
        }
    }

    private void UpdateDifficultyButtonLabel()
    {
        if (difficultyButton == null)
        {
            return;
        }

        var text = difficultyButton.GetComponentInChildren<Text>();
        if (text == null)
        {
            return;
        }

        var selected = Difficulty.Selected;
        var label = Difficulty.For(selected).Label;
        text.text = selected == Difficulty.Active
            ? $"Difficulty: {label}"
            : $"Difficulty: {label} (next)";
    }

    private void SyncMapStyleControlFromState()
    {
        var current = PlayerPrefs.GetInt(MapStylePrefKey, 0);
        var manager = UnityEngine.Object.FindAnyObjectByType<WorldMapBackgroundManager>();
        if (manager != null)
        {
            current = manager.CurrentMapStyleIndex;
        }

        UpdateMapStyleButtonLabel(current);
    }

    private void OnMapStyleButtonClicked()
    {
        var manager = UnityEngine.Object.FindAnyObjectByType<WorldMapBackgroundManager>();
        var current = manager != null ? manager.CurrentMapStyleIndex : PlayerPrefs.GetInt(MapStylePrefKey, 0);
        var next = (current + 1) % 3;

        PlayerPrefs.SetInt(MapStylePrefKey, next);
        PlayerPrefs.Save();

        if (manager != null)
        {
            manager.ApplyMapStyleIndex(next, persist: true, immediate: false);
        }

        UpdateMapStyleButtonLabel(next);
    }

    private void UpdateMapStyleButtonLabel(int index)
    {
        if (mapStyleButton == null)
        {
            return;
        }

        var label = index switch
        {
            1 => "Flat Oval",
            2 => "Flat",
            _ => "Round Sphere",
        };

        var text = mapStyleButton.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.text = $"Map Style: {label}";
        }
    }
}
