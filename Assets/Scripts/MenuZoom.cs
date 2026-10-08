using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MenuZoom : MonoBehaviour
{
    [Header("Camera Settings")]
    public Transform cameraTransform;
    public Transform startPos;
    public Transform zoomPos;
    [Tooltip("Seconds for the dolly move to complete.")]
    public float zoomDuration = 0.9f;

    [Header("Idle Sway - very subtle breathing motion once the camera has settled, so the shot never looks perfectly frozen")]
    public float swayAmplitude = 0.015f;
    public float swaySpeed = 0.35f;

    [Header("Audio Settings")]
    public AudioSource audioSource;    // Drag your AudioSource component here
    public AudioClip zoomInSound;     // Optional: Sound when zooming in
    public AudioClip zoomOutSound;    // Optional: Sound when zooming out/canceling

    [Header("State")]
    public bool isZoomed = false;

    private Coroutine moveRoutine;
    private Vector3 settledPosition;
    private Quaternion settledRotation;
    private bool isSettled;

    void Start()
    {
        // FIX 1: Force the game time back to normal speed
        Time.timeScale = 1f;

        // FIX 2: Force Unity's global audio back on!
        // If your gameplay scene paused the audio using AudioListener.pause = true,
        // this line will un-mute the game as soon as the main menu loads.
        AudioListener.pause = false;

        // Reset state on start
        isZoomed = false;

        // Safety check: Automatically grab an AudioSource if one is attached to this GameObject
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        // Safety check: Find Main Camera if missing
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        // Snap camera to the start position instantly
        if (cameraTransform != null && startPos != null)
        {
            cameraTransform.position = startPos.position;
            cameraTransform.rotation = startPos.rotation;
            settledPosition = startPos.position;
            settledRotation = startPos.rotation;
            isSettled = true;
        }
    }

    void Update()
    {
        if (cameraTransform == null) return;

        if (isSettled)
        {
            // Small drift layered on top of the settled pose - amplitude is deliberately tiny so it
            // reads as ambient breathing rather than motion, and never fights the eased dolly below.
            float x = Mathf.Sin(Time.time * swaySpeed) * swayAmplitude;
            float y = Mathf.Sin(Time.time * swaySpeed * 0.7f + 1.3f) * swayAmplitude * 0.6f;
            cameraTransform.position = settledPosition + cameraTransform.right * x + cameraTransform.up * y;
            cameraTransform.rotation = settledRotation;
        }

        // Right-click to go back
        if (isZoomed && Input.GetMouseButtonDown(1))
        {
            ToggleZoom();
        }
    }

    public void ToggleZoom()
    {
        if (startPos == null || zoomPos == null || cameraTransform == null) return;

        isZoomed = !isZoomed;
        PlaySound(isZoomed ? zoomInSound : zoomOutSound);

        Transform target = isZoomed ? zoomPos : startPos;
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(EaseTo(target));
    }

    // Fixed-duration ease-out dolly - decelerates smoothly into the landing pose instead of the old
    // asymptotic Lerp-forever approach, so it settles cleanly rather than chasing the target forever.
    IEnumerator EaseTo(Transform target)
    {
        isSettled = false;
        Vector3 fromPos = cameraTransform.position;
        Quaternion fromRot = cameraTransform.rotation;
        Vector3 toPos = target.position;
        Quaternion toRot = target.rotation;

        float t = 0f;
        while (t < zoomDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / zoomDuration);
            float eased = 1f - Mathf.Pow(1f - p, 3f); // ease-out cubic
            cameraTransform.position = Vector3.Lerp(fromPos, toPos, eased);
            cameraTransform.rotation = Quaternion.Slerp(fromRot, toRot, eased);
            yield return null;
        }

        cameraTransform.position = toPos;
        cameraTransform.rotation = toRot;
        settledPosition = toPos;
        settledRotation = toRot;
        isSettled = true;
    }

    // Helper method to safely play audio clips without cutting off
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, SfxVolume.Multiplier);
        }
    }
}
