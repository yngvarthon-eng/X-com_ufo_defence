using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class WorldMapBackgroundManager : MonoBehaviour
{
    private const int DefaultMapIndex = 0;
    private const string BackgroundObjectName = "WorldMapBackground";
    private const string DropdownObjectName = "MapDropdown";
    private const string MapStylePrefKey = "xcon.worldmap.styleIndex";

    [Header("World Map Sprites")]
    [SerializeField] private Sprite roundSphereMap;
    [SerializeField] private Sprite flatOvalMap;
    [SerializeField] private Sprite flatMap;

    [Header("UI Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Dropdown mapDropdown;
    [SerializeField] private Button resetMapStyleButton;

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

    private Dictionary<int, Sprite> mapOptions;
    private Coroutine fadeTransitionCoroutine;
    private int currentMapIndex = -1;
    private AspectRatioFitter aspectRatioFitter;

    void Awake()
    {
        ResolveReferences();
        ApplyLayoutSettings();

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
    }

    private void OnValidate()
    {
        ApplyLayoutSettings();
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

    private void ApplyLayoutSettings()
    {
        if (backgroundImage == null)
        {
            return;
        }

        backgroundImage.preserveAspect = forcePreserveAspect;

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

            aspectRatioFitter.aspectMode = aspectMode;
        }
        else
        {
            aspectRatioFitter = backgroundImage.GetComponent<AspectRatioFitter>();
            if (aspectRatioFitter != null)
            {
                aspectRatioFitter.enabled = false;
            }
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
        aspectRatioFitter.aspectMode = aspectMode;
        aspectRatioFitter.aspectRatio = rect.width / rect.height;
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
}
