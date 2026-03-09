using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using XCon.UI.Boxes;

using Object = UnityEngine.Object;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class UFOManager : MonoBehaviour
{
    [Header("Boxes")]
    [SerializeField] private bool publishUfoSpottedBoxes = true;
    [SerializeField] private float thinkingDelaySeconds = 0.25f;

    [Header("UFO Visuals")]
    [Tooltip("Optional prefab spawned at the contact position when a UFO is detected.")]
    [SerializeField] private GameObject ufoPrefab;

    [Tooltip("If true and a prefab is assigned, spawns a visual UFO per contact.")]
    [SerializeField] private bool spawnUfoVisuals = true;

    [Tooltip("Vertical offset (Y) applied when spawning the UFO visual.")]
    [SerializeField] private float ufoVisualSpawnYOffset = 0f;

    [Tooltip("If true, Intercept will remove the spawned UFO visual.")]
    [SerializeField] private bool destroyUfoVisualOnIntercept = true;

    [Tooltip("Delay before destroying the UFO visual when Intercept is chosen.")]
    [Min(0f)]
    [SerializeField] private float interceptDestroyDelaySeconds = 1.0f;

    [Header("Auto Spawn")]
    [SerializeField] private bool enableAutoSpawn = true;

    [Tooltip("Base contact rate in contacts per real-time minute (Poisson process).")]
    [Min(0f)]
    [SerializeField] private float baseContactsPerMinute = 0.2f;

    [Tooltip("Minimum time between auto spawns (seconds).")]
    [Min(0f)]
    [SerializeField] private float minAutoSpawnIntervalSeconds = 15f;

    [Tooltip("Maximum time between auto spawns (seconds). 0 = no maximum.")]
    [Min(0f)]
    [SerializeField] private float maxAutoSpawnIntervalSeconds = 0f;

    [Tooltip("If true, auto spawning pauses while awaiting a response choice.")]
    [SerializeField] private bool pauseAutoSpawnWhileAwaitingResponse = true;

    [Tooltip("Center for random auto-spawn positions.")]
    [SerializeField] private Vector3 autoSpawnAreaCenter = Vector3.zero;

    [Tooltip("Half-extents for random auto-spawn positions (X/Z used).")]
    [SerializeField] private Vector2 autoSpawnAreaExtentsXZ = new Vector2(20f, 20f);

    [Tooltip("If >0, increases spawn rate with escalation pressure. 0.25 means +25% per pressure step.")]
    [Min(0f)]
    [SerializeField] private float escalationRateMultiplierPerPressure = 0.20f;

    [Tooltip("If >0, increases spawn rate with PermitRisk. 0.5 means up to +50% at PermitRisk=100.")]
    [Min(0f)]
    [SerializeField] private float permitRiskRateMultiplier = 0.35f;

    [Header("Response")]
    [SerializeField] private bool enableResponseChoices = true;
    [Tooltip("How long the response choice stays active before it expires (seconds). 0 = never expires.")]
    [Min(0f)]
    [SerializeField] private float responseChoiceTimeoutSeconds = 20f;

    [Tooltip("How many past contacts to keep in memory for response tracking.")]
    [Min(1)]
    [SerializeField] private int maxStoredContacts = 128;

    [Header("Meters")]
    [Tooltip("Awareness/training/public readiness. Persists for the play session.")]
    [Range(0f, 100f)]
    [SerializeField] private float education = 5f;

    [Tooltip("Risk of making bad concessions / losing legal ground. Persists for the play session.")]
    [Range(0f, 100f)]
    [SerializeField] private float permitRisk = 0f;

    [Tooltip("Education gain applied whenever an Education Bulletin is published.")]
    [Range(0f, 25f)]
    [SerializeField] private float educationGainPerBulletin = 2f;

    [Tooltip("Education gain applied on any incident assessment.")]
    [Range(0f, 10f)]
    [SerializeField] private float educationGainPerIncident = 0.25f;

    [Header("Incident Rules")]
    [Tooltip("Base distribution for what this contact is (segment + intent).")]
    [SerializeField] private List<ContactOutcomeWeight> contactOutcomeWeights = new();

    [Tooltip("If the incident is smuggler-linked, probability that the crew tries to enforce the 'Cargo of Quiet Names' rule.")]
    [Range(0f, 1f)]
    [SerializeField] private float quietCargoChanceWhenSmuggler = 0.85f;

    [Header("Behavior Tells")]
    [Tooltip("Chance that a Dark contact is operating solo (lower risk posture).")]
    [Range(0f, 1f)]
    [SerializeField] private float darkSoloChance = 0.20f;

    [Tooltip("If not solo, minimum estimated Dark crew count.")]
    [Min(2)]
    [SerializeField] private int darkCrewMin = 2;

    [Tooltip("If not solo, maximum estimated Dark crew count.")]
    [Min(2)]
    [SerializeField] private int darkCrewMax = 3;

    [Header("Progression")]
    [Tooltip("If enabled (and ContactOutcomeWeights is empty), early-game contacts skew toward Human criminal craft, then ramp into alien segments over time.")]
    [SerializeField] private bool enableSoftStart = true;

    [Tooltip("How long (real-time seconds) the soft-start ramp lasts.")]
    [Min(0f)]
    [SerializeField] private float softStartDurationSeconds = 600f;

    [Header("Lore")]
    [Tooltip("If enabled, publishes a one-time high-trust warning message on the first UFO contact to seed awareness/education.")]
    [SerializeField] private bool publishPapalWarningOnFirstContact = true;

    [TextArea]
    [SerializeField] private string papalWarningBody =
        "A high-trust warning has been issued: unknown craft activity is linked to trafficking, coercion, and hostile influence.\n\n" +
        "Do not approach crash sites. Report sightings early. Certified response teams only.";

    [Header("Debug")]
    [Tooltip("If enabled, incident and response boxes include a short readout of the current Strategic Priority multipliers.")]
    [SerializeField] private bool debugShowPriorityEffectsInBoxes = false;

    [Tooltip("If enabled, logs applied priority multipliers/deltas to the Console.")]
    [SerializeField] private bool debugLogPriorityEffects = false;

    [Tooltip("If enabled, logs UFO spawn/move events to the Console.")]
    [SerializeField] private bool debugLogUfoEvents = false;

    private float softStartBeganAt;
    private bool publishedPapalWarning;

    private int nextContactId;
    private readonly Dictionary<int, ContactRecord> contactRecordsById = new();
    private readonly Queue<int> contactIdOrder = new();

    private readonly Dictionary<int, GameObject> ufoVisualByContactId = new();

    private readonly Dictionary<UFOSpeciesSegment, int> escalationStageBySegment = new();
    private readonly Dictionary<UFOSpeciesSegment, bool> overreachPrimedBySegment = new();

    private int pendingContactId;
    private Vector3 pendingContactPosition;
    private float pendingContactBeganAt;
    private bool hasPendingResponse;
    private int pendingLastCountdownSeconds = -1;

    private float nextAutoSpawnAt;

    private struct ContactRecord
    {
        public int ContactId;
        public Vector3 Position;
        public float SpawnedAt;

        public bool HasOutcome;
        public UFOSpeciesSegment Segment;
        public UFOIntent Intent;
        public bool QuietCargo;

        public bool HasResponse;
        public UFOResponseChoice Response;
        public float RespondedAt;
    }

    private void Awake()
    {
        softStartBeganAt = Time.realtimeSinceStartup;

        if (enableAutoSpawn)
        {
            ScheduleNextAutoSpawn(fromNowSeconds: 0f);
        }
    }

    private void OnValidate()
    {
        if (autoSpawnAreaExtentsXZ.x < 0f) autoSpawnAreaExtentsXZ.x = 0f;
        if (autoSpawnAreaExtentsXZ.y < 0f) autoSpawnAreaExtentsXZ.y = 0f;
    }

    [Serializable]
    private struct ContactOutcomeWeight
    {
        public UFOSpeciesSegment Segment;
        public UFOIntent Intent;
        [Min(0f)] public float Weight;
    }

    private enum UFOSpeciesSegment
    {
        Unknown = 0,
        Grey = 1,
        Dark = 2,
        Green = 3,
        Human = 4,
    }

    private enum UFOIntent
    {
        Unknown = 0,
        Scout = 1,
        Smuggle = 2,
        Negotiate = 3,
        Raid = 4,
        Seed = 5,
        Recover = 6,
        FalseAlarm = 7,
    }

    private enum UFOBehaviorTag
    {
        None = 0,
        TiltChallenge = 1,
    }

    public void SpawnUFO(Vector3 position)
    {
        DebugLogUfoEvent("UFO spawned at " + position);

        if (!publishUfoSpottedBoxes)
        {
            return;
        }

        var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
        if (queue == null)
        {
            Debug.LogWarning("[UFO] BoxMessageQueue not found; cannot publish UFO spotted messages.");
            return;
        }

        var contactId = RegisterContact(position);
        TrySpawnUfoVisual(contactId, position);

        queue.Publish(new BoxMessage(
            triggerKey: "ufo/spotted",
            channel: BoxChannel.Info,
            severity: BoxSeverity.Warn,
            sourceTag: "Radar",
            title: "UFO Spotted",
            body: $"Contact #{contactId}\nUFO sighted at {position}."));

        PublishPapalWarningIfNeeded(queue);

        PublishIncidentAssessment(queue, contactId, position, forcedOutcome: null, forcedQuietCargo: null);

        PublishResponseChoice(queue, contactId, position);

        StartCoroutine(PublishThinking(queue));
    }

    private void TrySpawnUfoVisual(int contactId, Vector3 position)
    {
        if (!spawnUfoVisuals || ufoPrefab == null)
        {
            return;
        }

        if (ufoVisualByContactId.TryGetValue(contactId, out var existing) && existing != null)
        {
            Destroy(existing);
        }

        var spawnPos = position + Vector3.up * ufoVisualSpawnYOffset;
        var spawned = Instantiate(ufoPrefab, spawnPos, Quaternion.identity);
        spawned.name = $"{ufoPrefab.name} (Contact {contactId})";
        ufoVisualByContactId[contactId] = spawned;
    }

    private void Update()
    {
        TickAutoSpawn();

        if (!enableResponseChoices || !hasPendingResponse)
        {
            return;
        }

        var effectiveTimeout = GetEffectiveResponseChoiceTimeoutSeconds();
        if (effectiveTimeout > 0f && Time.realtimeSinceStartup - pendingContactBeganAt > effectiveTimeout)
        {
            ExpirePendingResponse();
            return;
        }

        RefreshResponseChoiceCountdownIfNeeded();

    #if ENABLE_LEGACY_INPUT_MANAGER
        var handled = false;
    #endif

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // 1-4: Ignore / Track / Intercept / Dispatch
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) { HandleResponseChoice(UFOResponseChoice.Ignore);
#if ENABLE_LEGACY_INPUT_MANAGER
                handled = true;
#endif
            }
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) { HandleResponseChoice(UFOResponseChoice.Track);
#if ENABLE_LEGACY_INPUT_MANAGER
                handled = true;
#endif
            }
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) { HandleResponseChoice(UFOResponseChoice.Intercept);
#if ENABLE_LEGACY_INPUT_MANAGER
                handled = true;
#endif
            }
            else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) { HandleResponseChoice(UFOResponseChoice.Dispatch);
#if ENABLE_LEGACY_INPUT_MANAGER
                handled = true;
#endif
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!handled)
        {
            // Legacy input fallback (works when the legacy input manager is enabled).
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) HandleResponseChoice(UFOResponseChoice.Ignore);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) HandleResponseChoice(UFOResponseChoice.Track);
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) HandleResponseChoice(UFOResponseChoice.Intercept);
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) HandleResponseChoice(UFOResponseChoice.Dispatch);
        }
