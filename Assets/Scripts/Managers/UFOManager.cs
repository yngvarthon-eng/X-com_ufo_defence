using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using XCon.UI.Boxes;
using XCon.Smuggling;
using UnityEngine.UI;

using Object = UnityEngine.Object;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class UFOManager : MonoBehaviour
{
    [Header("Boxes")]
    [SerializeField] private bool publishUfoSpottedBoxes = true;
    [SerializeField] private float thinkingDelaySeconds = 0.15f;

    [Header("UFO Visuals")]
    [Tooltip("Optional prefab spawned at the contact position when a UFO is detected.")]
    [SerializeField] private GameObject ufoPrefab;

    [Tooltip("Optional specialized visual used for smuggler-themed craft.")]
    [SerializeField] private GameObject smugglerUfoPrefab;

    [Tooltip("If true and a smuggler prefab is assigned, use it as the default spawned visual.")]
    [SerializeField] private bool useSmugglerPrefabAsDefaultVisual = true;

    [Tooltip("If enabled and both prefabs are assigned, spawns will alternate between base and smuggler visuals.")]
    [SerializeField] private bool alternateBetweenAssignedUfoVisuals = false;

    [Tooltip("If true and a prefab is assigned, spawns a visual UFO per contact.")]
    [SerializeField] private bool spawnUfoVisuals = true;

    [Tooltip("Vertical offset (Y) applied when spawning the UFO visual.")]
    [SerializeField] private float ufoVisualSpawnYOffset = 0f;

    [Tooltip("If enabled, scales spawned UFO visuals to a consistent world size based on renderer bounds.")]
    [SerializeField] private bool autoNormalizeUfoVisualSize = true;

    [Tooltip("Approximate target max dimension in world units after auto normalization.")]
    [Min(0.1f)]
    [SerializeField] private float targetUfoVisualSize = 3f;

    [Tooltip("If true, Intercept will remove the spawned UFO visual.")]
    [SerializeField] private bool destroyUfoVisualOnIntercept = true;

    [Tooltip("Delay before destroying the UFO visual when Intercept is chosen.")]
    [Min(0f)]
    [SerializeField] private float interceptDestroyDelaySeconds = 1.0f;

    [Tooltip("If true, dims the world map background while any UFO visual is active so 3D visuals are not fully hidden by UI.")]
    [SerializeField] private bool dimWorldMapBackgroundWhileUfoVisible = true;

    [Range(0f, 1f)]
    [Tooltip("Background alpha used while at least one UFO visual is active.")]
    [SerializeField] private float worldMapBackgroundDimAlpha = 0.45f;

    [Tooltip("If enabled, spawned UFO visuals start large, drift away from camera, and auto-vanish.")]
    [SerializeField] private bool animateAndExpireUfoVisuals = true;

    [Tooltip("If true, visual lifetime follows the effective response timeout when timeout is enabled.")]
    [SerializeField] private bool useResponseTimeoutForVisualLifetime = true;

    [Tooltip("Fallback visual lifetime when response timeout is disabled or not used.")]
    [Min(0.5f)]
    [SerializeField] private float ufoVisualLifetimeSeconds = 20f;

    [Tooltip("How far the UFO visual travels away from the camera before vanishing.")]
    [Min(0.1f)]
    [SerializeField] private float ufoVisualTravelDistance = 25f;

    [Tooltip("If enabled, animated UFO visuals are positioned using camera viewport space so they remain visible while traveling.")]
    [SerializeField] private bool keepAnimatedUfoInCameraView = true;

    [Tooltip("Viewport margin used when picking the UFO start position (0 = edge, 0.5 = center only).")]
    [Range(0f, 0.45f)]
    [SerializeField] private float ufoViewportMargin = 0.18f;

    [Tooltip("Depth from camera for initial animated UFO placement when viewport anchoring is enabled.")]
    [Min(1f)]
    [SerializeField] private float ufoStartDepthFromCamera = 10f;

    [Tooltip("Depth from camera for final animated UFO position when viewport anchoring is enabled.")]
    [Min(2f)]
    [SerializeField] private float ufoEndDepthFromCamera = 40f;

    [Tooltip("Initial scale multiplier applied at spawn before drifting away.")]
    [Min(0.1f)]
    [SerializeField] private float ufoVisualStartScaleMultiplier = 1.8f;

    [Tooltip("Final scale multiplier when the visual reaches the end of its travel.")]
    [Min(0.05f)]
    [SerializeField] private float ufoVisualEndScaleMultiplier = 0.3f;

    [Header("Auto Spawn")]
    [SerializeField] private bool enableAutoSpawn = true;

    [Tooltip("Base contact rate in contacts per real-time minute (Poisson process).")]
    [Min(0f)]
    [SerializeField] private float baseContactsPerMinute = 0.24f;

    [Tooltip("Minimum time between auto spawns (seconds).")]
    [Min(0f)]
    [SerializeField] private float minAutoSpawnIntervalSeconds = 12f;

    [Tooltip("Maximum time between auto spawns (seconds). 0 = no maximum.")]
    [Min(0f)]
    [SerializeField] private float maxAutoSpawnIntervalSeconds = 55f;

    [Tooltip("If true, auto spawning pauses while awaiting a response choice.")]
    [SerializeField] private bool pauseAutoSpawnWhileAwaitingResponse = true;

    [Tooltip("Center for random auto-spawn positions.")]
    [SerializeField] private Vector3 autoSpawnAreaCenter = Vector3.zero;

    [Tooltip("Half-extents for random auto-spawn positions (X/Z used).")]
    [SerializeField] private Vector2 autoSpawnAreaExtentsXZ = new Vector2(20f, 20f);

    [Tooltip("If >0, increases spawn rate with escalation pressure. 0.25 means +25% per pressure step.")]
    [Min(0f)]
    [SerializeField] private float escalationRateMultiplierPerPressure = 0.16f;

    [Tooltip("If >0, increases spawn rate with PermitRisk. 0.5 means up to +50% at PermitRisk=100.")]
    [Min(0f)]
    [SerializeField] private float permitRiskRateMultiplier = 0.28f;

    [Header("Response")]
    [SerializeField] private bool enableResponseChoices = true;
    [Tooltip("Delay before showing the response choice after incident assessment (seconds).")]
    [Min(0f)]
    [SerializeField] private float responseChoiceDelaySeconds = 1.2f;
    [Tooltip("How long the response choice stays active before it expires (seconds). 0 = never expires.")]
    [Min(0f)]
    [SerializeField] private float responseChoiceTimeoutSeconds = 26f;

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
    [SerializeField] private float educationGainPerBulletin = 1.5f;

    [Tooltip("Education gain applied on any incident assessment.")]
    [Range(0f, 10f)]
    [SerializeField] private float educationGainPerIncident = 0.2f;

    [Header("Incident Rules")]
    [Tooltip("Base distribution for what this contact is (segment + intent).")]
    [SerializeField] private List<ContactOutcomeWeight> contactOutcomeWeights = new();

    [Tooltip("If the incident is smuggler-linked, probability that the crew tries to enforce the 'Cargo of Quiet Names' rule.")]
    [Range(0f, 1f)]
    [SerializeField] private float quietCargoChanceWhenSmuggler = 0.75f;

    [Header("Mule Cargo (goods-only smugglers)")]
    [Tooltip("If enabled, non-Quiet-Cargo smuggler contacts are Mules carrying hidden goods with a hazard level.")]
    [SerializeField] private bool enableMuleCargo = true;

    [Min(0f)] [SerializeField] private float muleWeightContraband = 0.45f;
    [Min(0f)] [SerializeField] private float muleWeightHeavyMetals = 0.25f;
    [Min(0f)] [SerializeField] private float muleWeightDrugPrecursors = 0.25f;
    [Min(0f)] [SerializeField] private float muleWeightNuclearWarhead = 0.05f;

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

    // Soft-start ramp length (real-time seconds) comes from the difficulty preset (see Difficulty.cs).
    private static float softStartDurationSeconds => Difficulty.ActivePreset.SoftStartDurationSeconds;

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

    [Tooltip("Temporary debug hotkey to spawn a UFO in front of the active camera during play mode.")]
    [SerializeField] private bool debugEnableSpawnHotkey = true;

    [Tooltip("Distance in front of camera used by the temporary spawn hotkey.")]
    [Min(1f)]
    [SerializeField] private float debugSpawnDistanceFromCamera = 14f;

    private float softStartBeganAt;
    private bool publishedPapalWarning;

    private int nextContactId;
    private readonly Dictionary<int, ContactRecord> contactRecordsById = new();
    private readonly Queue<int> contactIdOrder = new();

    private readonly Dictionary<int, GameObject> ufoVisualByContactId = new();
    private readonly Dictionary<int, Coroutine> ufoVisualLifecycleCoroutineByContactId = new();
    private bool spawnSmugglerOnNextContact;
    private Image worldMapBackgroundImage;
    private bool hasCachedBackgroundAlpha;
    private float cachedBackgroundAlpha = 1f;

    private readonly Dictionary<UFOSpeciesSegment, int> escalationStageBySegment = new();
    private readonly Dictionary<UFOSpeciesSegment, bool> overreachPrimedBySegment = new();

    private int pendingContactId;
    private Vector3 pendingContactPosition;
    private float pendingContactBeganAt;
    private bool hasPendingResponse;
    private int pendingLastCountdownSeconds = -1;
    private bool pendingResponseShownPaused;

    private float nextAutoSpawnAt;
    private int respondedContactsCount;
    private int expiredResponsesCount;
    private int consecutiveExpiredResponsesCount;
    private ProceduralAudioCuePlayer cuePlayer;

    public float Education => education;
    public float PermitRisk => permitRisk;
    public int TotalContactsSpawned => nextContactId;
    public int RespondedContactsCount => respondedContactsCount;
    public int ExpiredResponsesCount => expiredResponsesCount;
    public int ConsecutiveExpiredResponsesCount => consecutiveExpiredResponsesCount;
    public float BaseContactsPerMinute => baseContactsPerMinute;

    public void SetBaseContactsPerMinute(float value)
    {
        baseContactsPerMinute = Mathf.Max(0f, value);

        if (!enableAutoSpawn)
        {
            return;
        }

        // Re-sample next spawn so rate changes are reflected immediately.
        ScheduleNextAutoSpawn(fromNowSeconds: null);
    }

    public void ApplyPersistentState(float loadedEducation, float loadedPermitRisk, int loadedTotalContacts, int loadedRespondedContacts, int loadedExpiredResponses, int loadedConsecutiveExpiredResponses)
    {
        education = Mathf.Clamp(loadedEducation, 0f, 100f);
        permitRisk = Mathf.Clamp(loadedPermitRisk, 0f, 100f);

        nextContactId = Mathf.Max(0, loadedTotalContacts);
        respondedContactsCount = Mathf.Clamp(loadedRespondedContacts, 0, nextContactId);
        expiredResponsesCount = Mathf.Clamp(loadedExpiredResponses, 0, nextContactId);
        consecutiveExpiredResponsesCount = Mathf.Clamp(loadedConsecutiveExpiredResponses, 0, nextContactId);

        // Loaded sessions are reconstructed from aggregate stats, so clear prior contact detail caches.
        contactRecordsById.Clear();
        contactIdOrder.Clear();
    }

    private struct ContactRecord
    {
        public int ContactId;
        public Vector3 Position;
        public float SpawnedAt;

        public bool HasOutcome;
        public UFOSpeciesSegment Segment;
        public UFOIntent Intent;
        public bool QuietCargo;

        public bool HasMuleCargo;
        public MuleCargo MuleCargo;

        public bool HasResponse;
        public UFOResponseChoice Response;
        public float RespondedAt;
    }

    private void Awake()
    {
        softStartBeganAt = Time.realtimeSinceStartup;
        spawnSmugglerOnNextContact = useSmugglerPrefabAsDefaultVisual;
        cuePlayer = GetComponent<ProceduralAudioCuePlayer>();
        if (cuePlayer == null)
        {
            cuePlayer = UnityEngine.Object.FindAnyObjectByType<ProceduralAudioCuePlayer>();
        }

        if (enableAutoSpawn)
        {
            ScheduleNextAutoSpawn(fromNowSeconds: 0f);
        }
    }

    private void OnValidate()
    {
        if (autoSpawnAreaExtentsXZ.x < 0f) autoSpawnAreaExtentsXZ.x = 0f;
        if (autoSpawnAreaExtentsXZ.y < 0f) autoSpawnAreaExtentsXZ.y = 0f;
        worldMapBackgroundDimAlpha = Mathf.Clamp01(worldMapBackgroundDimAlpha);
        ufoVisualStartScaleMultiplier = Mathf.Max(0.1f, ufoVisualStartScaleMultiplier);
        ufoVisualEndScaleMultiplier = Mathf.Max(0.05f, ufoVisualEndScaleMultiplier);
        ufoVisualLifetimeSeconds = Mathf.Max(0.5f, ufoVisualLifetimeSeconds);
        ufoVisualTravelDistance = Mathf.Max(0.1f, ufoVisualTravelDistance);
        ufoViewportMargin = Mathf.Clamp(ufoViewportMargin, 0f, 0.45f);
        ufoStartDepthFromCamera = Mathf.Max(1f, ufoStartDepthFromCamera);
        ufoEndDepthFromCamera = Mathf.Max(ufoStartDepthFromCamera + 0.1f, ufoEndDepthFromCamera);
    }

    private void OnDisable()
    {
        StopAllVisualLifecycleCoroutines();
        RestoreWorldMapBackgroundAlpha();
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
        cuePlayer?.PlayUfoDetected();

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
            triggerKey: $"ufo/spotted/{contactId}",
            channel: BoxChannel.Info,
            severity: BoxSeverity.Info,
            sourceTag: "Radar",
            title: "UFO Spotted",
            body: $"Contact #{contactId}\nUFO sighted at {position}."));

        PublishPapalWarningIfNeeded(queue);

        PublishIncidentAssessment(queue, contactId, position, forcedOutcome: null, forcedQuietCargo: null);

        if (responseChoiceDelaySeconds > 0f)
        {
            StartCoroutine(PublishResponseChoiceAfterDelay(queue, contactId, position, responseChoiceDelaySeconds));
        }
        else
        {
            PublishResponseChoice(queue, contactId, position);
        }

        StartCoroutine(PublishThinking(queue));
    }

    private void TrySpawnUfoVisual(int contactId, Vector3 position)
    {
        if (!spawnUfoVisuals)
        {
            return;
        }

        var visualPrefab = GetVisualPrefabForSpawn();
        if (visualPrefab == null)
        {
            return;
        }

        if (ufoVisualByContactId.TryGetValue(contactId, out var existing) && existing != null)
        {
            Destroy(existing);
        }

        var spawnPos = position + Vector3.up * ufoVisualSpawnYOffset;
        var spawned = Instantiate(visualPrefab, spawnPos, Quaternion.identity);
        spawned.name = $"{visualPrefab.name} (Contact {contactId})";
        PrepareSpawnedVisual(spawned);
        ufoVisualByContactId[contactId] = spawned;
        StartVisualLifecycle(contactId, spawned);
        UpdateWorldMapBackgroundVisibility();
    }

    private void StartVisualLifecycle(int contactId, GameObject ufoVisual)
    {
        StopVisualLifecycle(contactId);

        if (!animateAndExpireUfoVisuals || ufoVisual == null)
        {
            return;
        }

        var routine = StartCoroutine(AnimateAndExpireVisual(contactId, ufoVisual));
        ufoVisualLifecycleCoroutineByContactId[contactId] = routine;
    }

    private void StopVisualLifecycle(int contactId)
    {
        if (!ufoVisualLifecycleCoroutineByContactId.TryGetValue(contactId, out var routine) || routine == null)
        {
            ufoVisualLifecycleCoroutineByContactId.Remove(contactId);
            return;
        }

        StopCoroutine(routine);
        ufoVisualLifecycleCoroutineByContactId.Remove(contactId);
    }

    private void StopAllVisualLifecycleCoroutines()
    {
        foreach (var kvp in ufoVisualLifecycleCoroutineByContactId)
        {
            if (kvp.Value != null)
            {
                StopCoroutine(kvp.Value);
            }
        }

        ufoVisualLifecycleCoroutineByContactId.Clear();
    }

    private IEnumerator AnimateAndExpireVisual(int contactId, GameObject ufoVisual)
    {
        var lifeSeconds = GetUfoVisualLifetimeSeconds();
        if (lifeSeconds <= 0f || ufoVisual == null)
        {
            yield break;
        }

        var startPosition = ufoVisual.transform.position;
        var endPosition = ComputeVisualEndPosition(startPosition);

        if (keepAnimatedUfoInCameraView && TryGetCameraAnchoredTrajectory(out var anchoredStart, out var anchoredEnd))
        {
            startPosition = anchoredStart;
            endPosition = anchoredEnd;
            ufoVisual.transform.position = startPosition;
        }

        var normalizedScale = ufoVisual.transform.localScale;
        var startScale = normalizedScale * ufoVisualStartScaleMultiplier;
        var endScale = normalizedScale * ufoVisualEndScaleMultiplier;
        ufoVisual.transform.localScale = startScale;

        var startTime = Time.realtimeSinceStartup;
        while (ufoVisual != null)
        {
            if (GameMenuController.IsPaused)
            {
                startTime += Time.unscaledDeltaTime;
                yield return null;
                continue;
            }

            var elapsed = Time.realtimeSinceStartup - startTime;
            var t = Mathf.Clamp01(elapsed / lifeSeconds);

            ufoVisual.transform.position = Vector3.Lerp(startPosition, endPosition, t);
            ufoVisual.transform.localScale = Vector3.Lerp(startScale, endScale, t);

            if (t >= 1f)
            {
                break;
            }

            yield return null;
        }

        ufoVisualLifecycleCoroutineByContactId.Remove(contactId);
        RemoveUfoVisual(contactId, 0f);
    }

    private float GetUfoVisualLifetimeSeconds()
    {
        if (useResponseTimeoutForVisualLifetime)
        {
            var timeout = GetEffectiveResponseChoiceTimeoutSeconds();
            if (timeout > 0f)
            {
                return timeout;
            }
        }

        return ufoVisualLifetimeSeconds;
    }

    private Vector3 ComputeVisualEndPosition(Vector3 startPosition)
    {
        var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (cam == null)
        {
            return startPosition + Vector3.forward * ufoVisualTravelDistance;
        }

        var awayFromCamera = startPosition - cam.transform.position;
        if (awayFromCamera.sqrMagnitude < 0.0001f)
        {
            awayFromCamera = cam.transform.forward;
        }

        awayFromCamera.Normalize();
        return startPosition + awayFromCamera * ufoVisualTravelDistance;
    }

    private bool TryGetCameraAnchoredTrajectory(out Vector3 startPosition, out Vector3 endPosition)
    {
        startPosition = default;
        endPosition = default;

        var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (cam == null)
        {
            return false;
        }

        var min = ufoViewportMargin;
        var max = 1f - ufoViewportMargin;
        var x = UnityEngine.Random.Range(min, max);
        var y = UnityEngine.Random.Range(min, max);

        var endX = Mathf.Lerp(x, 0.5f, 0.5f);
        var endY = Mathf.Lerp(y, 0.5f, 0.5f);

        startPosition = cam.ViewportToWorldPoint(new Vector3(x, y, ufoStartDepthFromCamera));
        endPosition = cam.ViewportToWorldPoint(new Vector3(endX, endY, ufoEndDepthFromCamera));
        return true;
    }

    private void PrepareSpawnedVisual(GameObject spawned)
    {
        if (spawned == null)
        {
            return;
        }

        if (!spawned.activeSelf)
        {
            spawned.SetActive(true);
        }

        if (!autoNormalizeUfoVisualSize)
        {
            return;
        }

        var renderers = spawned.GetComponentsInChildren<Renderer>(includeInactive: true);
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning($"[UFO] Spawned visual '{spawned.name}' has no Renderer components. Check model import.");
            return;
        }

        var hasBounds = false;
        var bounds = new Bounds(spawned.transform.position, Vector3.zero);
        foreach (var r in renderers)
        {
            if (r == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        if (!hasBounds)
        {
            return;
        }

        var maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maxDimension <= 0.0001f)
        {
            Debug.LogWarning($"[UFO] Spawned visual '{spawned.name}' has near-zero bounds. Check model scale/import units.");
            return;
        }

        var scaleFactor = targetUfoVisualSize / maxDimension;
        if (Mathf.Abs(scaleFactor - 1f) < 0.01f)
        {
            return;
        }

        spawned.transform.localScale *= scaleFactor;
        DebugLogUfoEvent($"UFO visual normalized: maxDimension={maxDimension:0.###}, scaleFactor={scaleFactor:0.###}, target={targetUfoVisualSize:0.###}");
    }

    private GameObject GetVisualPrefabForSpawn()
    {
        if (alternateBetweenAssignedUfoVisuals && smugglerUfoPrefab != null && ufoPrefab != null)
        {
            var selected = spawnSmugglerOnNextContact ? smugglerUfoPrefab : ufoPrefab;
            spawnSmugglerOnNextContact = !spawnSmugglerOnNextContact;
            return selected;
        }

        if (useSmugglerPrefabAsDefaultVisual && smugglerUfoPrefab != null)
        {
            return smugglerUfoPrefab;
        }

        return ufoPrefab != null ? ufoPrefab : smugglerUfoPrefab;
    }

    private void Update()
    {
        if (GameMenuController.IsPaused)
        {
            HoldPendingResponseWhilePaused();
            return;
        }

        if (pendingResponseShownPaused)
        {
            // Force the countdown text to refresh now that the game is running again.
            pendingResponseShownPaused = false;
            pendingLastCountdownSeconds = -1;
        }

        TickAutoSpawn();
        TryHandleDebugSpawnHotkey();

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
        perSecond *= Difficulty.ActivePreset.SpawnRateMultiplier;

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

    /// <summary>Difficulty scales permit risk increases only; reductions are left as they are.</summary>
    private static float ApplyDifficultyToPermitRiskGain(float delta)
    {
        return delta > 0f ? delta * Difficulty.ActivePreset.PermitRiskGainMultiplier : delta;
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

        return Mathf.Max(1f, responseChoiceTimeoutSeconds * mult * Difficulty.ActivePreset.ResponseTimeoutMultiplier);
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

    private void TryHandleDebugSpawnHotkey()
    {
        if (!debugEnableSpawnHotkey || !Application.isPlaying)
        {
            return;
        }

        var spawnRequested = false;

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.f8Key.wasPressedThisFrame || keyboard.uKey.wasPressedThisFrame))
        {
            spawnRequested = true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!spawnRequested && (Input.GetKeyDown(KeyCode.F8) || Input.GetKeyDown(KeyCode.U)))
        {
            spawnRequested = true;
        }
#endif

        if (!spawnRequested)
        {
            return;
        }

        var spawnPosition = GetDebugSpawnPositionFromCamera();
        Debug.Log($"[UFO][Debug] Hotkey spawn at {spawnPosition}. (F8/U)");
        SpawnUFO(spawnPosition);
    }

    private Vector3 GetDebugSpawnPositionFromCamera()
    {
        var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (cam == null)
        {
            return new Vector3(autoSpawnAreaCenter.x, autoSpawnAreaCenter.y, autoSpawnAreaCenter.z + debugSpawnDistanceFromCamera);
        }

        var forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        var pos = cam.transform.position + forward * debugSpawnDistanceFromCamera;
        return pos;
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

        var applied = ApplyDifficultyToPermitRiskGain(rawDelta * mult);
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

        // Incident assessments can be critical too; explicitly yield focus to response choice.
        queue.DismissIfCurrent($"ufo/incident/{contactId}");

        pendingContactId = contactId;
        pendingContactPosition = position;
        pendingContactBeganAt = Time.realtimeSinceStartup;
        hasPendingResponse = true;

        pendingLastCountdownSeconds = -1;
        pendingResponseShownPaused = false;

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

    private IEnumerator PublishResponseChoiceAfterDelay(BoxMessageQueue queue, int contactId, Vector3 position, float delaySeconds)
    {
        if (delaySeconds > 0f)
        {
            yield return WaitForGameplaySeconds(delaySeconds);
        }

        PublishResponseChoice(queue, contactId, position);
    }

    private void HandleResponseChoice(UFOResponseChoice choice)
    {
        if (!hasPendingResponse)
        {
            return;
        }

        hasPendingResponse = false;
        pendingLastCountdownSeconds = -1;
        pendingResponseShownPaused = false;
        consecutiveExpiredResponsesCount = 0;
        cuePlayer?.PlayResponseAccepted();

        StoreContactResponse(pendingContactId, choice);
        ApplyResponseEffects(pendingContactId, choice);
        // Before ExecuteResponseActions: an Intercept removes the UFO visual, and a spill needs
        // its screen position to place the hazard site marker.
        ApplyMuleCargoConsequences(pendingContactId, choice, expired: false);
        ExecuteResponseActions(pendingContactId, choice, pendingContactPosition);

        var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
        if (queue == null)
        {
            return;
        }

        ClearResponseChoicePrompt(queue, pendingContactId);

        var choiceText = choice == UFOResponseChoice.Dispatch
            ? $"Dispatch ({SquadLoadoutRules.Label(GetSquadLoadout())} kit)"
            : choice.ToString();
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

    /// <summary>
    /// While paused, push the response timer start forward so paused time does not count,
    /// and mark the prompt as paused so the frozen countdown is not mistaken for a hang.
    /// </summary>
    private void HoldPendingResponseWhilePaused()
    {
        if (!enableResponseChoices || !hasPendingResponse)
        {
            return;
        }

        pendingContactBeganAt += Time.unscaledDeltaTime;

        if (pendingResponseShownPaused)
        {
            return;
        }

        pendingResponseShownPaused = true;

        var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
        if (queue == null)
        {
            return;
        }

        var effectiveTimeout = GetEffectiveResponseChoiceTimeoutSeconds();
        int? remaining = effectiveTimeout > 0f
            ? Mathf.Max(0, Mathf.CeilToInt(effectiveTimeout - (Time.realtimeSinceStartup - pendingContactBeganAt)))
            : (int?)null;
        queue.UpdateBody(GetResponseChoiceTriggerKey(pendingContactId), BuildResponseChoiceBody(pendingContactId, remaining, paused: true));
    }

    private static string BuildResponseChoiceBody(int contactId, int? remainingSeconds, bool paused = false)
    {
        var timeoutText = string.Empty;
        if (remainingSeconds.HasValue)
        {
            var total = Mathf.Max(0, remainingSeconds.Value);
            var minutes = total / 60;
            var seconds = total % 60;
            timeoutText = paused
                ? $"\n(PAUSED at {minutes}:{seconds:00}, resume to continue)"
                : $"\n(Expires in {minutes}:{seconds:00})";
        }
        else if (paused)
        {
            timeoutText = "\n(PAUSED, resume to continue)";
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
        var body = BuildResponseChoiceBody(pendingContactId, remaining);
        queue.UpdateBody(key, body);

        // Safety net: if another message took focus, reassert the active response prompt.
        var current = queue.Current;
        if (!current.HasValue || current.Value.TriggerKey != key)
        {
            queue.Publish(new BoxMessage(
                triggerKey: key,
                channel: BoxChannel.Info,
                severity: BoxSeverity.Critical,
                sourceTag: "Operations",
                title: "Response Choice",
                body: body));
        }
    }

    /// <summary>
    /// Drop the open Response Choice without counting it as a miss. Used when the mission ends,
    /// so a frozen prompt is not left on screen behind the mission end panel.
    /// </summary>
    public void CancelPendingResponse()
    {
        if (!hasPendingResponse)
        {
            return;
        }

        hasPendingResponse = false;
        pendingLastCountdownSeconds = -1;
        pendingResponseShownPaused = false;

        var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
        ClearResponseChoicePrompt(queue, pendingContactId);
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
        pendingResponseShownPaused = false;
        expiredResponsesCount++;
        consecutiveExpiredResponsesCount++;
        cuePlayer?.PlayResponseExpired();
        ApplyMuleCargoConsequences(pendingContactId, UFOResponseChoice.Ignore, expired: true);

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
                    RemoveUfoVisual(contactId, interceptDestroyDelaySeconds);
                }
                break;
            }
        }
    }

    private void RemoveUfoVisual(int contactId, float destroyDelaySeconds)
    {
        StopVisualLifecycle(contactId);

        if (!ufoVisualByContactId.TryGetValue(contactId, out var ufo) || ufo == null)
        {
            ufoVisualByContactId.Remove(contactId);
            UpdateWorldMapBackgroundVisibility();
            return;
        }

        ufoVisualByContactId.Remove(contactId);

        if (destroyDelaySeconds > 0f)
        {
            Destroy(ufo, destroyDelaySeconds);
        }
        else
        {
            Destroy(ufo);
        }

        UpdateWorldMapBackgroundVisibility();
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

        if (!record.HasResponse)
        {
            respondedContactsCount++;
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
        var kit = GetSquadLoadout();
        var hostileKitOffset = SquadLoadoutRules.HostileEducationOffset(kit);

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
                    if (kit == SquadLoadout.Recon)
                    {
                        ApplyEducationDelta(+SquadLoadoutRules.ReconEducationGain);
                    }
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
                    if (effectiveEducation >= 35f + hostileKitOffset)
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
                    if (kit == SquadLoadout.Heavy)
                    {
                        // Soldiers with heavy weapons spook a peaceful contact.
                        ApplyPermitRiskDeltaFromResponse(+1f);
                    }
                    else
                    {
                        AdjustEscalationStageForChoice(segment, -1, choice);
                        ApplyPermitRiskDeltaFromResponse(-2.5f);
                        if (kit == SquadLoadout.Recon)
                        {
                            ApplyEducationDelta(+SquadLoadoutRules.ReconEducationGain);
                        }
                    }
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
                    if (effectiveEducation >= 25f + hostileKitOffset)
                    {
                        AdjustEscalationStageForChoice(segment, -1, choice);
                    }
                    else if (SquadLoadoutRules.IsWeakAgainstHostiles(kit))
                    {
                        // A lightly equipped squad that cannot hold its ground emboldens a Dark contact.
                        AdjustEscalationStageForChoice(segment, +1, choice);
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

            if (ufoVisualByContactId.ContainsKey(oldest))
            {
                RemoveUfoVisual(oldest, 0f);
            }
        }
    }

    private void UpdateWorldMapBackgroundVisibility()
    {
        if (!dimWorldMapBackgroundWhileUfoVisible)
        {
            RestoreWorldMapBackgroundAlpha();
            return;
        }

        if (!TryGetWorldMapBackgroundImage(out var image) || image == null)
        {
            return;
        }

        var hasActiveVisuals = false;
        foreach (var kvp in ufoVisualByContactId)
        {
            if (kvp.Value != null)
            {
                hasActiveVisuals = true;
                break;
            }
        }

        if (hasActiveVisuals)
        {
            if (!hasCachedBackgroundAlpha)
            {
                cachedBackgroundAlpha = image.color.a;
                hasCachedBackgroundAlpha = true;
            }

            var color = image.color;
            color.a = worldMapBackgroundDimAlpha;
            image.color = color;
            return;
        }

        RestoreWorldMapBackgroundAlpha();
    }

    private bool TryGetWorldMapBackgroundImage(out Image image)
    {
        image = worldMapBackgroundImage;
        if (image != null)
        {
            return true;
        }

        // During disable/teardown Unity can assert on scene-wide Find calls.
        if (!isActiveAndEnabled || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
        {
            return false;
        }

        var background = GameObject.Find("WorldMapBackground");
        if (background == null)
        {
            return false;
        }

        image = background.GetComponent<Image>();
        worldMapBackgroundImage = image;
        return image != null;
    }

    private void RestoreWorldMapBackgroundAlpha()
    {
        if (!hasCachedBackgroundAlpha)
        {
            return;
        }

        // OnDisable can run while scene objects are deactivating; avoid Find() there.
        var image = worldMapBackgroundImage;
        if (image == null)
        {
            hasCachedBackgroundAlpha = false;
            return;
        }

        var color = image.color;
        color.a = cachedBackgroundAlpha;
        image.color = color;
        hasCachedBackgroundAlpha = false;
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
        MuleCargo? muleCargo = null;
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
                        else if (enableMuleCargo)
                        {
                            var cargoType = debugForcedMuleCargo ?? MuleCargoRules.Roll(
                                UnityEngine.Random.value,
                                muleWeightContraband,
                                muleWeightHeavyMetals,
                                muleWeightDrugPrecursors,
                                muleWeightNuclearWarhead * GetNetworkWarheadWeightMultiplier());
                            debugForcedMuleCargo = null;
                            muleCargo = new MuleCargo { Type = cargoType, Revealed = false };

                            severity = BoxSeverity.Warn;
                            title = "Mule Cargo Craft";
                            body = "Low-tech cargo craft suspected. No crew, no life support: goods only.\n" +
                                   BuildMuleCargoIntelLine(muleCargo.Value) + "\n\n" +
                                   "Track = scan the hold.  Dispatch = recover it intact.\n" +
                                   "Intercept = shoot it down (risk of spill).  Ignore = it gets delivered.";
                            publishEducationBulletin = true;
                            educationTitle = "Education Bulletin";
                            educationBody = "Public advisory: if a cargo craft comes down, stay upwind and keep away. Unknown cargo may be toxic or radioactive.";
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
            if (muleCargo.HasValue)
            {
                StoreMuleCargo(contactId, muleCargo.Value);
            }

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
            var bulletinTitle = educationTitle ?? "Education Bulletin";
            var bulletinBody = $"Contact #{contactId}\n\n{educationBody}\n\nEscalation: {outcome.Segment} {bulletinStage}/3 ({StageLabel(bulletinStage)})\n\nMeters: Education {Mathf.RoundToInt(education)}  |  PermitRisk {Mathf.RoundToInt(permitRisk)}{(debugShowPriorityEffectsInBoxes ? $"\n\n{BuildPriorityEffectsReadout()}" : string.Empty)}";

            // Avoid stealing focus during the response handoff window.
            if (enableResponseChoices)
            {
                var delay = Mathf.Max(0f, responseChoiceDelaySeconds) + 0.35f;
                StartCoroutine(PublishEducationBulletinAfterDelay(queue, contactId, bulletinTitle, bulletinBody, delay));
            }
            else
            {
                queue.Publish(new BoxMessage(
                    triggerKey: $"education/bulletin/ufo/{contactId}",
                    channel: BoxChannel.Info,
                    severity: BoxSeverity.Info,
                    sourceTag: "Education",
                    title: bulletinTitle,
                    body: bulletinBody));
            }
        }
    }

    private IEnumerator PublishEducationBulletinAfterDelay(BoxMessageQueue queue, int contactId, string title, string body, float delaySeconds)
    {
        if (queue == null)
        {
            yield break;
        }

        if (delaySeconds > 0f)
        {
            yield return WaitForGameplaySeconds(delaySeconds);
        }

        queue.Publish(new BoxMessage(
            triggerKey: $"education/bulletin/ufo/{contactId}",
            channel: BoxChannel.Info,
            severity: BoxSeverity.Info,
            sourceTag: "Education",
            title: title,
            body: body));
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
        permitDelta = ApplyDifficultyToPermitRiskGain(permitDelta);
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
            yield return WaitForGameplaySeconds(thinkingDelaySeconds);
        }

        queue.Publish(new BoxMessage(
            triggerKey: "thinking/pick_priority",
            channel: BoxChannel.Thinking,
            severity: BoxSeverity.Info,
            sourceTag: "Commander",
            title: "Pick a Priority",
            body: "Strategic Priority:\n1) Coverage\n2) Research\n3) Response\n\nTip: Use 1-4 when a contact response prompt is active."));
    }

    private IEnumerator WaitForGameplaySeconds(float seconds)
    {
        var remaining = Mathf.Max(0f, seconds);
        while (remaining > 0f)
        {
            if (!GameMenuController.IsPaused)
            {
                remaining -= Time.unscaledDeltaTime;
            }

            yield return null;
        }
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

    // ---------------------------------------------------------------------
    // Mule cargo (see Design_ExodusCraft.md, "Gen 0b: Mule")
    // ---------------------------------------------------------------------

    private MuleCargoType? debugForcedMuleCargo;

    public int MuleCargoSpills { get; private set; }
    public int WarheadsSeized { get; private set; }
    public int WarheadsDelivered { get; private set; }

    /// <summary>Restore Mule totals from a save. Called after <see cref="ApplyPersistentState"/>.</summary>
    public void ApplyMuleCargoState(int loadedSpills, int loadedWarheadsSeized, int loadedWarheadsDelivered)
    {
        MuleCargoSpills = Mathf.Max(0, loadedSpills);
        WarheadsSeized = Mathf.Max(0, loadedWarheadsSeized);
        WarheadsDelivered = Mathf.Max(0, loadedWarheadsDelivered);
    }

    private void StoreMuleCargo(int contactId, MuleCargo cargo)
    {
        if (!contactRecordsById.TryGetValue(contactId, out var record))
        {
            return;
        }

        record.HasMuleCargo = true;
        record.MuleCargo = cargo;
        contactRecordsById[contactId] = record;
    }

    private void ApplyMuleCargoConsequences(int contactId, UFOResponseChoice choice, bool expired)
    {
        if (!contactRecordsById.TryGetValue(contactId, out var record) || !record.HasMuleCargo)
        {
            return;
        }

        var cargo = record.MuleCargo;
        var name = MuleCargoRules.DisplayName(cargo.Type);
        var hazard = MuleCargoRules.HazardLabel(cargo.Hazard);
        var isWarhead = cargo.Type == MuleCargoType.NuclearWarhead;

        var severity = BoxSeverity.Info;
        string title;
        string body;
        MuleOutcome outcome;

        switch (choice)
        {
            case UFOResponseChoice.Track:
                outcome = MuleOutcome.Scanned;
                cargo.Revealed = true;
                ApplyEducationDelta(+0.5f);
                severity = cargo.Hazard == CargoHazard.None ? BoxSeverity.Info : BoxSeverity.Warn;
                if (isWarhead) severity = BoxSeverity.Critical;
                title = "Cargo Scan";
                body = $"Scan of the hold is complete.\nCargo: {name}\nHazard: {hazard}" +
                       (isWarhead ? "\n\nA nuclear warhead is in the air. Recovery should be the top priority." : string.Empty);
                break;

            case UFOResponseChoice.Dispatch:
            {
                cargo.Revealed = true;
                var kit = GetSquadLoadout();

                // Heavy weapons can rupture a hazardous hold during the assault.
                if (kit == SquadLoadout.Heavy
                    && cargo.Hazard != CargoHazard.None
                    && UnityEngine.Random.value < MuleCargoRules.SpillChanceOnIntercept(cargo.Type) * SquadLoadoutRules.HeavyRuptureShareOfSpillChance)
                {
                    outcome = MuleOutcome.Spilled;
                    MuleCargoSpills++;
                    AdjustEscalationStage(UFOSpeciesSegment.Human, +1);
                    ApplyEducationDelta(-1f);
                    severity = BoxSeverity.Critical;
                    title = isWarhead ? "Warhead Casing Breached" : "Hold Ruptured";
                    OpenHazardSite(contactId, cargo.Hazard);
                    body = $"The squad's heavy weapons tore open the hold during the assault.\nCargo: {name}\nHazard: {hazard}\n\n" +
                           "A hazard site is now open on the map. Press H to send the hazmat team.\n" +
                           "A HAZMAT kit (refit with L) recovers hazardous cargo safely.";
                    break;
                }

                outcome = MuleOutcome.Recovered;
                ApplyEducationDelta(+0.5f);

                var exposureText = string.Empty;
                if (UnityEngine.Random.value < SquadLoadoutRules.ExposureChance(kit, cargo.Hazard))
                {
                    var squad = GetSquadManager();
                    if (squad != null)
                    {
                        squad.RegisterExposure();
                    }

                    AddPermitRiskFromHazard(SquadLoadoutRules.ExposurePermitRisk(cargo.Hazard));
                    exposureText = $"\n\nSquad exposed: two soldiers without sealed suits were treated for {hazard.ToLowerInvariant()} exposure. " +
                                   "The press is asking questions. A HAZMAT kit (refit with L) prevents this.";
                }
                else if (kit == SquadLoadout.Hazmat && cargo.Hazard != CargoHazard.None)
                {
                    exposureText = "\n\nSealed suits kept the squad safe.";
                }

                if (isWarhead)
                {
                    WarheadsSeized++;
                    AdjustEscalationStage(UFOSpeciesSegment.Human, -1);
                    ApplyEducationDelta(+1.5f);
                    severity = BoxSeverity.Critical;
                    title = "Warhead Seized";
                    body = "The recovery team secured a nuclear warhead from the Mule's hold, intact.\n" +
                           "The smuggling network has lost its most dangerous shipment.";
                }
                else
                {
                    title = "Cargo Recovered";
                    body = $"The recovery team secured the hold intact.\nCargo: {name}\nHazard: {hazard}\n\nEvidence logged. The network will feel this.";
                }

                body += exposureText;
                break;
            }

            case UFOResponseChoice.Intercept:
            {
                cargo.Revealed = true;
                var spilled = UnityEngine.Random.value < MuleCargoRules.SpillChanceOnIntercept(cargo.Type);
                outcome = spilled ? MuleOutcome.Spilled : MuleOutcome.DownedIntact;
                if (spilled)
                {
                    MuleCargoSpills++;
                    AdjustEscalationStage(UFOSpeciesSegment.Human, +1);
                    ApplyEducationDelta(-1f);
                    severity = BoxSeverity.Critical;
                    title = isWarhead ? "Radioactive Breach" : "Hazardous Spill";
                    OpenHazardSite(contactId, cargo.Hazard);
                    body = $"The Mule broke apart on impact and the hold split open.\nCargo: {name}\nHazard: {hazard}\n\n" +
                           (isWarhead
                               ? "The warhead casing is breached. The crash zone is contaminated. Evacuate and send hazmat teams."
                               : "Contamination around the crash site. Keep civilians away and send hazmat teams.") +
                           "\n\nA hazard site is now open on the map. Press H to send the hazmat team.";
                }
                else
                {
                    if (isWarhead)
                    {
                        WarheadsSeized++;
                    }

                    severity = isWarhead ? BoxSeverity.Warn : BoxSeverity.Info;
                    title = "Mule Down";
                    body = $"The Mule is down and the hold held together.\nCargo: {name}\nHazard: {hazard}" +
                           (isWarhead ? "\n\nThe warhead survived the crash intact. That was lucky." : string.Empty);
                }
                break;
            }

            default: // Ignore, or the response timer ran out.
                outcome = MuleOutcome.Delivered;
                if (isWarhead)
                {
                    WarheadsDelivered++;
                    AdjustEscalationStage(UFOSpeciesSegment.Human, +1);
                    severity = BoxSeverity.Critical;
                }
                else if (cargo.Hazard != CargoHazard.None)
                {
                    severity = BoxSeverity.Warn;
                }

                title = "Cargo Delivered";
                body = (expired ? "No one responded in time. " : "The Mule was left alone. ") +
                       $"It delivered its cargo.\n\nIntelligence later confirms:\nCargo: {name}\nHazard: {hazard}" +
                       (isWarhead ? "\n\nA nuclear warhead is now in criminal hands." : string.Empty);
                cargo.Revealed = true;
                break;
        }

        record.MuleCargo = cargo;
        contactRecordsById[contactId] = record;

        var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
        if (queue != null)
        {
            queue.Publish(new BoxMessage(
                triggerKey: $"ufo/cargo/{contactId}",
                channel: BoxChannel.Info,
                severity: severity,
                sourceTag: "Smuggling",
                title: title,
                body: $"Contact #{contactId}\n\n{body}\n\nTotals: Spills {MuleCargoSpills}  |  Warheads seized {WarheadsSeized}  |  Warheads delivered {WarheadsDelivered}"));
        }

        // After the cargo message, so the lead and any warhead event follow it.
        var network = SmugglingNetwork.Instance != null ? SmugglingNetwork.Instance : GetComponent<SmugglingNetwork>();
        if (network != null)
        {
            network.OnMuleOutcome(contactId, cargo, outcome);
            if (outcome == MuleOutcome.Recovered && GetSquadLoadout() == SquadLoadout.Recon)
            {
                network.AddBonusLeads(SquadLoadoutRules.ReconExtraLeads, $"Contact #{contactId}: the recon squad photographed the hold's shipping marks");
            }
        }
    }

    private float GetNetworkWarheadWeightMultiplier()
    {
        var network = SmugglingNetwork.Instance != null ? SmugglingNetwork.Instance : GetComponent<SmugglingNetwork>();
        return network != null ? network.WarheadWeightMultiplier : 1f;
    }

    /// <summary>What the Mule alert says about the cargo, depending on NIA's network intel.</summary>
    private string BuildMuleCargoIntelLine(MuleCargo cargo)
    {
        var network = SmugglingNetwork.Instance != null ? SmugglingNetwork.Instance : GetComponent<SmugglingNetwork>();
        if (network != null && network.InformantActive)
        {
            return $"Cargo (informant): {MuleCargoRules.DisplayName(cargo.Type)}. Hazard: {MuleCargoRules.HazardLabel(cargo.Hazard)}.\n" +
                   BuildSquadKitTip(cargo.Hazard, hazardKnown: true);
        }

        if (network != null && network.CourierRouteKnown)
        {
            return $"Cargo: UNKNOWN. Hazard class (courier route intel): {MuleCargoRules.HazardLabel(cargo.Hazard)}.\n" +
                   BuildSquadKitTip(cargo.Hazard, hazardKnown: true);
        }

        return "Cargo: UNKNOWN. Could be harmless, toxic, or worse.\n" + BuildSquadKitTip(cargo.Hazard, hazardKnown: false);
    }

    /// <summary>Which kit suits a Dispatch on this Mule, plus the kit the squad carries now.</summary>
    private string BuildSquadKitTip(CargoHazard hazard, bool hazardKnown)
    {
        var current = $"Squad kit now: {SquadLoadoutRules.Label(GetSquadLoadout())} [L].";
        if (!hazardKnown)
        {
            return $"Tip: a HAZMAT kit recovers any cargo safely. {current}";
        }

        return hazard == CargoHazard.None
            ? $"Tip: any kit is safe; RECON finds an extra network lead. {current}"
            : $"Tip: HAZMAT kit for a safe recovery; HEAVY may rupture the hold. {current}";
    }

    private SquadManager GetSquadManager()
    {
        return SquadManager.Instance != null ? SquadManager.Instance : GetComponent<SquadManager>();
    }

    private SquadLoadout GetSquadLoadout()
    {
        var squad = GetSquadManager();
        return squad != null ? squad.Loadout : SquadLoadout.Standard;
    }

    /// <summary>Raise or lower the human smuggler escalation stage (used by network story events).</summary>
    public void AdjustHumanEscalation(int delta)
    {
        AdjustEscalationStage(UFOSpeciesSegment.Human, delta);
    }

    private void OpenHazardSite(int contactId, CargoHazard hazard)
    {
        var sites = HazardSiteManager.Instance != null ? HazardSiteManager.Instance : GetComponent<HazardSiteManager>();
        if (sites == null)
        {
            return;
        }

        sites.CreateSite(contactId, hazard, GetContactViewportPosition(contactId));
    }

    /// <summary>Where the contact's UFO is on screen (0..1), or a point near the middle if it has no visual.</summary>
    private Vector2 GetContactViewportPosition(int contactId)
    {
        var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (cam != null && ufoVisualByContactId.TryGetValue(contactId, out var visual) && visual != null)
        {
            var vp = cam.WorldToViewportPoint(visual.transform.position);
            if (vp.z > 0f)
            {
                return new Vector2(vp.x, vp.y);
            }
        }

        return new Vector2(UnityEngine.Random.Range(0.3f, 0.7f), UnityEngine.Random.Range(0.35f, 0.65f));
    }

    /// <summary>Permit risk from open hazard sites. Increases are scaled by difficulty.</summary>
    public void AddPermitRiskFromHazard(float delta)
    {
        permitRisk = Mathf.Clamp(permitRisk + ApplyDifficultyToPermitRiskGain(delta), 0f, 100f);
    }

    public void AddEducationFromHazard(float delta)
    {
        ApplyEducationDelta(delta);
    }

    [ContextMenu("Debug/Incident: Mule (Random Cargo)")]
    private void DebugIncidentMuleRandom() => DebugIncidentMule(null);

    [ContextMenu("Debug/Incident: Mule (Nuclear Warhead)")]
    private void DebugIncidentMuleWarhead() => DebugIncidentMule(MuleCargoType.NuclearWarhead);

    private void DebugIncidentMule(MuleCargoType? forcedCargo)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[UFO] Enter Play Mode to run the debug action.");
            return;
        }

        if (!enableMuleCargo)
        {
            Debug.LogWarning("[UFO] Mule cargo is disabled on this UFOManager.");
            return;
        }

        var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
        if (queue == null)
        {
            Debug.LogWarning("[UFO] BoxMessageQueue not found; cannot publish debug incident.");
            return;
        }

        var position = new Vector3(10f, 0f, -4f);
        var contactId = RegisterContact(position);
        TrySpawnUfoVisual(contactId, position);
        queue.Publish(new BoxMessage(
            triggerKey: $"ufo/spotted/{contactId}",
            channel: BoxChannel.Info,
            severity: BoxSeverity.Warn,
            sourceTag: "Radar",
            title: "UFO Spotted",
            body: $"Contact #{contactId}\nUFO sighted at {position}."));

        debugForcedMuleCargo = forcedCargo;
        PublishIncidentAssessment(queue, contactId, position, forcedOutcome: (UFOSpeciesSegment.Human, UFOIntent.Smuggle), forcedQuietCargo: false);
        PublishResponseChoice(queue, contactId, position);
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
