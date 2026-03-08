using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace XCon.UI.Boxes
{
    public sealed class BoxSystemBootstrap : MonoBehaviour
    {
        private const string RootName = "BoxSystem (Runtime)";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateOnceBeforeSceneLoad()
        {
            EnsureCreated("BeforeSceneLoad");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateOnceAfterSceneLoad()
        {
            EnsureCreated("AfterSceneLoad");
        }

        private static void EnsureCreated(string phase)
        {
            var activeScene = SceneManager.GetActiveScene();

            if (GameObject.Find(RootName) != null)
            {
                return;
            }

            var root = new GameObject(RootName);

            // In the Editor, keep it in the active scene so it is visible in the Hierarchy.
            // In builds, persist across scene loads.
#if !UNITY_EDITOR
            DontDestroyOnLoad(root);
#endif

#if UNITY_EDITOR
            var sceneName = activeScene.IsValid() ? activeScene.name : "<invalid>";
            Debug.Log($"[BoxSystem] Bootstrapping ({phase}) in scene '{sceneName}'.");
#endif

            root.AddComponent<BoxMessageQueue>();

            var view = root.AddComponent<BoxSystemView>();
            view.BuildUI();

            root.AddComponent<BoxDebugHotkeys>();

#if UNITY_EDITOR
            // Make it obvious (even if Console is filtered) that the system is alive.
            var queue = root.GetComponent<BoxMessageQueue>();
            if (queue != null)
            {
                const string readyKey = "debug/boxsystem/ready";

                queue.Publish(new BoxMessage(
                    triggerKey: "debug/boxsystem/ready",
                    channel: BoxChannel.Info,
                    severity: BoxSeverity.Info,
                    sourceTag: "System",
                    title: "BoxSystem Ready",
                    body: "Press 1/2/3 (or F1/F2/F3). Esc to dismiss."));

                // Don't let the Ready message block subsequent hotkey messages.
                var autoDismiss = root.AddComponent<BoxAutoDismissCurrentMessage>();
                autoDismiss.Configure(queue, readyKey, delaySeconds: 2.0f);
            }
#endif
        }
    }

    public sealed class BoxSystemView : MonoBehaviour
    {
        private Text titleText;
        private Text sourceText;
        private Text bodyText;
        private GameObject panel;
        private bool subscribed;

        public void BuildUI()
        {
            var canvasGo = new GameObject("BoxCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            panel = new GameObject("BoxPanel", typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);

            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.65f);

            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.02f, 0.02f);
            panelRect.anchorMax = new Vector2(0.45f, 0.22f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var titleGo = CreateText("Title", panel.transform, fontSize: 22, fontStyle: FontStyle.Bold);
            titleText = titleGo.GetComponent<Text>();
            SetRect(titleGo.GetComponent<RectTransform>(), new Vector2(0.03f, 0.72f), new Vector2(0.97f, 0.97f));

            var sourceGo = CreateText("Source", panel.transform, fontSize: 14, fontStyle: FontStyle.Italic);
            sourceText = sourceGo.GetComponent<Text>();
            sourceText.color = new Color(1f, 1f, 1f, 0.85f);
            SetRect(sourceGo.GetComponent<RectTransform>(), new Vector2(0.03f, 0.58f), new Vector2(0.97f, 0.72f));

            var bodyGo = CreateText("Body", panel.transform, fontSize: 18, fontStyle: FontStyle.Normal);
            bodyText = bodyGo.GetComponent<Text>();
            bodyText.color = new Color(1f, 1f, 1f, 0.95f);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Truncate;
            SetRect(bodyGo.GetComponent<RectTransform>(), new Vector2(0.03f, 0.10f), new Vector2(0.97f, 0.58f));

            var hintGo = CreateText("Hint", panel.transform, fontSize: 12, fontStyle: FontStyle.Normal);
            var hintText = hintGo.GetComponent<Text>();
            hintText.text = "F1=Info  F2=Thinking  F3=Critical  Esc=Dismiss";
            hintText.color = new Color(1f, 1f, 1f, 0.6f);
            hintText.alignment = TextAnchor.LowerRight;
            SetRect(hintGo.GetComponent<RectTransform>(), new Vector2(0.03f, 0.00f), new Vector2(0.97f, 0.10f));

            panel.SetActive(false);

            TrySubscribe();
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void Update()
        {
            // In some editor/playmode configurations, Instance may not be available at BuildUI time.
            if (!subscribed)
            {
                TrySubscribe();
            }
        }

        private void OnDestroy()
        {
            if (BoxMessageQueue.Instance != null)
            {
                BoxMessageQueue.Instance.CurrentMessageChanged -= OnMessageChanged;
            }
        }

        private void TrySubscribe()
        {
            if (subscribed)
            {
                return;
            }

            var queue = BoxMessageQueue.Instance;
            if (queue == null)
            {
                return;
            }

            queue.CurrentMessageChanged += OnMessageChanged;
            subscribed = true;

            // Sync current state immediately.
            OnMessageChanged(queue.Current);
        }

        private void OnMessageChanged(BoxMessage? message)
        {
            if (!panel)
            {
                return;
            }

            if (!message.HasValue)
            {
                panel.SetActive(false);
                return;
            }

            var m = message.Value;
            panel.SetActive(true);

            titleText.text = m.Title;
            bodyText.text = m.Body;
            sourceText.text = string.IsNullOrWhiteSpace(m.SourceTag) ? $"{m.Channel}" : $"{m.Channel} · {m.SourceTag} · {m.Severity}";
        }

        private static GameObject CreateText(string name, Transform parent, int fontSize, FontStyle fontStyle)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            // Unity 6 no longer ships Arial.ttf as a built-in resource.
            // LegacyRuntime.ttf is the supported built-in fallback.
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;

            return go;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    public sealed class BoxDebugHotkeys : MonoBehaviour
    {
        private bool loggedReady;

        private void Update()
        {
            var queue = BoxMessageQueue.Instance;
            if (queue == null)
            {
                queue = UnityEngine.Object.FindAnyObjectByType<BoxMessageQueue>();
                if (queue == null)
                {
                    return;
                }
            }

#if UNITY_EDITOR
            if (!loggedReady)
            {
                loggedReady = true;
                Debug.Log("[BoxSystem] Hotkeys active. Use 1/2/3 (or F1/F2/F3) and Esc to dismiss.");
            }
#endif

            if (WasPressed(DebugKey.Info))
            {
                queue.Publish(new BoxMessage(
                    triggerKey: "debug/info/contact_detected",
                    channel: BoxChannel.Info,
                    severity: BoxSeverity.Info,
                    sourceTag: "NIA",
                    title: "Contact Detected",
                    body: "Contact detected. Region: North Sea. Confidence: High."));
            }

            if (WasPressed(DebugKey.Thinking))
            {
                queue.Publish(new BoxMessage(
                    triggerKey: "debug/thinking/stall",
                    channel: BoxChannel.Thinking,
                    severity: BoxSeverity.Info,
                    sourceTag: "Commander",
                    title: "Next Move",
                    body: "You’re waiting. Pick a priority: coverage, research, or response."));
            }

            if (WasPressed(DebugKey.Critical))
            {
                queue.Publish(new BoxMessage(
                    triggerKey: "debug/info/intercept_window",
                    channel: BoxChannel.Info,
                    severity: BoxSeverity.Critical,
                    sourceTag: "System",
                    title: "Intercept Window",
                    body: "Intercept window: 02:15 remaining."));
            }

            if (WasPressed(DebugKey.Dismiss))
            {
                queue.DismissCurrent();
            }
        }

        private enum DebugKey { Info, Thinking, Critical, Dismiss }

        private static bool WasPressed(DebugKey key)
        {
            var pressed = false;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                pressed |= key switch
                {
                    DebugKey.Info => keyboard.f1Key.wasPressedThisFrame || keyboard.digit1Key.wasPressedThisFrame,
                    DebugKey.Thinking => keyboard.f2Key.wasPressedThisFrame || keyboard.digit2Key.wasPressedThisFrame,
                    DebugKey.Critical => keyboard.f3Key.wasPressedThisFrame || keyboard.digit3Key.wasPressedThisFrame,
                    _ => keyboard.escapeKey.wasPressedThisFrame || keyboard.backquoteKey.wasPressedThisFrame,
                };
            }
#endif

            return pressed;
        }
    }

    public sealed class BoxAutoDismissCurrentMessage : MonoBehaviour
    {
        private BoxMessageQueue queue;
        private string triggerKey;
        private float delaySeconds;
        private bool configured;

        public void Configure(BoxMessageQueue queue, string triggerKey, float delaySeconds)
        {
            this.queue = queue;
            this.triggerKey = triggerKey;
            this.delaySeconds = delaySeconds;
            configured = true;
        }

        private void Start()
        {
            if (!configured || queue == null || string.IsNullOrWhiteSpace(triggerKey))
            {
                Destroy(this);
                return;
            }

            StartCoroutine(DismissAfterDelay());
        }

        private System.Collections.IEnumerator DismissAfterDelay()
        {
            yield return new WaitForSecondsRealtime(delaySeconds);

            if (queue == null)
            {
                yield break;
            }

            var current = queue.Current;
            if (current.HasValue && current.Value.TriggerKey == triggerKey)
            {
                queue.DismissCurrent();
            }

            Destroy(this);
        }
    }
}
