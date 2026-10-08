using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

// Hover/click feedback for a single clipboard menu item. Coexists with the existing Animator-driven
// scale-up (Button.transition = Animation, "Highlighted" state) rather than replacing it - this script
// only touches the label's color and a shared smear marker, so there's no fight over the same property.
[RequireComponent(typeof(TextMeshProUGUI))]
public class MenuItemHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Color Shift")]
    public Color dullColor = new Color(0.45f, 0.10f, 0.10f, 1f);
    public Color brightColor = new Color(0.95f, 0.18f, 0.15f, 1f);
    public float colorLerpSpeed = 10f;

    [Header("Shared Marker - a blood smear that snaps beside whichever item is hovered")]
    public RectTransform marker;
    public Vector2 markerOffset = new Vector2(-70f, 0f);

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip hoverSound;

    private TextMeshProUGUI label;
    private RectTransform rt;
    private Color targetColor;

    void Awake()
    {
        label = GetComponent<TextMeshProUGUI>();
        rt = GetComponent<RectTransform>();
        label.color = dullColor;
        targetColor = dullColor;
        if (audioSource == null) audioSource = FindFirstObjectByType<AudioSource>();
    }

    void Update()
    {
        label.color = Color.Lerp(label.color, targetColor, Time.unscaledDeltaTime * colorLerpSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetColor = brightColor;

        if (marker != null)
        {
            marker.gameObject.SetActive(true);
            marker.SetParent(rt.parent, false);
            marker.anchoredPosition = rt.anchoredPosition + markerOffset;
        }

        if (audioSource != null && hoverSound != null)
            audioSource.PlayOneShot(hoverSound, SfxVolume.Multiplier);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetColor = dullColor;
        if (marker != null) marker.gameObject.SetActive(false);
    }

    // Clicking a word opens a page that hides the clipboard while the word is still lit, so no pointer
    // exit ever arrives. Without this reset the word came back stuck bright (with the smear beside it)
    // and hovering it again showed no change at all.
    void OnDisable()
    {
        targetColor = dullColor;
        if (label != null) label.color = dullColor;
        if (marker != null && rt != null && marker.parent == rt.parent) marker.gameObject.SetActive(false);
    }
}
