using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Gives every item in the Evidence Bag a small name caption under its icon, so the bag reads as a plain
// icon + name list. It adds nothing else: no markers, no tags, no opinions.
public class BagItemCaptions : MonoBehaviour
{
    [Tooltip("Only items inside this panel (the bag) get a caption; hotbar copies do not.")]
    public Transform bagRoot;
    public TMP_FontAsset font;
    public Material fontMaterial;
    public float refreshInterval = 0.25f;

    private const string CaptionName = "NameCaption";
    private float nextRefresh;

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    private void Refresh()
    {
        foreach (var item in FindObjectsByType<DraggableItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (string.IsNullOrEmpty(item.itemName)) continue;
            if (bagRoot != null && !item.transform.IsChildOf(bagRoot)) continue;

            Transform caption = item.transform.Find(CaptionName);
            if (caption == null) caption = Create(item.transform);
            var label = caption.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label.text != item.itemName) label.text = item.itemName;
        }
    }

    private Transform Create(Transform parent)
    {
        var go = new GameObject(CaptionName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(-8f, 30f);
        rt.anchoredPosition = new Vector2(0f, 4f);

        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.03f, 0.03f, 0.03f, 0.78f);
        bg.raycastTarget = false;

        var txtGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(go.transform, false);
        var trt = (RectTransform)txtGo.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(4f, 1f); trt.offsetMax = new Vector2(-4f, -1f);

        var tmp = txtGo.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        if (fontMaterial != null) tmp.fontSharedMaterial = fontMaterial;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 9f; tmp.fontSizeMax = 16f;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Truncate;
        tmp.color = new Color(0.90f, 0.86f, 0.76f, 1f);
        tmp.raycastTarget = false;
        return go.transform;
    }
}
