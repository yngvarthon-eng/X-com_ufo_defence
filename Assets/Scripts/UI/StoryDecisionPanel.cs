using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XCon.UI
{
    /// <summary>One button on a story decision.</summary>
    public readonly struct StoryOption
    {
        public StoryOption(string label, string detail, Action onChosen)
        {
            Label = label;
            Detail = detail;
            OnChosen = onChosen;
        }

        public string Label { get; }
        public string Detail { get; }
        public Action OnChosen { get; }
    }

    /// <summary>
    /// Modal story decision with buttons. Pauses the game while open and restores the previous
    /// pause state when closed. Decisions raised while one is open are queued.
    /// </summary>
    public sealed class StoryDecisionPanel : MonoBehaviour
    {
        private sealed class Decision
        {
            public string Source;
            public string Title;
            public string Body;
            public StoryOption[] Options;
        }

        private static StoryDecisionPanel instance;
        private readonly Queue<Decision> queue = new();
        private GameObject canvasGo;
        private bool wasPausedBeforeOpen;
        private bool open;

        public static bool IsOpen => instance != null && instance.open;

        public static void Show(string source, string title, string body, params StoryOption[] options)
        {
            if (options == null || options.Length == 0)
            {
                return;
            }

            if (instance == null)
            {
                var go = new GameObject("StoryDecisionPanel");
                instance = go.AddComponent<StoryDecisionPanel>();
            }

            instance.queue.Enqueue(new Decision { Source = source, Title = title, Body = body, Options = options });
            if (!instance.open)
            {
                instance.OpenNext();
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            // The game menu can unpause on close; keep the game held while a decision is on screen.
            if (open && !GameMenuController.IsPaused)
            {
                GameMenuController.SetPaused(true);
            }
        }

        private void OpenNext()
        {
            if (queue.Count == 0)
            {
                return;
            }

            var decision = queue.Dequeue();
            wasPausedBeforeOpen = GameMenuController.IsPaused;
            GameMenuController.SetPaused(true);
            open = true;
            Build(decision);
        }

        private void Choose(StoryOption option)
        {
            if (canvasGo != null)
            {
                Destroy(canvasGo);
                canvasGo = null;
            }

            open = false;
            GameMenuController.SetPaused(wasPausedBeforeOpen);

            try
            {
                option.OnChosen?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            OpenNext();
        }

        private void Build(Decision decision)
        {
            canvasGo = new GameObject("StoryDecisionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2500; // Above the mission end panel (2000), below Get Ready (3000).

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Dim everything behind the decision and block clicks to it.
            var dim = new GameObject("Dim", typeof(Image));
            dim.transform.SetParent(canvasGo.transform, false);
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            Stretch(dim.GetComponent<RectTransform>());

            const float width = 760f;
            const float optionHeight = 64f;
            const float optionGap = 10f;
            var height = 250f + decision.Options.Length * (optionHeight + optionGap);

            var panel = new GameObject("Panel", typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            panel.GetComponent<Image>().color = new Color(0.07f, 0.07f, 0.09f, 0.96f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(width, height);

            var source = CreateText(panel.transform, "Source", decision.Source.ToUpperInvariant(), 14, FontStyle.Bold, new Color(0.65f, 0.8f, 1f, 1f));
            Place(source.rectTransform, new Vector2(0f, -18f), new Vector2(width - 60f, 20f));

            var title = CreateText(panel.transform, "Title", decision.Title, 30, FontStyle.Bold, Color.white);
            Place(title.rectTransform, new Vector2(0f, -42f), new Vector2(width - 60f, 38f));

            var body = CreateText(panel.transform, "Body", decision.Body, 18, FontStyle.Normal, new Color(0.88f, 0.88f, 0.88f, 1f));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            Place(body.rectTransform, new Vector2(0f, -90f), new Vector2(width - 60f, 140f));

            var y = -240f;
            foreach (var option in decision.Options)
            {
                var captured = option;
                var button = CreateOptionButton(panel.transform, option, new Vector2(width - 60f, optionHeight));
                Place(button.GetComponent<RectTransform>(), new Vector2(0f, y), new Vector2(width - 60f, optionHeight));
                button.onClick.AddListener(() => Choose(captured));
                y -= optionHeight + optionGap;
            }
        }

        private static Button CreateOptionButton(Transform parent, StoryOption option, Vector2 size)
        {
            var go = new GameObject("Option", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.92f, 0.92f, 0.92f, 1f);

            var label = CreateText(go.transform, "Label", option.Label, 20, FontStyle.Bold, new Color(0.1f, 0.1f, 0.12f, 1f));
            label.alignment = TextAnchor.UpperLeft;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.offsetMin = new Vector2(16f, -32f);
            labelRect.offsetMax = new Vector2(-16f, -8f);

            var detail = CreateText(go.transform, "Detail", option.Detail, 15, FontStyle.Normal, new Color(0.25f, 0.25f, 0.3f, 1f));
            detail.alignment = TextAnchor.UpperLeft;
            var detailRect = detail.rectTransform;
            detailRect.anchorMin = new Vector2(0f, 0f);
            detailRect.anchorMax = new Vector2(1f, 1f);
            detailRect.offsetMin = new Vector2(16f, 6f);
            detailRect.offsetMax = new Vector2(-16f, -34f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            return button;
        }

        private static Text CreateText(Transform parent, string name, string value, int size, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.UpperCenter;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.text = value;
            return text;
        }

        private static void Place(RectTransform rect, Vector2 topOffset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = topOffset;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
