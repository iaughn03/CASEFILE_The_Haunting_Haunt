using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Classic screamer-style 2D scare: a startling face image SLAMS onto the full screen at an
// oversized punch-in scale, holds with a jittery judder, then cuts out fast - alongside a sharp
// audio sting and a camera shake. Layers on top of whatever scare (actor pop-in, picture swing,
// ghost chase) is already firing through JumpscareFlash, which calls Show() automatically.
public class Jumpscare2D : MonoBehaviour
{
    public static Jumpscare2D Instance;

    public Image pictureImage;
    public AudioClip jumpscareSound;
    public float showDuration = 0.35f;
    [Range(0f, 1f)] public float peakAlpha = 1f;
    public float fadeOutDuration = 0.12f;

    [Header("Punch-in")]
    [Tooltip("Starting scale the image slams in at before settling - bigger reads as closer/more aggressive.")]
    public float punchScale = 1.35f;
    public float settleScale = 1.05f;
    public float punchSettleSpeed = 10f;

    [Header("Judder")]
    [Tooltip("Max +/- pixel offset the image jitters by while held, so it doesn't sit dead-still.")]
    public float judderAmount = 18f;
    public float judderSpeed = 40f;

    [Header("Camera Shake")]
    public float shakeDuration = 0.35f;
    public float shakeMagnitude = 0.4f;

    [Header("Audio")]
    [Tooltip("The source clip runs long (a full jumpscare compilation) - this cuts it off after just the opening sting instead of playing it out.")]
    public float soundCutoffDuration = 0.8f;
    [Tooltip("Quick fade applied right before the cutoff so it doesn't end in an audible click/pop.")]
    public float soundFadeOutDuration = 0.15f;

    private AudioSource audioSource;
    private Coroutine activeRoutine;
    private Coroutine audioCutoffRoutine;
    private RectTransform rt;
    private PlayerMovement playerMovement;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        playerMovement = FindFirstObjectByType<PlayerMovement>();

        if (pictureImage != null)
        {
            rt = pictureImage.rectTransform;
            Color c = pictureImage.color;
            c.a = 0f;
            pictureImage.color = c;
            pictureImage.raycastTarget = false;
        }
    }

    // Optional overrides let a second, visually-distinct screamer (e.g. a JumpScare2 trigger) reuse
    // this same full-screen slam/judder/audio-cutoff presentation instead of duplicating it, without
    // touching the default picture/sound this Instance was configured with for the generic Flash() case.
    public void Show(Sprite pictureOverride = null, AudioClip soundOverride = null)
    {
        if (pictureImage == null) return;

        if (pictureOverride != null) pictureImage.sprite = pictureOverride;

        AudioClip clipToPlay = soundOverride != null ? soundOverride : jumpscareSound;
        if (clipToPlay != null)
        {
            audioSource.Stop();
            audioSource.clip = clipToPlay;
            audioSource.volume = SfxVolume.Multiplier;
            audioSource.Play();

            if (audioCutoffRoutine != null) StopCoroutine(audioCutoffRoutine);
            audioCutoffRoutine = StartCoroutine(AudioCutoffRoutine());
        }

        if (playerMovement != null) playerMovement.Shake(shakeDuration, shakeMagnitude);
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(ShowRoutine());
    }

    // PlayOneShot can't be stopped individually, and the source clip runs a full 35s - so this
    // plays it on the dedicated AudioSource instead and cuts it off short with a quick fade.
    private IEnumerator AudioCutoffRoutine()
    {
        yield return new WaitForSecondsRealtime(soundCutoffDuration);

        float startVolume = audioSource.volume;
        float t = 0f;
        while (t < soundFadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, t / soundFadeOutDuration);
            yield return null;
        }
        audioSource.Stop();
        audioSource.volume = SfxVolume.Multiplier;
    }

    private IEnumerator ShowRoutine()
    {
        Color c = pictureImage.color;
        c.a = peakAlpha;
        pictureImage.color = c;
        rt.localScale = Vector3.one * punchScale;

        float t = 0f;
        while (t < showDuration)
        {
            t += Time.unscaledDeltaTime;

            // Scale eases down from the oversized punch-in toward a still-slightly-zoomed settle,
            // never quite sitting still - a real jolt keeps moving instead of freezing instantly.
            float scale = Mathf.Lerp(rt.localScale.x, settleScale, punchSettleSpeed * Time.unscaledDeltaTime);
            rt.localScale = Vector3.one * scale;

            float jx = (Mathf.PerlinNoise(Time.unscaledTime * judderSpeed, 0f) - 0.5f) * 2f * judderAmount;
            float jy = (Mathf.PerlinNoise(0f, Time.unscaledTime * judderSpeed) - 0.5f) * 2f * judderAmount;
            rt.anchoredPosition = new Vector2(jx, jy);

            yield return null;
        }

        float fadeT = 0f;
        while (fadeT < fadeOutDuration)
        {
            fadeT += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(peakAlpha, 0f, fadeT / fadeOutDuration);
            pictureImage.color = c;
            yield return null;
        }
        c.a = 0f;
        pictureImage.color = c;
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
