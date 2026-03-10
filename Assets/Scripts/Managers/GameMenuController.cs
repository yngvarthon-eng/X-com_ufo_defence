using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameMenuController : MonoBehaviour
{
    private const string SaveSlotKey = "xcon.save.slot1";
    private const string AutoLoadPriorityPrefKey = "xcon.save.autoload.priority";
    private const string MapStylePrefKey = "xcon.worldmap.styleIndex";

    [Serializable]
    private struct SaveData
    {
        public int priority;
        public int mapStyleIndex;
        public string savedAtUtc;
    }

    private GameObject menuPanel;

    private void Start()
    {
        BuildMenuUI();
    }

    private void BuildMenuUI()
    {
        var canvasGo = new GameObject("GameMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var menuToggle = CreateButton(canvasGo.transform, "MenuToggleButton", "Menu", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(140f, 40f));
        menuToggle.onClick.AddListener(ToggleMenuPanel);

        menuPanel = new GameObject("GameMenuPanel", typeof(Image));
        menuPanel.transform.SetParent(canvasGo.transform, false);
        var panelImage = menuPanel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);

        var panelRect = menuPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-20f, -70f);
        panelRect.sizeDelta = new Vector2(280f, 250f);

        var newGameButton = CreateButton(menuPanel.transform, "NewGameButton", "New Game", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(240f, 42f));
        newGameButton.onClick.AddListener(NewGame);

        var loadButton = CreateButton(menuPanel.transform, "LoadButton", "Load", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(240f, 42f));
        loadButton.onClick.AddListener(LoadGame);

        var saveButton = CreateButton(menuPanel.transform, "SaveButton", "Save", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -134f), new Vector2(240f, 42f));
        saveButton.onClick.AddListener(SaveGame);

        var quitButton = CreateButton(menuPanel.transform, "QuitButton", "Quit", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(240f, 42f));
        quitButton.onClick.AddListener(QuitGame);

        menuPanel.SetActive(false);
    }

    private static Button CreateButton(Transform parent, string objectName, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
    {
        var go = new GameObject(objectName, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.92f);

        var button = go.GetComponent<Button>();

        var textGo = new GameObject("Label", typeof(Text));
        textGo.transform.SetParent(go.transform, false);

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textGo.GetComponent<Text>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.font = font;
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        text.text = label;

        return button;
    }

    private void ToggleMenuPanel()
    {
        if (menuPanel == null)
        {
            return;
        }

        menuPanel.SetActive(!menuPanel.activeSelf);
    }

    private void SaveGame()
    {
        var priority = GameManager.Instance != null ? (int)GameManager.Instance.CurrentPriority : (int)GameManager.StrategicPriority.Coverage;
        var mapStyle = PlayerPrefs.GetInt(MapStylePrefKey, 0);

        var data = new SaveData
        {
            priority = priority,
            mapStyleIndex = mapStyle,
            savedAtUtc = DateTime.UtcNow.ToString("O")
        };

        var json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SaveSlotKey, json);
        PlayerPrefs.Save();

        Debug.Log($"[Menu] Game saved. {json}");
    }

    private void LoadGame()
    {
        if (!PlayerPrefs.HasKey(SaveSlotKey))
        {
            Debug.LogWarning("[Menu] No save game found.");
            return;
        }

        var json = PlayerPrefs.GetString(SaveSlotKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[Menu] Save slot is empty.");
            return;
        }

        var data = JsonUtility.FromJson<SaveData>(json);
        PlayerPrefs.SetInt(AutoLoadPriorityPrefKey, data.priority);
        PlayerPrefs.SetInt(MapStylePrefKey, data.mapStyleIndex);
        PlayerPrefs.Save();

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void NewGame()
    {
        PlayerPrefs.DeleteKey(SaveSlotKey);
        PlayerPrefs.DeleteKey(AutoLoadPriorityPrefKey);
        PlayerPrefs.DeleteKey(MapStylePrefKey);
        PlayerPrefs.Save();

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
