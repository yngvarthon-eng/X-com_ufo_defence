using UnityEngine;
using XCon.UI.Boxes;
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
    private const string BestScorePrefKey = "xcon.mission.bestScore";
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
        public string commanderName;
    }

    [Header("Mission")]
    [SerializeField] private bool enableMissionEndConditions = true;
    [Min(30f)]
    [SerializeField] private float missionDurationSeconds = 20f * 60f;
    [Range(10f, 100f)]
    [SerializeField] private float permitRiskFailThreshold = 100f;
    [SerializeField] private bool pauseGameWhenMissionEnds = true;
    [SerializeField] private bool enableConsecutiveExpireFail = true;
    [Min(1)]
    [SerializeField] private int consecutiveExpireFailThreshold = 3;

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
    [SerializeField] private float fallbackTimeScoreMultiplier = 0.8f;

    [Header("Medals")]
    [SerializeField] private int bronzeScoreThreshold = 2500;
    [SerializeField] private int silverScoreThreshold = 4200;
    [SerializeField] private int goldScoreThreshold = 6200;

    private float missionElapsedSeconds;
    private bool missionEnded;
    private GameObject missionEndPanel;
    private bool freeOpsMode;
    private ProceduralAudioCuePlayer cuePlayer;
    private UFOManager cachedUfoManager;
    private int bestScore;
    private Text missionHudLabel;
    private GameObject missionHudPanel;
    private bool expireStreakWarningIssued;
    private NarrativeMood activeMissionMood = NarrativeMood.Military;
    private GameObject readyToPlayPanel;
    private Toggle readySkipToggle;
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
        missionDurationSeconds = Mathf.Max(30f, missionDurationSeconds);
        permitRiskFailThreshold = Mathf.Clamp(permitRiskFailThreshold, 10f, 100f);
        consecutiveExpireFailThreshold = Mathf.Max(1, consecutiveExpireFailThreshold);
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
        fallbackTimeScoreMultiplier = Mathf.Max(0f, fallbackTimeScoreMultiplier);

        bronzeScoreThreshold = Mathf.Max(0, bronzeScoreThreshold);
        silverScoreThreshold = Mathf.Max(bronzeScoreThreshold, silverScoreThreshold);
        goldScoreThreshold = Mathf.Max(silverScoreThreshold, goldScoreThreshold);
    }

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
        InitializeCommanderIdentity();
        Debug.Log($"GameManager started. Welcome, Commander {commanderName}!");

        ResetRuntimeSessionState();
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
        if (missionDurationSeconds < 30f)
        {
            missionDurationSeconds = 20f * 60f;
        }

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
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 120f);
        panelRect.sizeDelta = new Vector2(680f, 400f);

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

        var title = CreatePanelLabel(messageBox.transform, "ReadyTitle", "Get Ready To Play", 36, new Vector2(0f, -28f));
        title.alignment = TextAnchor.MiddleCenter;

        var body = CreatePanelLabel(
            messageBox.transform,
            "ReadyBody",
            "You are about to enter command operations.\nReview the board, then press OK to begin.",
            20,
            new Vector2(0f, -72f));
        body.alignment = TextAnchor.UpperCenter;
        body.rectTransform.sizeDelta = new Vector2(560f, 120f);

        readySkipToggle = CreatePanelToggle(
            readyToPlayPanel.transform,
            "ReadySkipNextTimeToggle",
            "Skip next time",
            new Vector2(0f, -278f),
            new Vector2(220f, 28f));
        readySkipToggle.SetIsOnWithoutNotify(false);
        readySkipToggle.onValueChanged.AddListener(OnReadySkipNextTimeChanged);

        var okButton = CreatePanelButton(readyToPlayPanel.transform, "ReadyOkButton", "OK", new Vector2(0f, -336f), new Vector2(180f, 48f));
        okButton.onClick.AddListener(ConfirmReadyToPlay);
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
                    "1-3: Strategic Priority (Coverage / Research / Response)\n" +
                    "1-4: Contact Response (Ignore / Track / Intercept / Dispatch)\n" +
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
                "1-3: Strategic Priority (Coverage / Research / Response)\n" +
                "1-4: Contact Response (Ignore / Track / Intercept / Dispatch)\n" +
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

        var body = $"Strategic Priority (press 1-3 anytime):\n1) Coverage\n2) Research\n3) Response\n\nCurrent: {choice}\n\nTip: Use 1-4 on active contact prompts to choose response.";

        var queue = BoxMessageQueue.Instance;
        if (queue == null)
        {
            return;
        }

        // Ensure feedback appears even if another message was active.
        queue.Publish(new BoxMessage(
            triggerKey: PickPriorityTriggerKey,
            channel: BoxChannel.Thinking,
            severity: BoxSeverity.Info,
            sourceTag: "Commander",
            title: "Strategic Priority Updated",
            body: body));

        queue.UpdateBody(PickPriorityTriggerKey, body);
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

            // Extra fallbacks that do not collide with debug F1/F2/F3 box hotkeys.
            if (keyboard.qKey.wasPressedThisFrame) { choice = StrategicPriority.Coverage; return true; }
            if (keyboard.wKey.wasPressedThisFrame) { choice = StrategicPriority.Research; return true; }
            if (keyboard.eKey.wasPressedThisFrame) { choice = StrategicPriority.Response; return true; }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { choice = StrategicPriority.Coverage; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { choice = StrategicPriority.Research; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) { choice = StrategicPriority.Response; return true; }
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

    private void EndMission(MissionEndReason reason, UFOManager ufo)
    {
        if (missionEnded)
        {
            return;
        }

        missionEnded = true;

        Debug.Log($"[Mission] Ended: {reason}. elapsed={missionElapsedSeconds:0.00}s duration={missionDurationSeconds:0.00}s permitRisk={(ufo != null ? ufo.PermitRisk : -1f):0.0} threshold={permitRiskFailThreshold:0.0}");

        if (pauseGameWhenMissionEnds)
        {
            GameMenuController.SetPaused(true);
        }

        var score = CalculateMissionScore(ufo);
        var isNewBest = score > bestScore;
        if (isNewBest)
        {
            bestScore = score;
            PlayerPrefs.SetInt(BestScorePrefKey, bestScore);
            PlayerPrefs.Save();
        }

        var medalTier = GetMedalTier(score);
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
        panelRect.sizeDelta = new Vector2(380f, 138f);

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
        var timeRemaining = Mathf.Max(0f, missionDurationSeconds - missionElapsedSeconds);
        var totalSeconds = Mathf.CeilToInt(timeRemaining);
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
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
                $"MISSION\n" +
                $"Time Left: {minutes}:{seconds:00}\n" +
                $"Score: {score}  Best: {bestScore}\n" +
                $"Priority: {currentPriority}\n" +
                BuildExpireStreakHudText(null);
            return;
        }

        var pausedSuffix = GameMenuController.IsPaused ? "  [PAUSED]" : string.Empty;
        var opsSuffix = freeOpsMode ? "  [FREE OPS]" : string.Empty;
        var archangelSuffix = activeMissionMood == NarrativeMood.Mysterious ? "  [ARCHANGEL PROTOCOL]" : string.Empty;
        var medalText = MedalTierToLabel(medalTier);
        var progressText = nextMedalTarget > 0
            ? $"Next Medal At: {nextMedalTarget}"
            : "Top Medal Reached";
        missionHudLabel.text =
            $"MISSION{pausedSuffix}{opsSuffix}{archangelSuffix}\n" +
            $"Time Left: {minutes}:{seconds:00}  Score: {score}\n" +
            $"Best: {bestScore}  Medal: {medalText}\n" +
            $"Priority: {currentPriority}  {progressText}\n" +
            $"Contacts: {ufo.TotalContactsSpawned}  Responses: {ufo.RespondedContactsCount}  Missed: {ufo.ExpiredResponsesCount}\n" +
            $"Education: {Mathf.RoundToInt(ufo.Education)}  PermitRisk: {Mathf.RoundToInt(ufo.PermitRisk)}\n" +
            BuildExpireStreakHudText(ufo);
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

    private int CalculateMissionScore(UFOManager ufo)
    {
        if (ufo == null)
        {
            var fallbackTime = Mathf.RoundToInt(missionElapsedSeconds * fallbackTimeScoreMultiplier);
            return Mathf.Max(0, fallbackTime);
        }

        var baseScore = ufo.RespondedContactsCount * responseScoreWeight;
        var contactCoverage = ufo.TotalContactsSpawned * contactScoreWeight;
        var educationScore = Mathf.RoundToInt(ufo.Education * educationScoreWeight);
        var missPenalty = ufo.ExpiredResponsesCount * missedResponsePenaltyWeight;
        var riskPenalty = Mathf.RoundToInt(ufo.PermitRisk * permitRiskPenaltyWeight);
        var survivalBonus = Mathf.RoundToInt(Mathf.Min(missionElapsedSeconds, missionDurationSeconds));

        return Mathf.Max(0, baseScore + contactCoverage + educationScore + survivalBonus - missPenalty - riskPenalty);
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
        panelRect.sizeDelta = new Vector2(560f, 460f);

        var headingText = reason == MissionEndReason.Victory
            ? $"Mission Complete, {commanderName}"
            : $"Mission Failed, {commanderName}";
        var heading = CreatePanelLabel(missionEndPanel.transform, "Heading", headingText, 34, new Vector2(0f, -44f));
        heading.alignment = TextAnchor.MiddleCenter;

        var medalTier = GetMedalTier(score);
        var badge = CreatePanelLabel(missionEndPanel.transform, "MedalBadge", $"{MedalTierToLabel(medalTier)} Medal", 24, new Vector2(0f, -88f));
        badge.alignment = TextAnchor.MiddleCenter;
        badge.color = GetMedalColor(medalTier);

        var summary = BuildMissionSummary(reason, ufo, score, bestScore, isNewBest);
        var summaryLabel = CreatePanelLabel(missionEndPanel.transform, "Summary", summary, 18, new Vector2(0f, -156f));
        summaryLabel.alignment = TextAnchor.UpperCenter;
        var summaryRect = summaryLabel.rectTransform;
        summaryRect.sizeDelta = new Vector2(500f, 210f);

        var primaryButtonText = reason == MissionEndReason.Victory ? "Play Again" : "Retry";
        var secondaryButtonText = reason == MissionEndReason.Victory ? "Continue Ops" : "Review";

        var retryButton = CreatePanelButton(missionEndPanel.transform, "PrimaryActionButton", primaryButtonText, new Vector2(-95f, -392f), new Vector2(170f, 46f));
        retryButton.onClick.AddListener(RestartMission);

        var secondaryButton = CreatePanelButton(missionEndPanel.transform, "SecondaryActionButton", secondaryButtonText, new Vector2(95f, -392f), new Vector2(170f, 46f));
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
        return $"{resultLine}\n\nTime: {elapsedMinutes}:{elapsedSecondsPart:00}\nContacts: {ufo.TotalContactsSpawned}  Responses: {ufo.RespondedContactsCount}  Missed: {ufo.ExpiredResponsesCount}\nEducation: {Mathf.RoundToInt(ufo.Education)}  PermitRisk: {Mathf.RoundToInt(ufo.PermitRisk)}\n\nScore Breakdown:\n<color=#8CFF8C>+ Responses: {responsePoints}</color>  <color=#8CFF8C>+ Coverage: {coveragePoints}</color>\n<color=#8CFF8C>+ Education: {educationPoints}</color>  <color=#8CFF8C>+ Survival: {survivalBonus}</color>\n<color=#FF9B9B>- Missed: {missedPenalty}</color>  <color=#FF9B9B>- Risk: {riskPenalty}</color>\n\nScore: {score}\nMedal: {medalText}\nBest: {bestScore}{bestSuffix}";
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
