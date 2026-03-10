using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class WorldMapBackgroundManager : MonoBehaviour
{
    private enum GuideLineOrientation
    {
        Horizontal,
        Vertical,
    }

    private const int DefaultMapIndex = 0;
    private const string BackgroundObjectName = "WorldMapBackground";
    private const string DropdownObjectName = "Map Style Selector";
    private const string ResetButtonObjectName = "MapSelectorButton";
    private const string PausePlayButtonObjectName = "PausePlayButton";
    private const string VolumeButtonObjectName = "VolumeButton";
    private const string MapStylePrefKey = "xcon.worldmap.styleIndex";

    [Header("World Map Sprites")]
    [SerializeField] private Sprite roundSphereMap;
    [SerializeField] private Sprite flatOvalMap;
    [SerializeField] private Sprite flatMap;

    [Header("UI Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Dropdown mapDropdown;
    [SerializeField] private Button resetMapStyleButton;
    [SerializeField] private Button pausePlayButton;
    [SerializeField] private Button volumeButton;

    [Header("Transitions")]
    [SerializeField] private bool enableFadeTransitions = true;
    [Min(0f)]
    [SerializeField] private float fadeDurationSeconds = 0.225f;

    [Header("Layout")]
    [Tooltip("If enabled, forces Image.preserveAspect on the background image.")]
    [SerializeField] private bool forcePreserveAspect = true;

    [Tooltip("If enabled, forces the background Image type to Simple to avoid sprite slicing distortion.")]
    [SerializeField] private bool forceSimpleImageType = true;

    [Tooltip("If enabled, adds/uses AspectRatioFitter and drives it from the active sprite aspect ratio.")]
    [SerializeField] private bool useAspectRatioFitter = true;

    [SerializeField] private AspectRatioFitter.AspectMode aspectMode = AspectRatioFitter.AspectMode.FitInParent;

    [Header("Coverage")]
    [Tooltip("If enabled, map backgrounds are normalized to fill the viewport similarly across map styles (may crop edges).")]
    [SerializeField] private bool normalizeMapCoverage = true;

    [Tooltip("Uniform zoom applied after normalization. Increase slightly if maps still look too small.")]
    [Range(0.8f, 1.4f)]
    [SerializeField] private float normalizedCoverageZoom = 1.02f;

    [Header("Guide Line")]
    [SerializeField] private bool showCenterGuideLine = true;
    [SerializeField] private GuideLineOrientation guideLineOrientation = GuideLineOrientation.Horizontal;
    [Range(0f, 40f)]
    [SerializeField] private float guideLineCurvature = 10f;
    [Range(1f, 12f)]
    [SerializeField] private float guideLineThickness = 2f;
    [Range(0.2f, 1f)]
    [SerializeField] private float guideLineLengthNormalized = 0.92f;
    [SerializeField] private Color guideLineColor = new Color(0.9f, 0.95f, 1f, 0.55f);
    [SerializeField] private Color guideTopTint = new Color(1f, 1f, 1f, 0.12f);
    [SerializeField] private Color guideBottomTint = new Color(0f, 0f, 0f, 0.16f);

    private Dictionary<int, Sprite> mapOptions;
    private Coroutine fadeTransitionCoroutine;
    private int currentMapIndex = -1;
    private AspectRatioFitter aspectRatioFitter;
    private Image centerGuideLineImage;
    private Sprite centerGuideLineSprite;

    void Awake()
    {
        ResolveReferences();
        EnsurePausePlayButtonExists();
        EnsureVolumeButtonExists();
        ApplyLayoutSettings();
        RefreshCenterGuideLine();

        mapOptions = new Dictionary<int, Sprite>
        {
            { 0, roundSphereMap },
            { 1, flatOvalMap },
            { 2, flatMap }
        };

        if (mapDropdown != null)
        {
            mapDropdown.options.Clear();
            mapDropdown.options.Add(new Dropdown.OptionData("Round Sphere"));
            mapDropdown.options.Add(new Dropdown.OptionData("Flat Oval"));
            mapDropdown.options.Add(new Dropdown.OptionData("Flat"));

            var startupIndex = GetStartupMapIndex();
            // Keep the visible selection in sync with the selected startup background.
            mapDropdown.SetValueWithoutNotify(startupIndex);
            mapDropdown.onValueChanged.AddListener(OnMapDropdownChanged);

            SetMapBackground(startupIndex, immediate: true);
        }
        else
        {
            SetMapBackground(GetStartupMapIndex(), immediate: true);
        }

        if (resetMapStyleButton != null)
        {
            resetMapStyleButton.onClick.AddListener(ResetMapStyleToDefault);
        }

        if (pausePlayButton != null)
        {
            pausePlayButton.onClick.AddListener(TogglePausePlay);
            RefreshPausePlayButtonLabel();
        }

        if (volumeButton != null)
        {
            volumeButton.onClick.AddListener(CycleSfxVolume);
            RefreshVolumeButtonLabel();
        }

        GameMenuController.PauseStateChanged += HandlePauseStateChanged;
    }

    private void OnDestroy()
    {
        if (fadeTransitionCoroutine != null)
        {
            StopCoroutine(fadeTransitionCoroutine);
            fadeTransitionCoroutine = null;
        }

        if (mapDropdown != null)
        {
            mapDropdown.onValueChanged.RemoveListener(OnMapDropdownChanged);
        }

        if (resetMapStyleButton != null)
        {
            resetMapStyleButton.onClick.RemoveListener(ResetMapStyleToDefault);
        }

        if (pausePlayButton != null)
        {
            pausePlayButton.onClick.RemoveListener(TogglePausePlay);
        }

        if (volumeButton != null)
        {
            volumeButton.onClick.RemoveListener(CycleSfxVolume);
        }

        GameMenuController.PauseStateChanged -= HandlePauseStateChanged;

        if (centerGuideLineSprite != null)
        {
            Destroy(centerGuideLineSprite);
            centerGuideLineSprite = null;
        }
    }

    private void HandlePauseStateChanged(bool _)
    {
        RefreshPausePlayButtonLabel();
    }

    private void OnValidate()
    {
        normalizedCoverageZoom = Mathf.Clamp(normalizedCoverageZoom, 0.8f, 1.4f);
        guideLineCurvature = Mathf.Clamp(guideLineCurvature, 0f, 40f);
        guideLineThickness = Mathf.Clamp(guideLineThickness, 1f, 12f);
        guideLineLengthNormalized = Mathf.Clamp(guideLineLengthNormalized, 0.2f, 1f);
        ApplyLayoutSettings(allowRectTransformWrites: false);
    }

    private void OnMapDropdownChanged(int index)
    {
        SaveSelectedMapIndex(index);
        SetMapBackground(index, immediate: false);
    }

    public void ResetMapStyleToDefault()
    {
        PlayerPrefs.DeleteKey(MapStylePrefKey);
        PlayerPrefs.Save();

        currentMapIndex = -1;

        if (mapDropdown != null)
        {
            mapDropdown.SetValueWithoutNotify(DefaultMapIndex);
        }

        SetMapBackground(DefaultMapIndex, immediate: true);
    }

    private void SetMapBackground(int index, bool immediate)
    {
        if (backgroundImage == null)
        {
            Debug.LogWarning("[WorldMapBackgroundManager] Missing Background Image reference.");
            return;
        }

        if (mapOptions == null || !mapOptions.TryGetValue(index, out var selectedSprite) || selectedSprite == null)
        {
            selectedSprite = GetFirstAvailableSprite();
            if (selectedSprite == null)
            {
                Debug.LogWarning("[WorldMapBackgroundManager] No map sprites assigned. Set roundSphereMap/flatOvalMap/flatMap in the inspector.");
                return;
            }
        }

        if (index == currentMapIndex)
        {
            return;
        }

        currentMapIndex = index;

        var shouldFade = enableFadeTransitions && !immediate && fadeDurationSeconds > 0f && isActiveAndEnabled;
        if (!shouldFade)
        {
            ApplySprite(selectedSprite);
            return;
        }

        if (fadeTransitionCoroutine != null)
        {
            StopCoroutine(fadeTransitionCoroutine);
        }

        fadeTransitionCoroutine = StartCoroutine(FadeToSprite(selectedSprite));
    }

    private void ApplyLayoutSettings(bool allowRectTransformWrites = true)
    {
        if (backgroundImage == null)
        {
            return;
        }

        // Avoid applying aspect fit twice (Image + AspectRatioFitter), which can cause excessive shrinking.
        backgroundImage.preserveAspect = useAspectRatioFitter ? false : forcePreserveAspect;

        if (forceSimpleImageType)
        {
            backgroundImage.type = Image.Type.Simple;
        }

        if (useAspectRatioFitter)
        {
            aspectRatioFitter = backgroundImage.GetComponent<AspectRatioFitter>();
            if (aspectRatioFitter == null)
            {
                aspectRatioFitter = backgroundImage.gameObject.AddComponent<AspectRatioFitter>();
            }

            aspectRatioFitter.aspectMode = normalizeMapCoverage
                ? AspectRatioFitter.AspectMode.EnvelopeParent
                : aspectMode;
        }
        else
        {
            aspectRatioFitter = backgroundImage.GetComponent<AspectRatioFitter>();
            if (aspectRatioFitter != null)
            {
                aspectRatioFitter.enabled = false;
            }
        }

        if (allowRectTransformWrites)
        {
            ApplyCoverageNormalization();
        }
    }

    private void ResolveReferences()
    {
        if (backgroundImage == null)
        {
            var backgroundObject = GameObject.Find(BackgroundObjectName);
            if (backgroundObject != null)
            {
                backgroundImage = backgroundObject.GetComponent<Image>();
            }
        }

        if (mapDropdown == null)
        {
            var dropdownObject = GameObject.Find(DropdownObjectName);
            if (dropdownObject != null)
            {
                mapDropdown = dropdownObject.GetComponent<Dropdown>();
            }
        }

        if (resetMapStyleButton == null)
        {
            var resetButtonObject = GameObject.Find(ResetButtonObjectName);
            if (resetButtonObject != null)
            {
                resetMapStyleButton = resetButtonObject.GetComponent<Button>();
            }
        }

        if (pausePlayButton == null)
        {
            var pauseButtonObject = GameObject.Find(PausePlayButtonObjectName);
            if (pauseButtonObject != null)
            {
                pausePlayButton = pauseButtonObject.GetComponent<Button>();
            }
        }

        if (volumeButton == null)
        {
            var volumeButtonObject = GameObject.Find(VolumeButtonObjectName);
            if (volumeButtonObject != null)
            {
                volumeButton = volumeButtonObject.GetComponent<Button>();
            }
        }
    }

    private void EnsurePausePlayButtonExists()
    {
        if (pausePlayButton != null || resetMapStyleButton == null)
        {
            return;
        }

        var template = resetMapStyleButton.gameObject;
        var clone = Instantiate(template, template.transform.parent);
        clone.name = PausePlayButtonObjectName;

        if (clone.TryGetComponent<RectTransform>(out var cloneRect)
            && template.TryGetComponent<RectTransform>(out var templateRect))
        {
            cloneRect.anchorMin = templateRect.anchorMin;
            cloneRect.anchorMax = templateRect.anchorMax;
            cloneRect.pivot = templateRect.pivot;
            cloneRect.sizeDelta = templateRect.sizeDelta;
            cloneRect.anchoredPosition = templateRect.anchoredPosition + new Vector2(0f, 38f);
        }

        pausePlayButton = clone.GetComponent<Button>();
        RefreshPausePlayButtonLabel();
    }

    private void EnsureVolumeButtonExists()
    {
        if (volumeButton != null)
        {
            return;
        }

        var templateButton = pausePlayButton != null ? pausePlayButton : resetMapStyleButton;
        if (templateButton == null)
        {
            return;
        }

        var template = templateButton.gameObject;
        var clone = Instantiate(template, template.transform.parent);
        clone.name = VolumeButtonObjectName;

        if (clone.TryGetComponent<RectTransform>(out var cloneRect)
            && template.TryGetComponent<RectTransform>(out var templateRect))
        {
            cloneRect.anchorMin = templateRect.anchorMin;
            cloneRect.anchorMax = templateRect.anchorMax;
            cloneRect.pivot = templateRect.pivot;
            cloneRect.sizeDelta = templateRect.sizeDelta;
            cloneRect.anchoredPosition = templateRect.anchoredPosition + new Vector2(0f, 38f);
        }

        volumeButton = clone.GetComponent<Button>();
        RefreshVolumeButtonLabel();
    }

    private void TogglePausePlay()
    {
        GameMenuController.TogglePause();
        RefreshPausePlayButtonLabel();
    }

    private void RefreshPausePlayButtonLabel()
    {
        if (pausePlayButton == null)
        {
            return;
        }

        var isPaused = GameMenuController.IsPaused;
        var label = isPaused ? "Play" : "Pause";

        var tmp = pausePlayButton.GetComponentInChildren<TMP_Text>(includeInactive: true);
        if (tmp != null)
        {
            tmp.text = label;
            return;
        }

        var legacyText = pausePlayButton.GetComponentInChildren<Text>(includeInactive: true);
        if (legacyText != null)
        {
            legacyText.text = label;
        }
    }

    private void CycleSfxVolume()
    {
        var cuePlayer = UnityEngine.Object.FindAnyObjectByType<ProceduralAudioCuePlayer>();
        var current = cuePlayer != null ? cuePlayer.GetSfxVolume() : PlayerPrefs.GetFloat("xcon.audio.sfxVolume", 0.22f);

        var next = 1f;
        if (current > 0.75f)
        {
            next = 0f;
        }
        else if (current > 0.5f)
        {
            next = 0.75f;
        }
        else if (current > 0.25f)
        {
            next = 0.5f;
        }
        else if (current > 0.01f)
        {
            next = 0.25f;
        }

        if (cuePlayer != null)
        {
            cuePlayer.SetSfxVolume(next, persist: true);
        }
        else
        {
            PlayerPrefs.SetFloat("xcon.audio.sfxVolume", Mathf.Clamp01(next));
            PlayerPrefs.Save();
        }

        RefreshVolumeButtonLabel();
    }

    private void RefreshVolumeButtonLabel()
    {
        if (volumeButton == null)
        {
            return;
        }

        var cuePlayer = UnityEngine.Object.FindAnyObjectByType<ProceduralAudioCuePlayer>();
        var volume = cuePlayer != null ? cuePlayer.GetSfxVolume() : PlayerPrefs.GetFloat("xcon.audio.sfxVolume", 0.22f);
        var label = $"Vol {Mathf.RoundToInt(Mathf.Clamp01(volume) * 100f)}%";

        var tmp = volumeButton.GetComponentInChildren<TMP_Text>(includeInactive: true);
        if (tmp != null)
        {
            tmp.text = label;
            return;
        }

        var legacyText = volumeButton.GetComponentInChildren<Text>(includeInactive: true);
        if (legacyText != null)
        {
            legacyText.text = label;
        }
    }

    private Sprite GetFirstAvailableSprite()
    {
        if (roundSphereMap != null)
        {
            return roundSphereMap;
        }

        if (flatOvalMap != null)
        {
            return flatOvalMap;
        }

        if (flatMap != null)
        {
            return flatMap;
        }

        return null;
    }

    private int GetStartupMapIndex()
    {
        var savedIndex = PlayerPrefs.GetInt(MapStylePrefKey, DefaultMapIndex);
        if (TryGetSpriteForIndex(savedIndex, out _))
        {
            return savedIndex;
        }

        if (TryGetFirstAvailableIndex(out var firstAvailable))
        {
            return firstAvailable;
        }

        return DefaultMapIndex;
    }

    private void SaveSelectedMapIndex(int index)
    {
        if (!TryGetSpriteForIndex(index, out _))
        {
            return;
        }

        PlayerPrefs.SetInt(MapStylePrefKey, index);
        PlayerPrefs.Save();
    }

    private bool TryGetSpriteForIndex(int index, out Sprite sprite)
    {
        sprite = null;
        if (mapOptions == null)
        {
            return false;
        }

        return mapOptions.TryGetValue(index, out sprite) && sprite != null;
    }

    private bool TryGetFirstAvailableIndex(out int index)
    {
        index = DefaultMapIndex;
        if (TryGetSpriteForIndex(0, out _))
        {
            index = 0;
            return true;
        }

        if (TryGetSpriteForIndex(1, out _))
        {
            index = 1;
            return true;
        }

        if (TryGetSpriteForIndex(2, out _))
        {
            index = 2;
            return true;
        }

        return false;
    }

    private void ApplySprite(Sprite sprite)
    {
        backgroundImage.sprite = sprite;
        UpdateAspectFromSprite(sprite);

        var color = backgroundImage.color;
        color.a = 1f;
        backgroundImage.color = color;
    }

    private void UpdateAspectFromSprite(Sprite sprite)
    {
        if (!useAspectRatioFitter || aspectRatioFitter == null || sprite == null)
        {
            return;
        }

        var rect = sprite.rect;
        if (rect.height <= 0f)
        {
            return;
        }

        aspectRatioFitter.enabled = true;
        aspectRatioFitter.aspectMode = normalizeMapCoverage
            ? AspectRatioFitter.AspectMode.EnvelopeParent
            : aspectMode;
        aspectRatioFitter.aspectRatio = rect.width / rect.height;

        ApplyCoverageNormalization();
    }

    private void ApplyCoverageNormalization()
    {
        if (backgroundImage == null)
        {
            return;
        }

        var rect = backgroundImage.rectTransform;
        if (rect == null)
        {
            return;
        }

        if (!normalizeMapCoverage)
        {
            rect.localScale = Vector3.one;
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one * normalizedCoverageZoom;
    }

    private IEnumerator FadeToSprite(Sprite nextSprite)
    {
        var color = backgroundImage.color;
        var halfDuration = fadeDurationSeconds * 0.5f;

        if (halfDuration <= 0f)
        {
            ApplySprite(nextSprite);
            fadeTransitionCoroutine = null;
            yield break;
        }

        var fadeOutStartAlpha = color.a;
        var elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / halfDuration);
            color.a = Mathf.Lerp(fadeOutStartAlpha, 0f, t);
            backgroundImage.color = color;
            yield return null;
        }

        backgroundImage.sprite = nextSprite;
        UpdateAspectFromSprite(nextSprite);

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / halfDuration);
            color.a = Mathf.Lerp(0f, 1f, t);
            backgroundImage.color = color;
            yield return null;
        }

        color.a = 1f;
        backgroundImage.color = color;
        fadeTransitionCoroutine = null;
    }

    private void RefreshCenterGuideLine()
    {
        if (!showCenterGuideLine)
        {
            if (centerGuideLineImage != null)
            {
                centerGuideLineImage.gameObject.SetActive(false);
            }

            return;
        }

        EnsureCenterGuideLineExists();
        if (centerGuideLineImage == null)
        {
            return;
        }

        if (centerGuideLineSprite != null)
        {
            Destroy(centerGuideLineSprite);
            centerGuideLineSprite = null;
        }

        centerGuideLineSprite = CreateCurvedGuideLineSprite();
        centerGuideLineImage.sprite = centerGuideLineSprite;
        centerGuideLineImage.color = Color.white;
        centerGuideLineImage.gameObject.SetActive(true);

        if (!centerGuideLineImage.TryGetComponent<RectTransform>(out var rect))
        {
            return;
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private void EnsureCenterGuideLineExists()
    {
        if (centerGuideLineImage != null)
        {
            return;
        }

        Transform parent = null;
        if (backgroundImage != null)
        {
            parent = backgroundImage.transform.parent;
        }
        else if (pausePlayButton != null)
        {
            parent = pausePlayButton.transform.parent;
        }

        if (parent == null)
        {
            return;
        }

        var go = new GameObject("CenterGuideLine", typeof(Image));
        go.transform.SetParent(parent, false);
        centerGuideLineImage = go.GetComponent<Image>();
        centerGuideLineImage.raycastTarget = false;
    }

    private Sprite CreateCurvedGuideLineSprite()
    {
        const int width = 1024;
        const int height = 1024;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var thicknessPx = Mathf.Max(1f, guideLineThickness * 1.25f);
        var curveNorm = (guideLineCurvature / 40f) * 0.24f;
        var lineColor = guideLineColor;
        var topTint = guideTopTint;
        var bottomTint = guideBottomTint;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var nx = (x / (float)(width - 1)) * 2f - 1f;
                var ny = (y / (float)(height - 1)) * 2f - 1f;

                float signedDistPx;
                if (guideLineOrientation == GuideLineOrientation.Horizontal)
                {
                    var yCurveNorm = curveNorm * (nx * nx - 1f);
                    signedDistPx = (ny - yCurveNorm) * (height * 0.5f);
                }
                else
                {
                    var xCurveNorm = curveNorm * (ny * ny - 1f);
                    signedDistPx = (nx - xCurveNorm) * (width * 0.5f);
                }

                var baseColor = signedDistPx > 0f ? topTint : bottomTint;
                var absDistPx = Mathf.Abs(signedDistPx);
                if (absDistPx <= thicknessPx)
                {
                    var t = Mathf.Clamp01(absDistPx / thicknessPx);
                    baseColor = Color.Lerp(lineColor, baseColor, t);
                }

                tex.SetPixel(x, y, baseColor);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }
}
