using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Drop onto any Button for the scale-punch + rust glow feedback the project's default Button
// ColorTint transition doesn't provide on its own. Runs on unscaled time so it still responds
// while the pause menu has Time.timeScale at 0.
[RequireComponent(typeof(RectTransform))]
public class ButtonHoverPunch : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    public float hoverScale = 1.05f;
    public float pressScale = 0.96f;
    public float lerpSpeed = 12f;

    [Tooltip("Optional - tints toward glowColor on hover/select. Leave empty to skip color feedback.")]
    public Image glowTarget;
    public Color glowColor = CrimeSceneTheme.AmberGlow;
    [Range(0f, 1f)]
    [Tooltip("Blend amount toward glowColor - kept low so baked-in button art doesn't flatten out.")]
    public float glowStrength = 0.35f;

    [Tooltip("Optional - label that brightens toward hoverTextColor on hover/select. Leave empty to skip.")]
    public TMP_Text hoverText;
    public Color hoverTextColor = Color.white;

    [Tooltip("Off for buttons that start out selected (the Quit dialog's No), so they don't sit there looking permanently hovered. Their selected state is shown some other way.")]
    public bool selectionCountsAsHover = true;

    private RectTransform rect;
    private Vector3 baseScale;
    private Color baseGlowColor;
    private Color baseTextColor;
    private bool hovering;
    private bool pressed;
    private bool selected;

    private void Awake()
    {
        rect = (RectTransform)transform;
        baseScale = rect.localScale;
        if (glowTarget != null) baseGlowColor = glowTarget.color;
        if (hoverText != null) baseTextColor = hoverText.color;
    }

    // Lets an owner (e.g. SettingsTabs' selected state) change the resting colors without the hover
    // lerp snapping them back to whatever was captured in Awake.
    public void SetBaseColors(Color plate, Color text)
    {
        baseGlowColor = plate;
        baseTextColor = text;
        if (hovering || selected) return;
        if (glowTarget != null) glowTarget.color = plate;
        if (hoverText != null) hoverText.color = text;
    }

    private void OnDisable()
    {
        hovering = false;
        pressed = false;
        selected = false;
        if (rect != null) rect.localScale = baseScale;
        if (glowTarget != null) glowTarget.color = baseGlowColor;
        if (hoverText != null) hoverText.color = baseTextColor;
    }

    private void Update()
    {
        bool active = hovering || (selected && selectionCountsAsHover);

        float targetScale = pressed ? pressScale : (active ? hoverScale : 1f);
        rect.localScale = Vector3.Lerp(rect.localScale, baseScale * targetScale, lerpSpeed * Time.unscaledDeltaTime);

        if (glowTarget != null)
        {
            Color target = active ? Color.Lerp(baseGlowColor, glowColor, glowStrength) : baseGlowColor;
            glowTarget.color = Color.Lerp(glowTarget.color, target, lerpSpeed * Time.unscaledDeltaTime);
        }

        if (hoverText != null)
        {
            Color target = active ? hoverTextColor : baseTextColor;
            hoverText.color = Color.Lerp(hoverText.color, target, lerpSpeed * Time.unscaledDeltaTime);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => hovering = true;
    public void OnPointerExit(PointerEventData eventData) => hovering = false;
    public void OnPointerDown(PointerEventData eventData) => pressed = true;
    public void OnPointerUp(PointerEventData eventData) => pressed = false;
    public void OnSelect(BaseEventData eventData) => selected = true;
    public void OnDeselect(BaseEventData eventData) => selected = false;
}
