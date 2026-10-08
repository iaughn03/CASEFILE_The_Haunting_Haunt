using UnityEngine;

// Faint, irregular intensity flicker for a practical light source - uses layered Perlin noise rather
// than pure random jitter so it drifts smoothly instead of buzzing, like an old bulb with a loose filament.
public class LightFlicker : MonoBehaviour
{
    public Light targetLight;
    public float baseIntensity = 7000f;
    [Tooltip("How much the intensity dips below baseIntensity at its lowest.")]
    public float flickerAmount = 900f;
    public float flickerSpeed = 1.3f;

    private float noiseSeed;

    void Awake()
    {
        if (targetLight == null) targetLight = GetComponent<Light>();
        noiseSeed = Random.Range(0f, 100f);
    }

    void Update()
    {
        if (targetLight == null) return;
        float n = Mathf.PerlinNoise(noiseSeed, Time.time * flickerSpeed);
        targetLight.intensity = baseIntensity - n * flickerAmount;
    }
}
