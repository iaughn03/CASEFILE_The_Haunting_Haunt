using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
using System.Collections;

public class MainMenu : MonoBehaviour
{
    [Header("Menu Panels")]
    public GameObject mainButtons;
    public GameObject settingsPanel;
    [Tooltip("Glossary of legal terms (Homicide/Murder/Parricide) - purely reference, doesn't gate or unlock anything.")]
    public GameObject codexPanel;
    [Tooltip("Full-screen How to Play guide on SettingsCanvas: goal, controls, the ghost, evidence and closing the case.")]
    public GameObject howToPlayPanel;

    [Header("Audio Settings")]
    public AudioMixer mainMixer;
    public Slider volumeSlider;
    [Tooltip("Controls MusicVolume.Multiplier, the same shared multiplier MusicManager's crossfade reads in gameplay scenes - so this slider stays in sync whichever scene it's set from.")]
    public Slider musicVolumeSlider;
    [Tooltip("Controls SfxVolume.Multiplier, which every one-shot sound effect in the project already reads from.")]
    public Slider sfxVolumeSlider;

    [Header("Brightness")]
    [Tooltip("The post-processing Volume whose exposure this adjusts - lets players compensate for a dark horror scene without the game actually being brighter, since it only affects display exposure.")]
    public Volume postProcessVolume;
    public Slider brightnessSlider;
    [Tooltip("Full-screen black image on a canvas above every other menu canvas, so lowering brightness darkens the menus and the Settings screen too, not just the 3D room.")]
    public Image brightnessOverlay;
    private const string BRIGHTNESS_PREF_KEY = "Brightness";
    private const float DEFAULT_BRIGHTNESS = 0.7f;

    [Header("Percentage Text - mirrors PauseManager's in-game settings panel")]
    public TMP_Text masterVolumeText;
    public TMP_Text musicVolumeText;
    public TMP_Text sfxVolumeText;
    public TMP_Text brightnessText;

    [Header("Play Transition - cinematic push into black")]
    [Tooltip("Full-screen black CanvasGroup that fades in while the camera pushes in, selling the sense of falling into the paper before the next scene loads.")]
    public CanvasGroup transitionFade;
    public Camera menuCamera;
    [Tooltip("Seconds for the zoom-and-fade push before the next scene starts loading.")]
    public float pushInDuration = 0.6f;
    [Tooltip("Field of view the camera zooms down to - smaller number = tighter zoom.")]
    public float pushInTargetFOV = 8f;
    [Tooltip("Black bars that slide in from the top and bottom edges for a widescreen/cinematic feel.")]
    public RectTransform letterboxTop;
    public RectTransform letterboxBottom;
    [Tooltip("Height each letterbox bar grows to, in canvas pixels.")]
    public float letterboxHeight = 140f;
    [Tooltip("What the camera actually zooms in on - the clipboard. Without this, narrowing the FOV just magnifies whatever was already at the center of the original establishing shot, which doesn't line up with the clipboard unless the camera happened to be framed perfectly on it beforehand.")]
    public Transform pushTarget;
    [Tooltip("The clipboard's own dolly-zoom controller. Its Update() re-asserts the camera's settled rotation/position every single frame once the initial click-in dolly finishes, which fights this script's own rotation lerp for the same camera - has to be disabled for the duration of the Play-button push, or the two scripts flicker between each other's rotation each frame.")]
    public MenuZoom menuZoom;

    private const string MIXER_PARAMETER = "MasterVolume";
    // Shared with PauseManager's in-game settings panel so a volume/brightness change made in either
    // the Main Menu or in gameplay carries over to the other instead of silently resetting.
    private const string VOLUME_PREF_KEY = "MasterVolume";

    private bool isTransitioning = false;

    private void Awake()
    {
        // Unpause everything before anything else initializes
        Time.timeScale = 1f;
        AudioListener.pause = false;
        // PauseManager (in gameplay scenes) writes straight to this global, static multiplier via its
        // own separate "MasterVolume" PlayerPrefs key - unrelated to this script's mixer-based
        // "SavedMasterVolume" slider. If a previous session left it lowered, it silences the main menu
        // too since nothing here ever restored it. Re-derive it from the same PlayerPrefs key
        // PauseManager reads/writes so returning to the main menu doesn't stay muted.
        AudioListener.volume = PlayerPrefs.GetFloat("MasterVolume", 1f);
    }

    private void Start()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        // Force Audio Source components in the scene to wake up
        AudioSource[] allAudioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (AudioSource source in allAudioSources)
        {
            if (!source.isPlaying && source.playOnAwake)
            {
                source.Play();
            }
        }

