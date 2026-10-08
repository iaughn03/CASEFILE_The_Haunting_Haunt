using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Slowly breathes the Volume's Vignette intensity around its base value - subtle enough to read as
// ambience rather than an obvious effect, like the shot itself is quietly holding its breath.
public class VignettePulse : MonoBehaviour
{
    public Volume volume;
    public float baseIntensity = 0.4f;
    public float pulseAmplitude = 0.06f;
    public float pulseSpeed = 0.25f;

    private Vignette vignette;

    void Awake()
    {
        if (volume == null) volume = FindFirstObjectByType<Volume>();
        if (volume != null) volume.profile.TryGet(out vignette);
    }

    void Update()
    {
        if (vignette == null) return;
        vignette.intensity.value = baseIntensity + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
    }
}
