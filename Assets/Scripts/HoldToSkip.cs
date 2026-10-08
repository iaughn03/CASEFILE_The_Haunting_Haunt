using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class HoldToSkip : MonoBehaviour
{
    [Header("Timing Settings")]
    public float lockDuration = 2.0f;  // Time before skipping is allowed
    public float holdDuration = 2.0f;  // How long to hold to skip
    [Tooltip("Falls back to this if videoPlayer isn't assigned. When videoPlayer IS assigned, this is overwritten at Start() with the clip's real length, so it can never drift out of sync with the actual video again.")]
    public float videoDuration = 10.0f;
    public VideoPlayer videoPlayer;
    public string nextSceneName;

    [Header("UI Components")]
    public GameObject skipUI;
    public Image fillImage;
    public CanvasGroup skipUIGroup;    // Optional: To fade the UI or change transparency

    [Header("Transition Fade")]
    [Tooltip("Full-screen black CanvasGroup - fades in from black at the start and out to black just before the scene change, so the cut isn't jarring.")]
    public CanvasGroup fadeOverlay;
    public float fadeInDuration = 1f;
    public float fadeOutDuration = 1f;

    private float currentHoldTime = 0f;
    private float levelTimer = 0f;
    private bool isSceneLoading = false; // Prevents the scene from trying to load twice

    void Start()
    {
        if (videoPlayer != null && videoPlayer.clip != null)
            videoDuration = (float)videoPlayer.clip.length;

        // Make sure the UI is visible but the bar is empty
        if (skipUI != null) skipUI.SetActive(true);
        if (fillImage != null) fillImage.fillAmount = 0;

        // Optional: Make it look "disabled" (semi-transparent) at the start
        if (skipUIGroup != null) skipUIGroup.alpha = 0.5f;

        if (fadeOverlay != null)
        {
            fadeOverlay.alpha = 1f;
            fadeOverlay.blocksRaycasts = false;
        }
    }

    void Update()
    {
        levelTimer += Time.deltaTime;

        if (fadeOverlay != null)
        {
            float fadeInT = Mathf.Clamp01(levelTimer / fadeInDuration);
            float fadeOutT = Mathf.Clamp01((levelTimer - (videoDuration - fadeOutDuration)) / fadeOutDuration);
            fadeOverlay.alpha = Mathf.Max(1f - fadeInT, fadeOutT);
        }

        // Automatically go to the next scene once the video has actually finished playing.
        if (levelTimer >= videoDuration)
        {
            LoadNextLevel();
            return; // Exit Update loop so we don't process inputs
        }

        // Only allow holding if the 2-second lock has passed
        if (levelTimer >= lockDuration)
        {
            // Optional: visual cue that skip is now active
            if (skipUIGroup != null && skipUIGroup.alpha < 1f) skipUIGroup.alpha = 1f;

            if (Input.anyKey)
            {
                currentHoldTime += Time.deltaTime;
            }
            else
            {
                currentHoldTime -= Time.deltaTime;
            }
        }

        // Clamp values and update UI
        currentHoldTime = Mathf.Clamp(currentHoldTime, 0, holdDuration);

        if (fillImage != null)
            fillImage.fillAmount = currentHoldTime / holdDuration;

        // Trigger Skip if held long enough
        if (currentHoldTime >= holdDuration)
        {
            LoadNextLevel();
        }
    }

    void LoadNextLevel()
    {
        // Check "isSceneLoading" so Unity doesn't double-load the scene if you skip right at the last second
        if (!string.IsNullOrEmpty(nextSceneName) && !isSceneLoading)
        {
            isSceneLoading = true;
            if (fadeOverlay != null) fadeOverlay.alpha = 1f;
            SceneManager.LoadScene(nextSceneName);
        }
    }
}