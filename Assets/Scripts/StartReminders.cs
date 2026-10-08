using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Two short tips shown once per level in the upper-left corner, after the player first walks a few
// steps: what the flashlight is worth (it slows the ghost; F turns it on), then where collected clues
// go (the bag). Built at runtime in every scene that has a player with a flashlight, so no scene
// needs to carry it.
//
// Card layout:  | [ F ]  FLASHLIGHT                           |
//               |        Body text, left aligned; the card    |
//               |        grows to fit it.                     |
//               |___________________________ (time remaining) |
public class StartReminders : MonoBehaviour
{
    private const float MoveDistanceToStart = 1.5f;
    private const float ShowSeconds = 10f;
    private const float AfterUseSeconds = 1.5f;
    private const float FadeSeconds = 0.3f;
    private const float SlideDistance = 18f;

    private const float CardWidth = 640f;
    private const float MinCardHeight = 120f;
    private const float CardLeft = 40f;
    private const float CardTop = -64f;     // clear of the FPS readout in the very corner
    private const float Pad = 24f;
    private const float KeySize = 64f;
    private const float BodySize = 21f;
    private const float BodyTop = 52f;      // title band
    private const float BodyBottom = 22f;

    private static readonly Color Fill = new Color(0.035f, 0.03f, 0.028f, 0.9f);
    private static readonly Color Accent = new Color(0.74f, 0.15f, 0.13f, 1f);
    private static readonly Color Brass = new Color(0.78f, 0.66f, 0.42f, 1f);
    private static readonly Color BodyColor = new Color(0.84f, 0.82f, 0.78f, 1f);
    private static readonly Color KeyFill = new Color(0.1f, 0.085f, 0.075f, 1f);
    private const string KeyColorHex = "#E8CF8E";

    private CanvasGroup group;
    private RectTransform card;
    private RectTransform keyCap;
    private TMP_Text keyText;
    private TMP_Text title;
    private TMP_Text body;
    private RectTransform bodyRect, titleRect;
    private RectTransform timeBar;
    private PlayerMovement player;

    // True from the first tip card appearing until the last one has faded out (including the short
    // gap between them). The ghost stands down for that stretch so the player can read in peace.
    public static bool TipsShowing { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Hook()
    {
        TipsShowing = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // The scene Play mode / the build starts in.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void FirstScene() => OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var pm = FindFirstObjectByType<PlayerMovement>();
        var fl = FindFirstObjectByType<FlashlightController>(FindObjectsInactive.Include);
        if (pm == null || fl == null || pm.gameObject.scene != scene) return;
        if (FindFirstObjectByType<StartReminders>() != null) return;
        var go = new GameObject("StartReminders");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<StartReminders>().player = pm;
    }

    private void Start()
    {
        Build();
        StartCoroutine(Run());
    }

    // Shown only during actual play: hidden while paused, reading a note, in the bag, etc.
    private static bool Playing => Cursor.lockState == CursorLockMode.Locked && Time.timeScale > 0f;

    private IEnumerator Run()
    {
        while (!Playing) yield return null;
        Vector3 start = player.transform.position;
        while (true)
        {
            Vector3 d = player.transform.position - start; d.y = 0f;
            if (Playing && d.magnitude >= MoveDistanceToStart) break;
            yield return null;
        }

        TipsShowing = true;
        var fKey = GameKeys.Get(GameKeys.Action.Flashlight);
        yield return Show(fKey, "FLASHLIGHT",
            "Your only light, and your only defence: shine it on the ghost to slow it down. The battery drains while it is on, so grab every battery you find. Press " + Key(fKey) + " to switch it on.",
            () => GameKeys.Down(GameKeys.Action.Flashlight));

        yield return new WaitForSecondsRealtime(0.8f);

        var bagKey = GameKeys.Get(GameKeys.Action.EvidenceBag);
        yield return Show(bagKey, "EVIDENCE BAG",
            "Every clue you collect is kept in your bag. Press " + Key(bagKey) + " to open it and examine what you have found.",
            () => PauseManager.isBagOpen || GameKeys.Down(GameKeys.Action.EvidenceBag));

        TipsShowing = false;
        Destroy(gameObject);
    }

    private void OnDestroy() => TipsShowing = false;

    private static string Key(KeyCode k) => "<color=" + KeyColorHex + ">" + GameKeys.KeyName(k) + "</color>";

    // Visible time only counts during play. Once the player does the thing, it stays a moment longer.
    private IEnumerator Show(KeyCode key, string heading, string text, System.Func<bool> used)
    {
        title.text = heading;
        body.text = text;
        SetKey(GameKeys.KeyName(key));
        float shown = 0f, sinceUsed = -1f, alpha = 0f;
        while (true)
        {
            bool playing = Playing;
            if (sinceUsed < 0f && used()) sinceUsed = 0f;
            if (playing)
            {
                shown += Time.unscaledDeltaTime;
                if (sinceUsed >= 0f) sinceUsed += Time.unscaledDeltaTime;
            }
            bool done = shown >= ShowSeconds || sinceUsed >= AfterUseSeconds;
            float target = playing && !done ? 1f : 0f;
            alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime / FadeSeconds);

            group.alpha = alpha;
            float eased = 1f - (1f - alpha) * (1f - alpha);
            card.anchoredPosition = new Vector2(CardLeft - SlideDistance * (1f - eased), CardTop);
            float left = sinceUsed >= 0f ? 0f : 1f - Mathf.Clamp01(shown / ShowSeconds);
            timeBar.localScale = new Vector3(left, 1f, 1f);

            if (done && alpha <= 0f) yield break;
            yield return null;
        }
    }

