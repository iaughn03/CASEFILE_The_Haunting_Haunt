using UnityEngine;
using System.Collections;

// A different flavor of scare from the main chasing ghost: instead of a slow, visible threat you
// can see coming, this snaps a monster directly into the player's face for a split second with a
// loud sound and a camera punch, then it's gone. Place this on a trigger volume anywhere a sudden
// scare fits (a doorway, a dark corner, right after picking something up).
public class JumpScareTrigger : MonoBehaviour
{
    [Header("Scare Actor")]
    [Tooltip("Shared scare monster in the scene, disabled by default. Multiple triggers can point at the same one.")]
    public GameObject scareActor;
    public float appearDuration = 0.35f;
    public float distanceFromCamera = 0.9f;
    [Tooltip("Trigger fired on the actor's own Animator when it appears, so it lunges/reaches instead of standing frozen. Leave blank to skip.")]
    public string appearAnimTrigger = "Attack";
    [Tooltip("Raycast layer checked before placing the actor, so it never pops up inside a wall - falls back to whatever clear distance is actually available.")]
    public LayerMask occlusionMask;

    [Header("Feedback")]
    [Tooltip("The creature vocalization layer - one is picked at random each time this fires.")]
    public AudioClip[] scareSounds;
    [Tooltip("A sharp, non-vocal impact layer played simultaneously with scareSounds - this is what actually sells the 'hit', a scream alone reads thin/dry.")]
    public AudioClip[] impactSounds;
    [Tooltip("Both layers are pitched down slightly (with a little random variance) so repeated scares don't sound identical, and so it reads as menacing rather than shrill.")]
    public Vector2 pitchRange = new Vector2(0.85f, 0.98f);
    public float shakeDuration = 0.4f;
    public float shakeMagnitude = 0.35f;
    [Tooltip("How far the camera FOV punches in on reveal (degrees) - a small camera flinch to sell the impact. 0 disables it.")]
    public float fovPunch = 6f;
    public float fovRecoverySpeed = 12f;
    [Tooltip("Seconds before this scare's sounds are faded out and stopped. PlayOneShot plays a clip to its end, so a long file (JumpScare.mp3 is a 35s compilation) used to keep screaming long after the scare was over.")]
    public float soundCutoffDuration = 1.2f;
    public float soundFadeOutDuration = 0.15f;

    [Header("Trigger Behavior")]
    public bool oneShot = true;
    public float rearmCooldown = 20f; // only used if oneShot is false
    [Tooltip("Minimum seconds since ANY other scare in the level (see JumpscareDirector) - stops a cluster of triggers in one corridor firing back-to-back. Fire too soon and this one is skipped but stays armed, so coming back through later still gets it.")]
    public float minSecondsSinceLastScare = 20f;

    private bool hasTriggered = false;
    private bool initialized = false;
    private Camera playerCamera;
    private PlayerMovement playerMovement;
    private AudioSource audioSource;
    private Animator actorAnimator;
    private float baseFov;
    private Coroutine fovRoutine;

    void Start()
    {
        EnsureInitialized();
    }

