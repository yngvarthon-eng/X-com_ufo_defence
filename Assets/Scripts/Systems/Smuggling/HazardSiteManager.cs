using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XCon.UI.Boxes;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace XCon.Smuggling
{
    /// <summary>Pure numbers for hazard sites (see Design_ExodusCraft.md, "Gen 0b: Mule").</summary>
    public static class HazardSiteRules
    {
        /// <summary>Permit risk added per minute while a site is open (before the difficulty multiplier).</summary>
        public static float PermitRiskPerMinute(CargoHazard hazard) => hazard switch
        {
            CargoHazard.Chemical => 0.6f,
            CargoHazard.Toxic => 1.0f,
            CargoHazard.Radioactive => 2.5f,
            _ => 0f,
        };

        /// <summary>How long the hazmat team needs on site.</summary>
        public static float ContainmentSeconds(CargoHazard hazard) => hazard switch
        {
            CargoHazard.Chemical => 30f,
            CargoHazard.Toxic => 45f,
            CargoHazard.Radioactive => 90f,
            _ => 0f,
        };

        /// <summary>A radioactive site left open this long contaminates the region.</summary>
        public const float RadioactiveContaminationSeconds = 3f * 60f;

        /// <summary>One-off permit risk jump when a region is contaminated (before the difficulty multiplier).</summary>
        public const float ContaminationPermitRiskJump = 15f;

        public const float ContainedEducationGain = 1f;

        public static Color MarkerColor(CargoHazard hazard) => hazard switch
        {
            CargoHazard.Chemical => new Color(1f, 0.86f, 0.2f, 1f),
            CargoHazard.Toxic => new Color(0.35f, 1f, 0.35f, 1f),
            CargoHazard.Radioactive => new Color(1f, 0.22f, 0.18f, 1f),
            _ => Color.white,
        };
    }

    /// <summary>
    /// Hazard sites left by Mules that spill their cargo when shot down.
    /// Open sites drain permit risk until the single hazmat team (key H) contains them.
    /// </summary>
    public sealed class HazardSiteManager : MonoBehaviour
    {
        private sealed class HazardSite
        {
            public int Id;
            public int ContactId;
            public CargoHazard Hazard;
            public float OpenSeconds; // Gameplay time the site has been open (pause-aware).
            public bool Contaminated;
            public RectTransform Marker;
            public Image Ring;
        }

        public static HazardSiteManager Instance { get; private set; }

        private readonly List<HazardSite> openSites = new();
        private int nextSiteId = 1;

        private HazardSite hazmatTarget;
        private float hazmatRemainingSeconds;

        private UFOManager ufo;
        private RectTransform markerLayer;
        private static Sprite ringSprite;

        public int SitesContained { get; private set; }
        public int RegionsContaminated { get; private set; }
        public int OpenSiteCount => openSites.Count;
        public bool HazmatBusy => hazmatTarget != null;

        private void Awake()
        {
            Instance = this;
            ufo = GetComponent<UFOManager>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Restore totals from a save. Open sites themselves are not saved.</summary>
        public void ApplyPersistentState(int loadedSitesContained, int loadedRegionsContaminated)
        {
            SitesContained = Mathf.Max(0, loadedSitesContained);
            RegionsContaminated = Mathf.Max(0, loadedRegionsContaminated);
        }

        /// <summary>Open a hazard site at a viewport position (0..1). Returns false for harmless cargo.</summary>
        public bool CreateSite(int contactId, CargoHazard hazard, Vector2 viewportPosition)
        {
            if (hazard == CargoHazard.None)
            {
                return false;
            }

            var site = new HazardSite
            {
                Id = nextSiteId++,
                ContactId = contactId,
                Hazard = hazard,
            };
            CreateMarker(site, viewportPosition);
            openSites.Add(site);

            var minutes = HazardSiteRules.PermitRiskPerMinute(hazard) * Difficulty.ActivePreset.PermitRiskGainMultiplier;
            Publish(
                $"hazard/site/{site.Id}",
                hazard == CargoHazard.Radioactive ? BoxSeverity.Critical : BoxSeverity.Warn,
                "Hazard Site",
                $"Site #{site.Id} ({MuleCargoRules.HazardLabel(hazard)}) is open where contact #{contactId} came down.\n" +
                $"Permit risk +{minutes:0.0}/min until contained.\n" +
                (hazard == CargoHazard.Radioactive
                    ? $"Contain within {Mathf.RoundToInt(HazardSiteRules.RadioactiveContaminationSeconds / 60f)} min or the region is contaminated.\n"
                    : string.Empty) +
                $"\nPress H to send the hazmat team ({Mathf.RoundToInt(HazardSiteRules.ContainmentSeconds(hazard))} s on site).");
            return true;
        }

        private void Update()
        {
            if (GameMenuController.IsPaused)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            TickOpenSites(dt);
            TickHazmat(dt);
            AnimateMarkers();

            if (WasHazmatKeyPressed())
            {
                DispatchHazmat();
            }
        }

        private void TickOpenSites(float dt)
        {
            for (var i = 0; i < openSites.Count; i++)
            {
                var site = openSites[i];
                site.OpenSeconds += dt;

                if (ufo != null)
                {
                    ufo.AddPermitRiskFromHazard(HazardSiteRules.PermitRiskPerMinute(site.Hazard) / 60f * dt);
                }

                if (site.Hazard == CargoHazard.Radioactive
                    && !site.Contaminated
                    && site.OpenSeconds >= HazardSiteRules.RadioactiveContaminationSeconds)
                {
                    ContaminateRegion(site);
                }
            }
        }

        private void ContaminateRegion(HazardSite site)
        {
            site.Contaminated = true;
            RegionsContaminated++;

            if (ufo != null)
            {
                ufo.AddPermitRiskFromHazard(HazardSiteRules.ContaminationPermitRiskJump);
            }

            Publish(
                $"hazard/contaminated/{site.Id}",
                BoxSeverity.Critical,
                "Region Contaminated",
                $"Site #{site.Id} was left open too long. Radioactive material has spread beyond the crash zone.\n" +
                "Civil authorities are evacuating the area. Permit risk has jumped.\n\n" +
                "The site still needs containing.");
        }

        private void TickHazmat(float dt)
        {
            if (hazmatTarget == null)
            {
                return;
            }

            hazmatRemainingSeconds -= dt;
            if (hazmatRemainingSeconds > 0f)
            {
                return;
            }

            var site = hazmatTarget;
            hazmatTarget = null;
            hazmatRemainingSeconds = 0f;

            openSites.Remove(site);
            if (site.Marker != null)
            {
                Destroy(site.Marker.gameObject);
            }

            SitesContained++;
            if (ufo != null)
            {
                ufo.AddEducationFromHazard(HazardSiteRules.ContainedEducationGain);
            }

            Publish(
                $"hazard/contained/{site.Id}",
                BoxSeverity.Info,
                "Site Contained",
                $"The hazmat team has sealed site #{site.Id} ({MuleCargoRules.HazardLabel(site.Hazard)}).\n" +
                (openSites.Count > 0 ? $"{openSites.Count} site(s) still open. Press H to send the team again." : "No open hazard sites remain."));
        }

        private void DispatchHazmat()
        {
            if (hazmatTarget != null)
            {
                Publish(
                    "hazard/hazmat/busy",
                    BoxSeverity.Info,
                    "Hazmat Team Busy",
                    $"The team is still on site #{hazmatTarget.Id}. Done in {FormatSeconds(hazmatRemainingSeconds)}.");
                return;
            }

            if (openSites.Count == 0)
            {
                Publish("hazard/hazmat/idle", BoxSeverity.Info, "Hazmat Team", "No open hazard sites.");
                return;
            }

            // Oldest first: the list is kept in creation order.
            hazmatTarget = openSites[0];
            hazmatRemainingSeconds = HazardSiteRules.ContainmentSeconds(hazmatTarget.Hazard);

            Publish(
                $"hazard/hazmat/{hazmatTarget.Id}",
                BoxSeverity.Info,
                "Hazmat Dispatched",
                $"The hazmat team is heading to site #{hazmatTarget.Id} ({MuleCargoRules.HazardLabel(hazmatTarget.Hazard)}).\n" +
                $"Containment takes {FormatSeconds(hazmatRemainingSeconds)}.");
        }

        /// <summary>One HUD line, or empty when there is nothing to show.</summary>
        public string BuildHudLine()
        {
            if (openSites.Count == 0 && hazmatTarget == null)
            {
                return string.Empty;
            }

            var radioactive = 0;
            for (var i = 0; i < openSites.Count; i++)
            {
                if (openSites[i].Hazard == CargoHazard.Radioactive)
                {
                    radioactive++;
                }
            }

            var sites = radioactive > 0
                ? $"{openSites.Count} open (<color=#FF8A8A>{radioactive} RADIOACTIVE</color>)"
                : $"{openSites.Count} open";
            var team = hazmatTarget != null ? $"busy {FormatSeconds(hazmatRemainingSeconds)}" : "ready [H]";
            return $"Hazard sites: {sites}  |  Hazmat: {team}";
        }

        private static string FormatSeconds(float seconds)
        {
            var total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        private static void Publish(string key, BoxSeverity severity, string title, string body)
        {
            var queue = BoxMessageQueue.Instance != null ? BoxMessageQueue.Instance : Object.FindAnyObjectByType<BoxMessageQueue>();
            if (queue == null)
            {
                return;
            }

            queue.Publish(new BoxMessage(
                triggerKey: key,
                channel: BoxChannel.Info,
                severity: severity,
                sourceTag: "Hazmat",
                title: title,
                body: body));
        }

        private static bool WasHazmatKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.hKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.H))
            {
                return true;
            }
#endif
            return false;
        }

        // ---------------------------------------------------------------------
        // Markers: UI rings on an overlay canvas, below the message box.
        // ---------------------------------------------------------------------

        private void EnsureMarkerLayer()
        {
            if (markerLayer != null)
            {
                return;
            }

            var canvasGo = new GameObject("HazardSiteCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            markerLayer = canvasGo.GetComponent<RectTransform>();
        }

        private void CreateMarker(HazardSite site, Vector2 viewportPosition)
        {
            EnsureMarkerLayer();

            var markerGo = new GameObject($"HazardSite_{site.Id}", typeof(RectTransform));
            markerGo.transform.SetParent(markerLayer, false);
            var rect = markerGo.GetComponent<RectTransform>();
            var clamped = new Vector2(Mathf.Clamp(viewportPosition.x, 0.08f, 0.92f), Mathf.Clamp(viewportPosition.y, 0.12f, 0.9f));
            rect.anchorMin = clamped;
            rect.anchorMax = clamped;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(64f, 64f);

            var ringGo = new GameObject("Ring", typeof(Image));
            ringGo.transform.SetParent(markerGo.transform, false);
            var ring = ringGo.GetComponent<Image>();
            ring.sprite = GetRingSprite();
            ring.color = HazardSiteRules.MarkerColor(site.Hazard);
            ring.raycastTarget = false;
            var ringRect = ring.rectTransform;
            ringRect.anchorMin = Vector2.zero;
            ringRect.anchorMax = Vector2.one;
            ringRect.offsetMin = Vector2.zero;
            ringRect.offsetMax = Vector2.zero;

            var labelGo = new GameObject("Label", typeof(Text));
            labelGo.transform.SetParent(markerGo.transform, false);
            var label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 14;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.UpperCenter;
            label.color = HazardSiteRules.MarkerColor(site.Hazard);
            label.raycastTarget = false;
            label.text = $"#{site.Id} {MuleCargoRules.HazardLabel(site.Hazard).ToUpperInvariant()}";
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -4f);
            labelRect.sizeDelta = new Vector2(160f, 20f);

            site.Marker = rect;
            site.Ring = ring;
        }

        private void AnimateMarkers()
        {
            var t = Time.unscaledTime;
            for (var i = 0; i < openSites.Count; i++)
            {
                var site = openSites[i];
                if (site.Ring == null)
                {
                    continue;
                }

                var baseColor = HazardSiteRules.MarkerColor(site.Hazard);
                var radioactive = site.Hazard == CargoHazard.Radioactive;
                var speed = radioactive ? 6f : 2.5f;
                var pulse = 0.5f + 0.5f * Mathf.Sin(t * speed + site.Id);
                baseColor.a = radioactive ? Mathf.Lerp(0.35f, 1f, pulse) : Mathf.Lerp(0.6f, 0.95f, pulse);
                site.Ring.color = baseColor;

                var scale = radioactive ? Mathf.Lerp(0.9f, 1.15f, pulse) : 1f;
                if (site == hazmatTarget)
                {
                    scale *= 0.85f; // Team on site: ring tightens.
                }

                site.Marker.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private static Sprite GetRingSprite()
        {
            if (ringSprite != null)
            {
                return ringSprite;
            }

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var center = (size - 1) * 0.5f;
            const float outer = 30f;
            const float inner = 22f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    // Soft one-pixel edges on both sides of the ring.
                    var a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return ringSprite;
        }

        // ---------------------------------------------------------------------
        // Debug
        // ---------------------------------------------------------------------

        [ContextMenu("Debug/Incident: Hazard Site (Radioactive)")]
        private void DebugSiteRadioactive() => DebugSite(CargoHazard.Radioactive);

        [ContextMenu("Debug/Incident: Hazard Site (Toxic)")]
        private void DebugSiteToxic() => DebugSite(CargoHazard.Toxic);

        [ContextMenu("Debug/Incident: Hazard Site (Chemical)")]
        private void DebugSiteChemical() => DebugSite(CargoHazard.Chemical);

        private void DebugSite(CargoHazard hazard)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Hazmat] Enter Play Mode to run the debug action.");
                return;
            }

            CreateSite(contactId: 0, hazard, new Vector2(Random.Range(0.25f, 0.75f), Random.Range(0.3f, 0.7f)));
        }
    }
}
