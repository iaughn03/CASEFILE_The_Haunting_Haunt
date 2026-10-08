using UnityEngine;
using UnityEngine.UI;

// Sits on the SettingMenu root. Its "RESET TO DEFAULT" button already resets audio and brightness
// (wired to PauseManager / MainMenu in each scene); this adds graphics, display, mouse and keybinds.
public class SettingsResetHook : MonoBehaviour
{
    public Button resetButton;

    private void Awake()
    {
        if (resetButton == null)
        {
            var t = transform.Find("ResetButton");
            if (t != null) resetButton = t.GetComponent<Button>();
        }
        if (resetButton != null) resetButton.onClick.AddListener(ResetAll);
    }

    public void ResetAll()
    {
        GameSettings.ResetToDefaults();
        GameKeys.ResetToDefaults();
        SettingsPageBuilder.RefreshEverything();
    }
}
