using UnityEngine;
using System.Collections;

// A different scare shape entirely from the creature pop-in: a framed picture hangs still on the
// wall as ambient dressing, then suddenly whips into a violent swing with a flash and a slam. The
// "jump" comes from the violence of the motion itself, not from what's depicted on the picture -
// the same trick Layers of Fear built a whole game around (paintings that move/change when you're
// not looking) and P.T. used with its looping hallway photo. Doesn't need a specific disturbing
// image to work.
public class PictureJumpScare : MonoBehaviour
{
    [Header("Picture Prop")]
    [Tooltip("The frame/picture transform that swings - assign the visual object, not this trigger.")]
    public Transform pictureFrame;
    public Renderer pictureRenderer;
    public Color flashColor = new Color(0.55f, 0.02f, 0.02f, 1f);

    [Header("Swing Motion")]
    public float swingAngle = 55f;
    public int swingCount = 3;
    public float swingSpeed = 18f;
    public float settleSpeed = 4f;

    [Header("Feedback")]
    public AudioClip[] slamSounds;
    public float shakeDuration = 0.3f;
    public float shakeMagnitude = 0.2f;

    [Header("Trigger Behavior")]
    public bool oneShot = true;
    public float rearmCooldown = 25f;
    [Tooltip("Minimum seconds since ANY other scare in the level (see JumpscareDirector) - stops a cluster of triggers in one corridor firing back-to-back. Fire too soon and this one is skipped but stays armed, so coming back through later still gets it.")]
    public float minSecondsSinceLastScare = 20f;

    private bool hasTriggered = false;
    private bool initialized = false;
    private PlayerMovement playerMovement;
    private AudioSource audioSource;
    private Quaternion restRotation;
    private Color restColor;

    void Start()
    {
        EnsureInitialized();
    }

    // Same reasoning as JumpScareTrigger: Start() may not have run yet if something calls
    // TriggerScare() directly instead of via collision, so every entry point routes through here.
    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        playerMovement = FindFirstObjectByType<PlayerMovement>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // 3D - this one should sound like it's genuinely coming from the wall

        if (pictureFrame != null) restRotation = pictureFrame.localRotation;
        if (pictureRenderer != null) restColor = pictureRenderer.material.color;
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (!other.CompareTag("Player")) return;
        TriggerScare();
    }

    public void TriggerScare()
    {
        if (hasTriggered) return;
        // Spaced against every other scare in the level, not just this trigger's own cooldown.
        if (!JumpscareDirector.TryClaim(minSecondsSinceLastScare)) return;

        hasTriggered = true;
        EnsureInitialized();
        StartCoroutine(DoScare());
    }

    IEnumerator DoScare()
    {
        AudioClip clip = slamSounds != null && slamSounds.Length > 0 ? slamSounds[Random.Range(0, slamSounds.Length)] : null;
        if (clip != null) audioSource.PlayOneShot(clip, SfxVolume.Multiplier);
        if (playerMovement != null) playerMovement.Shake(shakeDuration, shakeMagnitude);
        if (JumpscareFlash.Instance != null) JumpscareFlash.Instance.Flash(0.5f, 0.15f);

        if (pictureRenderer != null) pictureRenderer.material.color = flashColor;

        if (pictureFrame != null)
        {
            float angle = swingAngle;
            for (int i = 0; i < swingCount; i++)
            {
                yield return Swing(angle);
                angle *= -0.7f;
            }

            float settleT = 0f;
            Quaternion from = pictureFrame.localRotation;
            while (settleT < 1f)
            {
                settleT += Time.deltaTime * settleSpeed;
                pictureFrame.localRotation = Quaternion.Slerp(from, restRotation, settleT);
                yield return null;
            }
            pictureFrame.localRotation = restRotation;
        }

        if (pictureRenderer != null)
        {
            float fadeT = 0f;
            while (fadeT < 1f)
            {
                fadeT += Time.deltaTime * 2f;
                pictureRenderer.material.color = Color.Lerp(flashColor, restColor, fadeT);
                yield return null;
            }
            pictureRenderer.material.color = restColor;
        }

        if (!oneShot)
        {
            yield return new WaitForSeconds(rearmCooldown);
            hasTriggered = false;
        }
    }

    IEnumerator Swing(float targetAngle)
    {
        Quaternion from = pictureFrame.localRotation;
        Quaternion to = restRotation * Quaternion.Euler(0f, 0f, targetAngle);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * swingSpeed;
            pictureFrame.localRotation = Quaternion.Slerp(from, to, t);
            yield return null;
        }
    }
}
