using UnityEngine;
using UnityEngine.UI;

// Constant, low-level tension tied to how close the ghost actually is - not just a binary "she's
// chasing you or she isn't". A looped breathing cue and a pulsing screen vignette both scale with
// distance, and the pulse speeds up the closer she gets, so proximity itself reads as a threat even
// while she's just roaming nearby and hasn't noticed the player yet.
public class ProximityDread : MonoBehaviour
{
    [Header("References")]
    public Transform ghost;
    public Transform player;
    public AudioSource heartbeatSource;
    public Image vignetteOverlay;

    [Header("Distance Response")]
    public float maxDistance = 16f; // dread starts fading in
    public float minDistance = 2.5f; // at/below this, effect is at full intensity
    public float responseSmoothing = 2.5f; // how quickly the intensity follows distance changes

    [Header("Audio")]
    [Range(0f, 1f)] public float maxVolume = 0.55f;

    [Header("Vignette Pulse")]
    [Range(0f, 1f)] public float maxVignetteAlpha = 0.5f;
    public float slowPulseSpeed = 1.6f;
    public float fastPulseSpeed = 7f;

    private float currentIntensity;
    private GhostController ghostController;

    private void Start()
    {
        if (ghost == null)
        {
            ghostController = FindFirstObjectByType<GhostController>(FindObjectsInactive.Include);
            if (ghostController != null) ghost = ghostController.transform;
        }
        else
        {
            ghostController = ghost.GetComponent<GhostController>();
        }

        if (player == null)
        {
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) player = pm.transform;
        }

        if (heartbeatSource != null)
        {
            heartbeatSource.loop = true;
            heartbeatSource.volume = 0f;
            if (!heartbeatSource.isPlaying) heartbeatSource.Play();
        }

        if (vignetteOverlay != null)
        {
            Color c = vignetteOverlay.color;
            c.a = 0f;
            vignetteOverlay.color = c;
            vignetteOverlay.raycastTarget = false;
        }
    }

    private void Update()
    {
        if (ghost == null || player == null) return;

        float distance = Vector3.Distance(ghost.position, player.position);
        float targetIntensity = 1f - Mathf.InverseLerp(minDistance, maxDistance, distance);
        targetIntensity = Mathf.Clamp01(targetIntensity);

        currentIntensity = Mathf.MoveTowards(currentIntensity, targetIntensity, responseSmoothing * Time.deltaTime);

        if (heartbeatSource != null)
        {
            heartbeatSource.volume = currentIntensity * maxVolume * SfxVolume.Multiplier;
        }

        if (vignetteOverlay != null)
        {
            if (currentIntensity <= 0.001f)
            {
                SetVignetteAlpha(0f);
            }
            else
            {
                float pulseSpeed = Mathf.Lerp(slowPulseSpeed, fastPulseSpeed, currentIntensity);
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
                SetVignetteAlpha(currentIntensity * maxVignetteAlpha * pulse);
            }
        }
    }

    private void SetVignetteAlpha(float alpha)
    {
        Color c = vignetteOverlay.color;
        c.a = alpha;
        vignetteOverlay.color = c;
    }
}
