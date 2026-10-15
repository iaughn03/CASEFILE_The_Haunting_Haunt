using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

public class PauseManager : MonoBehaviour
{
    public static bool isPaused = false;
    public static bool isBagOpen = false;
    public static bool isCaseUiOpen = false;

    // Called by the case panels (signature compare, ruling) so the HUD hides while they are up and the
    // right state (gameplay or bag) is restored when they close.
    public void SetCaseUiOpen(bool open)
    {
        isCaseUiOpen = open;
        UpdateGameState();
    }

    [Header("Menu Panels")]
    public GameObject pauseMenuUI;
    public GameObject settingsMenuUI;
    public GameObject confirmationUI;
    [Tooltip("\"Are you sure?\" dialog shown before Restart reloads the level. Empty = Restart reloads straight away.")]
    public GameObject restartConfirmationUI;
    public GameObject bagMenuUI;
    public GameObject taskbarMenuUI;
    public GameObject controlsMenuUI;
    [Tooltip("The Guide (glossary) panel opened from the pause menu's Guide button.")]
    public GameObject codexUI;

    [Header("HUD Elements")]
    public GameObject hotbarUI;
    public GameObject batteryUI;
    public GameObject batteryBackgroundUI;
    public GameObject objectivePanelUI;
    [Tooltip("PlayerHealthBarBackground - was never hidden while paused, so it kept showing through the pause menu, settings, and Codex panels.")]
    public GameObject healthBarUI;
    [Tooltip("PlayerEnergyBarBackground - same issue as healthBarUI, kept showing through the pause menu and Codex panel.")]
    public GameObject energyBarUI;

    [Header("Equipment Objects")]
    public GameObject flashlightObject;
    public GameObject handKeyObject;

    [Header("Inventory Logic")]
    public int currentSlot = 0;
    public int totalSlots = 4;

    [Header("Master Volume Mixer")]
    [Tooltip("The project's AudioMixer. Master Volume also drives its exposed volume parameter in dB (0-1 mapped with Log10). AudioListener.volume is still set as well, because only a few AudioSources are routed through this mixer.")]
    public AudioMixer mainMixer;
    public string mixerVolumeParameter = "MasterVolume";

