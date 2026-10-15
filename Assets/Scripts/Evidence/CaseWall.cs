using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The Final Inspection's corkboard. The victim's photo sits in the middle and every clue the player
// collected is pinned around it. Dragging from one clue onto another pins a red string between them,
// whatever the pair. A right string gets a paper tag with the line it proves; a wrong one is pinned the
// same way but gets no tag and says nothing about its cost, which only the Case Summary shows
// (FinalInspection reads MadeLinks and WrongPairs, and listens to LinksChanged to count the strings).
public class CaseWall : MonoBehaviour
{
    [Header("Data")]
    public CaseLinkSet linkSet;

    [Header("Layers (children of the board, drawn in this order)")]
    public RectTransform cardsLayer;
    public RectTransform stringsLayer;
    public RectTransform pinsLayer;

    [Header("Style")]
    public TMP_FontAsset font;
    public Material fontMaterial;
    public Vector2 cardSize = new Vector2(206f, 148f);
    public Vector2 ellipse = new Vector2(565f, 238f);
    [Tooltip("Shape of the track the cards sit on: 2 = ellipse, higher squares it off towards a rounded rectangle so a crowded wall can use the board's corners.")]
    public float ringSquareness = 2f;
    [Tooltip("Space cards by equal distance along the track instead of equal angle. Equal angle bunches them at the ends of a wide track, which overlaps the cards once the wall holds more than ~10 clues.")]
    public bool evenSpacing = false;
    public Color stringColor = new Color(0.72f, 0.07f, 0.07f, 1f);
    public float stringThickness = 5f;
    [Tooltip("Width of the paper tag that shows what a right string proves.")]
    public float proofTagWidth = 250f;

    [Header("Board art (sprites are generated at runtime)")]
    public Image corkImage;
    [Tooltip("The victim's photo; pinned strings run behind it.")]
    public RectTransform photoRect;
    [Tooltip("Static pins (e.g. on the victim's photo).")]
    public Image[] pinImages;

    [Header("Audio")]
    public AudioSource audioSource;

    private void Awake()
    {
        if (corkImage != null && corkImage.sprite == null) corkImage.sprite = FinalInspectionArt.Cork();
        if (pinImages != null)
            foreach (var p in pinImages) if (p != null) p.sprite = FinalInspectionArt.Circle();
        SetUpLayers();
    }

    // Pinned strings run BEHIND the cards (a string drawn over a card hid the end of its name, e.g.
    // "Forms" on the diary card); the string being pulled rides in a layer above the cards so it can be
    // seen. That layer is a copy of the strings layer's rect, so positions carry over unchanged.
    private RectTransform tagsLayer;

    private void SetUpLayers()
    {
        if (tagsLayer != null || stringsLayer == null || cardsLayer == null || stringsLayer.parent != cardsLayer.parent) return;

        // behind the victim's photo as well as the cards
        int below = cardsLayer.GetSiblingIndex();
        if (photoRect != null && photoRect.parent == stringsLayer.parent) below = Mathf.Min(below, photoRect.GetSiblingIndex());
        stringsLayer.SetSiblingIndex(below);

        var go = new GameObject("TagsLayer", typeof(RectTransform));
        tagsLayer = (RectTransform)go.transform;
        tagsLayer.SetParent(stringsLayer.parent, false);
        tagsLayer.anchorMin = stringsLayer.anchorMin; tagsLayer.anchorMax = stringsLayer.anchorMax;
        tagsLayer.pivot = stringsLayer.pivot;
        tagsLayer.anchoredPosition = stringsLayer.anchoredPosition; tagsLayer.sizeDelta = stringsLayer.sizeDelta;
        tagsLayer.localScale = stringsLayer.localScale; tagsLayer.localRotation = stringsLayer.localRotation;
        tagsLayer.SetSiblingIndex(cardsLayer.GetSiblingIndex() + 1);
    }

    private RectTransform TagParent => tagsLayer != null ? tagsLayer : stringsLayer;