        if (mainMixer != null && volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveListener(SetVolume);
            float savedVolume = PlayerPrefs.GetFloat(VOLUME_PREF_KEY, 1.0f);
            volumeSlider.value = savedVolume;
            ApplyVolumeToMixer(savedVolume);
            UpdatePercentText(masterVolumeText, savedVolume);
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveListener(SetMusicVolume);
            musicVolumeSlider.value = MusicVolume.Multiplier;
            UpdatePercentText(musicVolumeText, MusicVolume.Multiplier);
            musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveListener(SetSfxVolume);
            sfxVolumeSlider.value = SfxVolume.Multiplier;
            UpdatePercentText(sfxVolumeText, SfxVolume.Multiplier);
            sfxVolumeSlider.onValueChanged.AddListener(SetSfxVolume);
        }

        if (brightnessSlider != null)
        {
            brightnessSlider.onValueChanged.RemoveListener(SetBrightness);
            float savedBrightness = PlayerPrefs.GetFloat(BRIGHTNESS_PREF_KEY, 0.7f);
            brightnessSlider.value = savedBrightness;
            ApplyBrightness(savedBrightness);
            UpdatePercentText(brightnessText, savedBrightness);
            brightnessSlider.onValueChanged.AddListener(SetBrightness);
        }

    }

    private static void UpdatePercentText(TMP_Text text, float value)
    {
        if (text != null) text.text = Mathf.RoundToInt(value * 100) + "%";
    }

    public void SetVolume(float sliderValue)
    {
        ApplyVolumeToMixer(sliderValue);
        AudioListener.volume = sliderValue;
        UpdatePercentText(masterVolumeText, sliderValue);
        PlayerPrefs.SetFloat(VOLUME_PREF_KEY, sliderValue);
        PlayerPrefs.Save();
    }

    // MusicVolume.Multiplier persists itself (see MusicVolume.cs) under the same key gameplay's
    // MusicManager crossfade reads, so setting it here is all that's needed to keep both in sync.
    public void SetMusicVolume(float sliderValue)
    {
        MusicVolume.Multiplier = sliderValue;
        UpdatePercentText(musicVolumeText, sliderValue);
    }

    private void ApplyVolumeToMixer(float value)
    {
        if (mainMixer == null) return;

        // Convert linear 0-1 to logarithmic -80 to 0dB
        float volumeInDb = Mathf.Log10(Mathf.Max(0.0001f, value)) * 20;

        mainMixer.SetFloat(MIXER_PARAMETER, volumeInDb);
    }

    // SfxVolume.Multiplier already persists itself (see SfxVolume.cs) and every one-shot SFX call
    // site in the project reads it directly, so this just needs to set the value.
    public void SetSfxVolume(float sliderValue)
    {
        SfxVolume.Multiplier = sliderValue;
        UpdatePercentText(sfxVolumeText, sliderValue);
    }

    // Adjusts display exposure only (not actual scene lighting), so players can compensate for a dark
    // horror scene on their specific monitor without changing how the game is actually lit for anyone
    // else. Slider is 0-1 with 0.5 as neutral (no shift); moving it maps to roughly -2..+2 EV.
    public void SetBrightness(float sliderValue)
    {
        ApplyBrightness(sliderValue);
        UpdatePercentText(brightnessText, sliderValue);
        PlayerPrefs.SetFloat(BRIGHTNESS_PREF_KEY, sliderValue);
        PlayerPrefs.Save();
    }

    // Mirrors PauseManager.ResetSettings() - same defaults (1.0 for every volume, 0.7 for brightness).
    public void ResetSettings()
    {
        SetVolume(1f);
        SetMusicVolume(1f);
        SetSfxVolume(1f);
        SetBrightness(0.7f);

        if (volumeSlider != null) volumeSlider.value = 1f;
        if (musicVolumeSlider != null) musicVolumeSlider.value = 1f;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = 1f;
        if (brightnessSlider != null) brightnessSlider.value = 0.7f;
    }

    private void ApplyBrightness(float sliderValue)
    {
        // The menu looks as authored at the default and darkens below it (never fully black, so the
        // slider can still be found and raised again).
        if (brightnessOverlay != null)
        {
            Color c = brightnessOverlay.color;
            c.a = Mathf.Clamp(DEFAULT_BRIGHTNESS - sliderValue, 0f, 0.7f);
            brightnessOverlay.color = c;
        }

        if (postProcessVolume == null || postProcessVolume.profile == null) return;
        if (postProcessVolume.profile.TryGet<ColorAdjustments>(out var colorAdjustments))
        {
            colorAdjustments.postExposure.value = (sliderValue - 0.5f) * 4f;
        }
    }

    // --- Navigation & Game Logic ---
    public void OpenSettings()
    {
        HideMainButtons();
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        ShowMainButtons();
    }

    public void OpenCodex()
    {
        HideMainButtons();
        if (codexPanel != null) codexPanel.SetActive(true);
    }

    public void CloseCodex()
    {
        if (codexPanel != null) codexPanel.SetActive(false);
        ShowMainButtons();
    }

    public void OpenHowToPlay()
    {
        HideMainButtons();
        if (howToPlayPanel != null) howToPlayPanel.SetActive(true);
    }

    public void CloseHowToPlay()
    {
        if (howToPlayPanel != null) howToPlayPanel.SetActive(false);
        ShowMainButtons();
    }

    // A clicked word stays the EventSystem's selected object, and a selected Button plays its
    // "Selected" state instead of "Highlighted", so it stopped growing on hover once you came back
    // from its page. Clearing the selection before the words reappear, and restarting their
    // Animators, puts every word back to a clean hoverable state (same fix as MapMainMenu.Start).
    private void HideMainButtons()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (mainButtons != null) mainButtons.SetActive(false);
    }

    private void ShowMainButtons()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (mainButtons == null) return;
        mainButtons.SetActive(true);
        foreach (var animator in mainButtons.GetComponentsInChildren<Animator>(true))
        {
            animator.Rebind();
            animator.Update(0f);
        }
    }

    public void PlayGame()
    {
        if (isTransitioning) return;
        StartCoroutine(PushIntoPageThenLoad("MapMainMenu"));
    }

    IEnumerator PushIntoPageThenLoad(string sceneName)
    {
        isTransitioning = true;
        yield return StartCoroutine(PushIntoPage());
        yield return StartCoroutine(LoadLevelAsync(sceneName));
    }

    // Sells "diving into the page" as an actual cinematic beat rather than a flat fade: the camera's
    // FOV rapidly narrows (an optical zoom, not a physical move, so there's no risk of pushing the
    // camera through geometry), black letterbox bars slide in from the top and bottom, and the screen
    // fades to black - all landing together right before the cut to the next scene.
    IEnumerator PushIntoPage()
    {
        if (menuCamera == null) menuCamera = Camera.main;
        if (menuZoom == null) menuZoom = FindFirstObjectByType<MenuZoom>();
        // Stop MenuZoom's Update() from re-snapping the camera back to its settled dolly pose every
        // frame - it would otherwise fight this coroutine's own rotation lerp on the exact same
        // transform and the two would flicker between each other's values each frame.
        if (menuZoom != null) menuZoom.enabled = false;
        float startFOV = menuCamera != null ? menuCamera.fieldOfView : 60f;
        Quaternion startRot = menuCamera != null ? menuCamera.transform.rotation : Quaternion.identity;
        // Narrowing FOV alone zooms toward whatever was already dead-center in the establishing shot -
        // that's only the clipboard if the camera happened to be framed perfectly on it beforehand,
        // which it wasn't (the zoom was landing below the clipboard instead). Slerping rotation toward
        // actually looking at pushTarget over the same duration means the zoom converges on the
        // clipboard regardless of the camera's exact starting aim.
        Quaternion targetRot = startRot;
        if (menuCamera != null && pushTarget != null)
        {
            Vector3 toTarget = pushTarget.position - menuCamera.transform.position;
            if (toTarget.sqrMagnitude > 0.0001f) targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        }

        float t = 0f;
        while (t < pushInDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / pushInDuration);
            float easedZoom = p * p * p; // accelerates hard into the page
            float easedBars = 1f - (1f - p) * (1f - p); // ease-out - bars settle rather than slam in
            if (menuCamera != null)
            {
                menuCamera.fieldOfView = Mathf.Lerp(startFOV, pushInTargetFOV, easedZoom);
                menuCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, easedZoom);
            }
            if (transitionFade != null) transitionFade.alpha = easedZoom;
            float barHeight = Mathf.Lerp(0f, letterboxHeight, easedBars);
            if (letterboxTop != null) letterboxTop.sizeDelta = new Vector2(letterboxTop.sizeDelta.x, barHeight);
            if (letterboxBottom != null) letterboxBottom.sizeDelta = new Vector2(letterboxBottom.sizeDelta.x, barHeight);
            yield return null;
        }
        if (menuCamera != null)
        {
            menuCamera.fieldOfView = pushInTargetFOV;
            menuCamera.transform.rotation = targetRot;
        }
        if (transitionFade != null) transitionFade.alpha = 1f;
        if (letterboxTop != null) letterboxTop.sizeDelta = new Vector2(letterboxTop.sizeDelta.x, letterboxHeight);
        if (letterboxBottom != null) letterboxBottom.sizeDelta = new Vector2(letterboxBottom.sizeDelta.x, letterboxHeight);
    }

    IEnumerator LoadLevelAsync(string sceneName)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        while (!operation.isDone) yield return null;
    }

    // Application.Quit() is a no-op inside the Editor (it only closes an actual built .exe), so
    // pressing Exit during a Play Mode test looked like it did nothing - this also stops Play Mode
    // directly when running in the Editor, so testing it here now actually behaves like quitting.
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}