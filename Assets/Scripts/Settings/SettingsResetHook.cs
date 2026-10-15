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
        // APPLY button + confirmation, on every scene's Settings without touching the scenes
        if (GetComponent<SettingsApplyBar>() == null) gameObject.AddComponent<SettingsApplyBar>();
    }

    public void ResetAll()
    {
        // only edits the values; they take effect when APPLY is pressed
        GameSettings.ResetToDefaults(false);
        GameKeys.ResetToDefaults(false);
        SettingsPageBuilder.RefreshEverything();
    }
}
