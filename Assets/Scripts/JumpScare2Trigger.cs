using UnityEngine;
using System.Collections;

// A second full-screen screamer, distinct from the default one JumpscareFlash fires automatically -
// this one carries its own picture/sound (JumpScare2) so it can be dropped on a trigger volume
// anywhere in the level and play a different scare than whatever the ambient Flash() jumpscare uses.
// Reuses Jumpscare2D's slam/judder/audio-cutoff presentation via its override parameters instead of
// duplicating that logic.
public class JumpScare2Trigger : MonoBehaviour
{
    [Header("Scare Assets")]
    [Tooltip("Assign Assets/Image/JumpScare2.jpg (as a Sprite).")]
    public Sprite jumpscareImage;
    [Tooltip("Assign Assets/Sounds/JumpScare2.mp3.")]
    public AudioClip jumpscareSound;

    [Header("Feedback")]
    public float shakeDuration = 0.35f;
    public float shakeMagnitude = 0.4f;
    [Tooltip("Seconds between entering the volume and the scare landing. A short pause lets the player stop, reach for the key or look into the cupboard first, so it hits when their guard is down instead of right on the threshold.")]
    public float triggerDelay = 0f;

    [Header("Trigger Behavior")]
    public bool oneShot = true;
    public float rearmCooldown = 20f;
    [Tooltip("Minimum seconds since ANY other scare in the level (see JumpscareDirector) - stops a cluster of triggers in one corridor firing back-to-back. Fire too soon and this one is skipped but stays armed, so coming back through later still gets it.")]
    public float minSecondsSinceLastScare = 20f;

    private bool hasTriggered = false;
    private bool initialized = false;
    private PlayerMovement playerMovement;

    void Start()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;
        playerMovement = FindFirstObjectByType<PlayerMovement>();
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
        if (triggerDelay > 0f) yield return new WaitForSeconds(triggerDelay);

        if (playerMovement != null) playerMovement.Shake(shakeDuration, shakeMagnitude);
        if (Jumpscare2D.Instance != null) Jumpscare2D.Instance.Show(jumpscareImage, jumpscareSound);

        if (!oneShot)
        {
            yield return new WaitForSeconds(rearmCooldown);
            hasTriggered = false;
        }
        yield break;
    }
}