#endif
    }

    private void TickAutoSpawn()
    {
        if (!enableAutoSpawn)
        {
            nextAutoSpawnAt = 0f;
            return;
        }

        // If auto-spawn is enabled after Awake (e.g., toggled during play), schedule rather than spawning immediately.
        if (nextAutoSpawnAt <= 0f)
        {
            ScheduleNextAutoSpawn(fromNowSeconds: null);
            return;
        }

        if (pauseAutoSpawnWhileAwaitingResponse && hasPendingResponse)
        {
            return;
        }

        if (Time.realtimeSinceStartup < nextAutoSpawnAt)
        {
            return;
        }

        SpawnUFO(PickAutoSpawnPosition());
        ScheduleNextAutoSpawn(fromNowSeconds: null);
    }

    private Vector3 PickAutoSpawnPosition()
    {
        var x = autoSpawnAreaCenter.x + UnityEngine.Random.Range(-autoSpawnAreaExtentsXZ.x, autoSpawnAreaExtentsXZ.x);
        var z = autoSpawnAreaCenter.z + UnityEngine.Random.Range(-autoSpawnAreaExtentsXZ.y, autoSpawnAreaExtentsXZ.y);
        return new Vector3(x, autoSpawnAreaCenter.y, z);
    }

    private float GetEffectiveContactsPerSecond()
    {
        var perMinute = Mathf.Max(0f, baseContactsPerMinute);
        var perSecond = perMinute / 60f;
        if (perSecond <= 0f)
        {
            return 0f;
        }

        perSecond *= GetPriorityContactRateMultiplier();

        // Pressure: Probe=0, Commit=1, Overreach=2. Summed across active segments.
        var pressure = 0;
        pressure += Mathf.Max(0, GetEscalationStage(UFOSpeciesSegment.Human) - 1);
        pressure += Mathf.Max(0, GetEscalationStage(UFOSpeciesSegment.Grey) - 1);
        pressure += Mathf.Max(0, GetEscalationStage(UFOSpeciesSegment.Dark) - 1);
        pressure += Mathf.Max(0, GetEscalationStage(UFOSpeciesSegment.Green) - 1);

        var escalationMult = 1f + escalationRateMultiplierPerPressure * pressure;
        var permitMult = 1f + permitRiskRateMultiplier * (Mathf.Clamp01(permitRisk / 100f));
        return perSecond * escalationMult * permitMult;
    }

    private float GetPriorityContactRateMultiplier()
    {
        var priority = GameManager.Instance != null
            ? GameManager.Instance.CurrentPriority
            : GameManager.StrategicPriority.Coverage;

        return priority switch
        {
            GameManager.StrategicPriority.Coverage => 1.25f,
            GameManager.StrategicPriority.Research => 0.85f,
            GameManager.StrategicPriority.Response => 1.00f,
            _ => 1.00f,
        };
    }

    private float GetEffectiveResponseChoiceTimeoutSeconds()
    {
        if (responseChoiceTimeoutSeconds <= 0f)
        {
            return responseChoiceTimeoutSeconds;
        }

        var priority = GameManager.Instance != null
            ? GameManager.Instance.CurrentPriority
            : GameManager.StrategicPriority.Coverage;

        var mult = priority switch
        {
            GameManager.StrategicPriority.Coverage => 1.00f,
            GameManager.StrategicPriority.Research => 0.90f,
            GameManager.StrategicPriority.Response => 1.50f,
            _ => 1.00f,
        };

        return Mathf.Max(1f, responseChoiceTimeoutSeconds * mult);
    }

    private GameManager.StrategicPriority GetCurrentPriority()
    {
        return GameManager.Instance != null
            ? GameManager.Instance.CurrentPriority
            : GameManager.StrategicPriority.Coverage;
    }

    private float GetPriorityEducationGainMultiplier()
    {
        return GetCurrentPriority() switch
        {
            GameManager.StrategicPriority.Coverage => 1.00f,
            GameManager.StrategicPriority.Research => 1.50f,
            GameManager.StrategicPriority.Response => 1.10f,
            _ => 1.00f,
        };
    }

    private float GetPriorityEducationThresholdBonus()
    {
        return GetCurrentPriority() switch
        {
            GameManager.StrategicPriority.Coverage => 5f,
            GameManager.StrategicPriority.Research => 12f,
            GameManager.StrategicPriority.Response => 0f,
            _ => 0f,
        };
    }

    private float GetPriorityPermitRiskFromOutcomeMultiplier()
    {
        return GetCurrentPriority() switch
        {
            GameManager.StrategicPriority.Coverage => 1.00f,
            GameManager.StrategicPriority.Research => 0.90f,
            GameManager.StrategicPriority.Response => 0.85f,
            _ => 1.00f,
        };
    }

    private void DebugLogPriority(string message)
    {
        if (!debugLogPriorityEffects)
        {
            return;
        }

        Debug.Log(message);
    }

    private void DebugLogUfoEvent(string message)
    {
        if (!debugLogUfoEvents)
        {
            return;
        }

        Debug.Log(message);
    }

    private string BuildPriorityEffectsReadout()
    {
        var priority = GetCurrentPriority();
        var contactRateMult = GetPriorityContactRateMultiplier();
        var eduMult = GetPriorityEducationGainMultiplier();
        var eduBonus = GetPriorityEducationThresholdBonus();
        var permitOutcomeMult = GetPriorityPermitRiskFromOutcomeMultiplier();

        var timeoutMult = 1.00f;
        if (responseChoiceTimeoutSeconds > 0f)
        {
            timeoutMult = GetEffectiveResponseChoiceTimeoutSeconds() / responseChoiceTimeoutSeconds;
        }

        return $"Priority: {priority}\nEffects: SpawnRate x{contactRateMult:0.00} | ResponseTime x{timeoutMult:0.00} | EduGain x{eduMult:0.00} | EduGate +{eduBonus:0} | PermitOutcome x{permitOutcomeMult:0.00}";
    }

    private void ApplyEducationDelta(float rawDelta)
    {
        if (Mathf.Approximately(rawDelta, 0f))
        {
            return;
        }

        var delta = rawDelta * GetPriorityEducationGainMultiplier();
        education = Mathf.Clamp(education + delta, 0f, 100f);

        DebugLogPriority($"[UFO][Priority] Education delta raw={rawDelta:0.###} applied={delta:0.###} mult={GetPriorityEducationGainMultiplier():0.00} priority={GetCurrentPriority()} now={education:0.0}");
    }

    private void ApplyPermitRiskDeltaFromResponse(float rawDelta)
    {
        if (Mathf.Approximately(rawDelta, 0f))
        {
            return;
        }

        var priority = GetCurrentPriority();
        var mult = 1.00f;
        if (rawDelta < 0f)
        {
            mult = priority switch
            {
                GameManager.StrategicPriority.Coverage => 1.00f,
                GameManager.StrategicPriority.Research => 1.10f,
                GameManager.StrategicPriority.Response => 1.25f,
                _ => 1.00f,
            };
        }
        else
        {
            mult = priority switch
            {
                GameManager.StrategicPriority.Coverage => 1.00f,
                GameManager.StrategicPriority.Research => 0.90f,
                GameManager.StrategicPriority.Response => 0.85f,
                _ => 1.00f,
            };
        }

        var applied = rawDelta * mult;
        permitRisk = Mathf.Clamp(permitRisk + applied, 0f, 100f);

        DebugLogPriority($"[UFO][Priority] PermitRisk(response) delta raw={rawDelta:0.###} applied={applied:0.###} mult={mult:0.00} priority={priority} now={permitRisk:0.0}");
    }

    private void AdjustEscalationStageForChoice(UFOSpeciesSegment segment, int baseDelta, UFOResponseChoice choice)
    {
        if (baseDelta == 0)
        {
            return;
        }

        var priority = GetCurrentPriority();
        var delta = baseDelta;

        if (baseDelta < 0)
        {
            switch (priority)
            {
                case GameManager.StrategicPriority.Response:
                    if (choice == UFOResponseChoice.Dispatch || choice == UFOResponseChoice.Intercept)
                    {
                        delta = baseDelta - 1;
                    }
                    break;
                case GameManager.StrategicPriority.Research:
                case GameManager.StrategicPriority.Coverage:
                    if (choice == UFOResponseChoice.Track)
                    {
                        delta = baseDelta - 1;
                    }
                    break;
            }
        }

        AdjustEscalationStage(segment, delta);
    }

    private void ScheduleNextAutoSpawn(float? fromNowSeconds)
    {
        var now = Time.realtimeSinceStartup;

        float dt;
        if (fromNowSeconds.HasValue)
        {
            dt = Mathf.Max(0f, fromNowSeconds.Value);
        }
        else
        {
            // Exponential inter-arrival time: dt = -ln(U)/lambda
            var lambda = GetEffectiveContactsPerSecond();
            if (lambda <= 0f)
            {
                // Disabled by rate; check again later.
                dt = 10f;
            }
            else
            {
                var u = Mathf.Clamp(UnityEngine.Random.value, 0.0001f, 0.9999f);
                dt = -Mathf.Log(u) / lambda;
            }
        }

        if (minAutoSpawnIntervalSeconds > 0f)
        {
            dt = Mathf.Max(dt, minAutoSpawnIntervalSeconds);
        }

        if (maxAutoSpawnIntervalSeconds > 0f)
        {
            dt = Mathf.Min(dt, maxAutoSpawnIntervalSeconds);
        }

        nextAutoSpawnAt = now + dt;
    }

    private enum UFOResponseChoice
    {
        Ignore = 0,
        Track = 1,
        Intercept = 2,
        Dispatch = 3,
    }

    private void PublishResponseChoice(BoxMessageQueue queue, int contactId, Vector3 position)
    {
        if (!enableResponseChoices || queue == null)
        {
            return;
        }

        pendingContactId = contactId;
        pendingContactPosition = position;
        pendingContactBeganAt = Time.realtimeSinceStartup;
        hasPendingResponse = true;

        pendingLastCountdownSeconds = -1;

        var effectiveTimeout = GetEffectiveResponseChoiceTimeoutSeconds();
        var initialSeconds = effectiveTimeout > 0f
            ? Mathf.CeilToInt(effectiveTimeout)
            : (int?)null;

        queue.Publish(new BoxMessage(
            triggerKey: $"ufo/response_choice/{pendingContactId}",
            channel: BoxChannel.Info,
            severity: BoxSeverity.Critical,
            sourceTag: "Operations",
            title: "Response Choice",
                body: BuildResponseChoiceBody(pendingContactId, initialSeconds)));
    }

    private void HandleResponseChoice(UFOResponseChoice choice)
    {
        if (!hasPendingResponse)
        {
            return;
        }

        hasPendingResponse = false;
        pendingLastCountdownSeconds = -1;

        StoreContactResponse(pendingContactId, choice);
        ApplyResponseEffects(pendingContactId, choice);
        ExecuteResponseActions(pendingContactId, choice, pendingContactPosition);

        var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
        if (queue == null)
        {
            return;
        }

        ClearResponseChoicePrompt(queue, pendingContactId);

        var choiceText = choice.ToString();
        var escalationText = TryGetEscalationSummaryForContact(pendingContactId, out var summary)
            ? $"\nEscalation: {summary}"
            : string.Empty;
        var priorityText = debugShowPriorityEffectsInBoxes
            ? $"\n\n{BuildPriorityEffectsReadout()}"
            : string.Empty;
        queue.Publish(new BoxMessage(
            triggerKey: $"ufo/response_confirm/{pendingContactId}",
            channel: BoxChannel.Info,
            severity: BoxSeverity.Warn,
            sourceTag: "Operations",
            title: "Response Set",
            body: $"Contact #{pendingContactId}\nDecision: {choiceText}\nLocation: {pendingContactPosition}{escalationText}\n\nMeters: Education {Mathf.RoundToInt(education)}  |  PermitRisk {Mathf.RoundToInt(permitRisk)}{priorityText}"));
    }

    private static string GetResponseChoiceTriggerKey(int contactId) => $"ufo/response_choice/{contactId}";

    private static string BuildResponseChoiceBody(int contactId, int? remainingSeconds)
    {
        var timeoutText = string.Empty;
        if (remainingSeconds.HasValue)
        {
            var total = Mathf.Max(0, remainingSeconds.Value);
            var minutes = total / 60;
            var seconds = total % 60;
            timeoutText = $"\n(Expires in {minutes}:{seconds:00})";
        }

        return $"1 Ignore   2 Track   3 Intercept   4 Dispatch\nContact #{contactId}{timeoutText}";
    }

    private void RefreshResponseChoiceCountdownIfNeeded()
    {
        if (!hasPendingResponse)
        {
            return;
        }

        var effectiveTimeout = GetEffectiveResponseChoiceTimeoutSeconds();
        if (effectiveTimeout <= 0f)
        {
            return;
        }

        var elapsed = Time.realtimeSinceStartup - pendingContactBeganAt;
        var remaining = Mathf.CeilToInt(effectiveTimeout - elapsed);
        remaining = Mathf.Max(0, remaining);

        if (remaining == pendingLastCountdownSeconds)
        {
            return;
        }

        pendingLastCountdownSeconds = remaining;

        var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
        if (queue == null)
        {
            return;
        }

        var key = GetResponseChoiceTriggerKey(pendingContactId);
        queue.UpdateBody(key, BuildResponseChoiceBody(pendingContactId, remaining));
    }

    private static void ClearResponseChoicePrompt(BoxMessageQueue queue, int contactId)
    {
        if (queue == null)
        {
            return;
        }

        var key = GetResponseChoiceTriggerKey(contactId);
        queue.DismissIfCurrent(key);
        queue.RemoveQueued(key);
    }

    private void ExpirePendingResponse()
    {
        if (!hasPendingResponse)
        {
            return;
        }

        hasPendingResponse = false;
        pendingLastCountdownSeconds = -1;

        var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
        if (queue == null)
        {
            return;
        }

        ClearResponseChoicePrompt(queue, pendingContactId);

        queue.Publish(new BoxMessage(
            triggerKey: $"ufo/response_expired/{pendingContactId}",
            channel: BoxChannel.Info,
            severity: BoxSeverity.Warn,
            sourceTag: "Operations",
            title: "Response Expired",
            body: $"Contact #{pendingContactId}\nNo response was selected in time."));
    }

    private void ExecuteResponseActions(int contactId, UFOResponseChoice choice, Vector3 position)
    {
        switch (choice)
        {
            case UFOResponseChoice.Dispatch:
            {
                var squad = UnityEngine.Object.FindAnyObjectByType<SquadManager>();
                if (squad != null)
                {
                    squad.DeploySquad(position);
                }
                else
                {
                    Debug.LogWarning("[UFO] SquadManager not found; cannot dispatch squad.");
                }
                break;
            }

            case UFOResponseChoice.Intercept:
            {
                if (!destroyUfoVisualOnIntercept)
                {
                    break;
                }

                if (ufoVisualByContactId.TryGetValue(contactId, out var ufo) && ufo != null)
                {
                    ufoVisualByContactId.Remove(contactId);
                    Destroy(ufo, interceptDestroyDelaySeconds);
                }
                break;
            }
        }
    }

    public bool TryGetStoredResponse(int contactId, out string response)
    {
        if (contactRecordsById.TryGetValue(contactId, out var record) && record.HasResponse)
        {
            response = record.Response.ToString();
            return true;
        }

        response = default;
        return false;
    }

    private int RegisterContact(Vector3 position)
    {
        nextContactId++;
        var contactId = nextContactId;

        var record = new ContactRecord
        {
            ContactId = contactId,
            Position = position,
            SpawnedAt = Time.realtimeSinceStartup,
            HasOutcome = false,
            Segment = default,
            Intent = default,
            QuietCargo = false,
            HasResponse = false,
            Response = default,
            RespondedAt = 0f,
        };

        contactRecordsById[contactId] = record;
        contactIdOrder.Enqueue(contactId);

        PruneContactRecordsIfNeeded();
        return contactId;
    }

    private void StoreContactResponse(int contactId, UFOResponseChoice choice)
    {
        if (!contactRecordsById.TryGetValue(contactId, out var record))
        {
            return;
        }

        record.HasResponse = true;
        record.Response = choice;
        record.RespondedAt = Time.realtimeSinceStartup;
        contactRecordsById[contactId] = record;
    }

    private void StoreContactOutcome(int contactId, (UFOSpeciesSegment Segment, UFOIntent Intent) outcome, bool quietCargo)
    {
        if (!contactRecordsById.TryGetValue(contactId, out var record))
        {
            return;
        }

        record.HasOutcome = true;
        record.Segment = outcome.Segment;
        record.Intent = outcome.Intent;
        record.QuietCargo = quietCargo;
        contactRecordsById[contactId] = record;
    }

    private int GetEscalationStage(UFOSpeciesSegment segment)
    {
        if (segment == UFOSpeciesSegment.Unknown)
        {
            return 1;
        }

        if (!escalationStageBySegment.TryGetValue(segment, out var stage) || stage < 1)
        {
            stage = 1;
        }

        return Mathf.Clamp(stage, 1, 3);
    }

    private void AdjustEscalationStage(UFOSpeciesSegment segment, int delta)
    {
        if (segment == UFOSpeciesSegment.Unknown)
        {
            return;
        }

        escalationStageBySegment[segment] = Mathf.Clamp(GetEscalationStage(segment) + delta, 1, 3);

        if (delta != 0)
        {
            overreachPrimedBySegment[segment] = false;
        }
    }

    private void ClearOverreachPrime(UFOSpeciesSegment segment)
    {
        if (segment == UFOSpeciesSegment.Unknown)
        {
            return;
        }

        overreachPrimedBySegment[segment] = false;
    }

    private static string StageLabel(int stage)
    {
        return stage switch
        {
            1 => "Probe",
            2 => "Commit",
            3 => "Overreach",
            _ => "Probe",
        };
    }

    private bool TryGetEscalationSummaryForContact(int contactId, out string summary)
    {
        if (contactRecordsById.TryGetValue(contactId, out var record) && record.HasOutcome)
        {
            var stage = GetEscalationStage(record.Segment);
            summary = $"{record.Segment} {stage}/3 ({StageLabel(stage)})";
            return true;
        }

        summary = default;
        return false;
    }

    private void ApplyResponseEffects(int contactId, UFOResponseChoice choice)
    {
        if (!contactRecordsById.TryGetValue(contactId, out var record) || !record.HasOutcome)
        {
            return;
        }

        var segment = record.Segment;
        var effectiveEducation = education + GetPriorityEducationThresholdBonus();

        DebugLogPriority($"[UFO][Priority] ApplyResponseEffects contact={contactId} choice={choice} segment={segment} priority={GetCurrentPriority()} education={education:0.0} effectiveEducation={effectiveEducation:0.0} permitRisk={permitRisk:0.0}");

        // Any active response disrupts escalation momentum.
        if (choice != UFOResponseChoice.Ignore)
        {
            ClearOverreachPrime(segment);
        }

        // Baseline: ignoring generally advances escalation (nobody "punished" their signature play).
        if (choice == UFOResponseChoice.Ignore)
        {
            var stage = GetEscalationStage(segment);

            // Slow the climb to Overreach: stage 1 -> 2 is one ignore; stage 2 -> 3 requires two ignores.
            if (stage <= 1)
            {
                AdjustEscalationStage(segment, +1);
            }
            else if (stage == 2)
            {
                var primed = overreachPrimedBySegment.TryGetValue(segment, out var p) && p;
                if (!primed)
                {
                    overreachPrimedBySegment[segment] = true;
                }
                else
                {
                    overreachPrimedBySegment[segment] = false;
                    AdjustEscalationStage(segment, +1);
                }
            }

            if (segment == UFOSpeciesSegment.Green)
            {
                ApplyPermitRiskDeltaFromResponse(+2f);
            }
            return;
        }

        // Minimal exploit rules. Education gates how effectively you convert tells into advantage.
        switch (segment)
        {
            case UFOSpeciesSegment.Human:
                if (choice == UFOResponseChoice.Track)
                {
                    AdjustEscalationStageForChoice(segment, -1, choice);
                    ApplyEducationDelta(+0.5f);
                }
                else if (choice == UFOResponseChoice.Dispatch)
                {
                    AdjustEscalationStageForChoice(segment, -1, choice);
                    ApplyPermitRiskDeltaFromResponse(-0.5f);
                }
                else if (choice == UFOResponseChoice.Intercept)
                {
                    if (effectiveEducation >= 45f)
                    {
                        AdjustEscalationStageForChoice(segment, -1, choice);
                        ApplyEducationDelta(+0.5f);
                    }
                    else
                    {
                        AdjustEscalationStageForChoice(segment, +1, choice);
                        ApplyPermitRiskDeltaFromResponse(+0.5f);
                    }
                }
                break;

            case UFOSpeciesSegment.Grey:
                if (choice == UFOResponseChoice.Track)
                {
                    if (effectiveEducation >= 25f)
                    {
                        AdjustEscalationStageForChoice(segment, -1, choice);
                    }
                }
                else if (choice == UFOResponseChoice.Dispatch)
                {
                    if (effectiveEducation >= 35f)
                    {
                        AdjustEscalationStageForChoice(segment, -1, choice);
                    }
                    else
                    {
                        AdjustEscalationStageForChoice(segment, +1, choice);
                    }
                }
                else if (choice == UFOResponseChoice.Intercept)
                {
                    AdjustEscalationStageForChoice(segment, +1, choice);
                }
                break;

            case UFOSpeciesSegment.Green:
                if (choice == UFOResponseChoice.Track)
                {
                    ApplyPermitRiskDeltaFromResponse(-(effectiveEducation >= 15f ? 1.5f : 1f));
                }
                else if (choice == UFOResponseChoice.Dispatch)
                {
                    AdjustEscalationStageForChoice(segment, -1, choice);
                    ApplyPermitRiskDeltaFromResponse(-2.5f);
                }
                else if (choice == UFOResponseChoice.Intercept)
                {
                    if (effectiveEducation >= 55f)
                    {
                        AdjustEscalationStageForChoice(segment, -1, choice);
                        ApplyPermitRiskDeltaFromResponse(-3f);
                    }
                    else
                    {
                        AdjustEscalationStageForChoice(segment, +1, choice);
                        ApplyPermitRiskDeltaFromResponse(+1.5f);
                    }
                }
                break;

            case UFOSpeciesSegment.Dark:
                if (choice == UFOResponseChoice.Dispatch)
                {
                    if (effectiveEducation >= 25f)
                    {
                        AdjustEscalationStageForChoice(segment, -1, choice);
                    }
                }
                else if (choice == UFOResponseChoice.Track)
                {
                    if (effectiveEducation >= 55f)
                    {
                        AdjustEscalationStageForChoice(segment, -1, choice);
                    }
                    else
                    {
                        AdjustEscalationStageForChoice(segment, +1, choice);
                    }
                }
                else if (choice == UFOResponseChoice.Intercept)
                {
                    AdjustEscalationStageForChoice(segment, +1, choice);
                }
                break;
        }
    }

    private void PruneContactRecordsIfNeeded()
    {
        var limit = Mathf.Max(1, maxStoredContacts);
        while (contactIdOrder.Count > limit)
        {
            var oldest = contactIdOrder.Dequeue();
            contactRecordsById.Remove(oldest);

            if (ufoVisualByContactId.TryGetValue(oldest, out var ufo) && ufo != null)
            {
                Destroy(ufo);
            }
            ufoVisualByContactId.Remove(oldest);
        }
    }

    private void PublishPapalWarningIfNeeded(BoxMessageQueue queue)
    {
        if (!publishPapalWarningOnFirstContact || publishedPapalWarning)
        {
            return;
        }

        publishedPapalWarning = true;

        queue.Publish(new BoxMessage(
            triggerKey: "lore/pope/warning",
            channel: BoxChannel.Thinking,
            severity: BoxSeverity.Info,
            sourceTag: "Papal Advisory",
            title: "A Warning Is Issued",
            body: string.IsNullOrWhiteSpace(papalWarningBody)
                ? "A high-trust warning has been issued. Do not approach unknown craft. Report sightings early."
                : papalWarningBody));
    }

    private void PublishIncidentAssessment(
        BoxMessageQueue queue,
        int contactId,
        Vector3 position,
        (UFOSpeciesSegment Segment, UFOIntent Intent)? forcedOutcome,
        bool? forcedQuietCargo)
    {
        var outcome = forcedOutcome ?? PickOutcomeOrDefault();

        // Hard rule: Greens do not use "Smuggle" intent in this game's logic.
        // If configured via Inspector weights, remap to the closest equivalent.
        if (outcome.Segment == UFOSpeciesSegment.Green && outcome.Intent == UFOIntent.Smuggle)
        {
            outcome = (UFOSpeciesSegment.Green, UFOIntent.Negotiate);
        }

        var severity = BoxSeverity.Warn;
        var title = "Incident Assessment";
        var body = string.Empty;

        var quietCargo = false;
        var publishEducationBulletin = false;
        string educationTitle = null;
        string educationBody = null;

        var behavior = GetBehaviorTag(outcome);
        var crewEstimate = GetCrewEstimate(outcome);

        switch (outcome.Segment)
        {
            case UFOSpeciesSegment.Green:
            {
                switch (outcome.Intent)
                {
                    case UFOIntent.Negotiate:
                        severity = BoxSeverity.Critical;
                        title = "Permit Attempt";
                        body = "Green delegation suspected. Expect a request for access, 'aid', or legal footholds. This is an influence operation.";
                        publishEducationBulletin = true;
                        educationTitle = "Education Bulletin";
                        educationBody = "Leadership advisory: do not grant landing permits or protected zones to unknown factions. Require verification and oversight.";
                        break;

                    case UFOIntent.Seed:
                        severity = BoxSeverity.Critical;
                        title = "Foothold Activity";
                        body = "Green seeding activity suspected. Prioritize verification, perimeter control, and evidence recovery.";
                        publishEducationBulletin = true;
                        educationTitle = "Education Bulletin";
                        educationBody = "Public advisory: avoid contact with unknown materials or devices. Report anomalies to X-CON.";
                        break;

                    case UFOIntent.Raid:
                        severity = BoxSeverity.Critical;
                        title = "Enforcement Raid";
                        body = "Green raid activity suspected. Expect coordinated pressure and forced compliance. Dispatch response assets.";
                        break;

                    default:
                        severity = BoxSeverity.Warn;
                        title = "Green Activity";
                        body = "Green-linked activity suspected. Maintain standoff distance and dispatch certified response.";
                        break;
                }

                break;
            }

            case UFOSpeciesSegment.Grey:
                severity = BoxSeverity.Warn;
                title = outcome.Intent == UFOIntent.Scout ? "Grey Scout" : "Grey Contact";
                body = "Grey-linked behavior suspected. Expect observation, misdirection, and selective disclosure. Secure data and avoid escalation.";
                break;

            case UFOSpeciesSegment.Dark:
                severity = GetDarkSeverity(behavior, crewEstimate);
                title = behavior == UFOBehaviorTag.TiltChallenge ? "Dark Challenge" : "Dark Activity";
                body = BuildDarkBody(behavior, crewEstimate);
                publishEducationBulletin = true;
                educationTitle = "Education Bulletin";
                educationBody = "Field advisory: watch for disorientation and panic. Maintain buddy checks and follow trained protocols.";
                break;

            case UFOSpeciesSegment.Human:
                switch (outcome.Intent)
                {
                    case UFOIntent.Smuggle:
                    {
                        if (forcedQuietCargo.HasValue)
                        {
                            quietCargo = forcedQuietCargo.Value;
                        }
                        else
                        {
                            quietCargo = UnityEngine.Random.value < quietCargoChanceWhenSmuggler;
                        }
                        if (quietCargo)
                        {
                            severity = BoxSeverity.Critical;
                            title = "Quiet Cargo Pattern";
                            body = "Human criminal smuggler craft suspected. 'Cargo of Quiet Names' pattern likely. Civilians may avoid the site—dispatch trained personnel only.";
                            publishEducationBulletin = true;
                            educationTitle = "Education Bulletin";
                            educationBody = "Public advisory: do not approach unknown craft or crash sites. Report sightings immediately. Certified personnel only.";
                        }
                        else
                        {
                            severity = BoxSeverity.Warn;
                            title = "Human Smuggler Craft";
                            body = "Human criminal UFO-capable activity suspected. Expect lower-grade weapons but unpredictable intent. Smuggler staging likely—treat as hostile.";
                            publishEducationBulletin = true;
                            educationTitle = "Education Bulletin";
                            educationBody = "Public advisory: do not approach unknown craft. Report sightings and avoid interference with recovery teams.";
                        }

                        break;
                    }

                    case UFOIntent.Raid:
                        severity = BoxSeverity.Warn;
                        title = "Human Raid";
                        body = "Human criminal activity suspected. Expect opportunistic looting and fast extraction. Secure evidence quickly.";
                        break;

                    default:
                        severity = BoxSeverity.Info;
                        title = "Human Activity";
                        body = "Human-linked activity suspected. Maintain standoff distance and dispatch certified response.";
                        break;
                }

                break;

            default:
                if (outcome.Intent == UFOIntent.FalseAlarm)
                {
                    severity = BoxSeverity.Info;
                    title = "Contact Uncertain";
                    body = "Visual contact is inconsistent. Could be atmospheric distortion or a decoy. Keep sensors on it.";
                }
                else
                {
                    severity = BoxSeverity.Warn;
                    title = "Assessment Pending";
                    body = "Insufficient data. Hold civilians back and dispatch certified personnel.";
                }
                break;
        }

            StoreContactOutcome(contactId, outcome, quietCargo);

            ApplyMetersForOutcome(outcome, publishedEducationBulletin: publishEducationBulletin, quietCargo: quietCargo);

        body = $"{body}\n\nSegment: {outcome.Segment}  |  Intent: {outcome.Intent}";

        if (behavior != UFOBehaviorTag.None)
        {
            body += $"\nBehavior: {behavior}";
        }

        if (crewEstimate.HasValue)
        {
            body += crewEstimate.Value == 1
                ? "\nCrew estimate: Solo (lower risk posture)"
                : $"\nCrew estimate: {crewEstimate.Value} (pilot + systems)";
        }

        if (enableSoftStart && (contactOutcomeWeights == null || contactOutcomeWeights.Count == 0) && softStartDurationSeconds > 0f)
        {
            var t = Mathf.Clamp01((Time.realtimeSinceStartup - softStartBeganAt) / softStartDurationSeconds);
            body = $"{body}\nPhase: Ramp {Mathf.RoundToInt(t * 100f)}%";
        }

        var stage = GetEscalationStage(outcome.Segment);
        body = $"{body}\nEscalation: {outcome.Segment} {stage}/3 ({StageLabel(stage)})";
        body = $"{body}\n\nMeters: Education {Mathf.RoundToInt(education)}  |  PermitRisk {Mathf.RoundToInt(permitRisk)}";
        if (debugShowPriorityEffectsInBoxes)
        {
            body = $"{body}\n\n{BuildPriorityEffectsReadout()}";
        }

        body = $"Contact #{contactId}\n\n{body}";

        queue.Publish(new BoxMessage(
            triggerKey: $"ufo/incident/{contactId}",
            channel: BoxChannel.Info,
            severity: severity,
            sourceTag: "Analysis",
            title: title,
            body: $"{body}\n\nLast known: {position}"));

        if (publishEducationBulletin && !string.IsNullOrWhiteSpace(educationBody))
        {
            var bulletinStage = GetEscalationStage(outcome.Segment);
            queue.Publish(new BoxMessage(
                triggerKey: $"education/bulletin/ufo/{contactId}",
                channel: BoxChannel.Info,
                severity: BoxSeverity.Info,
                sourceTag: "Education",
                title: educationTitle ?? "Education Bulletin",
                body: $"Contact #{contactId}\n\n{educationBody}\n\nEscalation: {outcome.Segment} {bulletinStage}/3 ({StageLabel(bulletinStage)})\n\nMeters: Education {Mathf.RoundToInt(education)}  |  PermitRisk {Mathf.RoundToInt(permitRisk)}{(debugShowPriorityEffectsInBoxes ? $"\n\n{BuildPriorityEffectsReadout()}" : string.Empty)}"));
        }
    }

    private void ApplyMetersForOutcome((UFOSpeciesSegment Segment, UFOIntent Intent) outcome, bool publishedEducationBulletin, bool quietCargo)
    {
        // Education always rises slowly from contact frequency.
        ApplyEducationDelta(educationGainPerIncident);
        if (publishedEducationBulletin)
        {
            ApplyEducationDelta(educationGainPerBulletin);
        }

        // PermitRisk is primarily driven by influence/foothold operations.
        var permitDelta = 0f;
        if (outcome.Segment == UFOSpeciesSegment.Green)
        {
            switch (outcome.Intent)
            {
                case UFOIntent.Negotiate:
                    permitDelta += 6f;
                    break;
                case UFOIntent.Seed:
                    permitDelta += 4f;
                    break;
                case UFOIntent.Raid:
                    permitDelta += 2f;
                    break;
                default:
                    permitDelta += 1f;
                    break;
            }
        }
        else if (outcome.Segment == UFOSpeciesSegment.Dark)
        {
            permitDelta += 0.5f;
        }
        else if (outcome.Segment == UFOSpeciesSegment.Human && outcome.Intent == UFOIntent.Smuggle)
        {
            permitDelta += quietCargo ? 0.75f : 0.25f;
        }

        var rawPermitDelta = permitDelta;
        permitDelta *= GetPriorityPermitRiskFromOutcomeMultiplier();
        permitRisk = Mathf.Clamp(permitRisk + permitDelta, 0f, 100f);

        DebugLogPriority($"[UFO][Priority] PermitRisk(outcome) raw={rawPermitDelta:0.###} applied={permitDelta:0.###} mult={GetPriorityPermitRiskFromOutcomeMultiplier():0.00} priority={GetCurrentPriority()} now={permitRisk:0.0} outcome={outcome.Segment}/{outcome.Intent}");
    }

    private UFOBehaviorTag GetBehaviorTag((UFOSpeciesSegment Segment, UFOIntent Intent) outcome)
    {
        if (outcome.Segment == UFOSpeciesSegment.Dark && outcome.Intent == UFOIntent.Raid)
        {
            return UFOBehaviorTag.TiltChallenge;
        }

        return UFOBehaviorTag.None;
    }

    private int? GetCrewEstimate((UFOSpeciesSegment Segment, UFOIntent Intent) outcome)
    {
        if (outcome.Segment != UFOSpeciesSegment.Dark)
        {
            return null;
        }

        if (UnityEngine.Random.value < darkSoloChance)
        {
            return 1;
        }

        var min = Mathf.Max(2, darkCrewMin);
        var max = Mathf.Max(min, darkCrewMax);
        return UnityEngine.Random.Range(min, max + 1);
    }

    private static BoxSeverity GetDarkSeverity(UFOBehaviorTag behavior, int? crewEstimate)
    {
        // Tactical read: solo contacts are dangerous but often "testing".
        // 2+ crew implies dedicated systems/weapons operation and higher escalation risk.
        if (!crewEstimate.HasValue)
        {
            return BoxSeverity.Warn;
        }

        if (crewEstimate.Value <= 1)
        {
            return BoxSeverity.Warn;
        }

        if (behavior == UFOBehaviorTag.TiltChallenge)
        {
            return BoxSeverity.Critical;
        }

        return BoxSeverity.Warn;
    }

    private static string BuildDarkBody(UFOBehaviorTag behavior, int? crewEstimate)
    {
        var baseText = behavior == UFOBehaviorTag.TiltChallenge
            ? "Dark-linked contact halted and rolled on-axis (challenge display)."
            : "Dark-linked activity suspected.";

        if (!crewEstimate.HasValue)
        {
            return baseText + " Maintain standoff and hold formation discipline.";
        }

        if (crewEstimate.Value <= 1)
        {
            return baseText + " Solo operator suspected. Risk is lower but unpredictable—avoid pursuit and keep teams anchored.";
        }

        // 2+ crew
        return baseText + " Multi-operator crew suspected (pilot + systems). Do not engage. Avoid pursuit into unknown envelope. Maintain standoff and request escalation assets.";
    }

    private (UFOSpeciesSegment Segment, UFOIntent Intent) PickOutcomeOrDefault()
    {
        // Provide sane defaults even if inspector list is empty.
        if (contactOutcomeWeights == null || contactOutcomeWeights.Count == 0)
        {
            return PickDefaultOutcomeWithSoftStart();
        }

        var total = 0f;
        for (var i = 0; i < contactOutcomeWeights.Count; i++)
        {
            var w = contactOutcomeWeights[i].Weight;
            if (w > 0f)
            {
                total += w;
            }
        }

        if (total <= 0f)
        {
            return (UFOSpeciesSegment.Unknown, UFOIntent.Unknown);
        }

        var roll = UnityEngine.Random.value * total;
        for (var i = 0; i < contactOutcomeWeights.Count; i++)
        {
            var entry = contactOutcomeWeights[i];
            if (entry.Weight <= 0f)
            {
                continue;
            }

            roll -= entry.Weight;
            if (roll <= 0f)
            {
                return (entry.Segment, entry.Intent);
            }
        }

        var last = contactOutcomeWeights[contactOutcomeWeights.Count - 1];
        return (last.Segment, last.Intent);
    }

    private (UFOSpeciesSegment Segment, UFOIntent Intent) PickDefaultOutcomeWithSoftStart()
    {
        // Late-game defaults tuned so overall Quiet Cargo frequency is ~56%.
        // Approx: P(QuietCargo) = P(Green+Smuggle) * quietCargoChanceWhenSmuggler.
        // With quietCargoChanceWhenSmuggler=0.85, target P(Green+Smuggle) ~= 0.56/0.85 ~= 0.66.
        var late = PickWeightedDynamic(
            ((UFOSpeciesSegment.Human, UFOIntent.Smuggle), 0.66f),
            ((UFOSpeciesSegment.Grey, UFOIntent.Scout), 0.16f),
            ((UFOSpeciesSegment.Dark, UFOIntent.Raid), 0.09f),
            ((UFOSpeciesSegment.Green, UFOIntent.Negotiate), 0.06f),
            ((UFOSpeciesSegment.Unknown, UFOIntent.FalseAlarm), 0.03f));

        if (!enableSoftStart || softStartDurationSeconds <= 0f)
        {
            return late;
        }

        // Soft-start: make early gameplay more grounded. Mostly Human criminal craft + false alarms.
        // As the ramp progresses, the late-game distribution takes over.
        var t = Mathf.Clamp01((Time.realtimeSinceStartup - softStartBeganAt) / softStartDurationSeconds);

        // Blend between two normalized endpoints so totals stay consistent across the ramp.
        var wHumanSmuggle = Mathf.Lerp(0.55f, 0.66f, t);
        var wHumanRaid = Mathf.Lerp(0.15f, 0.00f, t);
        var wFalseAlarm = Mathf.Lerp(0.15f, 0.03f, t);
        var wGreyScout = Mathf.Lerp(0.10f, 0.16f, t);
        var wDarkRaid = Mathf.Lerp(0.00f, 0.09f, t);
        var wGreenNegotiate = Mathf.Lerp(0.05f, 0.06f, t);

        return PickWeightedDynamic(
            ((UFOSpeciesSegment.Human, UFOIntent.Smuggle), wHumanSmuggle),
            ((UFOSpeciesSegment.Human, UFOIntent.Raid), wHumanRaid),
            ((UFOSpeciesSegment.Unknown, UFOIntent.FalseAlarm), wFalseAlarm),
            ((UFOSpeciesSegment.Grey, UFOIntent.Scout), wGreyScout),
            ((UFOSpeciesSegment.Dark, UFOIntent.Raid), wDarkRaid),
            ((UFOSpeciesSegment.Green, UFOIntent.Negotiate), wGreenNegotiate));
    }

    private static (UFOSpeciesSegment Segment, UFOIntent Intent) PickWeightedDynamic(
        params ((UFOSpeciesSegment Segment, UFOIntent Intent) outcome, float weight)[] entries)
    {
        if (entries == null || entries.Length == 0)
        {
            return (UFOSpeciesSegment.Unknown, UFOIntent.Unknown);
        }

        var total = 0f;
        for (var i = 0; i < entries.Length; i++)
        {
            var w = entries[i].weight;
            if (w > 0f)
            {
                total += w;
            }
        }

        if (total <= 0f)
        {
            return (UFOSpeciesSegment.Unknown, UFOIntent.Unknown);
        }

        var roll = UnityEngine.Random.value * total;
        for (var i = 0; i < entries.Length; i++)
        {
            var w = entries[i].weight;
            if (w <= 0f)
            {
                continue;
            }

            roll -= w;
            if (roll <= 0f)
            {
                return entries[i].outcome;
            }
        }

        return entries[entries.Length - 1].outcome;
    }

    public void MoveUFO(GameObject ufo, Vector3 targetPosition)
    {
        // TODO: Move UFO to target position
        ufo.transform.position = targetPosition;
        DebugLogUfoEvent("UFO moved to " + targetPosition);
    }

    private IEnumerator PublishThinking(BoxMessageQueue queue)
    {
        if (thinkingDelaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(thinkingDelaySeconds);
        }

        queue.Publish(new BoxMessage(
            triggerKey: "thinking/pick_priority",
            channel: BoxChannel.Thinking,
            severity: BoxSeverity.Info,
            sourceTag: "Commander",
            title: "Pick a Priority",
            body: "Pick a priority:\n1) Coverage\n2) Research\n3) Response"));
    }

    [ContextMenu("Debug/Spot UFO")]
    private void DebugSpotUfo()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[UFO] Enter Play Mode to run the debug action.");
            return;
        }

        SpawnUFO(new Vector3(12f, 0f, 8f));
    }

    [ContextMenu("Debug/Meters: Reset")]
    private void DebugResetMeters()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[UFO] Enter Play Mode to run the debug action.");
            return;
        }

        education = 5f;
        permitRisk = 0f;
        Debug.Log($"[UFO] Meters reset. Education={education}, PermitRisk={permitRisk}");
    }

    [ContextMenu("Debug/Meters: Log")]
    private void DebugLogMeters()
    {
        Debug.Log($"[UFO] Meters: Education={education}, PermitRisk={permitRisk}");
    }

    [ContextMenu("Debug/Contacts: Log Stored Responses")]
    private void DebugLogStoredResponses()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[UFO] Enter Play Mode to run the debug action.");
            return;
        }

        if (contactRecordsById.Count == 0)
        {
            Debug.Log("[UFO] No stored contacts.");
            return;
        }

        foreach (var kvp in contactRecordsById)
        {
            var r = kvp.Value;
            var responseText = r.HasResponse ? r.Response.ToString() : "(none)";
            Debug.Log($"[UFO] Contact #{r.ContactId} at {r.Position} response={responseText}");
        }
    }

    [ContextMenu("Debug/Incident: Human Smuggle (Quiet Cargo)")]
    private void DebugIncidentHumanSmuggleQuietCargo()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[UFO] Enter Play Mode to run the debug action.");
            return;
        }

        var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
        if (queue == null)
        {
            Debug.LogWarning("[UFO] BoxMessageQueue not found; cannot publish debug incident.");
            return;
        }

        var position = new Vector3(14f, 0f, 6f);
        var contactId = RegisterContact(position);
        queue.Publish(new BoxMessage(
            triggerKey: $"ufo/spotted/{contactId}",
            channel: BoxChannel.Info,
            severity: BoxSeverity.Warn,
            sourceTag: "Radar",
            title: "UFO Spotted",
            body: $"Contact #{contactId}\nUFO sighted at {position}."));

        PublishIncidentAssessment(queue, contactId, position, forcedOutcome: (UFOSpeciesSegment.Human, UFOIntent.Smuggle), forcedQuietCargo: true);
        PublishResponseChoice(queue, contactId, position);
    }

    [ContextMenu("Debug/Incident: Human Smuggle (No Quiet Cargo)")]
    private void DebugIncidentHumanSmuggleNoQuietCargo()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[UFO] Enter Play Mode to run the debug action.");
            return;
        }

        var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
        if (queue == null)
        {
            Debug.LogWarning("[UFO] BoxMessageQueue not found; cannot publish debug incident.");
            return;
        }

        var position = new Vector3(10f, 0f, 10f);
        var contactId = RegisterContact(position);
        queue.Publish(new BoxMessage(
            triggerKey: $"ufo/spotted/{contactId}",
            channel: BoxChannel.Info,
            severity: BoxSeverity.Warn,
            sourceTag: "Radar",
            title: "UFO Spotted",
            body: $"Contact #{contactId}\nUFO sighted at {position}."));

        PublishIncidentAssessment(queue, contactId, position, forcedOutcome: (UFOSpeciesSegment.Human, UFOIntent.Smuggle), forcedQuietCargo: false);
        PublishResponseChoice(queue, contactId, position);
    }

    [ContextMenu("Debug/Incident: Grey Scout")]
    private void DebugIncidentGreyScout()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[UFO] Enter Play Mode to run the debug action.");
            return;
        }

        var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
        if (queue == null)
        {
            Debug.LogWarning("[UFO] BoxMessageQueue not found; cannot publish debug incident.");
            return;
        }

        var position = new Vector3(8f, 0f, 14f);
        var contactId = RegisterContact(position);
        queue.Publish(new BoxMessage(
            triggerKey: $"ufo/spotted/{contactId}",
            channel: BoxChannel.Info,
            severity: BoxSeverity.Warn,
            sourceTag: "Radar",
            title: "UFO Spotted",
            body: $"Contact #{contactId}\nUFO sighted at {position}."));

        PublishIncidentAssessment(queue, contactId, position, forcedOutcome: (UFOSpeciesSegment.Grey, UFOIntent.Scout), forcedQuietCargo: null);
        PublishResponseChoice(queue, contactId, position);
    }

    [ContextMenu("Debug/Incident: Dark Raid")]
    private void DebugIncidentDarkRaid()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[UFO] Enter Play Mode to run the debug action.");
            return;
        }

        var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
        if (queue == null)
        {
            Debug.LogWarning("[UFO] BoxMessageQueue not found; cannot publish debug incident.");
            return;
        }

        var position = new Vector3(18f, 0f, 2f);
        var contactId = RegisterContact(position);
        queue.Publish(new BoxMessage(
            triggerKey: $"ufo/spotted/{contactId}",
            channel: BoxChannel.Info,
            severity: BoxSeverity.Warn,
            sourceTag: "Radar",
            title: "UFO Spotted",
            body: $"Contact #{contactId}\nUFO sighted at {position}."));

        PublishIncidentAssessment(queue, contactId, position, forcedOutcome: (UFOSpeciesSegment.Dark, UFOIntent.Raid), forcedQuietCargo: null);
        PublishResponseChoice(queue, contactId, position);
    }

    [ContextMenu("Debug/Incident: Green Permit Attempt")]
    private void DebugIncidentGreenPermitAttempt()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[UFO] Enter Play Mode to run the debug action.");
            return;
        }

        var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
        if (queue == null)
        {
            Debug.LogWarning("[UFO] BoxMessageQueue not found; cannot publish debug incident.");
            return;
        }

        var position = new Vector3(3f, 0f, 18f);
        var contactId = RegisterContact(position);
        queue.Publish(new BoxMessage(
            triggerKey: $"ufo/spotted/{contactId}",
            channel: BoxChannel.Info,
            severity: BoxSeverity.Warn,
            sourceTag: "Radar",
            title: "UFO Spotted",
            body: $"Contact #{contactId}\nUFO sighted at {position}."));

        PublishIncidentAssessment(queue, contactId, position, forcedOutcome: (UFOSpeciesSegment.Green, UFOIntent.Negotiate), forcedQuietCargo: null);
        PublishResponseChoice(queue, contactId, position);
    }
}
