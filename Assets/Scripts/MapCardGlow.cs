using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A warm glow around a map card while the mouse is over it or it is selected with the keyboard or a
// controller, and no glow otherwise, so no card looks picked when the screen opens. The glow is a
// soft sliced image built at runtime as the card's previous sibling (behind the card), and it copies
// the card's scale so it follows the Animator's hover grow.
[RequireComponent(typeof(RectTransform))]
public class MapCardGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public Color glowColor = new Color(1f, 0.78f, 0.42f, 0.6f);
    [Tooltip("How far the glow reaches past the card's edge, in canvas pixels.")]
    public float spread = 44f;
    public float fadeSpeed = 8f;

    private static Sprite glowSprite;
    private RectTransform glow;
    private Image glowImage;
    private bool hovered, selected;
    private float alpha;

    private void Awake()
    {
        var card = (RectTransform)transform;
        var go = new GameObject(name + "_Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        glow = (RectTransform)go.transform;
        glow.SetParent(card.parent, false);
        glow.SetSiblingIndex(card.GetSiblingIndex());
        glow.anchorMin = card.anchorMin;
        glow.anchorMax = card.anchorMax;
        glow.pivot = card.pivot;
        glow.anchoredPosition = card.anchoredPosition;
        glow.sizeDelta = card.sizeDelta + new Vector2(spread * 2f, spread * 2f);
        glowImage = go.GetComponent<Image>();
        glowImage.sprite = GlowSprite();
        glowImage.type = Image.Type.Sliced;
        glowImage.raycastTarget = false;
        SetAlpha(0f);
    }

    private void OnDisable()
    {
        hovered = selected = false;
        alpha = 0f;
        SetAlpha(0f);
    }

    public void OnPointerEnter(PointerEventData e) => hovered = true;
    public void OnPointerExit(PointerEventData e) => hovered = false;
    public void OnSelect(BaseEventData e) => selected = true;
    public void OnDeselect(BaseEventData e) => selected = false;

    private void LateUpdate()
    {
        float target = hovered || selected ? 1f : 0f;
        alpha = Mathf.MoveTowards(alpha, target, fadeSpeed * Time.unscaledDeltaTime);
        SetAlpha(alpha);
        if (glow != null) glow.localScale = transform.localScale;
    }

    private void SetAlpha(float a)
    {
        if (glowImage == null) return;
        var c = glowColor;
        c.a = glowColor.a * a;
        glowImage.color = c;
        glowImage.enabled = a > 0.001f;
    }

    // A white square that fades out over its outer border; sliced, so only the border is stretched.
    private static Sprite GlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int size = 128, border = 48;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // distance outside the inner rectangle, round at the corners
            float dx = Mathf.Max(border - x, x - (size - 1 - border), 0f);
            float dy = Mathf.Max(border - y, y - (size - 1 - border), 0f);
            float t = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / border);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, t * t * t));
        }
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        return glowSprite;
    }
}
