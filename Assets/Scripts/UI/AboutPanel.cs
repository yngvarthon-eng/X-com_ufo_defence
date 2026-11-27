using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "About & How to Play" window opened from the game menu. Tabs with scrollable text.
/// The texts live in <see cref="AboutContent"/> so they are easy to edit.
/// </summary>
public sealed class AboutPanel : MonoBehaviour
{
    private static readonly Color TabIdle = new(1f, 1f, 1f, 0.55f);
    private static readonly Color TabActive = new(1f, 0.86f, 0.45f, 1f);

    private GameObject root;
    private Text bodyText;
    private ScrollRect scroll;
    private Image[] tabImages;
    private GameObject highscoreView;
    private Text[] highscoreColumns;
    private GameMode highscoreMode = GameMode.Mission;
    private Image[] highscoreModeImages;
    private static readonly GameMode[] HighscoreModes = { GameMode.Mission, GameMode.Endurance };

    public bool IsOpen => root != null && root.activeSelf;

    public static AboutPanel Create(Transform canvas)
    {
        var host = new GameObject("AboutPanelHost", typeof(RectTransform));
        host.transform.SetParent(canvas, false);
        var panel = host.AddComponent<AboutPanel>();
        panel.Build(canvas);
        return panel;
    }

    public void Open(int tab = 0)
    {
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        ShowTab(tab);
    }

    public void OpenHighscores() => Open(AboutContent.HighscoresTab);

    public void Close()
    {
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    private void Build(Transform canvas)
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        // Full-screen dim backdrop: blocks clicks to the game; clicking it closes the window.
        root = new GameObject("AboutBackdrop", typeof(Image), typeof(Button));
        root.transform.SetParent(canvas, false);
        Stretch(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        var backdropButton = root.GetComponent<Button>();
        backdropButton.transition = Selectable.Transition.None;
        backdropButton.onClick.AddListener(Close);

        var panel = new GameObject("AboutWindow", typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1100f, 800f);
        panel.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.11f, 0.97f);

        // Tabs.
        var tabs = AboutContent.Tabs;
        tabImages = new Image[tabs.Length];
        const float tabWidth = 200f;
        const float tabGap = 10f;
        var startX = -((tabs.Length * tabWidth) + ((tabs.Length - 1) * tabGap)) / 2f + tabWidth / 2f;
        for (var i = 0; i < tabs.Length; i++)
        {
            var index = i;
            var tab = CreateButton(panel.transform, font, tabs[i].Title, new Vector2(0.5f, 1f), new Vector2(startX + i * (tabWidth + tabGap), -38f), new Vector2(tabWidth, 44f));
            tab.onClick.AddListener(() => ShowTab(index));
            tabImages[i] = tab.GetComponent<Image>();
        }

        // Scrollable body.
        var viewport = new GameObject("Viewport", typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(panel.transform, false);
        var viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect, new Vector2(28f, 84f), new Vector2(-48f, -76f));
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.03f);

        var content = new GameObject("Content", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 18, 28);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var textGo = new GameObject("Text", typeof(Text));
        textGo.transform.SetParent(content.transform, false);
        bodyText = textGo.GetComponent<Text>();
        bodyText.font = font;
        bodyText.fontSize = 19;
        bodyText.lineSpacing = 1.1f;
        bodyText.supportRichText = true;
        bodyText.alignment = TextAnchor.UpperLeft;
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        bodyText.color = new Color(0.92f, 0.93f, 0.95f, 1f);
        bodyText.raycastTarget = false;

        scroll = viewport.AddComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        scroll.verticalScrollbar = CreateScrollbar(panel.transform);
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        BuildHighscoreView(panel.transform, font);

        var close = CreateButton(panel.transform, font, "Close", new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(200f, 46f));
        close.onClick.AddListener(Close);

        var hint = new GameObject("Hint", typeof(Text));
        hint.transform.SetParent(panel.transform, false);
        var hintRect = hint.GetComponent<RectTransform>();
        hintRect.anchorMin = hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(1f, 0.5f);
        hintRect.anchoredPosition = new Vector2(-30f, 42f);
        hintRect.sizeDelta = new Vector2(320f, 30f);
        var hintText = hint.GetComponent<Text>();
        hintText.font = font;
        hintText.fontSize = 15;
        hintText.alignment = TextAnchor.MiddleRight;
        hintText.color = new Color(1f, 1f, 1f, 0.45f);
        hintText.text = "Mouse wheel to scroll  ·  Esc to close";
        hintText.raycastTarget = false;

