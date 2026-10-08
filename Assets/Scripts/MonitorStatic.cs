using UnityEngine;

// Keeps a "dead" CRT screen from reading as literally off - jitters its emission color/intensity
// like analog static rather than glowing at one flat, unnervingly perfect brightness. Uses the same
// layered-noise-plus-occasional-spike approach as LightFlicker, but faster and with harder glitch
// pops mixed in, since TV static reads as chaotic/electronic rather than a slow organic bulb drift.
public class MonitorStatic : MonoBehaviour
{
    public Renderer targetRenderer;
    public Color baseEmission = new Color(0.5f, 0.6f, 0.65f);
    public float flickerSpeed = 6f;
    [Range(0f, 1f)] public float minBrightness = 0.4f;
    [Tooltip("Chance per second of a brief harder glitch pop on top of the steady flicker.")]
    public float glitchChancePerSecond = 0.6f;
    public float glitchDuration = 0.08f;

    private MaterialPropertyBlock block;
    private float noiseSeed;
    private float glitchTimer;

    void Awake()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        noiseSeed = Random.Range(0f, 100f);
    }

    void Update()
    {
        if (targetRenderer == null) return;

        float n = Mathf.PerlinNoise(noiseSeed, Time.time * flickerSpeed);
        float brightness = Mathf.Lerp(minBrightness, 1f, n);

        if (glitchTimer > 0f)
        {
            glitchTimer -= Time.deltaTime;
            brightness = 1.4f; // brief overbright pop reads as a glitch/static burst rather than the steady flicker
        }
        else if (Random.value < glitchChancePerSecond * Time.deltaTime)
        {
            glitchTimer = glitchDuration;
        }

        // A script recompile during Play mode reloads the domain without re-running Awake(), leaving the
        // (unserialized) block null - GetPropertyBlock then throws every frame, and with Error Pause on
        // the whole game freezes. Recreate it instead.
        if (block == null) block = new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(block);
        block.SetColor("_EmissionColor", baseEmission * brightness);
        targetRenderer.SetPropertyBlock(block);
    }
}
