using UnityEngine;
using XCon.UI.Boxes;
using XCon.Smuggling;
using System;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private const string RootName = "GameManager";
    private const float MissionHudWidth = 420f;
    private const float MissionHudMinHeight = 138f;

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
    private const string WelcomeTriggerKey = "thinking/welcome_intro";
    private const string ArchangelProtocolBriefTriggerKey = "thinking/archangel_protocol/brief";
    private const string ArchangelProtocolEngagedTriggerKey = "thinking/archangel_protocol/engaged";
    private const string ArchangelProtocolDisengagedTriggerKey = "thinking/archangel_protocol/disengaged";
    private const string ExpireStreakWarningTriggerKey = "warning/expire_streak";
    private const string IntroMoodPrefKey = "xcon.intro.moodIndex";
    private const string AutoLoadPriorityPrefKey = "xcon.save.autoload.priority";
    public const string AutoLoadStatePrefKey = "xcon.save.autoload.state";
    private static string BestScorePrefKey => Difficulty.BestScorePrefKey(Difficulty.Active) + GameModes.KeySuffix(GameModes.Active);
    private const string CommanderNamePrefKey = "xcon.player.commanderName";
    private const string SkipReadyScreenOncePrefKey = "xcon.startup.skipReadyScreenOnce";

    [Header("Player")]
    [SerializeField] private string defaultCommanderName = "Commander";
    [SerializeField] private bool alternateIntroMoodEachSession = true;

    [Header("Startup")]
    [SerializeField] private bool showReadyToPlayScreenOnStart = true;

    private string commanderName = "Commander";

    [Serializable]
    public struct AutoLoadState
    {
        public int priority;
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
        public int hazardSitesContained;
        public int regionsContaminated;
        public string networkStateJson;
        public string hazardSitesJson;
        public string squadLoadout;
        public int squadExposures;
        public string commanderName;
    }

    [Header("Mission")]
    [SerializeField] private bool enableMissionEndConditions = true;
    // Mission length and miss-streak limit come from the difficulty preset (see Difficulty.cs).
    [Range(10f, 100f)]
    [SerializeField] private float permitRiskFailThreshold = 100f;
    [SerializeField] private bool pauseGameWhenMissionEnds = true;
    [SerializeField] private bool enableConsecutiveExpireFail = true;

    // Endurance and Training have no clock: the mission never ends in victory.
    private static float missionDurationSeconds => GameModes.HasTimeLimit ? Difficulty.ActivePreset.MissionDurationSeconds : float.PositiveInfinity;
    private static int consecutiveExpireFailThreshold => Mathf.Max(1, Difficulty.ActivePreset.ConsecutiveExpireFailThreshold);

    [Header("Narrative")]
    [SerializeField] private bool enableDynamicMoodShiftPosts = true;
    [Range(20f, 95f)]
    [SerializeField] private float moodShiftPermitRiskThreshold = 65f;
    [Range(10f, 80f)]
    [SerializeField] private float moodShiftPermitRiskRecoveryThreshold = 40f;

    [Header("Scoring")]
    [SerializeField] private int responseScoreWeight = 150;
    [SerializeField] private int contactScoreWeight = 25;
    [SerializeField] private int educationScoreWeight = 12;
    [SerializeField] private int missedResponsePenaltyWeight = 90;
    [SerializeField] private int permitRiskPenaltyWeight = 9;
    [SerializeField] private int warheadSeizedBonusWeight = 500;
    [SerializeField] private int cargoSpillPenaltyWeight = 120;
    [SerializeField] private int warheadDeliveredPenaltyWeight = 600;
    [SerializeField] private int hazardSiteContainedBonusWeight = 150;
    [SerializeField] private int regionContaminatedPenaltyWeight = 500;
    [SerializeField] private int squadExposurePenaltyWeight = 100;
    [SerializeField] private float fallbackTimeScoreMultiplier = 0.8f;

    [Header("Medals")]
    [SerializeField] private int bronzeScoreThreshold = 1900;
    [SerializeField] private int silverScoreThreshold = 3150;
    [SerializeField] private int goldScoreThreshold = 4650;

    private float missionElapsedSeconds;
    private bool missionEnded;
    private GameObject missionEndPanel;
    private bool freeOpsMode;
    private ProceduralAudioCuePlayer cuePlayer;
    private UFOManager cachedUfoManager;
    private int bestScore;
    private int lastHighscorePlace; // Place on the local highscore list for the mission that just ended; 0 = not on it.
    private Text missionHudLabel;
    private GameObject missionHudPanel;
    private bool expireStreakWarningIssued;
    private NarrativeMood activeMissionMood = NarrativeMood.Military;
    private GameObject readyToPlayPanel;
    private Toggle readySkipToggle;
    private Button readyDifficultyButton;
    private Button readyModeButton;
    private bool skipReadyNextTime;
    private bool startupWelcomePublished;
    private static bool readyScreenShownThisSession;

    private enum NarrativeMood
    {
        Military = 0,
        Mysterious = 1,
    }

    private readonly struct StartupPost
    {
        public StartupPost(string sourceTag, string title, string body)
        {
            SourceTag = sourceTag;
            Title = title;
            Body = body;
        }

        public string SourceTag { get; }
        public string Title { get; }
        public string Body { get; }
    }

    public string CommanderName => commanderName;
    public float MissionElapsedSeconds => missionElapsedSeconds;
    public int BestScore => bestScore;

    private enum MissionEndReason
    {
        Victory,
        PermitRiskFailure,
        ConsecutiveExpireFailure,
    }

    private enum MedalTier
    {
        None,
        Bronze,
        Silver,
        Gold,
    }

    private void OnValidate()
    {
        permitRiskFailThreshold = Mathf.Clamp(permitRiskFailThreshold, 10f, 100f);
        moodShiftPermitRiskThreshold = Mathf.Clamp(moodShiftPermitRiskThreshold, 20f, 95f);
        moodShiftPermitRiskRecoveryThreshold = Mathf.Clamp(moodShiftPermitRiskRecoveryThreshold, 10f, 80f);
        if (moodShiftPermitRiskRecoveryThreshold >= moodShiftPermitRiskThreshold)
        {
            moodShiftPermitRiskRecoveryThreshold = Mathf.Max(10f, moodShiftPermitRiskThreshold - 10f);
        }

        responseScoreWeight = Mathf.Max(0, responseScoreWeight);
        contactScoreWeight = Mathf.Max(0, contactScoreWeight);
        educationScoreWeight = Mathf.Max(0, educationScoreWeight);
        missedResponsePenaltyWeight = Mathf.Max(0, missedResponsePenaltyWeight);
        permitRiskPenaltyWeight = Mathf.Max(0, permitRiskPenaltyWeight);
        warheadSeizedBonusWeight = Mathf.Max(0, warheadSeizedBonusWeight);
        cargoSpillPenaltyWeight = Mathf.Max(0, cargoSpillPenaltyWeight);
        warheadDeliveredPenaltyWeight = Mathf.Max(0, warheadDeliveredPenaltyWeight);
        hazardSiteContainedBonusWeight = Mathf.Max(0, hazardSiteContainedBonusWeight);
        regionContaminatedPenaltyWeight = Mathf.Max(0, regionContaminatedPenaltyWeight);
        squadExposurePenaltyWeight = Mathf.Max(0, squadExposurePenaltyWeight);
        fallbackTimeScoreMultiplier = Mathf.Max(0f, fallbackTimeScoreMultiplier);

        bronzeScoreThreshold = Mathf.Max(0, bronzeScoreThreshold);
        silverScoreThreshold = Mathf.Max(bronzeScoreThreshold, silverScoreThreshold);
        goldScoreThreshold = Mathf.Max(silverScoreThreshold, goldScoreThreshold);
    }

    // Builds and the Editor start the same way: the scene's GameManager (with its tuned Inspector values)
    // is used, and a fresh one only gets created after the scene loads if the scene has none.
    // It is not kept across scene loads: New Game, Load and Play Again reload the scene and rely on a
    // fresh GameManager running Start() again.
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessionStatics()
    {
        readyScreenShownThisSession = false;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        InitializeCommanderIdentity();
        Debug.Log($"GameManager started. Welcome, Commander {commanderName}!");

        ResetRuntimeSessionState();
        Difficulty.LockForMission();
        GameModes.LockForMission();
        EnsureMissionConfigIsValid();

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

        cachedUfoManager = GetComponent<UFOManager>();

        if (GetComponent<HazardSiteManager>() == null)
        {
            gameObject.AddComponent<HazardSiteManager>();
        }

        if (GetComponent<SmugglingNetwork>() == null)
        {
            gameObject.AddComponent<SmugglingNetwork>();
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

        if (GetComponent<GameMenuController>() == null)
        {
            gameObject.AddComponent<GameMenuController>();
        }

        if (GetComponent<ProceduralAudioCuePlayer>() == null)
        {
            gameObject.AddComponent<ProceduralAudioCuePlayer>();
        }

        cuePlayer = GetComponent<ProceduralAudioCuePlayer>();

        bestScore = PlayerPrefs.GetInt(BestScorePrefKey, 0);
        TryApplyAutoLoadedState();
        BuildMissionHud();
        RefreshMissionHud();

        var skipReadyScreenOnce = PlayerPrefs.GetInt(SkipReadyScreenOncePrefKey, 0) == 1;
        if (skipReadyScreenOnce)
        {
            PlayerPrefs.DeleteKey(SkipReadyScreenOncePrefKey);
            PlayerPrefs.Save();
        }

        if (showReadyToPlayScreenOnStart && !readyScreenShownThisSession && !skipReadyScreenOnce)
        {
            ShowReadyToPlayPanel();
            return;
        }

        BeginOperationalStart();
    }

    private void EnsureMissionConfigIsValid()
    {
        permitRiskFailThreshold = Mathf.Clamp(permitRiskFailThreshold, 10f, 100f);
    }

    private void ResetRuntimeSessionState()
    {
        missionElapsedSeconds = 0f;
        missionEnded = false;
        freeOpsMode = false;
        expireStreakWarningIssued = false;
        activeMissionMood = NarrativeMood.Military;
        startupWelcomePublished = false;

        GameMenuController.SetPaused(false);

        DestroyIfExists("MissionEndCanvas");
        DestroyIfExists("MissionHudCanvas");

        missionEndPanel = null;
        missionHudPanel = null;
        missionHudLabel = null;
        readyToPlayPanel = null;
        readySkipToggle = null;
        readyDifficultyButton = null;
        skipReadyNextTime = false;
    }

    private void BeginOperationalStart()
    {
        SetBoxCanvasVisible(true);

        if (startupWelcomePublished)
        {
            return;
        }

        startupWelcomePublished = true;
        StartCoroutine(PublishStartupWelcomePrompt());
    }

    private void ShowReadyToPlayPanel()
    {
        if (readyToPlayPanel != null)
        {
            return;
        }

        readyScreenShownThisSession = true;
        GameMenuController.SetPaused(true);

        if (missionHudPanel != null)
        {
            missionHudPanel.SetActive(false);
        }

        SetBoxCanvasVisible(false);

        var canvasGo = new GameObject("ReadyToPlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3000;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        readyToPlayPanel = new GameObject("ReadyToPlayPanel", typeof(Image));
        readyToPlayPanel.transform.SetParent(canvasGo.transform, false);

        var panelImage = readyToPlayPanel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.9f);

        var panelRect = readyToPlayPanel.GetComponent<RectTransform>();
        // Sit in the bottom half so it doesn't cover the top-of-screen boxes.
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 30f);
        panelRect.sizeDelta = new Vector2(680f, 490f);

        var messageBox = new GameObject("ReadyMessageBox", typeof(Image));
        messageBox.transform.SetParent(readyToPlayPanel.transform, false);
        var messageBoxImage = messageBox.GetComponent<Image>();
        messageBoxImage.color = new Color(0.06f, 0.06f, 0.06f, 0.82f);
        var messageBoxRect = messageBox.GetComponent<RectTransform>();
        messageBoxRect.anchorMin = new Vector2(0.5f, 1f);
        messageBoxRect.anchorMax = new Vector2(0.5f, 1f);
        messageBoxRect.pivot = new Vector2(0.5f, 1f);
        messageBoxRect.anchoredPosition = new Vector2(0f, -22f);
        messageBoxRect.sizeDelta = new Vector2(620f, 220f);

        var title = CreatePanelLabel(messageBox.transform, "ReadyTitle", "Get Ready To Play", 36, new Vector2(0f, -16f));
        title.alignment = TextAnchor.MiddleCenter;
        // Default label rect is 220 tall; centred text in it landed on top of the body below.
        title.rectTransform.sizeDelta = new Vector2(560f, 48f);

        var body = CreatePanelLabel(
            messageBox.transform,
            "ReadyBody",
            "You are about to enter command operations.\nReview the board, then press OK to begin.\n\n" +
            "Keys: 1-4 respond  |  L squad kit  |  H hazmat team  |  Esc menu",
            20,
            new Vector2(0f, -76f));
        body.alignment = TextAnchor.UpperCenter;
        body.rectTransform.sizeDelta = new Vector2(560f, 120f);

        readySkipToggle = CreatePanelToggle(
            readyToPlayPanel.transform,
            "ReadySkipNextTimeToggle",
            "Skip next time",
            new Vector2(0f, -384f),
            new Vector2(220f, 28f));
        readySkipToggle.SetIsOnWithoutNotify(false);
        readySkipToggle.onValueChanged.AddListener(OnReadySkipNextTimeChanged);

        readyModeButton = CreatePanelButton(readyToPlayPanel.transform, "ReadyModeButton", BuildModeButtonLabel(), new Vector2(0f, -276f), new Vector2(520f, 44f));
        readyModeButton.onClick.AddListener(CycleModeOnReadyPanel);

        readyDifficultyButton = CreatePanelButton(readyToPlayPanel.transform, "ReadyDifficultyButton", BuildDifficultyButtonLabel(), new Vector2(0f, -328f), new Vector2(520f, 44f));
        readyDifficultyButton.onClick.AddListener(CycleDifficultyOnReadyPanel);

        var okButton = CreatePanelButton(readyToPlayPanel.transform, "ReadyOkButton", "OK", new Vector2(0f, -436f), new Vector2(180f, 48f));
        okButton.onClick.AddListener(ConfirmReadyToPlay);
    }

    private static string BuildDifficultyButtonLabel()
    {
        var preset = Difficulty.ActivePreset;
        return GameModes.HasTimeLimit
            ? $"Difficulty: {preset.Label} ({Mathf.RoundToInt(preset.MissionDurationSeconds / 60f)} min)"
            : $"Difficulty: {preset.Label}";
    }

    private static string BuildModeButtonLabel()
    {
        var mode = GameModes.Active;
        return $"Mode: {GameModes.Label(mode)} ({GameModes.ShortDescription(mode)})";
    }

    /// <summary>Like difficulty: the mission has not started yet, so the change applies right away.</summary>
    private void CycleModeOnReadyPanel()
    {
        GameModes.SetSelected(GameModes.Next(GameModes.Active));
        GameModes.LockForMission();
        bestScore = PlayerPrefs.GetInt(BestScorePrefKey, 0);
        SetButtonLabel(readyModeButton, BuildModeButtonLabel());
        SetButtonLabel(readyDifficultyButton, BuildDifficultyButtonLabel());
        RefreshMissionHud();
    }

    private static void SetButtonLabel(Button button, string text)
    {
        var label = button != null ? button.GetComponentInChildren<Text>() : null;
        if (label != null)
        {
            label.text = text;
        }
    }

    /// <summary>The mission has not started yet on the Get Ready panel, so the change applies right away.</summary>
    private void CycleDifficultyOnReadyPanel()
    {
        Difficulty.SetSelected(Difficulty.Next(Difficulty.Active));
        Difficulty.LockForMission();
        bestScore = PlayerPrefs.GetInt(BestScorePrefKey, 0);

        if (readyDifficultyButton != null)
        {
            var label = readyDifficultyButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = BuildDifficultyButtonLabel();
            }
        }

        RefreshMissionHud();
    }

    private void OnReadySkipNextTimeChanged(bool isOn)
    {
        skipReadyNextTime = isOn;
    }

    private void ConfirmReadyToPlay()
    {
        if (readyToPlayPanel != null)
        {
            if (readySkipToggle != null)
            {
                readySkipToggle.onValueChanged.RemoveListener(OnReadySkipNextTimeChanged);
                readySkipToggle = null;
            }

            var canvas = readyToPlayPanel.transform.parent != null
                ? readyToPlayPanel.transform.parent.gameObject
                : readyToPlayPanel;
            Destroy(canvas);
            readyToPlayPanel = null;
        }

        if (skipReadyNextTime)
        {
            PlayerPrefs.SetInt(SkipReadyScreenOncePrefKey, 1);
        }
        else
        {
            PlayerPrefs.DeleteKey(SkipReadyScreenOncePrefKey);
        }

        PlayerPrefs.Save();
        skipReadyNextTime = false;

        GameMenuController.SetPaused(false);

        if (missionHudPanel != null && !missionEnded)
        {
            missionHudPanel.SetActive(true);
        }

        SetBoxCanvasVisible(true);

        BeginOperationalStart();
    }

    private static void SetBoxCanvasVisible(bool visible)
    {
        var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        if (canvases == null || canvases.Length == 0)
        {
            return;
        }

        for (var i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (canvas == null)
            {
                continue;
            }

            var go = canvas.gameObject;
            if (go == null || go.name != "BoxCanvas")
            {
                continue;
            }

            go.SetActive(visible);
            return;
        }
    }

    private static Toggle CreatePanelToggle(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size)
    {
        var go = new GameObject(name, typeof(Toggle));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var toggle = go.GetComponent<Toggle>();

        var bg = new GameObject("Background", typeof(Image));
        bg.transform.SetParent(go.transform, false);
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.5f);
        bgRect.anchorMax = new Vector2(0f, 0.5f);
        bgRect.pivot = new Vector2(0f, 0.5f);
        bgRect.anchoredPosition = Vector2.zero;
        bgRect.sizeDelta = new Vector2(22f, 22f);
        var bgImage = bg.GetComponent<Image>();
        bgImage.color = new Color(1f, 1f, 1f, 0.92f);

        var check = new GameObject("Checkmark", typeof(Image));
        check.transform.SetParent(bg.transform, false);
        var checkRect = check.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.5f, 0.5f);
        checkRect.anchorMax = new Vector2(0.5f, 0.5f);
        checkRect.pivot = new Vector2(0.5f, 0.5f);
        checkRect.anchoredPosition = Vector2.zero;
        checkRect.sizeDelta = new Vector2(14f, 14f);
        var checkImage = check.GetComponent<Image>();
        checkImage.color = new Color(0.2f, 0.85f, 0.3f, 1f);

        var labelGo = new GameObject("Label", typeof(Text));
        labelGo.transform.SetParent(go.transform, false);
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(30f, 0f);
        labelRect.offsetMax = Vector2.zero;

        var labelText = labelGo.GetComponent<Text>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        labelText.font = font;
        labelText.fontSize = 18;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.color = Color.white;
        labelText.text = label;

        toggle.graphic = checkImage;
        toggle.targetGraphic = bgImage;
        return toggle;
    }

    private static void DestroyIfExists(string objectName)
    {
        var go = GameObject.Find(objectName);
        if (go == null)
        {
            return;
        }

        Destroy(go);
    }

    private void TryApplyAutoLoadedPriority()
    {
        if (!PlayerPrefs.HasKey(AutoLoadPriorityPrefKey))
        {
            return;
        }

        var raw = PlayerPrefs.GetInt(AutoLoadPriorityPrefKey, (int)StrategicPriority.Coverage);
        PlayerPrefs.DeleteKey(AutoLoadPriorityPrefKey);
        PlayerPrefs.Save();

        if (raw < (int)StrategicPriority.Coverage || raw > (int)StrategicPriority.Response)
        {
            raw = (int)StrategicPriority.Coverage;
        }

        SetPriority((StrategicPriority)raw, publishFeedback: true);
    }

    private void TryApplyAutoLoadedState()
    {
        if (PlayerPrefs.HasKey(AutoLoadStatePrefKey))
        {
            var json = PlayerPrefs.GetString(AutoLoadStatePrefKey, string.Empty);
            PlayerPrefs.DeleteKey(AutoLoadStatePrefKey);
            PlayerPrefs.Save();

            if (!string.IsNullOrWhiteSpace(json))
            {
                var state = JsonUtility.FromJson<AutoLoadState>(json);
                ApplyAutoLoadState(state);
                return;
            }
        }

        // Backward compatibility for older save data that only carried priority.
        TryApplyAutoLoadedPriority();
    }

    private void ApplyAutoLoadState(AutoLoadState state)
    {
        if (!string.IsNullOrWhiteSpace(state.commanderName))
        {
            SetCommanderName(state.commanderName, persist: true);
        }

        var rawPriority = state.priority;
        if (rawPriority < (int)StrategicPriority.Coverage || rawPriority > (int)StrategicPriority.Response)
        {
            rawPriority = (int)StrategicPriority.Coverage;
        }

        SetPriority((StrategicPriority)rawPriority, publishFeedback: false);

        missionElapsedSeconds = Mathf.Max(0f, state.missionElapsedSeconds);
        bestScore = Mathf.Max(bestScore, state.bestScore);
        PlayerPrefs.SetInt(BestScorePrefKey, bestScore);
        PlayerPrefs.Save();

        if (cachedUfoManager != null)
        {
            cachedUfoManager.ApplyPersistentState(
                state.education,
                state.permitRisk,
                state.totalContactsSpawned,
                state.respondedContactsCount,
                state.expiredResponsesCount,
                state.consecutiveExpiredResponsesCount);
            cachedUfoManager.ApplyMuleCargoState(
                state.muleCargoSpills,
                state.warheadsSeized,
                state.warheadsDelivered);
        }

        var hazardSites = GetHazardSites();
        if (hazardSites != null)
        {
            hazardSites.ApplyPersistentState(state.hazardSitesContained, state.regionsContaminated);
            hazardSites.ImportOpenSitesJson(state.hazardSitesJson);
        }

        var network = GetSmugglingNetwork();
        if (network != null)
        {
            network.ImportStateJson(state.networkStateJson);
        }

        var squad = GetSquad();
        if (squad != null)
        {
            squad.ApplyPersistentState(state.squadLoadout, state.squadExposures);
        }
    }

    public void SetCommanderName(string name, bool persist)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? defaultCommanderName : name.Trim();
        commanderName = trimmed;

        if (!persist)
        {
            return;
        }

        PlayerPrefs.SetString(CommanderNamePrefKey, commanderName);
        PlayerPrefs.Save();
    }

    private void InitializeCommanderIdentity()
    {
        var stored = PlayerPrefs.GetString(CommanderNamePrefKey, string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(stored))
        {
            commanderName = stored;
            return;
        }

        var osName = Environment.UserName;
        if (!string.IsNullOrWhiteSpace(osName))
        {
            SetCommanderName(osName, persist: true);
            return;
        }

        SetCommanderName(defaultCommanderName, persist: true);
    }

    private IEnumerator PublishStartupWelcomePrompt()
    {
        // Box system bootstraps at runtime; wait briefly to ensure queue availability.
        var timeoutAt = Time.realtimeSinceStartup + 2f;
        BoxMessageQueue queue = null;
        while (Time.realtimeSinceStartup < timeoutAt)
        {
            queue = BoxMessageQueue.Instance != null
                ? BoxMessageQueue.Instance
                : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();

            if (queue != null)
            {
                break;
            }

            yield return null;
        }

        if (queue == null)
        {
            yield break;
        }

        var primaryMood = ResolveStartupMood();
        var secondaryMood = primaryMood == NarrativeMood.Military
            ? NarrativeMood.Mysterious
            : NarrativeMood.Military;

        var welcomePost = BuildWelcomePost(primaryMood);
        var behaviorPost = BuildArchangelProtocolBriefPost(secondaryMood);

        queue.Publish(new BoxMessage(
            triggerKey: WelcomeTriggerKey,
            channel: BoxChannel.Thinking,
            severity: BoxSeverity.Info,
            sourceTag: welcomePost.SourceTag,
            title: welcomePost.Title,
            body: welcomePost.Body));

        queue.Publish(new BoxMessage(
            triggerKey: ArchangelProtocolBriefTriggerKey,
            channel: BoxChannel.Thinking,
            severity: BoxSeverity.Info,
            sourceTag: behaviorPost.SourceTag,
            title: behaviorPost.Title,
            body: behaviorPost.Body));
    }

    private NarrativeMood ResolveStartupMood()
    {
        if (!alternateIntroMoodEachSession)
        {
            return NarrativeMood.Military;
        }

        var stored = PlayerPrefs.GetInt(IntroMoodPrefKey, 0);
        var mood = stored == 1 ? NarrativeMood.Mysterious : NarrativeMood.Military;

        var next = mood == NarrativeMood.Military ? 1 : 0;
        PlayerPrefs.SetInt(IntroMoodPrefKey, next);
        PlayerPrefs.Save();

        return mood;
    }

    private StartupPost BuildWelcomePost(NarrativeMood mood)
    {
        if (mood == NarrativeMood.Mysterious)
        {
            return new StartupPost(
                sourceTag: "Whisper",
                title: $"Welcome Back, Commander {commanderName}",
                body:
                    $"The sky is speaking again, Commander {commanderName}.\n" +
                    "Signals hide intent; your choices reveal it.\n\n" +
                    "Core controls:\n" +
                    "Q W E: Strategic Priority (Coverage / Research / Response)\n" +
                    "1-4: Contact Response (Ignore / Track / Intercept / Dispatch)\n" +
                    "H: Send the hazmat team to the oldest hazard site (left when a Mule spills cargo)\n" +
                    "L: Change the squad kit used by Dispatch (Standard / Hazmat / Heavy / Recon)\n" +
                    "Box Feed: Read 'Archangel Protocol Brief' posts for live intent clues\n" +
                    "Esc: Dismiss non-interactive messages\n\n" +
                    "Choose a priority. The pattern will answer.");
        }

        return new StartupPost(
            sourceTag: "Commander",
            title: $"Welcome, Commander {commanderName}",
            body:
                $"Welcome to X-Con U.F.O Defence, Commander {commanderName}.\n" +
                "Your mission is to detect contacts, assess incidents, and choose the right response before threats escalate.\n\n" +
                "Core controls:\n" +
                "Q W E: Strategic Priority (Coverage / Research / Response)\n" +
                "1-4: Contact Response (Ignore / Track / Intercept / Dispatch)\n" +
                "H: Send the hazmat team to the oldest hazard site (left when a Mule spills cargo)\n" +
                "L: Change the squad kit used by Dispatch (Standard / Hazmat / Heavy / Recon)\n" +
                "Box Feed: Read 'Archangel Protocol Brief' posts for live intent clues\n" +
                "Esc: Dismiss non-interactive messages\n\n" +
                "Start by selecting your Strategic Priority. Good luck.");
    }

    private static StartupPost BuildArchangelProtocolBriefPost(NarrativeMood mood)
    {
        if (mood == NarrativeMood.Mysterious)
        {
            return new StartupPost(
                sourceTag: "Signals",
                title: "Archangel Protocol Brief",
                body:
                    "Tone shift detected. Read behavior, not noise.\n" +
                    "- Grey contact patterns usually mask intent through probes and misdirection.\n" +
                    "- Dark patterns indicate pressure, tempo, and likely escalation.\n" +
                    "- Solo signatures often lower immediate threat, but never zero.\n\n" +
                    "Respond by confidence:\n" +
                    "1) Track uncertainty.\n" +
                    "2) Intercept hostile momentum.\n" +
                    "3) Dispatch only when civilian risk is immediate.");
        }

        return new StartupPost(
            sourceTag: "Intelligence",
            title: "Archangel Protocol Brief",
            body:
                "Use behavior posts to infer intent before committing assets.\n" +
                "- Grey activity often probes, misdirects, or observes.\n" +
                "- Dark activity is high-risk; expect escalation pressure.\n" +
                "- Solo crew estimate usually means lower immediate threat.\n\n" +
                "Action guide:\n" +
                "1) Track when confidence is low.\n" +
                "2) Intercept when behavior trends hostile.\n" +
                "3) Dispatch when risk to civilians is immediate.");
    }

    private void Update()
    {
        TickMissionState();
        RefreshMissionHud();

        if (missionEnded)
        {
            return;
        }

        var queue = BoxMessageQueue.Instance;
        if (queue == null)
        {
            return;
        }

        if (!TryGetPriorityChoice(out var choice))
        {
            return;
        }

        SetPriority(choice, publishFeedback: true);
    }

    public void SetPriority(StrategicPriority choice, bool publishFeedback)
    {
        currentPriority = choice;

        if (!publishFeedback)
        {
            return;
        }

        var body = BuildPriorityPromptBody(choice);

        var queue = BoxMessageQueue.Instance;
        if (queue == null)
        {
            return;
        }

        // If the priority box is still showing or queued, update it in place. Otherwise show it
        // again, even if it was shown a moment ago (the queue normally drops repeats within seconds).
        if (queue.UpdateBody(PickPriorityTriggerKey, body) > 0)
        {
            return;
        }

        queue.ForgetRecentPublish(PickPriorityTriggerKey);
        queue.Publish(new BoxMessage(
            triggerKey: PickPriorityTriggerKey,
            channel: BoxChannel.Thinking,
            severity: BoxSeverity.Info,
            sourceTag: "Commander",
            title: "Strategic Priority Updated",
            body: body));
    }

    /// <summary>Text for the Strategic Priority box, always naming the priority in effect.</summary>
    public static string BuildPriorityPromptBody(StrategicPriority current)
    {
        return $"Strategic Priority (press anytime):\nQ) Coverage\nW) Research\nE) Response\n\nCurrent: {current}\n\nTip: Use 1-4 on active contact prompts to choose response.";
    }

    public static string BuildPriorityPromptBody()
    {
        return BuildPriorityPromptBody(Instance != null ? Instance.CurrentPriority : StrategicPriority.Coverage);
    }

    private static bool TryGetPriorityChoice(out StrategicPriority choice)
    {
        choice = default;
        if (GameMenuController.IsTypingInTextField)
        {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {

            // Q/W/E only: 1-4 belong to contact responses, so they must not also change the priority.
            if (keyboard.qKey.wasPressedThisFrame) { choice = StrategicPriority.Coverage; return true; }
            if (keyboard.wKey.wasPressedThisFrame) { choice = StrategicPriority.Research; return true; }
            if (keyboard.eKey.wasPressedThisFrame) { choice = StrategicPriority.Response; return true; }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Q)) { choice = StrategicPriority.Coverage; return true; }
        if (Input.GetKeyDown(KeyCode.W)) { choice = StrategicPriority.Research; return true; }
        if (Input.GetKeyDown(KeyCode.E)) { choice = StrategicPriority.Response; return true; }
#endif

        return false;
    }

    private void TickMissionState()
    {
        if (!enableMissionEndConditions || missionEnded || freeOpsMode)
        {
            return;
        }

        if (missionDurationSeconds < 1f)
        {
            return;
        }

        if (!GameMenuController.IsPaused)
        {
            missionElapsedSeconds += Time.unscaledDeltaTime;
        }

        if (GameModes.IsTraining)
        {
            return;
        }

        var ufo = cachedUfoManager != null ? cachedUfoManager : GetComponent<UFOManager>();
        cachedUfoManager = ufo;
        EvaluateDynamicMoodShift(ufo);

        if (ufo != null && ufo.PermitRisk >= permitRiskFailThreshold)
        {
            EndMission(MissionEndReason.PermitRiskFailure, ufo);
            return;
        }

        if (ufo != null && enableConsecutiveExpireFail)
        {
            var streak = ufo.ConsecutiveExpiredResponsesCount;
            var warningAt = Mathf.Max(1, consecutiveExpireFailThreshold - 1);

            if (consecutiveExpireFailThreshold > 1 && streak >= warningAt && streak < consecutiveExpireFailThreshold)
            {
                if (!expireStreakWarningIssued)
                {
                    PublishExpireStreakWarning(streak, consecutiveExpireFailThreshold);
                    cuePlayer?.PlayExpireStreakWarning();
                    expireStreakWarningIssued = true;
                }
            }
            else if (streak < warningAt)
            {
                expireStreakWarningIssued = false;
            }

            if (streak >= consecutiveExpireFailThreshold)
            {
                EndMission(MissionEndReason.ConsecutiveExpireFailure, ufo);
                return;
            }
        }

        if (missionElapsedSeconds >= missionDurationSeconds)
        {
            EndMission(MissionEndReason.Victory, ufo);
        }
    }

    private void EvaluateDynamicMoodShift(UFOManager ufo)
    {
        if (!enableDynamicMoodShiftPosts || missionEnded || ufo == null)
        {
            return;
        }

        var warningAt = Mathf.Max(1, consecutiveExpireFailThreshold - 1);
        var hasStreakPressure = enableConsecutiveExpireFail
            && consecutiveExpireFailThreshold > 1
            && ufo.ConsecutiveExpiredResponsesCount >= warningAt;
        var hasRiskPressure = ufo.PermitRisk >= moodShiftPermitRiskThreshold;
        var shouldGoMysterious = hasRiskPressure || hasStreakPressure;

        var recoveredRisk = ufo.PermitRisk <= moodShiftPermitRiskRecoveryThreshold;
        var recoveredStreak = !enableConsecutiveExpireFail || ufo.ConsecutiveExpiredResponsesCount <= 0;
        var shouldReturnMilitary = recoveredRisk && recoveredStreak;

        if (activeMissionMood == NarrativeMood.Military && shouldGoMysterious)
        {
            activeMissionMood = NarrativeMood.Mysterious;
            PublishMoodShiftMysterious(ufo);
            return;
        }

        if (activeMissionMood == NarrativeMood.Mysterious && shouldReturnMilitary)
        {
            activeMissionMood = NarrativeMood.Military;
            PublishMoodShiftMilitary(ufo);
        }
    }

    private void PublishMoodShiftMysterious(UFOManager ufo)
    {
        var queue = BoxMessageQueue.Instance != null
            ? BoxMessageQueue.Instance
            : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
        if (queue == null)
        {
            return;
        }

        var permitRisk = Mathf.RoundToInt(ufo.PermitRisk);
        var streak = ufo.ConsecutiveExpiredResponsesCount;
        queue.Publish(new BoxMessage(
            triggerKey: ArchangelProtocolEngagedTriggerKey,
            channel: BoxChannel.Thinking,
            severity: BoxSeverity.Warn,
            sourceTag: "Archangel",
            title: "Archangel Protocol Engaged",
            body:
                $"Pressure pattern rising: PermitRisk {permitRisk}. Miss streak {streak}.\n" +
                "Do not chase every signal. Track uncertainty, then cut only confirmed threats."));
        cuePlayer?.PlayMoodShiftMysterious();
    }

    private void PublishMoodShiftMilitary(UFOManager ufo)
    {
        var queue = BoxMessageQueue.Instance != null
            ? BoxMessageQueue.Instance
            : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
        if (queue == null)
        {
            return;
        }

        var permitRisk = Mathf.RoundToInt(ufo.PermitRisk);
        queue.Publish(new BoxMessage(
            triggerKey: ArchangelProtocolDisengagedTriggerKey,
            channel: BoxChannel.Thinking,
            severity: BoxSeverity.Info,
            sourceTag: "Operations",
            title: "Archangel Protocol Stand Down",
            body:
                $"Pressure easing: PermitRisk {permitRisk}.\n" +
                "Maintain discipline. Resume standard response doctrine and preserve readiness."));
        cuePlayer?.PlayMoodShiftMilitary();
    }

    // Playtest helpers: right-click the GameManager component in the Inspector during Play mode.
    [ContextMenu("Debug/End Mission Now (Victory)")]
    private void DebugEndMissionVictory() => EndMission(MissionEndReason.Victory, GetComponent<UFOManager>());

    [ContextMenu("Debug/End Mission Now (Permit Risk Failure)")]
    private void DebugEndMissionFailure() => EndMission(MissionEndReason.PermitRiskFailure, GetComponent<UFOManager>());

    private void EndMission(MissionEndReason reason, UFOManager ufo)
    {
        if (missionEnded)
        {
            return;
        }

        missionEnded = true;

        Debug.Log($"[Mission] Ended: {reason}. elapsed={missionElapsedSeconds:0.00}s duration={missionDurationSeconds:0.00}s permitRisk={(ufo != null ? ufo.PermitRisk : -1f):0.0} threshold={permitRiskFailThreshold:0.0}");

        if (ufo != null)
        {
            ufo.CancelPendingResponse();
        }

        if (pauseGameWhenMissionEnds)
        {
            GameMenuController.SetPaused(true);
        }

        var score = CalculateMissionScore(ufo);
        var isNewBest = !GameModes.IsTraining && score > bestScore;
        if (isNewBest)
        {
            bestScore = score;
            PlayerPrefs.SetInt(BestScorePrefKey, bestScore);
            PlayerPrefs.Save();
        }

        var medalTier = GetMedalTier(score);
        lastHighscorePlace = GameModes.IsTraining ? 0 : HighscoreTable.Submit(GameModes.Active, Difficulty.Active, new HighscoreTable.Entry
        {
            name = commanderName,
            score = score,
            medal = medalTier == MedalTier.None ? string.Empty : medalTier.ToString(),
            victory = reason == MissionEndReason.Victory,
            seconds = Mathf.FloorToInt(missionElapsedSeconds),
            dateUtc = DateTime.UtcNow.ToString("o"),
        });

        if (cuePlayer != null)
        {
            if (reason == MissionEndReason.Victory)
            {
                cuePlayer.PlayMissionVictory();
            }
            else
            {
                cuePlayer.PlayMissionFailure();
            }

            cuePlayer.PlayMedalAward((int)medalTier);
        }

        if (missionHudPanel != null)
        {
            missionHudPanel.SetActive(false);
        }

        ShowMissionEndPanel(reason, ufo, score, bestScore, isNewBest);
    }

    private void BuildMissionHud()
    {
        if (!enableMissionEndConditions || missionHudLabel != null)
        {
            return;
        }

        var canvasGo = new GameObject("MissionHudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        missionHudPanel = new GameObject("MissionHudPanel", typeof(Image));
        missionHudPanel.transform.SetParent(canvasGo.transform, false);

        var panelImage = missionHudPanel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.58f);

        var panelRect = missionHudPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(20f, -20f);
        panelRect.sizeDelta = new Vector2(MissionHudWidth, MissionHudMinHeight);

        var labelGo = new GameObject("MissionHudLabel", typeof(Text));
        labelGo.transform.SetParent(missionHudPanel.transform, false);

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 8f);
        labelRect.offsetMax = new Vector2(-12f, -8f);

        missionHudLabel = labelGo.GetComponent<Text>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        missionHudLabel.font = font;
        missionHudLabel.fontSize = 19;
        missionHudLabel.alignment = TextAnchor.UpperLeft;
        missionHudLabel.supportRichText = true;
        missionHudLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        missionHudLabel.verticalOverflow = VerticalWrapMode.Overflow;
        missionHudLabel.color = Color.white;
    }

    private void RefreshMissionHud()
    {
        if (!enableMissionEndConditions || missionHudLabel == null || missionEnded)
        {
            return;
        }

        var ufo = cachedUfoManager != null ? cachedUfoManager : GetComponent<UFOManager>();
        cachedUfoManager = ufo;

        var score = CalculateMissionScore(ufo);
        // Mission counts down; Endurance and Training count up.
        var totalSeconds = GameModes.HasTimeLimit
            ? Mathf.CeilToInt(Mathf.Max(0f, missionDurationSeconds - missionElapsedSeconds))
            : Mathf.FloorToInt(missionElapsedSeconds);
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        var timeLabel = GameModes.HasTimeLimit ? "Time Left" : "Time";
        var modeTitle = GameModes.Label(GameModes.Active).ToUpperInvariant();
        var medalTier = GetMedalTier(score);
        var nextMedalTarget = medalTier switch
        {
            MedalTier.None => bronzeScoreThreshold,
            MedalTier.Bronze => silverScoreThreshold,
            MedalTier.Silver => goldScoreThreshold,
            _ => -1,
        };

        if (ufo == null)
        {
            missionHudLabel.text =
                $"{modeTitle}\n" +
                $"{timeLabel}: {minutes}:{seconds:00}\n" +
                $"Score: {score}  Best: {bestScore}\n" +
                $"Priority: {currentPriority}\n" +
                BuildExpireStreakHudText(null);
            FitMissionHudPanelToText();
            return;
        }

        var pausedSuffix = GameMenuController.IsPaused ? "  [PAUSED]" : string.Empty;
        var opsSuffix = freeOpsMode ? "  [FREE OPS]" : string.Empty;
        var archangelSuffix = activeMissionMood == NarrativeMood.Mysterious ? "  [ARCHANGEL PROTOCOL]" : string.Empty;
        var difficultySuffix = $"  [{Difficulty.ActivePreset.Label.ToUpperInvariant()}]";
        var medalText = MedalTierToLabel(medalTier);
        var progressText = nextMedalTarget > 0
            ? $"Next Medal At: {nextMedalTarget}"
            : "Top Medal Reached";
        missionHudLabel.text =
            $"{modeTitle}{difficultySuffix}{pausedSuffix}{opsSuffix}{archangelSuffix}\n" +
            $"{timeLabel}: {minutes}:{seconds:00}  Score: {score}\n" +
            (GameModes.IsTraining ? "<color=#9FD3FF>Training: no fail, no highscores. U = spawn contact</color>\n" : string.Empty) +
            $"Best: {bestScore}  Medal: {medalText}\n" +
            $"Priority: {currentPriority}  {progressText}\n" +
            $"Contacts: {ufo.TotalContactsSpawned}  Responses: {ufo.RespondedContactsCount}  Missed: {ufo.ExpiredResponsesCount}\n" +
            $"Education: {Mathf.RoundToInt(ufo.Education)}  PermitRisk: {Mathf.RoundToInt(ufo.PermitRisk)}\n" +
            BuildExpireStreakHudText(ufo) +
            BuildHazardHudText();
        FitMissionHudPanelToText();
    }

    // The HUD gains lines as systems come online (squad, hazards, smuggling network),
    // so grow the backing panel to the text instead of letting it spill over the map.
    private void FitMissionHudPanelToText()
    {
        if (missionHudPanel == null || missionHudLabel == null)
        {
            return;
        }

        var panelRect = missionHudPanel.GetComponent<RectTransform>();
        const float verticalPadding = 16f; // matches label offsetMin.y (8) + offsetMax.y (8)
        var targetHeight = Mathf.Max(MissionHudMinHeight, missionHudLabel.preferredHeight + verticalPadding);
        if (!Mathf.Approximately(panelRect.sizeDelta.y, targetHeight))
        {
            panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, targetHeight);
        }
    }

    private SquadManager GetSquad()
    {
        return SquadManager.Instance != null ? SquadManager.Instance : GetComponent<SquadManager>();
    }

    private string BuildHazardHudText()
    {
        var squad = GetSquad();
        var squadLine = squad != null ? "\n" + squad.BuildHudLine() : string.Empty;
        return squadLine + BuildHazardAndNetworkHudText();
    }

    private string BuildHazardAndNetworkHudText()
    {
        var sites = GetHazardSites();
        var line = sites != null ? sites.BuildHudLine() : string.Empty;
        var network = GetSmugglingNetwork();
        var networkLine = network != null ? network.BuildHudLine() : string.Empty;

        var text = string.IsNullOrEmpty(line) ? string.Empty : "\n" + line;
        return string.IsNullOrEmpty(networkLine) ? text : text + "\n" + networkLine;
    }

    private SmugglingNetwork GetSmugglingNetwork()
    {
        return SmugglingNetwork.Instance != null ? SmugglingNetwork.Instance : GetComponent<SmugglingNetwork>();
    }

    private readonly System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>> networkScoreLines = new();

    /// <summary>Network and warhead story score lines (label, points), refreshed on each call.</summary>
    private System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>> GetNetworkScoreLines()
    {
        networkScoreLines.Clear();
        var network = GetSmugglingNetwork();
        if (network != null)
        {
            network.AppendScoreLines(networkScoreLines);
        }

        return networkScoreLines;
    }

    private HazardSiteManager GetHazardSites()
    {
        return HazardSiteManager.Instance != null ? HazardSiteManager.Instance : GetComponent<HazardSiteManager>();
    }

    private string BuildExpireStreakHudText(UFOManager ufo)
    {
        if (!enableConsecutiveExpireFail)
        {
            return "Miss Streak: OFF";
        }

        var streak = ufo != null ? ufo.ConsecutiveExpiredResponsesCount : 0;
        var threshold = Mathf.Max(1, consecutiveExpireFailThreshold);

        var color = "#A7E3FF";
        if (streak >= threshold - 1)
        {
            color = "#FF8A8A";
        }
        else if (streak >= Mathf.Max(1, threshold - 2))
        {
            color = "#FFD27A";
        }

        return $"Miss Streak: <color={color}>{streak}/{threshold}</color>";
    }

    private void PublishExpireStreakWarning(int streak, int threshold)
    {
        var queue = BoxMessageQueue.Instance != null
            ? BoxMessageQueue.Instance
            : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
        if (queue == null)
        {
            return;
        }

        queue.Publish(new BoxMessage(
            triggerKey: ExpireStreakWarningTriggerKey,
            channel: BoxChannel.Info,
            severity: BoxSeverity.Critical,
            sourceTag: "Operations",
            title: "Command Warning",
            body: $"Response miss streak is {streak}/{threshold}.\nOne more missed response will fail the mission."));
    }

    /// <summary>Mule score lines for the mission end panel. Empty when no Mule cargo was seized, spilled or delivered.</summary>
    private string BuildMuleBreakdownLines(UFOManager ufo)
    {
        var hazardSites = GetHazardSites();
        var contained = hazardSites != null ? hazardSites.SitesContained : 0;
        var contaminated = hazardSites != null ? hazardSites.RegionsContaminated : 0;

        var networkLines = GetNetworkScoreLines();
        var squad = GetSquad();
        var exposures = squad != null ? squad.Exposures : 0;

        if (ufo == null || (ufo.WarheadsSeized == 0 && ufo.MuleCargoSpills == 0 && ufo.WarheadsDelivered == 0 && contained == 0 && contaminated == 0 && exposures == 0 && networkLines.Count == 0))
        {
            return string.Empty;
        }

        var parts = new System.Collections.Generic.List<string>(5 + networkLines.Count);
        foreach (var line in networkLines)
        {
            parts.Add(line.Value >= 0
                ? $"<color=#8CFF8C>+ {line.Key}: {line.Value}</color>"
                : $"<color=#FF9B9B>- {line.Key}: {-line.Value}</color>");
        }

        if (contained > 0)
        {
            parts.Add($"<color=#8CFF8C>+ Sites contained ({contained}): {contained * hazardSiteContainedBonusWeight}</color>");
        }

        if (contaminated > 0)
        {
            parts.Add($"<color=#FF9B9B>- Regions contaminated ({contaminated}): {contaminated * regionContaminatedPenaltyWeight}</color>");
        }

        if (ufo.WarheadsSeized > 0)
        {
            parts.Add($"<color=#8CFF8C>+ Warheads seized ({ufo.WarheadsSeized}): {ufo.WarheadsSeized * warheadSeizedBonusWeight}</color>");
        }

        if (ufo.MuleCargoSpills > 0)
        {
            parts.Add($"<color=#FF9B9B>- Cargo spills ({ufo.MuleCargoSpills}): {ufo.MuleCargoSpills * cargoSpillPenaltyWeight}</color>");
        }

        if (exposures > 0)
        {
            parts.Add($"<color=#FF9B9B>- Squad exposures ({exposures}): {exposures * squadExposurePenaltyWeight}</color>");
        }

        if (ufo.WarheadsDelivered > 0)
        {
            parts.Add($"<color=#FF9B9B>- Warheads delivered ({ufo.WarheadsDelivered}): {ufo.WarheadsDelivered * warheadDeliveredPenaltyWeight}</color>");
        }

        return "\n" + string.Join("\n", parts);
    }

    private static int ApplyDifficultyScoreMultiplier(int rawScore)
    {
        return Mathf.Max(0, Mathf.RoundToInt(rawScore * Difficulty.ActivePreset.ScoreMultiplier));
    }

    /// <summary>Score line for the mission end panel showing the difficulty multiplier. Empty on Normal.</summary>
    private static string BuildDifficultyBreakdownLine()
    {
        var preset = Difficulty.ActivePreset;
        if (Mathf.Approximately(preset.ScoreMultiplier, 1f))
        {
            return string.Empty;
        }

        var color = preset.ScoreMultiplier > 1f ? "#8CFF8C" : "#FF9B9B";
        return $"\n<color={color}>x Difficulty ({preset.Label}): {preset.ScoreMultiplier:0.0#}</color>";
    }

    private int CalculateMissionScore(UFOManager ufo)
    {
        if (ufo == null)
        {
            var fallbackTime = Mathf.RoundToInt(missionElapsedSeconds * fallbackTimeScoreMultiplier);
            return ApplyDifficultyScoreMultiplier(Mathf.Max(0, fallbackTime));
        }

        var baseScore = ufo.RespondedContactsCount * responseScoreWeight;
        var contactCoverage = ufo.TotalContactsSpawned * contactScoreWeight;
        var educationScore = Mathf.RoundToInt(ufo.Education * educationScoreWeight);
        var missPenalty = ufo.ExpiredResponsesCount * missedResponsePenaltyWeight;
        var riskPenalty = Mathf.RoundToInt(ufo.PermitRisk * permitRiskPenaltyWeight);
        var survivalBonus = Mathf.RoundToInt(Mathf.Min(missionElapsedSeconds, missionDurationSeconds));
        var warheadBonus = ufo.WarheadsSeized * warheadSeizedBonusWeight;
        var spillPenalty = ufo.MuleCargoSpills * cargoSpillPenaltyWeight;
        var deliveredPenalty = ufo.WarheadsDelivered * warheadDeliveredPenaltyWeight;
        var hazardSites = GetHazardSites();
        var containedBonus = hazardSites != null ? hazardSites.SitesContained * hazardSiteContainedBonusWeight : 0;
        var contaminationPenalty = hazardSites != null ? hazardSites.RegionsContaminated * regionContaminatedPenaltyWeight : 0;
        var squad = GetSquad();
        var exposurePenalty = squad != null ? squad.Exposures * squadExposurePenaltyWeight : 0;

        var networkPoints = 0;
        foreach (var line in GetNetworkScoreLines())
        {
            networkPoints += line.Value;
        }

        var rawScore = Mathf.Max(0, baseScore + contactCoverage + educationScore + survivalBonus + warheadBonus + containedBonus + networkPoints
                                    - missPenalty - riskPenalty - spillPenalty - deliveredPenalty - contaminationPenalty - exposurePenalty);
        return ApplyDifficultyScoreMultiplier(rawScore);
    }

    private MedalTier GetMedalTier(int score)
    {
        if (score >= goldScoreThreshold)
        {
            return MedalTier.Gold;
        }

        if (score >= silverScoreThreshold)
        {
            return MedalTier.Silver;
        }

        if (score >= bronzeScoreThreshold)
        {
            return MedalTier.Bronze;
        }

        return MedalTier.None;
    }

    private static string MedalTierToLabel(MedalTier tier)
    {
        return tier switch
        {
            MedalTier.Gold => "Gold",
            MedalTier.Silver => "Silver",
            MedalTier.Bronze => "Bronze",
            _ => "No Medal",
        };
    }

    private static Color GetMedalColor(MedalTier tier)
    {
        return tier switch
        {
            MedalTier.Gold => new Color(1f, 0.84f, 0.24f, 1f),
            MedalTier.Silver => new Color(0.85f, 0.88f, 0.92f, 1f),
            MedalTier.Bronze => new Color(0.83f, 0.56f, 0.34f, 1f),
            _ => new Color(0.75f, 0.75f, 0.75f, 1f),
        };
    }

    private void ShowMissionEndPanel(MissionEndReason reason, UFOManager ufo, int score, int bestScore, bool isNewBest)
    {
        if (missionEndPanel != null)
        {
            var existingCanvas = missionEndPanel.transform.parent != null
                ? missionEndPanel.transform.parent.gameObject
                : missionEndPanel;
            Destroy(existingCanvas);
            missionEndPanel = null;
        }

        // The message box would otherwise show through and around the end panel.
        SetBoxCanvasVisible(false);

        var canvasGo = new GameObject("MissionEndCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        missionEndPanel = new GameObject("MissionEndPanel", typeof(Image));
        missionEndPanel.transform.SetParent(canvasGo.transform, false);

        var panelImage = missionEndPanel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.86f);

        var panelRect = missionEndPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        // Stacked top to bottom: heading, medal, summary, buttons. Each label gets a rect sized
        // to its own content; the 220-tall default rect let centred text drift onto its neighbours.
        const float endPanelWidth = 640f;
        const float endPanelHeight = 700f;
        panelRect.sizeDelta = new Vector2(endPanelWidth, endPanelHeight);

        var headingText = reason == MissionEndReason.Victory
            ? $"Mission Complete, {commanderName}"
            : GameModes.Active == GameMode.Endurance
                ? $"Endurance Over, {commanderName}"
                : $"Mission Failed, {commanderName}";
        var heading = CreatePanelLabel(missionEndPanel.transform, "Heading", headingText, 34, new Vector2(0f, -22f));
        heading.alignment = TextAnchor.MiddleCenter;
        heading.rectTransform.sizeDelta = new Vector2(endPanelWidth - 40f, 48f);

        var medalTier = GetMedalTier(score);
        var badgeText = medalTier == MedalTier.None ? MedalTierToLabel(medalTier) : $"{MedalTierToLabel(medalTier)} Medal";
        var badge = CreatePanelLabel(missionEndPanel.transform, "MedalBadge", badgeText, 24, new Vector2(0f, -74f));
        badge.alignment = TextAnchor.MiddleCenter;
        badge.rectTransform.sizeDelta = new Vector2(endPanelWidth - 40f, 34f);
        badge.color = GetMedalColor(medalTier);

        var summary = BuildMissionSummary(reason, ufo, score, bestScore, isNewBest);
        const float summaryTop = 120f;
        const float buttonHeight = 46f;
        const float buttonBottomMargin = 24f;
        const float buttonTop = endPanelHeight - buttonBottomMargin - buttonHeight;
        var summaryLabel = CreatePanelLabel(missionEndPanel.transform, "Summary", summary, 18, new Vector2(0f, -summaryTop));
        summaryLabel.alignment = TextAnchor.UpperCenter;
        // Mule and difficulty lines make the summary length vary; shrink to fit above the buttons.
        summaryLabel.verticalOverflow = VerticalWrapMode.Truncate;
        summaryLabel.resizeTextForBestFit = true;
        summaryLabel.resizeTextMinSize = 12;
        summaryLabel.resizeTextMaxSize = 18;
        var summaryRect = summaryLabel.rectTransform;
        summaryRect.sizeDelta = new Vector2(endPanelWidth - 60f, buttonTop - summaryTop - 16f);

        var primaryButtonText = reason == MissionEndReason.Victory ? "Play Again" : "Retry";
        var secondaryButtonText = reason == MissionEndReason.Victory ? "Continue Ops" : "Review";

        var retryButton = CreatePanelButton(missionEndPanel.transform, "PrimaryActionButton", primaryButtonText, new Vector2(-105f, -buttonTop), new Vector2(190f, buttonHeight));
        retryButton.onClick.AddListener(RestartMission);

        var secondaryButton = CreatePanelButton(missionEndPanel.transform, "SecondaryActionButton", secondaryButtonText, new Vector2(105f, -buttonTop), new Vector2(190f, buttonHeight));
        if (reason == MissionEndReason.Victory)
        {
            secondaryButton.onClick.AddListener(EnterFreeOpsMode);
        }
        else
        {
            secondaryButton.onClick.AddListener(ReviewAfterFailure);
        }
    }

    private string BuildMissionSummary(MissionEndReason reason, UFOManager ufo, int score, int bestScore, bool isNewBest)
    {
        var elapsed = Mathf.Max(0, Mathf.FloorToInt(missionElapsedSeconds));
        var elapsedMinutes = elapsed / 60;
        var elapsedSecondsPart = elapsed % 60;

        var resultLine = reason == MissionEndReason.Victory
            ? "You held the line until mission timer elapsed."
            : GameModes.Active == GameMode.Endurance
                ? $"You held the line for {elapsedMinutes}:{elapsedSecondsPart:00} before " +
                  (reason == MissionEndReason.ConsecutiveExpireFailure ? "missed responses broke command tempo." : "permit risk shut X-Con down.")
            : reason == MissionEndReason.ConsecutiveExpireFailure
                ? "Command tempo collapsed from repeated missed responses."
                : "Permit risk exceeded acceptable limits.";
        var medalTier = GetMedalTier(score);
        var medalText = MedalTierToLabel(medalTier);

        if (ufo == null)
        {
            return $"{resultLine}\n\nTime: {elapsedMinutes}:{elapsedSecondsPart:00}\nScore: {score}\nMedal: {medalText}\nBest: {bestScore}";
        }

        var responsePoints = ufo.RespondedContactsCount * responseScoreWeight;
        var coveragePoints = ufo.TotalContactsSpawned * contactScoreWeight;
        var educationPoints = Mathf.RoundToInt(ufo.Education * educationScoreWeight);
        var missedPenalty = ufo.ExpiredResponsesCount * missedResponsePenaltyWeight;
        var riskPenalty = Mathf.RoundToInt(ufo.PermitRisk * permitRiskPenaltyWeight);
        var survivalBonus = Mathf.RoundToInt(Mathf.Min(missionElapsedSeconds, missionDurationSeconds));

        var bestSuffix = isNewBest ? " (NEW)" : string.Empty;
        if (lastHighscorePlace > 0)
        {
            bestSuffix += $"\n<color=#FFDB73>Highscore list: #{lastHighscorePlace} on {Difficulty.ActivePreset.Label}</color>";
        }

        var muleLines = BuildMuleBreakdownLines(ufo);
        return $"{resultLine}\n\nMode: {GameModes.Label(GameModes.Active)}  Difficulty: {Difficulty.ActivePreset.Label}  Time: {elapsedMinutes}:{elapsedSecondsPart:00}\nContacts: {ufo.TotalContactsSpawned}  Responses: {ufo.RespondedContactsCount}  Missed: {ufo.ExpiredResponsesCount}\nEducation: {Mathf.RoundToInt(ufo.Education)}  PermitRisk: {Mathf.RoundToInt(ufo.PermitRisk)}\n\nScore Breakdown:\n<color=#8CFF8C>+ Responses: {responsePoints}</color>  <color=#8CFF8C>+ Coverage: {coveragePoints}</color>\n<color=#8CFF8C>+ Education: {educationPoints}</color>  <color=#8CFF8C>+ Survival: {survivalBonus}</color>\n<color=#FF9B9B>- Missed: {missedPenalty}</color>  <color=#FF9B9B>- Risk: {riskPenalty}</color>{muleLines}{BuildDifficultyBreakdownLine()}\n\nScore: {score}\nMedal: {medalText}\nBest: {bestScore}{bestSuffix}";
    }

    private static Text CreatePanelLabel(Transform parent, string name, string content, int fontSize, Vector2 anchoredPosition)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(500f, 220f);

        var text = go.GetComponent<Text>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.UpperCenter;
        text.color = Color.white;
        text.supportRichText = true;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = content;
        return text;
    }

    private static Button CreatePanelButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.92f);

        var button = go.GetComponent<Button>();

        var labelGo = new GameObject("Label", typeof(Text));
        labelGo.transform.SetParent(go.transform, false);

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var text = labelGo.GetComponent<Text>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.font = font;
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        text.text = label;

        return button;
    }

    private void RestartMission()
    {
        freeOpsMode = false;
        GameMenuController.SetPaused(false);
        SetBoxCanvasVisible(true);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void EnterFreeOpsMode()
    {
        freeOpsMode = true;
        missionEnded = false;

        if (missionEndPanel != null)
        {
            var canvas = missionEndPanel.transform.parent != null
                ? missionEndPanel.transform.parent.gameObject
                : missionEndPanel;
            Destroy(canvas);
            missionEndPanel = null;
        }

        if (missionHudPanel != null)
        {
            missionHudPanel.SetActive(true);
        }

        SetBoxCanvasVisible(true);
        GameMenuController.SetPaused(false);
    }

    private void ReviewAfterFailure()
    {
        EnterFreeOpsMode();
    }

    private static void QuitMission()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