    public int ValidLinks => madeLinks.Count;
    // Wrong strings pinned on the board; each pair only counts once.
    public int InvalidAttempts => wrongPairs.Count;

    // Links whose two clues are both pinned on this board - the most the player can actually make.
    public int AvailableLinks
    {
        get
        {
            if (linkSet == null) return 0;
            int n = 0;
            foreach (var l in linkSet.links)
                if (cards.Exists(c => c.data == l.clueA) && cards.Exists(c => c.data == l.clueB)) n++;
            return n;
        }
    }

    // Fired whenever a new string is pinned, right or wrong, so the prompt can show progress.
    public event System.Action LinksChanged;
    public IReadOnlyList<CaseLinkSet.Link> MadeLinks => madeLinks;
    public IReadOnlyList<(EvidenceData a, EvidenceData b)> WrongPairs => wrongPairs;

    private class Card
    {
        public EvidenceData data;
        public RectTransform rect;
        public RectTransform pin;
    }

    private readonly List<Card> cards = new List<Card>();
    private readonly List<CaseLinkSet.Link> madeLinks = new List<CaseLinkSet.Link>();
    private readonly List<(EvidenceData a, EvidenceData b)> wrongPairs = new List<(EvidenceData, EvidenceData)>();
    private readonly List<GameObject> spawned = new List<GameObject>();
    private RectTransform dragString;
    private Card dragFrom;

    public void Clear()
    {
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
        cards.Clear();
        cardMap.Clear();
        tagRects.Clear();
        madeLinks.Clear();
        wrongPairs.Clear();
        dragString = null;
        dragFrom = null;
    }

    // Pins one card per collected clue around the centre of the board.
    public void Build(List<EvidenceItem> collected)
    {
        Clear();
        int n = collected.Count;
        List<Vector2> spots = evenSpacing ? EvenSpots(n) : null;
        for (int i = 0; i < n; i++)
        {
            float angle = (-90f + 360f * i / Mathf.Max(1, n)) * Mathf.Deg2Rad;
            Vector2 pos = spots != null ? spots[i] : RingPoint(angle);
            float tilt = ((i * 37) % 7 - 3) * 0.9f;
            CreateCard(collected[i], pos, tilt);
        }
    }

