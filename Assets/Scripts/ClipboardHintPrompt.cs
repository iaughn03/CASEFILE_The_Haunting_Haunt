using UnityEngine;

// A gently pulsing "click the clipboard to begin" hint at the bottom of the screen.
// InteractableHoverPrompt only appears once the player is already hovering the clipboard, which
// doesn't help a first-time player who doesn't know it's clickable in the first place. This shows
// whenever the camera is zoomed out on the room: it fades away while zoomed in on the clipboard and
// fades back in after zooming out again (right click), so the way back in is never a mystery.
public class ClipboardHintPrompt : MonoBehaviour
{
    public MenuZoom menuZoom;
    public CanvasGroup hintGroup;

    [Header("Pulse (the 'beeping')")]
    [Tooltip("Breathing speed, in full pulses per second.")]
    public float pulseSpeed = 1.1f;
    public float pulseMinAlpha = 0.35f;
    public float pulseMaxAlpha = 1f;

    [Header("Show / Hide")]
    [Tooltip("How long the hint takes to fade out once the clipboard has been clicked.")]
    public float dismissFadeDuration = 0.3f;
    [Tooltip("How long the hint takes to fade back in after zooming out, so it returns once the camera has pulled back rather than popping up mid move.")]
    public float showFadeDuration = 0.8f;

    private float visibility = 1f;

    void Awake()
    {
        if (menuZoom == null) menuZoom = FindFirstObjectByType<MenuZoom>();
    }

    void Update()
    {
        if (hintGroup == null) return;

        bool show = menuZoom == null || (!menuZoom.isZoomed && menuZoom.enabled);
        float duration = Mathf.Max(0.01f, show ? showFadeDuration : dismissFadeDuration);
        visibility = Mathf.MoveTowards(visibility, show ? 1f : 0f, Time.unscaledDeltaTime / duration);

        float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        hintGroup.alpha = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, pulse) * visibility;
    }
}