    [Header("Audio Settings")]
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("Brightness Settings")]
    public Slider brightnessSlider;
    [Tooltip("Full-screen dim. It lives on its own sorting layer above every panel, so lowering brightness darkens the menus and the Settings screen too, not just the world.")]
    public Image brightnessOverlay;

    [Header("Percentage Text")]
    public TMP_Text masterVolumeText;
    public TMP_Text musicVolumeText;
    public TMP_Text sfxVolumeText;
    public TMP_Text brightnessText;

    private HotbarManager hotbar;

    [Header("Extra UI")]
    public GameObject crosshairUI;
    public GameObject notePanelUI;

    private void OnDestroy()
    {
        AudioListener.pause = false;
    }

    private void Start()
    {
        hotbar = FindFirstObjectByType<HotbarManager>();

        currentSlot = 0;

        if (flashlightObject != null) flashlightObject.SetActive(false);
        if (handKeyObject != null) handKeyObject.SetActive(false);

        if (confirmationUI != null) confirmationUI.SetActive(false);
        if (bagMenuUI != null) bagMenuUI.SetActive(false);
        if (taskbarMenuUI != null) taskbarMenuUI.SetActive(false);
        if (controlsMenuUI != null) controlsMenuUI.SetActive(false);

        float masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
        float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        float brightness = PlayerPrefs.GetFloat("Brightness", 0.7f);

        AudioListener.volume = masterVolume;

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = masterVolume;
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = musicVolume;
            musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = sfxVolume;
            sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);
        }

        if (brightnessSlider != null)
        {
            brightnessSlider.value = brightness;
            brightnessSlider.onValueChanged.AddListener(v => SettingsApplyBar.ShowPercent(brightnessText, v));
        }

        SetMasterVolume(masterVolume);
        SetMusicVolume(musicVolume);
        SetSFXVolume(sfxVolume);
        SetBrightness(brightness);

        isPaused = false;
        isBagOpen = false;
        isCaseUiOpen = false;

        UpdateGameState();
        UpdateEquipment();
    }

    private void Update()
    {
        // A keybind button is waiting for a key: that key (Esc included) belongs to the rebind, not
        // to the menus - otherwise the Esc that cancels a rebind would close Settings too.
        if (SettingsPageBuilder.IsCapturingKey) return;
        // Same for the Settings confirmation dialog: its Esc means "keep editing", nothing else.
        if (SettingsApplyBar.OwnsEscape) return;
        // The Case File sits over the bag, the pause menu or the Final Inspection and handles its own
        // Esc; the bag key must not shut the bag out from under it either.
        if (CaseFileUI.IsOpen) return;

        if (GameKeys.Down(GameKeys.Action.EvidenceBag))
        {
            // No opening the bag from inside a locker: InventoryToggle already refused, so the two
            // fell out of step, the locker kept hiding BagPanel every frame, and the game froze
            // behind an invisible bag. Closing is still allowed so a stuck bag can always be shut.
            if (!isPaused && !isCaseUiOpen && (isBagOpen || !IsPlayerHiding()))
            {
                ToggleBag();
            }
        }

        // Esc and gamepad B back out of the quit dialog - they always mean "No", never "quit".
        // Handled here rather than on the dialog so it can't race with the pause toggle below.
        if (confirmationUI != null && confirmationUI.activeSelf &&
            (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1)))
        {
            PlayMenuClick();
            CloseConfirmation();
            return;
        }

        // Same for the restart dialog: Esc means "No".
        if (restartConfirmationUI != null && restartConfirmationUI.activeSelf &&
            (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1)))
        {
            PlayMenuClick();
            CloseRestartConfirmation();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // The Final Inspection owns the screen; Esc must not open the pause menu behind it.
            if (isCaseUiOpen) return;

            // Opening/closing a menu by key gets the same click a menu button would.
            PlayMenuClick();

            if (NoteSystem.Instance != null &&
                NoteSystem.Instance.IsReading)
            {
                if (isPaused)
                    Resume();
                else
                    Pause();

                return;
            }

            if (isBagOpen)
            {
                ToggleBag();
            }
            else if (isPaused && settingsMenuUI != null && settingsMenuUI.activeSelf)
            {
                // Esc in Settings works like its Back button (and asks about unapplied changes).
                CloseSettings();
            }
            else
            {
                if (isPaused)
                    Resume();
                else
                    Pause();
            }
        }
    }

    public void ToggleBag()
    {
        isBagOpen = !isBagOpen;

        // The detail panel and the borrowed 3D item are not children of bagMenuUI, so hiding the bag
        // alone left an open inspection floating over gameplay with the item still parented to the
        // inspection stage. Clean up before the panels go away, while the lookups can still find it.
        if (!isBagOpen) CloseItemInspection();

        if (bagMenuUI != null)
            bagMenuUI.SetActive(isBagOpen);

        if (taskbarMenuUI != null)
            taskbarMenuUI.SetActive(isBagOpen);

        UpdateGameState();

        if (isBagOpen && taskbarMenuUI != null)
        {
            TaskbarUI uiScript = taskbarMenuUI.GetComponent<TaskbarUI>();

            if (uiScript != null)
                uiScript.RefreshTaskbarData();
        }
    }

    private PlayerHideState hideState;

    // PlayerHideState is only added the first time the player nears a hiding spot, so keep
    // retrying the lookup until it exists.
    private bool IsPlayerHiding()
    {
        if (hideState == null) hideState = FindFirstObjectByType<PlayerHideState>();
        return hideState != null && hideState.IsHiding;
    }

    private DescriptionPanel descriptionPanel;

    // Mirrors InventoryToggle's and DeductionGate's close cleanup: hide the item detail panel and
    // hand the borrowed world object back to where it came from.
    private void CloseItemInspection()
    {
        if (descriptionPanel == null)
            descriptionPanel = FindFirstObjectByType<DescriptionPanel>(FindObjectsInactive.Include);

        if (descriptionPanel != null) descriptionPanel.Hide();

        if (ItemInspection3D.Instance != null) ItemInspection3D.Instance.StopInspecting();
    }

    private UIClickSound clickSound;

    private void PlayMenuClick()
    {
        if (clickSound == null) clickSound = FindFirstObjectByType<UIClickSound>(FindObjectsInactive.Include);
        if (clickSound != null) clickSound.PlayClick();
    }

   private void UpdateGameState()
{
    if (isPaused)
    {
        // Pause Menu Open - game sounds freeze too. UI click sounds opt out via ignoreListenerPause.
        Time.timeScale = 0f;
        AudioListener.pause = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Hide HUD while paused
        if (hotbarUI != null)
            hotbarUI.SetActive(false);

        if (batteryUI != null)
            batteryUI.SetActive(false);

        if (batteryBackgroundUI != null)
            batteryBackgroundUI.SetActive(false);

        if (objectivePanelUI != null)
            objectivePanelUI.SetActive(false);

        if (healthBarUI != null)
            healthBarUI.SetActive(false);

        if (energyBarUI != null)
            energyBarUI.SetActive(false);
    }
    else if (isCaseUiOpen)
    {
        // Full-screen case panel (signature compare, ruling) - nothing of the HUD may show through it.
        Time.timeScale = 0f;
        AudioListener.pause = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (hotbarUI != null) hotbarUI.SetActive(false);
        if (batteryUI != null) batteryUI.SetActive(false);
        if (batteryBackgroundUI != null) batteryBackgroundUI.SetActive(false);
        if (objectivePanelUI != null) objectivePanelUI.SetActive(false);
        if (healthBarUI != null) healthBarUI.SetActive(false);
        if (energyBarUI != null) energyBarUI.SetActive(false);
    }
    else if (isBagOpen)
    {
        // Bag Open
        Time.timeScale = 0f;
        AudioListener.pause = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Keep the hotbar and flashlight battery meter visible while the bag is open.
        if (hotbarUI != null)
            hotbarUI.SetActive(true);

        if (batteryUI != null)
            batteryUI.SetActive(true);

        if (batteryBackgroundUI != null)
            batteryBackgroundUI.SetActive(true);

        // The task panel (evidence count and hint) belongs to the bag screen only.
        if (objectivePanelUI != null)
            objectivePanelUI.SetActive(true);

        if (healthBarUI != null)
            healthBarUI.SetActive(true);

        if (energyBarUI != null)
            energyBarUI.SetActive(true);
    }
    else
    {
        // Normal Gameplay
        Time.timeScale = 1f;
        AudioListener.pause = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Tasks show with the bag only, never in play or on the way out of a menu.
        if (objectivePanelUI != null)
            objectivePanelUI.SetActive(false);

        // Resuming inside a locker: the locker keeps the HUD hidden and restores it on the way out,
        // so turning it back on here only flashed it over the locker view.
        if (IsPlayerHiding()) return;

        if (hotbarUI != null)
            hotbarUI.SetActive(true);

        if (batteryUI != null)
            batteryUI.SetActive(true);

        if (batteryBackgroundUI != null)
            batteryBackgroundUI.SetActive(true);

        if (healthBarUI != null)
            healthBarUI.SetActive(true);

        if (energyBarUI != null)
            energyBarUI.SetActive(true);
    }
}
    // Hotbar selection and the held-key visual belong to PlayerInteract (keys 1-4 / scroll). The
    // flashlight is always carried (no hotbar slot) and F toggles it.
    private void UpdateEquipment()
    {
        if (flashlightObject != null)
            flashlightObject.SetActive(true);

        if (handKeyObject != null)
            handKeyObject.SetActive(false);
    }

    public void SetMasterVolume(float volume)
    {
        AudioListener.volume = volume;

        if (mainMixer != null)
            mainMixer.SetFloat(mixerVolumeParameter, Mathf.Log10(Mathf.Max(0.0001f, volume)) * 20f);

        if (masterVolumeText != null)
            masterVolumeText.text = Mathf.RoundToInt(volume * 100) + "%";

        PlayerPrefs.SetFloat("MasterVolume", volume);
        PlayerPrefs.Save();
    }

    public void SetMusicVolume(float volume)
    {
        // MusicManager's crossfade sets its AudioSources' volume every frame, so poking an
        // AudioSource directly here would just get overwritten - MusicVolume.Multiplier is the
        // shared value its crossfade actually reads (same pattern as SfxVolume.Multiplier).
        MusicVolume.Multiplier = volume;

        if (musicVolumeText != null)
            musicVolumeText.text = Mathf.RoundToInt(volume * 100) + "%";
    }

    public void SetSFXVolume(float volume)
    {
        SfxVolume.Multiplier = volume;

        if (sfxVolumeText != null)
            sfxVolumeText.text = Mathf.RoundToInt(volume * 100) + "%";

        PlayerPrefs.SetFloat("SFXVolume", volume);
        PlayerPrefs.Save();
    }

    public void SetBrightness(float value)
    {
        if (brightnessOverlay != null)
        {
            Color c = brightnessOverlay.color;
            // The scene looks as authored at the default (0.7) and darkens below it - menus and the
            // Settings screen included. Capped so 0% never blacks out the screen the slider is on.
            c.a = Mathf.Clamp(0.7f - value, 0f, 0.7f);
            brightnessOverlay.color = c;
        }

        if (brightnessText != null)
            brightnessText.text = Mathf.RoundToInt(value * 100) + "%";

        PlayerPrefs.SetFloat("Brightness", value);
        PlayerPrefs.Save();
    }

    // Volumes reset right away; brightness only moves its slider (it takes effect on APPLY).
    public void ResetSettings()
    {
        SetMasterVolume(1f);
        SetMusicVolume(1f);
        SetSFXVolume(1f);

        if (masterVolumeSlider != null)
            masterVolumeSlider.value = 1f;

        if (musicVolumeSlider != null)
            musicVolumeSlider.value = 1f;

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.value = 1f;

        if (brightnessSlider != null)
            brightnessSlider.value = 0.7f;
    }

    // Volume sliders apply as they move. Brightness only updates its percentage (see Start) until
    // SettingsApplyBar applies or reverts it.
    public bool BrightnessPending => SettingsApplyBar.SliderDiffers(brightnessSlider, PlayerPrefs.GetFloat("Brightness", 0.7f));

    public void ApplyPendingBrightness()
    {
        if (brightnessSlider != null) SetBrightness(brightnessSlider.value);
    }

    public void RevertPendingBrightness()
    {
        SettingsApplyBar.ShowSlider(brightnessSlider, brightnessText, PlayerPrefs.GetFloat("Brightness", 0.7f));
    }

    public void OpenControls()
    {
        if (settingsMenuUI != null)
            settingsMenuUI.SetActive(false);

        if (controlsMenuUI != null)
            controlsMenuUI.SetActive(true);
    }

    public void CloseControls()
    {
        if (controlsMenuUI != null)
            controlsMenuUI.SetActive(false);

        if (settingsMenuUI != null)
            settingsMenuUI.SetActive(true);
    }

    public void Resume()
    {
        isPaused = false;
        AudioListener.pause = false;

        pauseMenuUI.SetActive(false);
        if (codexUI != null) codexUI.SetActive(false);

        if (NoteSystem.Instance != null &&
            NoteSystem.Instance.IsReading)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        UpdateGameState();
    }

    public void Pause()
    {
        isPaused = true;

        pauseMenuUI.SetActive(true);
        pauseMenuUI.transform.SetAsLastSibling();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UpdateGameState();
    }

    public void OpenConfirmation()
    {
        pauseMenuUI.SetActive(false);
        confirmationUI.SetActive(true);
        confirmationUI.transform.SetAsLastSibling();

        // Still paused: keeps timeScale at 0 and the gameplay HUD hidden behind the dialog.
        UpdateGameState();
    }

    public void CloseConfirmation()
    {
        confirmationUI.SetActive(false);
        pauseMenuUI.SetActive(true);
        pauseMenuUI.transform.SetAsLastSibling();
        UpdateGameState();
    }

    public void OpenSettings()
    {
        pauseMenuUI.SetActive(false);
        settingsMenuUI.SetActive(true);

        // Already hidden by the paused state; kept explicit so the Evidence HUD can't show through Settings.
        if (objectivePanelUI != null)
            objectivePanelUI.SetActive(false);
    }

    public void CloseSettings()
    {
        // Unapplied changes: ask to apply or discard first; the dialog comes back here once answered.
        if (SettingsApplyBar.InterceptClose(settingsMenuUI, CloseSettings)) return;
        settingsMenuUI.SetActive(false);
        pauseMenuUI.SetActive(true);
    }

    public void OpenCodex()
    {
        if (codexUI == null) return;
        pauseMenuUI.SetActive(false);
        codexUI.SetActive(true);
        codexUI.transform.SetAsLastSibling();
    }

    public void CloseCodex()
    {
        if (codexUI != null) codexUI.SetActive(false);
        pauseMenuUI.SetActive(true);
        pauseMenuUI.transform.SetAsLastSibling();
    }

    public void ConfirmQuit()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene("MainMenu");
    }

    // The pause menu's Restart button opens this first, since a restart throws away every clue
    // collected so far. Yes calls RestartLevel, No calls CloseRestartConfirmation.
    public void OpenRestartConfirmation()
    {
        if (restartConfirmationUI == null) { RestartLevel(); return; }
        pauseMenuUI.SetActive(false);
        restartConfirmationUI.SetActive(true);
        restartConfirmationUI.transform.SetAsLastSibling();
        UpdateGameState();
    }

    public void CloseRestartConfirmation()
    {
        if (restartConfirmationUI != null) restartConfirmationUI.SetActive(false);
        pauseMenuUI.SetActive(true);
        pauseMenuUI.transform.SetAsLastSibling();
        UpdateGameState();
    }

    // Restart from the pause menu. Unpausing the AudioListener matters here: it is paused while the
    // menu is up and stays paused across a scene load, which would leave the fresh level silent.
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}