    // Keycap grows for longer key names ("TAB", "SPACE"); the text column starts after it, and the
    // card grows downward to fit the body text.
    private void SetKey(string label)
    {
        keyText.text = label;
        float w = Mathf.Max(KeySize, keyText.GetPreferredValues(label).x + 30f);
        keyCap.sizeDelta = new Vector2(w, KeySize);
        float textLeft = Pad + 6f + w + 22f;
        titleRect.offsetMin = new Vector2(textLeft, titleRect.offsetMin.y);
        bodyRect.offsetMin = new Vector2(textLeft, bodyRect.offsetMin.y);

        float textWidth = CardWidth - textLeft - Pad;
        float bodyHeight = body.GetPreferredValues(body.text, textWidth, 0f).y;
        card.sizeDelta = new Vector2(CardWidth, Mathf.Max(MinCardHeight, BodyTop + bodyHeight + BodyBottom));
    }

    // ------------------------------------------------------------------ construction

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;   // above the HUD canvas (0), below fades (999) and brightness (32000)
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        // match the game's own prompt text when there is one
        var pi = FindFirstObjectByType<PlayerInteract>();
        TMP_Text style = pi != null ? pi.interactionText : null;

        card = Rect("Card", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));   // upper-left corner
        card.sizeDelta = new Vector2(CardWidth, MinCardHeight);
        card.anchoredPosition = new Vector2(CardLeft, CardTop);
        Img(card, Fill);

        // red edge on the left
        var edge = Rect("Accent", card, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        edge.sizeDelta = new Vector2(5f, 0f);
        Img(edge, Accent);

        // time remaining, a hairline along the bottom that shrinks to the left
        var track = Rect("TimeTrack", card, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f));
        track.offsetMin = new Vector2(5f, 0f); track.offsetMax = new Vector2(0f, 2f);
        Img(track, new Color(1f, 1f, 1f, 0.06f));
        timeBar = Rect("TimeBar", track, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f));
        timeBar.offsetMin = timeBar.offsetMax = Vector2.zero;
        Img(timeBar, new Color(Brass.r, Brass.g, Brass.b, 0.7f));

        // keycap: brass rim, dark face; top-aligned with the title so a tall card still reads cleanly
        keyCap = Rect("KeyCap", card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        keyCap.anchoredPosition = new Vector2(Pad + 6f, -22f);
        keyCap.sizeDelta = new Vector2(KeySize, KeySize);
        Img(keyCap, Brass);
        var face = Rect("Face", keyCap, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        face.offsetMin = new Vector2(2f, 4f); face.offsetMax = new Vector2(-2f, -2f);   // thicker bottom rim reads as a key
        Img(face, KeyFill);
        keyText = Text("Key", face, style, 30f, TextAlignmentOptions.Center, BodyColor);
        var krt = (RectTransform)keyText.transform;
        krt.anchorMin = Vector2.zero; krt.anchorMax = Vector2.one; krt.offsetMin = krt.offsetMax = Vector2.zero;
        keyText.enableAutoSizing = true; keyText.fontSizeMin = 18f; keyText.fontSizeMax = 30f;

        title = Text("Title", card, style, 21f, TextAlignmentOptions.TopLeft, Brass);
        title.characterSpacing = 12f;
        titleRect = (RectTransform)title.transform;
        titleRect.anchorMin = new Vector2(0f, 1f); titleRect.anchorMax = new Vector2(1f, 1f); titleRect.pivot = new Vector2(0f, 1f);
        titleRect.offsetMin = new Vector2(0f, -48f); titleRect.offsetMax = new Vector2(-Pad, -20f);

        // fixed size (no auto-size): the card height is fitted to this text instead
        body = Text("Body", card, style, BodySize, TextAlignmentOptions.TopLeft, BodyColor);
        body.lineSpacing = 6f;
        bodyRect = (RectTransform)body.transform;
        bodyRect.anchorMin = new Vector2(0f, 0f); bodyRect.anchorMax = new Vector2(1f, 1f); bodyRect.pivot = new Vector2(0f, 1f);
        bodyRect.offsetMin = new Vector2(0f, BodyBottom - 6f); bodyRect.offsetMax = new Vector2(-Pad, -BodyTop);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        return rt;
    }

    private static void Img(RectTransform rt, Color c)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
    }

    private static TMP_Text Text(string name, Transform parent, TMP_Text style, float size, TextAlignmentOptions align, Color color)
    {
        var rt = Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (style != null && style.font != null) { t.font = style.font; t.fontSharedMaterial = style.fontSharedMaterial; }
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        t.richText = true;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }
}
