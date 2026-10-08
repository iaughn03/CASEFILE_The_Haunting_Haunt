using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Sits on a map selection button. Once that level has been finished at least once (see LevelProgress) it
// lays a plaque across the lower part of the card's picture: SOLVED in gold, or UNSOLVED in a dull red when
// the case was closed without the best ending, then the best score, a bar that fills to it, and the rank.
// The button itself is untouched, so the level can always be replayed.
public class MapSolvedBadge : MonoBehaviour
{
    [Tooltip("The scene this button loads - must match the string passed to MapMainMenu.PlayGame.")]
    public string sceneName;
    [Tooltip("Font for the SOLVED line. Left empty, it borrows the font of the button's own label.")]
    public TMP_FontAsset font;
    [Tooltip("Font for the smaller score and rank lines. Left empty, it uses the main font.")]
    public TMP_FontAsset detailFont;

    [Header("Text")]
    public string label = "SOLVED";
    public string openLabel = "UNSOLVED";
    public string scorePrefix = "BEST SCORE";

    [Header("Plaque")]
    [Tooltip("Vertical centre of the plaque on the card, 0 = bottom edge, 1 = top edge.")]
    public float bannerY = 0.2f;
    public float bannerHeight = 150f;
    [Tooltip("Gap kept clear on each side, as a fraction of the card width, so the plaque sits inside the frame.")]
    public float sideInset = 0.06f;
    public Color bannerColor = new Color(0.03f, 0.04f, 0.06f, 0.86f);
    [Tooltip("Rules, bar and SOLVED line when the case was solved.")]
    public Color solvedAccent = new Color(0.86f, 0.70f, 0.38f, 1f);
    [Tooltip("Rules, bar and UNSOLVED line when it was finished without solving it.")]
    public Color openAccent = new Color(0.72f, 0.24f, 0.20f, 1f);
    public Color labelColor = new Color(0.95f, 0.92f, 0.84f, 1f);
    public Color scoreColor = new Color(0.90f, 0.86f, 0.76f, 1f);
    public Color rankColor = new Color(0.70f, 0.66f, 0.58f, 1f);
    [Tooltip("Seconds the score bar takes to fill when the map screen opens.")]
    public float fillTime = 0.8f;

    private GameObject banner;
    private TextMeshProUGUI labelText, scoreText, rankText;
    private Image[] rules;
    private Image barFill;
    private RectTransform barFillRect;
    private Coroutine filling;

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        int best = LevelProgress.BestScore(sceneName);
        if (best < 0)
        {
            if (banner != null) banner.SetActive(false);
            return;
        }
        if (banner == null) Build();
        banner.SetActive(true);

        bool solved = LevelProgress.IsSolved(sceneName);
        Color accent = solved ? solvedAccent : openAccent;
        foreach (var r in rules) r.color = new Color(accent.r, accent.g, accent.b, 0.9f);
        barFill.color = accent;

        labelText.text = solved ? label : openLabel;
        labelText.color = solved ? labelColor : accent;
        scoreText.text = scorePrefix + "   <size=135%><b>" + best + "</b></size> / " + LevelProgress.MaxScore;
        rankText.text = LevelProgress.Rank(best);

        float target = Mathf.Clamp01(best / (float)LevelProgress.MaxScore);
        if (filling != null) StopCoroutine(filling);
        if (fillTime > 0f && isActiveAndEnabled) filling = StartCoroutine(Fill(target));
        else SetFill(target);
    }

    private IEnumerator Fill(float target)
    {
        SetFill(0f);
        for (float t = 0f; t < fillTime; t += Time.unscaledDeltaTime)
        {
            SetFill(target * Mathf.SmoothStep(0f, 1f, t / fillTime));
            yield return null;
        }
        SetFill(target);
        filling = null;
    }

    private void SetFill(float amount) => barFillRect.anchorMax = new Vector2(amount, 1f);

    private void Build()
    {
        banner = NewImage("SolvedBanner", transform, bannerColor);
        var rt = (RectTransform)banner.transform;
        rt.anchorMin = new Vector2(sideInset, bannerY);
        rt.anchorMax = new Vector2(1f - sideInset, bannerY);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0f, bannerHeight);
        rt.anchoredPosition = Vector2.zero;
        rt.SetAsLastSibling();

        // Thin double rule above and below, echoing the card frame; tinted by Refresh.
        rules = new[] { Rule(rt, 1f, -4f, 2f), Rule(rt, 1f, -9f, 1f), Rule(rt, 0f, 4f, 2f), Rule(rt, 0f, 9f, 1f) };

        TMP_FontAsset main = font;
        if (main == null)
        {
            var own = GetComponentInChildren<TMP_Text>(true);
            if (own != null) main = own.font;
        }
        TMP_FontAsset detail = detailFont != null ? detailFont : main;

        labelText = NewLine("Label", rt, main, 40f, labelColor, new Vector2(0f, 0.62f), new Vector2(1f, 0.93f), 14f);
        scoreText = NewLine("Score", rt, detail, 19f, scoreColor, new Vector2(0f, 0.38f), new Vector2(1f, 0.62f), 3f);

        // Score bar: a dark track with a fill whose right edge sits at best / max.
        var track = NewImage("ScoreTrack", rt, new Color(1f, 1f, 1f, 0.10f));
        var trackRect = (RectTransform)track.transform;
        trackRect.anchorMin = new Vector2(0.14f, 0.29f);
        trackRect.anchorMax = new Vector2(0.86f, 0.29f);
        trackRect.sizeDelta = new Vector2(0f, 6f);
        trackRect.anchoredPosition = Vector2.zero;

        barFill = NewImage("ScoreFill", trackRect, solvedAccent).GetComponent<Image>();
        barFillRect = (RectTransform)barFill.transform;
        barFillRect.anchorMin = Vector2.zero;
        barFillRect.anchorMax = new Vector2(0f, 1f);
        barFillRect.offsetMin = Vector2.zero;
        barFillRect.offsetMax = Vector2.zero;

        rankText = NewLine("Rank", rt, detail, 16f, rankColor, new Vector2(0f, 0.07f), new Vector2(1f, 0.24f), 2f);
        rankText.fontStyle = FontStyles.Italic;
    }

    private Image Rule(RectTransform parent, float edge, float offset, float thickness)
    {
        var line = NewImage("Rule", parent, solvedAccent);
        var rt = (RectTransform)line.transform;
        rt.anchorMin = new Vector2(0f, edge);
        rt.anchorMax = new Vector2(1f, edge);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0f, thickness);
        rt.anchoredPosition = new Vector2(0f, offset);
        return line.GetComponent<Image>();
    }

    private static GameObject NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false; // clicks go through to the button
        return go;
    }

    private static TextMeshProUGUI NewLine(string name, RectTransform parent, TMP_FontAsset f, float size, Color color, Vector2 min, Vector2 max, float spacing)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = new Vector2(10f, 0f); rt.offsetMax = new Vector2(-10f, 0f);

        var t = go.GetComponent<TextMeshProUGUI>();
        if (f != null) t.font = f;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = true; t.fontSizeMin = size * 0.5f; t.fontSizeMax = size;
        t.characterSpacing = spacing;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }
}
