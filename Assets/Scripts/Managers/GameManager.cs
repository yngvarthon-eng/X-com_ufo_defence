using UnityEngine;
using XCon.UI.Boxes;
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
    private const string AutoLoadPriorityPrefKey = "xcon.save.autoload.priority";
    private const string BestScorePrefKey = "xcon.mission.bestScore";

    [Header("Mission")]
    [SerializeField] private bool enableMissionEndConditions = true;
    [Min(30f)]
    [SerializeField] private float missionDurationSeconds = 20f * 60f;
    [Range(10f, 100f)]
    [SerializeField] private float permitRiskFailThreshold = 100f;
    [SerializeField] private bool pauseGameWhenMissionEnds = true;

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
    private UFOManager cachedUfoManager;
    private int bestScore;
    private Text missionHudLabel;
    private GameObject missionHudPanel;

    private enum MissionEndReason
    {
        Victory,
        PermitRiskFailure,
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

        TryApplyAutoLoadedPriority();

        bestScore = PlayerPrefs.GetInt(BestScorePrefKey, 0);
        BuildMissionHud();
        RefreshMissionHud();

        StartCoroutine(PublishStartupWelcomePrompt());
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

        queue.Publish(new BoxMessage(
            triggerKey: WelcomeTriggerKey,
            channel: BoxChannel.Thinking,
            severity: BoxSeverity.Info,
            sourceTag: "Commander",
            title: "Welcome, Commander",
            body:
                "Welcome to X-Con U.F.O Defence.\n" +
                "Your mission is to detect contacts, assess incidents, and choose the right response before threats escalate.\n\n" +
                "Core controls:\n" +
                "1-3: Strategic Priority (Coverage / Research / Response)\n" +
                "1-4: Contact Response (Ignore / Track / Intercept / Dispatch)\n" +
                "Esc: Dismiss non-interactive messages\n\n" +
                "Start by selecting your Strategic Priority. Good luck."));
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
        if (!enableMissionEndConditions || missionEnded)
        {
            return;
        }

        if (!GameMenuController.IsPaused)
        {
            missionElapsedSeconds += Time.unscaledDeltaTime;
        }

        var ufo = cachedUfoManager != null ? cachedUfoManager : GetComponent<UFOManager>();
        cachedUfoManager = ufo;
        if (ufo != null && ufo.PermitRisk >= permitRiskFailThreshold)
        {
            EndMission(MissionEndReason.PermitRiskFailure, ufo);
            return;
        }

        if (missionElapsedSeconds >= missionDurationSeconds)
        {
            EndMission(MissionEndReason.Victory, ufo);
        }
    }

    private void EndMission(MissionEndReason reason, UFOManager ufo)
    {
        if (missionEnded)
        {
            return;
        }

        missionEnded = true;

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
                $"Priority: {currentPriority}";
            return;
        }

        var pausedSuffix = GameMenuController.IsPaused ? "  [PAUSED]" : string.Empty;
        var medalText = MedalTierToLabel(medalTier);
        var progressText = nextMedalTarget > 0
            ? $"Next Medal At: {nextMedalTarget}"
            : "Top Medal Reached";
        missionHudLabel.text =
            $"MISSION{pausedSuffix}\n" +
            $"Time Left: {minutes}:{seconds:00}  Score: {score}\n" +
            $"Best: {bestScore}  Medal: {medalText}\n" +
            $"Priority: {currentPriority}  {progressText}\n" +
            $"Contacts: {ufo.TotalContactsSpawned}  Responses: {ufo.RespondedContactsCount}  Missed: {ufo.ExpiredResponsesCount}\n" +
            $"Education: {Mathf.RoundToInt(ufo.Education)}  PermitRisk: {Mathf.RoundToInt(ufo.PermitRisk)}";
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

    private void ShowMissionEndPanel(MissionEndReason reason, UFOManager ufo, int score, int bestScore, bool isNewBest)
    {
        if (missionEndPanel != null)
        {
            Destroy(missionEndPanel);
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
        panelRect.sizeDelta = new Vector2(560f, 380f);

        var heading = CreatePanelLabel(missionEndPanel.transform, "Heading", reason == MissionEndReason.Victory ? "Mission Complete" : "Mission Failed", 34, new Vector2(0f, -44f));
        heading.alignment = TextAnchor.MiddleCenter;

        var summary = BuildMissionSummary(reason, ufo, score, bestScore, isNewBest);
        var summaryLabel = CreatePanelLabel(missionEndPanel.transform, "Summary", summary, 20, new Vector2(0f, -178f));
        summaryLabel.alignment = TextAnchor.UpperCenter;

        var retryButton = CreatePanelButton(missionEndPanel.transform, "RetryButton", "Retry", new Vector2(-95f, -322f), new Vector2(170f, 46f));
        retryButton.onClick.AddListener(RetryMission);

        var quitButton = CreatePanelButton(missionEndPanel.transform, "QuitButton", "Quit", new Vector2(95f, -322f), new Vector2(170f, 46f));
        quitButton.onClick.AddListener(QuitMission);
    }

    private string BuildMissionSummary(MissionEndReason reason, UFOManager ufo, int score, int bestScore, bool isNewBest)
    {
        var elapsed = Mathf.Max(0, Mathf.FloorToInt(missionElapsedSeconds));
        var elapsedMinutes = elapsed / 60;
        var elapsedSecondsPart = elapsed % 60;

        var resultLine = reason == MissionEndReason.Victory
            ? "You held the line until mission timer elapsed."
            : "Permit risk exceeded acceptable limits.";
        var medalTier = GetMedalTier(score);
        var medalText = MedalTierToLabel(medalTier);

        if (ufo == null)
        {
            return $"{resultLine}\n\nTime: {elapsedMinutes}:{elapsedSecondsPart:00}\nScore: {score}\nMedal: {medalText}\nBest: {bestScore}";
        }

        var bestSuffix = isNewBest ? " (NEW)" : string.Empty;
        return $"{resultLine}\n\nTime: {elapsedMinutes}:{elapsedSecondsPart:00}\nContacts: {ufo.TotalContactsSpawned}\nResponses: {ufo.RespondedContactsCount}\nMissed: {ufo.ExpiredResponsesCount}\nEducation: {Mathf.RoundToInt(ufo.Education)}\nPermitRisk: {Mathf.RoundToInt(ufo.PermitRisk)}\n\nScore: {score}\nMedal: {medalText}\nBest: {bestScore}{bestSuffix}";
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

    private static void RetryMission()
    {
        GameMenuController.SetPaused(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