    // A superellipse: squareness 2 is the plain ellipse, larger values push towards the corners.
    private Vector2 RingPoint(float angle)
    {
        float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
        float k = 2f / Mathf.Max(0.1f, ringSquareness);
        return new Vector2(Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), k) * ellipse.x,
                           -Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), k) * ellipse.y);
    }

    // n spots at equal distances along the track, starting from the top like the equal-angle layout.
    private List<Vector2> EvenSpots(int n)
    {
        const int samples = 720;
        var pts = new Vector2[samples + 1];
        var dist = new float[samples + 1];
        for (int i = 0; i <= samples; i++)
        {
            pts[i] = RingPoint((-90f + 360f * i / samples) * Mathf.Deg2Rad);
            if (i > 0) dist[i] = dist[i - 1] + Vector2.Distance(pts[i], pts[i - 1]);
        }

        var spots = new List<Vector2>(n);
        int j = 0;
        for (int k = 0; k < n; k++)
        {
            float target = dist[samples] * k / Mathf.Max(1, n);
            while (j < samples - 1 && dist[j + 1] < target) j++;
            float seg = dist[j + 1] - dist[j];
            spots.Add(Vector2.Lerp(pts[j], pts[j + 1], seg > 0f ? (target - dist[j]) / seg : 0f));
        }
        return spots;
    }

    private void CreateCard(EvidenceItem item, Vector2 pos, float tilt)
    {
        var go = new GameObject("Card_" + item.DisplayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(cardsLayer, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = cardSize;
        rt.anchoredPosition = pos;
        rt.localRotation = Quaternion.Euler(0, 0, tilt);

        var plate = go.GetComponent<Image>();
        plate.color = new Color(0.90f, 0.86f, 0.74f, 1f);

        // Only clues with actual icon art get the dark photo strip - without this, a clue with no
        // sprite reads as a blank black rectangle stapled to the card rather than as a plain note.
        var inspect = item.GetComponent<InspectItem>();
        bool hasIcon = inspect != null && inspect.itemIcon != null;

        if (hasIcon)
        {
            var iconBg = NewImage("IconBg", rt, new Color(0.16f, 0.14f, 0.12f, 1f));
            iconBg.rectTransform.anchorMin = new Vector2(0.5f, 1f); iconBg.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            iconBg.rectTransform.pivot = new Vector2(0.5f, 1f);
            iconBg.rectTransform.sizeDelta = new Vector2(cardSize.x - 20f, cardSize.y * 0.46f);
            iconBg.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            iconBg.raycastTarget = false;

            var icon = NewImage("Icon", iconBg.rectTransform, Color.white);
            icon.sprite = inspect.itemIcon;
            icon.preserveAspect = true;
            icon.rectTransform.anchorMin = Vector2.zero; icon.rectTransform.anchorMax = Vector2.one;
            icon.rectTransform.offsetMin = new Vector2(6f, 4f); icon.rectTransform.offsetMax = new Vector2(-6f, -4f);
            icon.raycastTarget = false;
        }

        // With no icon the name gets the whole card, so long clue names stay readable instead of being
        // squeezed into the strip left under a photo that isn't there.
        var label = NewText("Name", rt, item.DisplayName, 20f, new Color(0.12f, 0.10f, 0.09f, 1f));
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true; label.fontSizeMin = 9f; label.fontSizeMax = 20f;
        if (hasIcon)
        {
            // Exactly the space left under the photo strip: dark text that spills up into the strip
            // disappears against it, which read as long names having their first line cut off.
            float stripBottom = 14f + cardSize.y * 0.46f;
            label.rectTransform.anchorMin = new Vector2(0f, 0f); label.rectTransform.anchorMax = new Vector2(1f, 0f);
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.rectTransform.sizeDelta = new Vector2(-14f, cardSize.y - stripBottom - 3f - 5f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 5f);
            label.overflowMode = TextOverflowModes.Truncate;
        }
        else
        {
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(9f, 8f);
            label.rectTransform.offsetMax = new Vector2(-9f, -8f);
        }

        var wallCard = go.AddComponent<WallCard>();
        var card = new Card { data = item.data, rect = rt };
        wallCard.Init(this, card.data);

        // pin sits on the card's top edge, in its own layer above the strings
        var pinImg = NewImage("Pin_" + item.DisplayName, pinsLayer, new Color(0.78f, 0.10f, 0.10f, 1f));
        pinImg.sprite = FinalInspectionArt.Circle();
        pinImg.rectTransform.sizeDelta = new Vector2(24f, 24f);
        pinImg.raycastTarget = false;
        var topLocal = new Vector3(0f, cardSize.y * 0.5f - 6f, 0f);
        Vector3 topWorld = rt.TransformPoint(topLocal);
        pinImg.rectTransform.position = topWorld;
        card.pin = pinImg.rectTransform;

        cards.Add(card);
        spawned.Add(go);
        spawned.Add(pinImg.gameObject);
        cardMap[wallCard] = card;
    }

    private readonly Dictionary<WallCard, Card> cardMap = new Dictionary<WallCard, Card>();

    // ---- dragging ---------------------------------------------------------------------------

    internal void BeginString(WallCard from, Vector2 screenPos)
    {
        if (!cardMap.TryGetValue(from, out dragFrom)) return;
        dragString = NewString("DragString");
        // while it's being pulled the string rides above the cards so it can be seen; once pinned it
        // drops back behind them (EndString)
        dragString.SetParent(TagParent, false);
        UpdateString(dragString, dragFrom.pin.position, ScreenToWorld(screenPos));
    }

    internal void DragString(Vector2 screenPos)
    {
        if (dragString == null || dragFrom == null) return;
        UpdateString(dragString, dragFrom.pin.position, ScreenToWorld(screenPos));
    }

    internal void EndString(Vector2 screenPos)
    {
        if (dragString == null || dragFrom == null) return;

        Card target = null;
        foreach (var c in cards)
        {
            if (c == dragFrom) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(c.rect, screenPos, null)) { target = c; break; }
        }

        var line = dragString;
        var from = dragFrom;
        dragString = null;
        dragFrom = null;

        if (target == null)
        {
            // Released over empty board: just let go, no attempt is counted.
            StartCoroutine(DropString(line, from.pin.position, ScreenToWorld(screenPos)));
            return;
        }

        // Right or wrong, the string is pinned the same way; only a right one gets its proof tag.
        var link = linkSet != null ? linkSet.Find(from.data, target.data) : null;
        bool already = link != null
            ? madeLinks.Contains(link)
            : wrongPairs.Exists(p => (p.a == from.data && p.b == target.data) || (p.a == target.data && p.b == from.data));
        if (already)
        {
            // this pair is already pinned (and a wrong one isn't charged twice)
            Destroy(line.gameObject);
            return;
        }

        if (link != null) madeLinks.Add(link);
        else wrongPairs.Add((from.data, target.data));

        PinLine(line, from, target);
        if (link != null) AddProofTag(from, target, link.proves);
        Play(FinalInspectionArt.Pin(), 0.8f);
        LinksChanged?.Invoke();
    }

    private void PinLine(RectTransform line, Card from, Card target)
    {
        line.SetParent(stringsLayer, false);
        UpdateString(line, from.pin.position, target.pin.position);
        spawned.Add(line.gameObject);
        line.name = "String_" + from.data.displayName + "_" + target.data.displayName;
    }

    // Puts back strings pinned on an earlier visit, after the board had to be rebuilt (the player left
    // the inspection and found more clues). Quietly, with their proof tags, right and wrong alike; a
    // pair whose cards are no longer both on the board is skipped.
    public void RestoreStrings(List<CaseLinkSet.Link> links, List<(EvidenceData a, EvidenceData b)> wrong)
    {
        foreach (var l in links)
        {
            Card a = cards.Find(c => c.data == l.clueA), b = cards.Find(c => c.data == l.clueB);
            if (a == null || b == null || madeLinks.Contains(l)) continue;
            madeLinks.Add(l);
            PinLine(NewString("String"), a, b);
            AddProofTag(a, b, l.proves);
        }
        foreach (var w in wrong)
        {
            Card a = cards.Find(c => c.data == w.a), b = cards.Find(c => c.data == w.b);
            if (a == null || b == null || wrongPairs.Contains(w)) continue;
            wrongPairs.Add(w);
            PinLine(NewString("String"), a, b);
        }
        LinksChanged?.Invoke();
    }

    // A string let go over empty board falls and fades.
    private IEnumerator DropString(RectTransform line, Vector3 start, Vector3 end)
    {
        var img = line.GetComponent<Image>();
        float t = 0f;
        const float dur = 0.42f;
        Vector3 fall = new Vector3(0f, -90f, 0f);
        while (t < dur && line != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            UpdateString(line, start, end + fall * (k * k));
            var c = img.color; c.a = 1f - k; img.color = c;
            yield return null;
        }
        if (line != null) Destroy(line.gameObject);
    }

    // ---- proof tags ---------------------------------------------------------------------------

    private readonly List<Rect> tagRects = new List<Rect>();

    // A paper tag with the line a right string proves, set along its string in the clearest spot it can
    // find: cards sit all round the board, so the plain midpoint often lands on a third clue's card.
    private void AddProofTag(Card a, Card b, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        RectTransform parent = TagParent;

        var plate = NewImage("Proof_" + a.data.displayName + "_" + b.data.displayName, parent, new Color(0.97f, 0.93f, 0.80f, 1f));
        plate.raycastTarget = false;
        var rt = plate.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

        var label = NewText("Text", rt, text, 17f, new Color(0.12f, 0.10f, 0.09f, 1f));
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(10f, 6f); label.rectTransform.offsetMax = new Vector2(-10f, -6f);

        float height = Mathf.Clamp(label.GetPreferredValues(text, proofTagWidth - 20f, 0f).y + 14f, 34f, 120f);
        Vector2 size = new Vector2(proofTagWidth, height);
        rt.sizeDelta = size;

        Vector2 pa = parent.InverseTransformPoint(a.pin.position);
        Vector2 pb = parent.InverseTransformPoint(b.pin.position);
        Vector2 pos = ProofTagSpot(parent, pa, pb, size);
        rt.localPosition = pos;
        rt.localRotation = Quaternion.Euler(0f, 0f, ((tagRects.Count * 37) % 5 - 2) * 0.8f);

        var tagRect = new Rect(pos - size * 0.5f, size);
        tagRects.Add(tagRect);
        spawned.Add(plate.gameObject);
        TieTagToString(parent, tagRect, pa, pb);
    }

    // Tries spots along the string and out to either side of it, and keeps the one covering the least
    // of the cards, their pins, the victim's photo and earlier tags. A crowded wall often has no clear
    // spot right on the string, so the search reaches out into the open gaps between the cards; among
    // clear spots the one nearest the string wins.
    private Vector2 ProofTagSpot(RectTransform parent, Vector2 pa, Vector2 pb, Vector2 size)
    {
        var blockers = new List<Rect>(tagRects);
        foreach (var c in cards) blockers.Add(LocalBounds(parent, c.rect));
        if (photoRect != null) blockers.Add(LocalBounds(parent, photoRect));
        // a tag over a pin hides which card the string starts from, so pins count several times over
        var pins = new List<Rect>();
        foreach (var c in cards) { var r = LocalBounds(parent, c.pin); pins.Add(new Rect(r.position - Vector2.one * 4f, r.size + Vector2.one * 8f)); }

        Rect board = parent.rect;
        Vector2 dir = pb - pa;
        Vector2 side = dir.sqrMagnitude > 0.01f ? new Vector2(-dir.y, dir.x).normalized : Vector2.up;

        Vector2 best = Vector2.Lerp(pa, pb, 0.5f);
        float bestCost = float.MaxValue;
        for (float t = 0.15f; t <= 0.851f; t += 0.05f)
            for (float off = -240f; off <= 240.1f; off += 15f)
            {
                Vector2 p = Vector2.Lerp(pa, pb, t) + side * off;
                p.x = Mathf.Clamp(p.x, board.xMin + size.x * 0.5f + 6f, board.xMax - size.x * 0.5f - 6f);
                p.y = Mathf.Clamp(p.y, board.yMin + size.y * 0.5f + 6f, board.yMax - size.y * 0.5f - 6f);
                var r = new Rect(p - size * 0.5f, size);
                float cost = 0f;
                foreach (var o in blockers) cost += OverlapArea(r, o);
                foreach (var o in pins) cost += OverlapArea(r, o) * 6f;
                cost += RectToSegment(r, pa, pb, out _) * 2f + Mathf.Abs(t - 0.5f) * 40f;
                if (cost < bestCost) { bestCost = cost; best = p; }
            }
        return best;
    }

    // A tag pushed off its string gets a short thread back to it, so it still reads as that string's tag.
    private void TieTagToString(RectTransform parent, Rect tag, Vector2 pa, Vector2 pb)
    {
        if (RectToSegment(tag, pa, pb, out Vector2 onString) < 4f) return;
        Vector2 onTag = new Vector2(Mathf.Clamp(onString.x, tag.xMin + 8f, tag.xMax - 8f),
                                    Mathf.Clamp(onString.y, tag.yMin + 4f, tag.yMax - 4f));
        var thread = NewString("ProofThread");
        thread.GetComponent<Image>().color = new Color(stringColor.r * 0.8f, stringColor.g * 0.8f, stringColor.b * 0.8f, 0.85f);
        UpdateString(thread, parent.TransformPoint(onString), parent.TransformPoint(onTag));
        thread.sizeDelta = new Vector2(thread.sizeDelta.x, Mathf.Max(1.5f, stringThickness * 0.4f));
        spawned.Add(thread.gameObject);
    }

    // Gap between a rect and the segment a to b (0 when the segment runs through it), plus the point on
    // the segment nearest the rect's centre.
    private static float RectToSegment(Rect r, Vector2 a, Vector2 b, out Vector2 nearest)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 0.01f ? Mathf.Clamp01(Vector2.Dot(r.center - a, ab) / ab.sqrMagnitude) : 0f;
        nearest = a + ab * t;

        // the segment runs through the rect: clip it against the rect's x and y slabs
        float t0 = 0f, t1 = 1f;
        if (Clip(-ab.x, a.x - r.xMin, ref t0, ref t1) && Clip(ab.x, r.xMax - a.x, ref t0, ref t1) &&
            Clip(-ab.y, a.y - r.yMin, ref t0, ref t1) && Clip(ab.y, r.yMax - a.y, ref t0, ref t1))
            return 0f;

        // otherwise the gap is set by an end of the segment or a corner of the rect
        float best = Mathf.Min(PointToRect(a, r), PointToRect(b, r));
        foreach (var c in new[] { r.min, r.max, new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMin) })
        {
            float s = ab.sqrMagnitude > 0.01f ? Mathf.Clamp01(Vector2.Dot(c - a, ab) / ab.sqrMagnitude) : 0f;
            best = Mathf.Min(best, Vector2.Distance(c, a + ab * s));
        }
        return best;
    }

    private static bool Clip(float p, float q, ref float t0, ref float t1)
    {
        if (Mathf.Abs(p) < 1e-6f) return q >= 0f;
        float t = q / p;
        if (p < 0f) { if (t > t1) return false; if (t > t0) t0 = t; }
        else { if (t < t0) return false; if (t < t1) t1 = t; }
        return true;
    }

    private static float PointToRect(Vector2 p, Rect r)
    {
        float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
        float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private static Rect LocalBounds(RectTransform space, RectTransform rt)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (var w in corners)
        {
            Vector2 l = space.InverseTransformPoint(w);
            min = Vector2.Min(min, l); max = Vector2.Max(max, l);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static float OverlapArea(Rect a, Rect b)
    {
        float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
        float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        return w > 0f && h > 0f ? w * h : 0f;
    }

    // ---- helpers ----------------------------------------------------------------------------

    private Vector3 ScreenToWorld(Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToWorldPointInRectangle(stringsLayer, screenPos, null, out var world);
        return world;
    }

    private RectTransform NewString(string name)
    {
        var img = NewImage(name, stringsLayer, stringColor);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        return rt;
    }

    // Stretches a thin image between two world points.
    private void UpdateString(RectTransform rt, Vector3 a, Vector3 b)
    {
        Vector3 la = stringsLayer.InverseTransformPoint(a), lb = stringsLayer.InverseTransformPoint(b);
        Vector2 d = lb - la;
        rt.localPosition = la;
        rt.sizeDelta = new Vector2(d.magnitude, stringThickness);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    private TMP_Text NewText(string name, Transform parent, string text, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        if (fontMaterial != null) t.fontSharedMaterial = fontMaterial;
        t.text = text; t.fontSize = size; t.color = color;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    private void Play(AudioClip clip, float volume)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume * SfxVolume.Multiplier);
    }
}

// Sits on each pinned clue card and forwards drag events to the wall.
public class WallCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private CaseWall wall;
    public EvidenceData Data { get; private set; }

    public void Init(CaseWall owner, EvidenceData data)
    {
        wall = owner;
        Data = data;
    }

    public void OnBeginDrag(PointerEventData e) { if (wall != null) wall.BeginString(this, e.position); }
    public void OnDrag(PointerEventData e) { if (wall != null) wall.DragString(e.position); }
    public void OnEndDrag(PointerEventData e) { if (wall != null) wall.EndString(e.position); }

    // A click with no drag (the event system stops counting it as a click once a drag starts) opens
    // this clue's page in the Case File, so it can be reread while deciding what it links to.
    public void OnPointerClick(PointerEventData e)
    {
        if (e.dragging || Data == null || CaseFileUI.Instance == null) return;
        CaseFileUI.Instance.OpenClue(Data);
    }
}
