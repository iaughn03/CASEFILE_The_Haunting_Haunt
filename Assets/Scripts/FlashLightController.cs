using UnityEngine;
using UnityEngine.UI;

public class FlashlightController : MonoBehaviour
{
    [Header("Settings")]
    public GameObject lightSource;
    public bool isOn = false;
    
    [Header("Battery Settings")]
    public float batteryLife = 100f;
    public float maxBattery = 100f;
    public float drainRate = 1f;
    public float flickerThreshold = 20f;

    [Header("UI Elements")]
    public Image batteryBarFill;
    public Image flashlightIcon;
    public Color activeColor = Color.white;
    // Increased the alpha slightly (0.4f) so it's visible but clearly "off"
    public Color inactiveColor = new Color(1, 1, 1, 0.4f);

    [Header("Toggle Audio")]
    public AudioSource audioSource;
    public AudioClip toggleClip;
    [Range(0f, 1f)] public float toggleVolume = 0.7f;

    // A spot light falls off with 1/distance^2, so a single intensity can never be right at both
    // ends of the beam: tuned bright enough to reach down a corridor it blows out a wall you walk
    // into, and tuned for the wall it dies after a couple of metres. Instead of one value we probe
    // what the beam is actually pointed at and scale intensity to suit that distance.
    [Header("Adaptive Beam")]
    public bool adaptiveIntensity = true;
    [Tooltip("What the beam probe considers a surface. Leave as Everything unless something invisible keeps dimming the light.")]
    public LayerMask beamProbeMask = ~0;
    [Tooltip("Distance at/below which the beam is at its dimmest - stops walls and floors blowing out.")]
    public float nearDistance = 1.5f;
    public float nearIntensity = 3f;
    [Tooltip("Distance at/beyond which the beam is at full strength, so it still reaches down a corridor.")]
    public float farDistance = 25f;
    public float farIntensity = 150f;
    [Tooltip("Width of the probe. A little thicker than a ray so a thin pole or railing doesn't dim the whole beam.")]
    public float probeRadius = 0.35f;
    [Tooltip("How fast the beam adapts. Low values look like an eye adjusting, high values snap.")]
    public float adaptSpeed = 8f;

    private Light lightComp;
    private PlayerMovement playerMovement;
    private float baseIntensity = 1.0f;
    private Transform playerRoot;
    private float currentIntensity;
    private float flickerMultiplier = 1f;
    private float flickerTimer;
    private readonly RaycastHit[] probeHits = new RaycastHit[8];

    void Start()
    {
        if (lightSource != null)
        {
            lightComp = lightSource.GetComponent<Light>();
            // Whatever's authored on the Light component in the scene is "full brightness" - toggling
            // back on and the low-battery flicker both need to return to this, not a hardcoded value
            // that silently overrides scene tuning the moment the player presses F.
            if (lightComp != null) baseIntensity = lightComp.intensity;
            lightSource.SetActive(isOn);
        }

        playerMovement = GetComponentInParent<PlayerMovement>();
        // The probe starts inside the player's own collider, so we need to know which transforms to
        // discard from the hits - otherwise the beam permanently reads "surface at 0m" and blacks out.
        playerRoot = playerMovement != null ? playerMovement.transform : transform.root;

        currentIntensity = adaptiveIntensity ? farIntensity : baseIntensity;

        // Force a UI update immediately to sync the icon state
        UpdateUI();
    }

    void Update()
    {
        if (GameKeys.Down(GameKeys.Action.Flashlight))
        {
            ToggleFlashlight();
        }

        if (isOn && batteryLife > 0)
        {
            batteryLife -= drainRate * Time.deltaTime;
            UpdateUI();
            UpdateFlicker();
            ApplyBeamIntensity();
        }
        else if (batteryLife <= 0 && isOn)
        {
            // Only turn off if it was actually on
            isOn = false;
            if (lightSource != null) lightSource.SetActive(false);
            UpdateUI();
            if (playerMovement != null) playerMovement.SetHoldingItem(false);
        }
    }

