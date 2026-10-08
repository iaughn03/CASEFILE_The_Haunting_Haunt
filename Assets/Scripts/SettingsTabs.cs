using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Drives the Settings menu's left-hand tab column: clicking a tab shows only that tab's page and
// restyles the tab buttons (selected look + label color).
public class SettingsTabs : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        public Button button;
        public Image background;
        public TMP_Text label;
        public GameObject page;
        [Tooltip("Optional - when set, selected/normal colors go through the hover component so hover feedback doesn't fight them.")]
        public ButtonHoverPunch punch;
    }

    public Tab[] tabs;
    [Tooltip("Optional sprite swap. Leave both empty when the selected look comes from tint/color alone.")]
    public Sprite normalSprite;
    public Sprite selectedSprite;
    public Color normalTextColor = new Color(0.65f, 0.5f, 0.28f, 1f);
    public Color selectedTextColor = new Color(1f, 0.85f, 0.5f, 1f);
    public Color normalPlateTint = Color.white;
    public Color selectedPlateTint = Color.white;

    private bool wired;
    private int currentIndex;

    private void Awake()
    {
        if (wired || tabs == null) return;
        wired = true;

        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            if (tabs[i].button != null) tabs[i].button.onClick.AddListener(() => Select(index));
        }
    }

    private void OnEnable()
    {
        Select(currentIndex);
    }

    public void Select(int index)
    {
        if (tabs == null) return;
        currentIndex = index;

        for (int i = 0; i < tabs.Length; i++)
        {
            bool isSelected = i == index;
            var tab = tabs[i];

            if (tab.page != null) tab.page.SetActive(isSelected);

            if (tab.background != null && normalSprite != null && selectedSprite != null)
                tab.background.sprite = isSelected ? selectedSprite : normalSprite;

            Color plate = isSelected ? selectedPlateTint : normalPlateTint;
            Color text = isSelected ? selectedTextColor : normalTextColor;

            if (tab.punch != null)
            {
                tab.punch.SetBaseColors(plate, text);
            }
            else
            {
                if (tab.background != null) tab.background.color = plate;
                if (tab.label != null) tab.label.color = text;
            }
        }
    }
}
