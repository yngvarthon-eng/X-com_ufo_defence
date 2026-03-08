using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class WorldMapBackgroundManager : MonoBehaviour
{
    [Header("World Map Sprites")]
    [SerializeField] private Sprite roundSphereMap;
    [SerializeField] private Sprite flatOvalMap;
    [SerializeField] private Sprite flatMap;

    [Header("UI Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Dropdown mapDropdown;

    private Dictionary<int, Sprite> mapOptions;

    void Awake()
    {
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
            mapDropdown.onValueChanged.AddListener(OnMapDropdownChanged);
        }

        SetMapBackground(0);
    }

    private void OnMapDropdownChanged(int index)
    {
        SetMapBackground(index);
    }

    private void SetMapBackground(int index)
    {
        if (backgroundImage != null && mapOptions.ContainsKey(index))
        {
            backgroundImage.sprite = mapOptions[index];
        }
    }
}