        root.SetActive(false);
    }

    private void ShowTab(int index)
    {
        var tabs = AboutContent.Tabs;
        index = Mathf.Clamp(index, 0, tabs.Length - 1);

        var isHighscores = index == AboutContent.HighscoresTab;
        scroll.gameObject.SetActive(!isHighscores);
        scroll.verticalScrollbar.gameObject.SetActive(!isHighscores);
        highscoreView.SetActive(isHighscores);
        if (isHighscores)
        {
            // Open on the list for the mode being played (Training has none).
            highscoreMode = GameModes.Active == GameMode.Endurance ? GameMode.Endurance : GameMode.Mission;
            FillHighscores();
        }
        else
        {
            bodyText.text = tabs[index].Body();
        }

        for (var i = 0; i < tabImages.Length; i++)
        {
            tabImages[i].color = i == index ? TabActive : TabIdle;
        }

        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 1f;
    }

    private void BuildHighscoreView(Transform panel, Font font)
    {
        highscoreView = new GameObject("Highscores", typeof(RectTransform));
        highscoreView.transform.SetParent(panel, false);
        Stretch(highscoreView.GetComponent<RectTransform>(), new Vector2(28f, 84f), new Vector2(-28f, -76f));

        // Mission / Endurance switch.
        highscoreModeImages = new Image[HighscoreModes.Length];
        for (var i = 0; i < HighscoreModes.Length; i++)
        {
            var mode = HighscoreModes[i];
            var button = CreateButton(highscoreView.transform, font, GameModes.Label(mode), new Vector2(0.5f, 1f), new Vector2((i - (HighscoreModes.Length - 1) / 2f) * 190f, -18f), new Vector2(180f, 36f));
            button.onClick.AddListener(() =>
            {
                highscoreMode = mode;
                FillHighscores();
            });
            highscoreModeImages[i] = button.GetComponent<Image>();
        }

        var levels = (DifficultyLevel[])Enum.GetValues(typeof(DifficultyLevel));
        highscoreColumns = new Text[levels.Length];
        for (var i = 0; i < levels.Length; i++)
        {
            var column = new GameObject($"Column{levels[i]}", typeof(Image));
            column.transform.SetParent(highscoreView.transform, false);
            var rect = column.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2((float)i / levels.Length, 0f);
            rect.anchorMax = new Vector2((float)(i + 1) / levels.Length, 1f);
            rect.offsetMin = new Vector2(i == 0 ? 0f : 8f, 0f);
            rect.offsetMax = new Vector2(i == levels.Length - 1 ? 0f : -8f, -48f);
            column.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.03f);

            var textGo = new GameObject("Text", typeof(Text));
            textGo.transform.SetParent(column.transform, false);
            Stretch(textGo.GetComponent<RectTransform>(), new Vector2(18f, 14f), new Vector2(-14f, -16f));
            var text = textGo.GetComponent<Text>();
            text.font = font;
            text.fontSize = 18;
            text.lineSpacing = 1.05f;
            text.supportRichText = true;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = new Color(0.92f, 0.93f, 0.95f, 1f);
            text.raycastTarget = false;
            highscoreColumns[i] = text;
        }

        highscoreView.SetActive(false);
    }

    private void FillHighscores()
    {
        var levels = (DifficultyLevel[])Enum.GetValues(typeof(DifficultyLevel));
        for (var i = 0; i < levels.Length && i < highscoreColumns.Length; i++)
        {
            highscoreColumns[i].text = BuildHighscoreColumn(highscoreMode, levels[i]);
        }

        for (var i = 0; i < highscoreModeImages.Length; i++)
        {
            highscoreModeImages[i].color = HighscoreModes[i] == highscoreMode ? TabActive : TabIdle;
        }
    }

    private static string BuildHighscoreColumn(GameMode mode, DifficultyLevel level)
    {
        var sb = new StringBuilder();
        var current = level == Difficulty.Selected && mode == GameModes.Selected ? "  <size=15><color=#9FD3FF>(selected)</color></size>" : string.Empty;
        sb.Append($"<size=24><b><color=#FFDB73>{Difficulty.For(level).Label}</color></b></size>{current}\n\n");

        var entries = HighscoreTable.Load(mode, level);
        if (entries.Count == 0)
        {
            sb.Append("<i><color=#9AA3AD>No results yet.\nFinish a mission on this\ndifficulty to get on the list.</color></i>");
            return sb.ToString();
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var name = string.IsNullOrWhiteSpace(e.name) ? "Commander" : e.name.Replace('<', '(').Replace('>', ')');
            var medal = string.IsNullOrEmpty(e.medal) ? string.Empty : $"  ·  <color={MedalColor(e.medal)}>{e.medal}</color>";
            var failed = e.victory ? string.Empty : "  ·  <color=#FF9B9B>failed</color>";
            var score = e.score.ToString("N0", CultureInfo.InvariantCulture);
            sb.Append($"<b>{i + 1}.</b>  {name}\n");
            if (mode == GameMode.Endurance)
            {
                // Endurance always ends in failure; time held is the headline.
                sb.Append($"      <b>{e.seconds / 60}:{e.seconds % 60:00}</b>  ·  {score} pts{medal}  ·  <color=#9AA3AD>{FormatDate(e.dateUtc)}</color>\n");
            }
            else
            {
                sb.Append($"      <b>{score}</b>{medal}{failed}  ·  <color=#9AA3AD>{FormatDate(e.dateUtc)}</color>\n");
            }
        }

        return sb.ToString();
    }

    private static string MedalColor(string medal) => medal switch
    {
        "Gold" => "#FFD34D",
        "Silver" => "#D7DEE6",
        "Bronze" => "#E0A060",
        _ => "#FFFFFF",
    };

    private static string FormatDate(string isoUtc)
    {
        return DateTime.TryParse(isoUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
            ? date.ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static Button CreateButton(Transform parent, Font font, string label, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(label + "Button", typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.92f);

        var textGo = new GameObject("Label", typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        Stretch(textGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        var text = textGo.GetComponent<Text>();
        text.font = font;
        text.fontSize = 19;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        text.text = label;
        return go.GetComponent<Button>();
    }

    private static Scrollbar CreateScrollbar(Transform panel)
    {
        var bar = new GameObject("Scrollbar", typeof(Image), typeof(Scrollbar));
        bar.transform.SetParent(panel, false);
        var barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(1f, 0.5f);
        barRect.offsetMin = new Vector2(-40f, 84f);
        barRect.offsetMax = new Vector2(-28f, -76f);
        bar.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

        var area = new GameObject("SlidingArea", typeof(RectTransform));
        area.transform.SetParent(bar.transform, false);
        Stretch(area.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        var handle = new GameObject("Handle", typeof(Image));
        handle.transform.SetParent(area.transform, false);
        Stretch(handle.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        handle.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.45f);

        var scrollbar = bar.GetComponent<Scrollbar>();
        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        return scrollbar;
    }
}

/// <summary>The texts shown in the About window. Unity rich text: &lt;b&gt;, &lt;i&gt;, &lt;size&gt;, &lt;color&gt;.</summary>
public static class AboutContent
{
    public readonly struct Tab
    {
        public Tab(string title, Func<string> body)
        {
            Title = title;
            Body = body;
        }

        public string Title { get; }
        public Func<string> Body { get; }
    }

    private const string Head = "<size=26><b><color=#FFDB73>";
    private const string HeadEnd = "</color></b></size>";
    private const string Sub = "<size=21><b><color=#9FD3FF>";
    private const string SubEnd = "</color></b></size>";

    public static readonly Tab[] Tabs =
    {
        new("About", About),
        new("How to Play", HowToPlay),
        new("Game Concepts", Concepts),
        new("Coming Later", ComingLater),
        new("Highscores", () => string.Empty), // Drawn by AboutPanel as three columns.
    };

    public const int HighscoresTab = 4;

    private static string About() =>
        $"<size=34><b>{Application.productName}</b></size>\n" +
        $"Version {Application.version}\n" +
        $"Created by {Application.companyName}\n\n" +
        $"{Head}The story{HeadEnd}\n" +
        "Nobody volunteers for X-Con.\n\n" +
        "Officially it is a recruitment service. In practice it recruits people with a past: " +
        "ex-convicts, former smugglers and drifters who know how criminal networks really work. " +
        "The sky is no longer empty. Alien crews scout, raid and challenge us, and human smugglers " +
        "fly stolen craft of their own. It takes someone who has been on the wrong side of the law " +
        "to read them.\n\n" +
        "You are their Commander. Watch the map, read the reports, and decide how X-Con answers " +
        "each contact. The public, the permit office and your own crew are all watching.\n\n" +
        $"{Head}This demo{HeadEnd}\n" +
        "Three ways to play, chosen on the welcome screen or in the menu:\n" +
        "<b>Mission</b>  —  hold the line until the clock runs out. 15 minutes on Normal and Hard, " +
        "20 minutes on Easy (longer, but slower and more forgiving).\n" +
        "<b>Endurance</b>  —  no clock. Contacts come faster and faster. How long can you last?\n" +
        "<b>Training</b>  —  no fail and no highscores. Press U to call in a contact whenever you like.\n\n" +
        $"{Head}Credits{HeadEnd}\n" +
        $"Game design and development: {Application.companyName}\n" +
        "Made with Unity.";

    private static string HowToPlay() =>
        $"{Head}Goal{HeadEnd}\n" +
        "<b>Mission:</b> survive until the timer reaches 0:00.  <b>Endurance:</b> survive as long as you can; " +
        "every minute brings more contacts and less time to answer.  <b>Training:</b> practise freely.\n\n" +
        "In Mission and Endurance you fail if:\n" +
        "  •  <b>Permit Risk</b> reaches 100, or\n" +
        "  •  you miss too many contacts in a row (3 on Normal, 5 on Easy, 2 on Hard).\n\n" +
        $"{Head}A contact, step by step{HeadEnd}\n" +
        "1.  A UFO appears on the map.\n" +
        "2.  An <b>Analysis</b> report describes it: who is likely behind it, what they seem to want, crew size.\n" +
        "3.  A few seconds later you are asked to respond. Answer before the countdown ends, or the contact is missed.\n\n" +
        $"{Sub}Your four responses{SubEnd}\n" +
        "<b>1  Ignore</b>  —  do nothing. Fine for false alarms, costly for real threats.\n" +
        "<b>2  Track</b>  —  follow and scan it. Low risk, and it reveals what a cargo craft carries.\n" +
        "<b>3  Intercept</b>  —  shoot it down. Stops it, but a wreck can spill its cargo.\n" +
        "<b>4  Dispatch</b>  —  send the squad. Best when people are at risk or cargo should be recovered intact.\n\n" +
        $"{Head}Strategic priority{HeadEnd}\n" +
        "Choose what X-Con focuses on. You can change it at any time.\n" +
        "<b>Q  Coverage</b>  —  watch more of the sky: more contacts, more chances to score.\n" +
        "<b>W  Research</b>  —  fewer contacts, and Education grows much faster.\n" +
        "<b>E  Response</b>  —  more time to answer, and outcomes raise less Permit Risk.\n\n" +
        $"{Head}All keys{HeadEnd}\n" +
        "<b>1 – 4</b>   Answer a contact (Ignore / Track / Intercept / Dispatch)\n" +
        "<b>Q W E</b>   Strategic priority (Coverage / Research / Response)\n" +
        "<b>H</b>   Send the hazmat team to the oldest hazard site\n" +
        "<b>L</b>   Change the squad kit (Standard / Hazmat / Heavy / Recon)\n" +
        "<b>Esc</b>   Close the current message; press again for the menu\n" +
        "<b>Menu  Leave Fullscreen</b>   Exit fullscreen in the browser (or hold Esc)\n" +
        "<b>U</b>   Call in a contact (Training only)\n\n" +
        $"{Head}Score and medals{HeadEnd}\n" +
        "You score for answered contacts, coverage, Education and every second you survive. " +
        "Missed contacts and Permit Risk cost points. Bronze, Silver and Gold medals unlock at " +
        "higher scores; the HUD shows the next one. Harder difficulties multiply your score. " +
        "The Highscores list keeps your top 10 for each mode and difficulty.\n\n" +
        $"{Head}Tips{HeadEnd}\n" +
        "  •  Read the Analysis report before you answer: the segment and intent tell you a lot.\n" +
        "  •  Track when you are unsure. Intercept when behaviour turns hostile. Dispatch when civilians are at risk.\n" +
        "  •  Watch the 'Archangel Protocol Brief' posts for clues about intent.\n" +
        "  •  Clean up hazard sites quickly: they raise Permit Risk every minute they stay open.";

    private static string Concepts() =>
        $"{Head}Who is out there{HeadEnd}\n" +
        "Every contact belongs to a <b>segment</b>:\n" +
        "<b>Grey</b>  —  observers. They scout, probe and misdirect. Secure your data and avoid escalation.\n" +
        "<b>Dark</b>  —  dangerous and unpredictable. Some put on 'challenge' displays to provoke you. Keep your teams anchored.\n" +
        "<b>Green</b>  —  enforcers. Raids and coordinated pressure. Certified response teams are needed.\n" +
        "<b>Human</b>  —  people flying craft of their own, mostly criminal smugglers.\n\n" +
        "Each segment <b>escalates</b> through three stages, Probe, Commit and Overreach, " +
        "depending on how you have handled it so far.\n\n" +
        $"{Head}Your two meters{HeadEnd}\n" +
        "<b>Education</b>  —  how well trained the public and your teams are. Higher Education makes " +
        "your responses more likely to succeed. It grows from research and education bulletins.\n" +
        "<b>Permit Risk</b>  —  the political and legal heat on X-Con. Bad calls, public operations and " +
        "open hazard sites raise it. At 100 your permit is pulled and the mission ends.\n\n" +
        $"{Head}Mules: smuggler cargo craft{HeadEnd}\n" +
        "Low-tech craft with no crew, carrying goods only. The cargo may be ordinary contraband, " +
        "toxic heavy metals, drug precursor chemicals, or, rarely, a nuclear warhead.\n" +
        "  •  Track scans the hold.  •  Dispatch recovers it intact.\n" +
        "  •  Intercept may spill the cargo.  •  Ignore lets it be delivered.\n\n" +
        $"{Head}Hazard sites{HeadEnd}\n" +
        "Spilled hazardous cargo leaves a hazard site on the map. It adds Permit Risk every minute " +
        "until the hazmat team contains it (key <b>H</b>).\n\n" +
        $"{Head}Squad kits{HeadEnd}\n" +
        "Dispatch uses the squad's current kit. Switching takes 20 seconds.\n" +
        "<b>Standard</b>  —  all-rounder.\n" +
        "<b>Hazmat</b>  —  sealed suits: no exposure when recovering hazardous cargo, but weak against hostile crews.\n" +
        "<b>Heavy</b>  —  strong against Grey and Dark crews, but heavy weapons can rupture a hazardous hold.\n" +
        "<b>Recon</b>  —  extra intelligence and Education, but weak against hostile crews.\n\n" +
        $"{Head}The smuggling network{HeadEnd}\n" +
        "Each Mule you deal with gives <b>leads</b> on the people behind them. With enough leads, " +
        "the Norwegian Intelligence Agency (NIA) can identify who receives the shipments, and you decide " +
        "whether to raid them or turn them into an informant. NIA's rule: never sack the spy.";

    private static string ComingLater() =>
        "<i>Ideas for future versions of the game. These are plans, not promises, and may change.</i>\n\n" +
        $"{Head}The X-Con recruits{HeadEnd}\n" +
        "Named squad members, each with a criminal past and the skills that came with it. Ranks, " +
        "promotions and a command hierarchy.\n\n" +
        $"{Head}Bases and research{HeadEnd}\n" +
        "Build and run bases around the world. Research projects that unlock detection systems, " +
        "equipment and new ways to respond.\n\n" +
        $"{Head}Squad missions{HeadEnd}\n" +
        "Take the squad to the crash site yourself: real-time tactical missions with pause to give orders.\n\n" +
        $"{Head}More of the sky{HeadEnd}\n" +
        "  •  More alien factions, alliances and betrayals.\n" +
        "  •  New craft generations, from crewed landers to motherships.\n" +
        "  •  The Lier Basin and the exodus story.\n" +
        "  •  Weather and forecasting that affect detection.\n" +
        "  •  Blackouts: crisis events that hit minds, life, or the whole planet.\n\n" +
        $"{Head}Intelligence and politics{HeadEnd}\n" +
        "A bigger role for the NIA, spies and counter-espionage, and human factions with their own agendas.\n\n" +
        $"{Head}Playing together{HeadEnd}\n" +
        "Co-op missions where each player commands part of the squad.";
}