    // Measures how far away whatever the beam is pointed at is, and eases the light towards the
    // intensity that suits that distance. Nothing in front means open space, so run at full power.
    void ApplyBeamIntensity()
    {
        if (lightComp == null) return;

        if (!adaptiveIntensity)
        {
            lightComp.intensity = baseIntensity * flickerMultiplier;
            return;
        }

        float target = IntensityForDistance(ProbeBeamDistance());
        // Time-independent easing, so the adapt speed feels the same at any frame rate.
        currentIntensity = Mathf.Lerp(currentIntensity, target, 1f - Mathf.Exp(-adaptSpeed * Time.deltaTime));
        lightComp.intensity = currentIntensity * flickerMultiplier;
    }

    float ProbeBeamDistance()
    {
        Transform beam = lightComp.transform;
        float maxDistance = Mathf.Max(farDistance, nearDistance);

        int count = Physics.SphereCastNonAlloc(beam.position, probeRadius, beam.forward, probeHits,
            maxDistance, beamProbeMask, QueryTriggerInteraction.Ignore);

        float nearest = maxDistance;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = probeHits[i];
            // distance 0 means the probe began overlapping this collider; either way, anything on
            // the player themselves is the hand holding the torch, not a surface being lit.
            if (hit.collider == null || hit.transform.IsChildOf(playerRoot)) continue;
            if (hit.distance > 0f && hit.distance < nearest) nearest = hit.distance;
        }
        return nearest;
    }

    float IntensityForDistance(float distance)
    {
        // Closer than the near anchor, keep falling off as distance^2. That's the same curve the
        // light itself applies, so the two cancel and a surface pressed right against the lens
        // stays at the brightness it had at nearDistance instead of burning out.
        if (distance < nearDistance)
        {
            float ratio = distance / Mathf.Max(nearDistance, 0.01f);
            return nearIntensity * ratio * ratio;
        }

        float t = Mathf.InverseLerp(nearDistance, farDistance, distance);
        return Mathf.Lerp(nearIntensity, farIntensity, t);
    }

    // Below the threshold the beam stutters: each flicker holds for a moment rather than
    // re-rolling every frame, which reads as a dying bulb instead of static.
    void UpdateFlicker()
    {
        if (batteryLife >= flickerThreshold)
        {
            flickerMultiplier = 1f;
            return;
        }

        flickerTimer -= Time.deltaTime;
        if (flickerTimer > 0f) return;

        // The closer to empty, the deeper and more frequent the dropouts.
        float severity = 1f - Mathf.Clamp01(batteryLife / Mathf.Max(flickerThreshold, 0.01f));
        flickerMultiplier = Random.value < severity * 0.5f ? Random.Range(0.15f, 0.6f) : 1f;
        flickerTimer = Random.Range(0.04f, 0.18f);
    }

    void ToggleFlashlight()
    {
        // Allow toggling OFF even if battery is 0,
        // but only allow toggling ON if there is battery.
        if (isOn)
        {
            isOn = false;
        }
        else if (batteryLife > 0)
        {
            isOn = true;
        }

        if (lightSource != null)
        {
            lightSource.SetActive(isOn);
            if (isOn && lightComp != null)
            {
                // Seed from the current aim so clicking on while facing a wall doesn't flash full
                // power for the frames it takes the beam to ease down.
                flickerMultiplier = 1f;
                flickerTimer = 0f;
                currentIntensity = adaptiveIntensity ? IntensityForDistance(ProbeBeamDistance()) : baseIntensity;
                lightComp.intensity = currentIntensity;
            }
        }

        if (audioSource != null && toggleClip != null)
            audioSource.PlayOneShot(toggleClip, toggleVolume * SfxVolume.Multiplier);

        UpdateUI();

        // Raise the character's arm into a holding pose while the flashlight is actively on
        if (playerMovement != null) playerMovement.SetHoldingItem(isOn);
    }

    // Battery pickups go through here so the bar jumps up the moment one is collected. Update only
    // redraws the bar while the light is on, so writing batteryLife directly left it looking stale.
    public void AddBattery(float amount)
    {
        batteryLife = Mathf.Min(maxBattery, batteryLife + amount);
        if (batteryLife >= flickerThreshold) flickerMultiplier = 1f;
        UpdateUI();
    }

    void UpdateUI()
    {
        // Battery Bar Logic
        if (batteryBarFill != null)
        {
            batteryBarFill.fillAmount = batteryLife / maxBattery;
        }

        // Icon Visual Logic
        if (flashlightIcon != null)
        {
            // This ensures the object stays active, but changes its color
            flashlightIcon.gameObject.SetActive(true); 
            flashlightIcon.color = isOn ? activeColor : inactiveColor;
        }
    }
}