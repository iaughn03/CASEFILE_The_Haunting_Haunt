using UnityEngine;

// Gentle pendulum-style sway for a ceiling-hung lamp. The lamp mesh's own pivot sits near the bottom
// of its shade (not at the cord's ceiling attachment), and it's a prefab instance so it can't be
// reparented under a proper pivot object - instead this rotates the transform directly around an
// explicit world-space pivot point (set to the cord's ceiling attachment) each frame, which gives the
// same "hangs and swings from up there" look without needing a hierarchy change. Uses layered Perlin
// noise like LightFlicker's flicker - a smooth organic drift reads as a real object settling/swaying,
// where pure sine or random jitter would look mechanical.
public class HangingLampSway : MonoBehaviour
{
    [Tooltip("World-space point this swings around - the cord's ceiling attachment point, not the lamp mesh's own pivot.")]
    public Vector3 pivotPoint;
    [Tooltip("Max tilt in degrees away from resting straight-down on each axis.")]
    public float swayAmount = 2.5f;
    public float swaySpeed = 0.4f;

    private Vector3 restLocalOffset; // this object's position relative to pivotPoint, before any sway is applied
    private Quaternion restRotation;
    private float noiseSeedX;
    private float noiseSeedZ;

    void Awake()
    {
        restLocalOffset = transform.position - pivotPoint;
        restRotation = transform.rotation;
        noiseSeedX = Random.Range(0f, 100f);
        noiseSeedZ = Random.Range(0f, 100f);
    }

    void Update()
    {
        float x = (Mathf.PerlinNoise(noiseSeedX, Time.time * swaySpeed) - 0.5f) * 2f * swayAmount;
        float z = (Mathf.PerlinNoise(noiseSeedZ, Time.time * swaySpeed) - 0.5f) * 2f * swayAmount;
        Quaternion sway = Quaternion.Euler(x, 0f, z);

        // Swinging a pendulum both tilts it AND moves its bottom end through an arc - applying the
        // sway rotation to the rest offset (rather than just to the lamp's own orientation in place)
        // is what produces that arc instead of the shade just spinning in place like a compass needle.
        transform.position = pivotPoint + sway * restLocalOffset;
        transform.rotation = sway * restRotation;
    }
}
