using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Text;

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
        private Text queueText;
        private GameObject panel;
        private readonly List<BoxMessage> queuedBuffer = new();
        private BoxMessageQueue queue;
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
            panelRect.anchorMax = new Vector2(0.45f, 0.30f);
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
            SetRect(bodyGo.GetComponent<RectTransform>(), new Vector2(0.03f, 0.16f), new Vector2(0.97f, 0.58f));

            var queueGo = CreateText("Queue", panel.transform, fontSize: 12, fontStyle: FontStyle.Normal);
            queueText = queueGo.GetComponent<Text>();
            queueText.color = new Color(1f, 1f, 1f, 0.7f);
            queueText.alignment = TextAnchor.LowerLeft;
            queueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            queueText.verticalOverflow = VerticalWrapMode.Truncate;
            SetRect(queueGo.GetComponent<RectTransform>(), new Vector2(0.03f, 0.10f), new Vector2(0.97f, 0.16f));

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

            if (queue == null)
            {
                queue = BoxMessageQueue.Instance;
            }

            if (queue == null)
            {
                return;
            }

            if (!queue.Current.HasValue)
            {
                return;
            }

            var current = queue.Current.Value;
            if (!string.IsNullOrWhiteSpace(current.TriggerKey)
                && current.TriggerKey.StartsWith("ufo/response_choice/", StringComparison.Ordinal))
            {
                // Don't allow dismissing interactive prompts; they should be answered instead.
                return;
            }

            var dismissPressed = false;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                dismissPressed |= keyboard.escapeKey.wasPressedThisFrame || keyboard.backquoteKey.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            dismissPressed |= Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.BackQuote);
#endif

            if (dismissPressed)
            {
                queue.DismissCurrent();
            }
        }

        private void OnDestroy()
        {
            if (queue != null)
            {
                queue.CurrentMessageChanged -= OnMessageChanged;
                queue.StateChanged -= OnStateChanged;
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

            this.queue = queue;

            queue.CurrentMessageChanged += OnMessageChanged;
            queue.StateChanged += OnStateChanged;
            subscribed = true;

            // Sync current state immediately.
            Refresh(queue);
        }

        private void OnStateChanged()
        {
            if (queue == null)
            {
                return;
            }

            Refresh(queue);
        }

        private void OnMessageChanged(BoxMessage? message)
        {
            if (queue == null)
            {
                queue = BoxMessageQueue.Instance;
            }

            if (queue != null)
            {
                Refresh(queue);
                return;
            }

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

        private void Refresh(BoxMessageQueue queue)
        {
            if (!panel)
            {
                return;
            }

            var message = queue.Current;
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

            queuedBuffer.Clear();
            queue.CopyQueuedMessages(queuedBuffer);
            queuedBuffer.Sort((a, b) => GetPriority(b).CompareTo(GetPriority(a)));

            queueText.text = BuildQueuePreview(queuedBuffer, maxItems: 3);
            queueText.gameObject.SetActive(!string.IsNullOrWhiteSpace(queueText.text));
        }

        private static string BuildQueuePreview(List<BoxMessage> queued, int maxItems)
        {
            if (queued == null || queued.Count == 0 || maxItems <= 0)
            {
                return string.Empty;
            }

            var count = Mathf.Min(maxItems, queued.Count);
            var sb = new StringBuilder(256);

            sb.AppendLine("Next:");
            for (var i = 0; i < count; i++)
            {
                var m = queued[i];
                sb.Append("- ");
                sb.Append(FormatTag(m));
                sb.Append(' ');
                sb.Append(m.Title);
                if (i < count - 1)
                {
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static string FormatTag(BoxMessage message)
        {
            if (message.Channel == BoxChannel.Thinking)
            {
                return "[Thinking]";
            }

            return message.Severity switch
            {
                BoxSeverity.Critical => "[Critical]",
                BoxSeverity.Warn => "[Warn]",
                _ => "[Info]",
            };
        }

        private static int GetPriority(BoxMessage message)
        {
            if (message.Channel == BoxChannel.Info)
            {
                return message.Severity switch
                {
                    BoxSeverity.Critical => 400,
                    BoxSeverity.Warn => 300,
                    _ => 100,
                };
            }

            return 200;
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
                    DebugKey.Info => keyboard.f1Key.wasPressedThisFrame,
                    DebugKey.Thinking => keyboard.f2Key.wasPressedThisFrame,
                    DebugKey.Critical => keyboard.f3Key.wasPressedThisFrame,
                    _ => keyboard.escapeKey.wasPressedThisFrame || keyboard.backquoteKey.wasPressedThisFrame,
                };
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            pressed |= key switch
            {
                DebugKey.Info => Input.GetKeyDown(KeyCode.F1),
                DebugKey.Thinking => Input.GetKeyDown(KeyCode.F2),
                DebugKey.Critical => Input.GetKeyDown(KeyCode.F3),
                _ => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.BackQuote),
            };
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
