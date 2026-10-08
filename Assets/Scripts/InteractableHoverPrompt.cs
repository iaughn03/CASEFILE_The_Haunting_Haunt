using UnityEngine;

// Tells the player the clipboard is clickable: a "Press the Clipboard" prompt that eases in while
// hovering and eases back out otherwise. No glow, no outline, no extra light - the camera's zoom-in
// on click is the only animation this whole interaction needs.
public class InteractableHoverPrompt : MonoBehaviour
{
    [Header("Prompt Text - eases in on hover, out otherwise")]
    public CanvasGroup promptGroup;
    public Transform promptTransform;
    public float hoverEaseDuration = 0.35f;
    [Tooltip("How far below its resting scale the label starts its entrance from (0.9 = starts at 90% size and eases up to 100%).")]
    public float promptEntranceScale = 0.92f;

    [Header("Zoom Awareness")]
    [Tooltip("Once the camera has zoomed in on the clipboard, the prompt has done its job - suppress it while zoomed in.")]
    public MenuZoom menuZoom;

    private bool isHovering;
    private float hoverT; // 0 = hidden, 1 = fully shown
    private Vector3 promptBaseScale = Vector3.one;

    void Awake()
    {
        if (menuZoom == null) menuZoom = FindFirstObjectByType<MenuZoom>();
        if (promptGroup != null) promptGroup.alpha = 0f;
    }

    void OnMouseEnter() => isHovering = true;
    void OnMouseExit() => isHovering = false;

    void Update()
    {
        bool zoomedIn = menuZoom != null && menuZoom.isZoomed;

        float hoverSpeed = 1f / Mathf.Max(0.01f, hoverEaseDuration);
        float target = (isHovering && !zoomedIn) ? 1f : 0f;
        hoverT = Mathf.MoveTowards(hoverT, target, Time.deltaTime * hoverSpeed);
        float eased = hoverT * hoverT * (3f - 2f * hoverT); // smoothstep

        if (promptGroup != null)
        {
            promptGroup.alpha = eased;
            if (promptTransform != null)
            {
                float scale = Mathf.Lerp(promptEntranceScale, 1f, eased);
                promptTransform.localScale = promptBaseScale * scale;
            }
        }
    }
}