    // Start() isn't guaranteed to have run yet on this component when another script (e.g.
    // DifficultyDirector, which fires a scare the instant an evidence threshold is crossed) calls
    // TriggerScare() directly rather than through OnTriggerEnter - so every entry point that touches
    // audioSource/playerCamera routes through here first instead of assuming Start() already fired.
    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
        if (pm != null)
        {
            playerMovement = pm;
            playerCamera = pm.playerCamera;
            if (playerCamera != null) baseFov = playerCamera.fieldOfView;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D - always heard clearly regardless of where the trigger sits

        if (scareActor != null)
        {
            scareActor.SetActive(false);
            actorAnimator = scareActor.GetComponent<Animator>();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (!other.CompareTag("Player")) return;

        TriggerScare();
    }

    // Lets a non-collision source (e.g. DifficultyDirector firing a scare when evidence progress
    // crosses a threshold) reuse the exact same scare presentation as a physical trigger volume.
    public void TriggerScare() => TriggerScare(false);

    // bypassSpacing is for milestone scares that have to land on cue (DifficultyDirector's escalation
    // beats) - they skip the wait but still reset the shared clock so ambient scares stay spaced from
    // them.
    public void TriggerScare(bool bypassSpacing)
    {
        if (hasTriggered) return;

        if (bypassSpacing) JumpscareDirector.Claim();
        else if (!JumpscareDirector.TryClaim(minSecondsSinceLastScare)) return;

        hasTriggered = true;
        EnsureInitialized();
        StartCoroutine(DoScare());
    }

    IEnumerator DoScare()
    {
        // Pitch is shared by the AudioSource, so both layers move together - still enough variance
        // to keep repeat triggers from sounding like a carbon copy of each other.
        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);

        AudioClip impact = PickRandom(impactSounds);
        if (impact != null) audioSource.PlayOneShot(impact, SfxVolume.Multiplier);

        AudioClip vocal = PickRandom(scareSounds);
        if (vocal != null) audioSource.PlayOneShot(vocal, SfxVolume.Multiplier);
        if (impact != null || vocal != null) StartCoroutine(AudioCutoffRoutine());

        if (playerMovement != null) playerMovement.Shake(shakeDuration, shakeMagnitude);
        if (JumpscareFlash.Instance != null) JumpscareFlash.Instance.Flash();

        if (scareActor != null && playerCamera != null)
        {
            PlaceActor();
            scareActor.SetActive(true);

            if (actorAnimator != null && !string.IsNullOrEmpty(appearAnimTrigger))
                actorAnimator.SetTrigger(appearAnimTrigger);
        }

        if (fovPunch > 0f && playerCamera != null)
        {
            if (fovRoutine != null) StopCoroutine(fovRoutine);
            fovRoutine = StartCoroutine(FovPunchRoutine());
        }

        yield return new WaitForSeconds(appearDuration);

        if (scareActor != null) scareActor.SetActive(false);

        if (!oneShot)
        {
            yield return new WaitForSeconds(rearmCooldown);
            hasTriggered = false;
        }
    }

    // Stop() on the source also silences its PlayOneShot clips, so fading the source volume and then
    // stopping it is what actually ends them. Realtime, so an open menu (timeScale 0) can't hold it.
    IEnumerator AudioCutoffRoutine()
    {
        yield return new WaitForSecondsRealtime(soundCutoffDuration);
        float t = 0f;
        while (t < soundFadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(1f, 0f, t / soundFadeOutDuration);
            yield return null;
        }
        audioSource.Stop();
        audioSource.volume = 1f;
    }

    // Raycasts from the camera along its forward direction so the actor never ends up placed
    // inside a wall/doorframe when the player is already standing close to one - it lands at
    // whatever clear distance is actually available instead of always the full configured one.
    void PlaceActor()
    {
        float actualDistance = distanceFromCamera;

        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, distanceFromCamera, occlusionMask))
        {
            actualDistance = Mathf.Max(0.35f, hit.distance - 0.15f);
        }

        Vector3 pos = playerCamera.transform.position + playerCamera.transform.forward * actualDistance;
        scareActor.transform.position = pos;
        scareActor.transform.rotation = Quaternion.LookRotation(-playerCamera.transform.forward, Vector3.up);
    }

    IEnumerator FovPunchRoutine()
    {
        baseFov = GameSettings.FieldOfView; // follow Settings > Display, which may have changed since the last scare
        playerCamera.fieldOfView = baseFov - fovPunch;
        while (Mathf.Abs(playerCamera.fieldOfView - baseFov) > 0.05f)
        {
            playerCamera.fieldOfView = Mathf.MoveTowards(playerCamera.fieldOfView, baseFov, fovRecoverySpeed * Time.unscaledDeltaTime);
            yield return null;
        }
        playerCamera.fieldOfView = baseFov;
    }

    AudioClip PickRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[Random.Range(0, clips.Length)];
    }
}
