using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The Case File: everything the investigator knows about the case, readable at any time.
//   Briefing  - victim, where and when found, what the police first thought, the task
//   People    - everyone in the file: who they are to the victim, facts on record, their statement
//   Evidence  - every clue in the bag (observation, what it could mean, the document's text) plus any
//               witness documents read in the level that are not evidence
//   Guide     - the three rulings in plain words, and this case's own page once it is solved
// Opens from a tab on the bag, from the case wall (also by clicking a card), from the ruling and
// accusation screens, and once on its own after the intro as the case briefing.
// Built in code at runtime, styled from the scene's Final Inspection (its rusted plate sprite and
// typewriter font), so every level gets the same panel without hand-built UI.
public class CaseFileUI : MonoBehaviour
{
    public static CaseFileUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.canvasRoot != null && Instance.canvasRoot.activeSelf;

    public CaseStoryData story;

    [Header("Style (taken from the Final Inspection when left empty)")]
    public Sprite buttonSprite;
    public TMP_FontAsset bodyFont;
    public TMP_FontAsset titleFont;

    // The fonts own default atlas materials draw solid blocks in this project; the Final Inspection
    // labels use working preset materials, so copies of those are used here.
    private Material bodyMaterial;
    private Material titleMaterial;

    public enum Tab { Briefing, People, Evidence, Guide }

    // ---- the Guide's three meanings (same wording as every Guide in the game) --------------------
    // Philippine Revised Penal Code, Arts. 246, 248 and 249 (see FinalInspection.Verdict), in the order
    // the law applies them: Parricide first, then Murder, and Homicide is whatever is left.

    public const string ParricideLaw = "Revised Penal Code, Art. 246";
    public const string ParricideMeaning = "Killing your father, mother or child, any other direct ascendant or descendant (a grandparent or grandchild, for example), or your spouse.";
    public const string ParricideShort = "The killer's relationship to the victim decides it, whatever the method.";
    public const string MurderLaw = "Revised Penal Code, Art. 248";
    public const string MurderMeaning = "A killing that is not parricide but has a qualifying circumstance, for example:\n\n"
        + "Deciding ahead of time to kill and carrying it out (evident premeditation).\n"
        + "Attacking so the victim cannot defend themselves (treachery).\n"
        + "Using poison or fire.\n"
        + "Killing for a reward.\n"
        + "Cruelty.";
    public const string MurderShort = "One qualifying circumstance is enough. A murder does not need all of them.";
    public const string HomicideLaw = "Revised Penal Code, Art. 249";
    public const string HomicideMeaning = "A killing that is neither parricide nor murder: the killer is not the victim's parent, child, other direct ascendant or descendant, or spouse, and there is no qualifying circumstance.";
    public const string HomicideShort = "No family tie of that kind, and no qualifying circumstance.";

    // ---- documents read in the level that are not evidence --------------------------------------

    private static readonly List<ReadableNote> documents = new List<ReadableNote>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => documents.Clear();

    public static void RegisterDocument(ReadableNote note)
    {
        if (note != null && !documents.Contains(note)) documents.Add(note);
    }

    // The bag's detail panel text for a clue: the neutral observation, then the clue note under it.
    public static string ObservationWithNote(EvidenceData data)
    {
        if (data == null) return "";
        if (string.IsNullOrWhiteSpace(data.clueNote)) return data.observation;
        return data.observation + "\n\n<color=#D9B66A>What it could mean</color>\n" + data.clueNote;
    }

    // ---- state -----------------------------------------------------------------------------------

    private class Page
    {
        public string heading, sub, body;
        public Sprite image;
        public EvidenceData clue;
    }

    private GameObject canvasRoot;
    private TMP_Text subtitleText, headingText, pageSubText, bodyText, pageIndicator, closeLabel;
    private Image pageImage;
    private RectTransform headingRect, pageSubRect, scrollRect, paperEdgeRect;
    private ScrollRect scroll;
    private Button prevButton, nextButton, closeButton;
    private readonly Button[] tabButtons = new Button[4];
    private GameObject tabRow;

    private Button wallFileButton, wallGuideButton;
    private Button bagTabButton;

    private readonly List<Page> pages = new List<Page>();
    private Tab tab;
    private int pageIndex;
    private bool introMode;
    private float openedAt;
    private System.Action onClosed;

    private static readonly Color PlateOn = Color.white;
    private static readonly Color PlateOff = new Color(0.42f, 0.42f, 0.42f, 1f);
    private static readonly Color LabelOn = new Color(0.96f, 0.90f, 0.72f, 1f);
    private static readonly Color LabelOff = new Color(0.62f, 0.60f, 0.56f, 1f);
    private static readonly Color Ink = new Color(0.13f, 0.11f, 0.09f, 1f);
    private static readonly Color InkSoft = new Color(0.40f, 0.31f, 0.22f, 1f);

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        TakeStyle();
        AddBagTab();
        AddFinalInspectionButtons();
        AddPauseGuideButton();
    }

    // A Guide button in the pause menu, between Options and Restart, made from a copy of the Options
    // button so it matches. The buttons below it move down one row.
    private void AddPauseGuideButton()
    {
        var pm = FindFirstObjectByType<PauseManager>(FindObjectsInactive.Include);
        if (pm == null || pm.pauseMenuUI == null) return;
        Button options = null;
        foreach (var b in pm.pauseMenuUI.GetComponentsInChildren<Button>(true))
            if (b.name == "OptionButton") { options = b; break; }
        if (options == null || options.transform.parent.Find("GuideButton") != null) return;

        var column = options.transform.parent;
        var guide = Instantiate(options.gameObject, column);
        guide.name = "GuideButton";
        var button = guide.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(OpenGuide);

        var label = guide.GetComponentInChildren<TMP_Text>(true);
        var optionsLabel = options.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            bool upper = optionsLabel != null && optionsLabel.text == optionsLabel.text.ToUpperInvariant();
            label.text = upper ? "GUIDE" : "Guide";
        }

        var optRect = (RectTransform)options.transform;
        float step = 132f;
        var rects = new List<RectTransform>();
        foreach (Transform child in column) if (child != guide.transform && child is RectTransform r) rects.Add(r);
        foreach (var r in rects)
            if (r.anchoredPosition.y < optRect.anchoredPosition.y - 1f) r.anchoredPosition -= new Vector2(0f, step);
        var guideRect = (RectTransform)guide.transform;
        guideRect.anchoredPosition = optRect.anchoredPosition - new Vector2(0f, step);
        guide.transform.SetSiblingIndex(options.transform.GetSiblingIndex() + 1);
    }

    private void TakeStyle()
    {
        var fi = FinalInspection.Instance != null ? FinalInspection.Instance : FindFirstObjectByType<FinalInspection>(FindObjectsInactive.Include);
        if (fi != null)
        {
            if (buttonSprite == null && fi.nextButton != null && fi.nextButton.image != null) buttonSprite = fi.nextButton.image.sprite;
            if (bodyFont == null && fi.nextLabel != null) bodyFont = fi.nextLabel.font;
            if (titleFont == null && fi.titleText != null) titleFont = fi.titleText.font;
            if (fi.nextLabel != null && fi.nextLabel.font == bodyFont) bodyMaterial = PlainCopy(fi.nextLabel.fontSharedMaterial);
            if (fi.titleText != null && fi.titleText.font == titleFont) titleMaterial = fi.titleText.fontSharedMaterial;
        }
        if (bodyFont == null) bodyFont = TMP_Settings.defaultFontAsset;
        if (titleFont == null) titleFont = bodyFont;
    }

    private static Material PlainCopy(Material source)
    {
        if (source == null) return null;
        var m = new Material(source) { name = source.name + " (Case File)" };
        m.DisableKeyword("OUTLINE_ON");
        m.DisableKeyword("UNDERLAY_ON");
        m.DisableKeyword("UNDERLAY_INNER");
        if (m.HasProperty(ShaderUtilities.ID_OutlineWidth)) m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        if (m.HasProperty(ShaderUtilities.ID_FaceDilate)) m.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
        if (m.HasProperty(ShaderUtilities.ID_UnderlayColor)) m.SetColor(ShaderUtilities.ID_UnderlayColor, Color.clear);
        return m;
    }

    // ---- public entry points ---------------------------------------------------------------------

    public void Open(Tab start) => OpenInternal(start, null, false, null);

    public void OpenClue(EvidenceData clue) => OpenInternal(Tab.Evidence, clue, false, null);

    public void OpenGuide() => OpenInternal(Tab.Guide, null, false, null);

    // Wired to the bag tab; also usable from a button in the scene.
    public void OpenFromBag() => Open(Tab.Briefing);

    // After the intro lines: the briefing alone, with Continue, then the level carries on.
    public bool ShowIntroBriefing(System.Action done)
    {
        if (story == null || string.IsNullOrWhiteSpace(story.briefing)) return false;
        OpenInternal(Tab.Briefing, null, true, done);
        return true;
    }

    public void Close()
    {
        if (!IsOpen) return;
        canvasRoot.SetActive(false);
        var done = onClosed;
        onClosed = null;
        introMode = false;
        done?.Invoke();
    }

    private void OpenInternal(Tab start, EvidenceData clue, bool intro, System.Action done)
    {
        if (canvasRoot == null) Build();
        introMode = intro;
        onClosed = done;
        openedAt = Time.unscaledTime;
        canvasRoot.SetActive(true);

        tabRow.SetActive(!intro);
        // no tab row on the briefing, so the page takes that room too
        paperEdgeRect.anchoredPosition = intro ? new Vector2(0f, -15f) : new Vector2(0f, -55f);
        paperEdgeRect.sizeDelta = intro ? new Vector2(1448f, 740f) : new Vector2(1448f, 628f);
        if (closeLabel != null) closeLabel.text = intro ? "Continue" : "Close";
        // the briefing has a single way on, so it sits where the eye lands: bottom centre
        var closeRect = (RectTransform)closeButton.transform;
        closeRect.anchorMin = closeRect.anchorMax = intro ? new Vector2(0.5f, 0f) : new Vector2(1f, 0f);
        closeRect.anchoredPosition = intro ? new Vector2(0f, 80f) : new Vector2(-220f, 80f);
        if (subtitleText != null) subtitleText.text = story != null ? story.caseTitle : "";

        ShowTab(start);
        if (clue != null)
        {
            int i = pages.FindIndex(p => p.clue == clue);
            if (i >= 0) ShowPage(i);
        }
    }

    // ---- per frame -------------------------------------------------------------------------------

    private void Update()
    {
        UpdateFinalInspectionButtons();
        UpdateBagTab();

        if (!IsOpen) return;
        if (Time.unscaledTime - openedAt < 0.4f) return; // a key held from the intro must not skip the briefing

        if (Input.GetKeyDown(KeyCode.Escape) || (introMode && Input.GetKeyDown(KeyCode.Return)))
        {
            Close();
            return;
        }
        if (!introMode)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) ShowPage(pageIndex + 1);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) ShowPage(pageIndex - 1);
        }
    }

    // ---- pages -----------------------------------------------------------------------------------

    private void ShowTab(Tab t)
    {
        tab = t;
        pages.Clear();
        switch (t)
        {
            case Tab.Briefing: BuildBriefingPages(); break;
            case Tab.People: BuildPeoplePages(); break;
            case Tab.Evidence: BuildEvidencePages(); break;
            case Tab.Guide: BuildGuidePages(); break;
        }
        if (pages.Count == 0) pages.Add(new Page { heading = "Nothing here yet", body = "" });

        for (int i = 0; i < tabButtons.Length; i++) MarkTab(tabButtons[i], i == (int)t);
        ShowPage(0);
    }

    private void ShowPage(int index)
    {
        if (pages.Count == 0) return;
        pageIndex = Mathf.Clamp(index, 0, pages.Count - 1);
        Page p = pages[pageIndex];

        bool hasImage = p.image != null;
        pageImage.gameObject.SetActive(hasImage);
        if (hasImage) pageImage.sprite = p.image;

        // text column starts right of the picture when there is one
        float left = hasImage ? 340f : 50f;
        SetLeft(headingRect, left);
        SetLeft(pageSubRect, left);
        SetLeft(scrollRect, left);

        headingText.text = p.heading ?? "";
        pageSubText.text = p.sub ?? "";
        bodyText.text = p.body ?? "";
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 1f;

        bool many = pages.Count > 1 && !introMode;
        prevButton.gameObject.SetActive(many);
        nextButton.gameObject.SetActive(many);
        SetUsable(prevButton, pageIndex > 0);
        SetUsable(nextButton, pageIndex < pages.Count - 1);
        pageIndicator.text = many ? (pageIndex + 1) + " / " + pages.Count : "";
    }

    // the plates have no colour transition, so a button that cannot be used is greyed by hand
    private static void SetUsable(Button b, bool usable)
    {
        b.interactable = usable;
        var punch = b.GetComponent<ButtonHoverPunch>();
        if (punch != null) punch.SetBaseColors(usable ? PlateOn : PlateOff, usable ? new Color(0.847f, 0.839f, 0.816f, 1f) : LabelOff);
    }

    private static void SetLeft(RectTransform r, float left)
    {
        if (r == null) return;
        r.offsetMin = new Vector2(left, r.offsetMin.y);
    }

    private void BuildBriefingPages()
    {
        pages.Add(new Page
        {
            heading = "Case Briefing",
            sub = story != null ? story.caseTitle : "",
            body = story != null ? story.briefing : "No briefing on file."
        });
    }

    private void BuildPeoplePages()
    {
        if (story == null || story.persons == null) return;
        foreach (var person in story.persons)
        {
            if (person == null || !IsRevealed(person)) continue;
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(person.profile)) sb.Append(person.profile.Trim());
            if (!string.IsNullOrWhiteSpace(person.statement))
            {
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append("<color=#7A2A1A>Statement</color>\n<i>").Append(person.statement.Trim()).Append("</i>");
            }
            pages.Add(new Page { heading = person.name, sub = person.relation, body = sb.ToString(), image = person.portrait });
        }
        if (pages.Count == 0) pages.Add(new Page { heading = "No one yet", body = "Nobody is in the file yet." });
    }

    private static bool IsRevealed(CaseStoryData.Person person)
    {
        if (person.revealedBy == null || person.revealedBy.Length == 0) return true;
        foreach (var data in person.revealedBy)
            if (data != null && CaseFile.IsInBag(data.displayName)) return true;
        return false;
    }

    private void BuildEvidencePages()
    {
        var items = new List<EvidenceItem>();
        foreach (var item in CaseFile.Items)
            if (item != null && item.data != null && CaseFile.IsInBag(item.DisplayName)) items.Add(item);
        items.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));

        foreach (var item in items)
        {
            EvidenceData d = item.data;
            var sb = new StringBuilder();
            sb.Append("<color=#7A2A1A>Observation</color>\n").Append(d.observation);
            if (!string.IsNullOrWhiteSpace(d.clueNote))
                sb.Append("\n\n<color=#7A2A1A>What it could mean</color>\n").Append(d.clueNote);
            var note = item.GetComponent<NoteObject>();
            if (note != null && !string.IsNullOrWhiteSpace(note.noteContent))
                sb.Append("\n\n<color=#7A2A1A>The document</color>\n").Append(note.noteContent.Trim());
            var inspect = item.GetComponent<InspectItem>();
            pages.Add(new Page
            {
                heading = d.displayName,
                sub = "Evidence",
                body = sb.ToString(),
                image = inspect != null ? inspect.itemIcon : null,
                clue = d
            });
        }

        foreach (var doc in documents)
        {
            if (doc == null) continue;
            pages.Add(new Page { heading = doc.title, sub = "Document read on site (not evidence)", body = doc.text });
        }

        if (pages.Count == 0)
            pages.Add(new Page { heading = "No evidence yet", body = "Clues you collect are kept here, with what each one shows." });
    }

    private void BuildGuidePages()
    {
        pages.Add(GuidePage("Parricide", ParricideLaw, ParricideMeaning, ParricideShort));
        pages.Add(GuidePage("Murder", MurderLaw, MurderMeaning, MurderShort));
        pages.Add(GuidePage("Homicide", HomicideLaw, HomicideMeaning, HomicideShort));
        pages.Add(new Page
        {
            heading = "Quick Comparison",
            sub = "Guide, Revised Penal Code",
            body = "<color=#7A2A1A>Parricide</color> (Art. 246) = the killer is the victim's father, mother, child, other direct ascendant or descendant, or spouse. Any method.\n\n"
                 + "<color=#7A2A1A>Murder</color> (Art. 248) = not parricide, but with a qualifying circumstance: evident premeditation, treachery, poison, fire, a reward, or cruelty.\n\n"
                 + "<color=#7A2A1A>Homicide</color> (Art. 249) = neither parricide nor murder."
        });

        if (story != null && !string.IsNullOrWhiteSpace(story.rulingWhy))
        {
            bool solved = LevelProgress.IsSolved(story.sceneName);
            pages.Add(new Page
            {
                heading = solved ? story.rulingWhyTitle : "This case",
                sub = "Guide, " + story.caseTitle,
                body = solved
                    ? story.rulingWhy
                    : "This page opens once you close this case with the right ruling and the right accused.\n\n"
                      + "Until then, the ruling is yours to decide from the evidence and the three meanings on the pages before this one."
            });
        }
    }

    private static Page GuidePage(string term, string law, string meaning, string inShort) => new Page
    {
        heading = term,
        sub = "Guide",
        body = "<color=#7A2A1A>" + law + "</color>\n\n" + meaning + "\n\n<color=#7A2A1A>In short:</color> " + inShort
    };

    // ---- building the panel ----------------------------------------------------------------------

    private void Build()
    {
        canvasRoot = new GameObject("CaseFileCanvas", typeof(RectTransform));
        canvasRoot.transform.SetParent(transform, false);
        var canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000; // above every game panel, under the brightness overlay (32000)
        var scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRoot.AddComponent<GraphicRaycaster>();

        var root = (RectTransform)canvasRoot.transform;

        var backdrop = NewImage("Backdrop", root, new Color(0.025f, 0.027f, 0.040f, 0.97f));
        Stretch(backdrop.rectTransform);

        var title = NewText("Title", root, titleFont, 64f, new Color(0.93f, 0.85f, 0.82f, 1f), TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1500f, 90f));
        title.text = "Case File";

        subtitleText = NewText("Subtitle", root, bodyFont, 28f, LabelOff, TextAlignmentOptions.Center);
        Place(subtitleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(1500f, 44f));

        tabRow = new GameObject("Tabs", typeof(RectTransform));
        var tabRect = (RectTransform)tabRow.transform;
        tabRect.SetParent(root, false);
        Place(tabRect, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(1300f, 70f));
        string[] names = { "Briefing", "People", "Evidence", "Guide" };
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            tabButtons[i] = NewButton(names[i], tabRect, new Vector2(-480f + 320f * i, 0f), new Vector2(300f, 66f), 30f, () => ShowTab((Tab)index));
            tabButtons[i].GetComponent<RectTransform>().anchorMin = tabButtons[i].GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
        }

        // the page: a sheet of file paper with a dark edge
        var edge = NewImage("PaperEdge", root, new Color(0.30f, 0.25f, 0.20f, 1f));
        paperEdgeRect = edge.rectTransform;
        Place(paperEdgeRect, new Vector2(0.5f, 0.5f), new Vector2(0f, -55f), new Vector2(1448f, 628f));
        var paper = NewImage("Paper", edge.rectTransform, new Color(0.93f, 0.90f, 0.81f, 1f));
        Stretch(paper.rectTransform, 4f);
        var paperRect = paper.rectTransform;

        pageImage = NewImage("Picture", paperRect, Color.white);
        pageImage.preserveAspect = true;
        var picRect = pageImage.rectTransform;
        picRect.anchorMin = picRect.anchorMax = new Vector2(0f, 1f);
        picRect.pivot = new Vector2(0f, 1f);
        picRect.anchoredPosition = new Vector2(40f, -40f);
        picRect.sizeDelta = new Vector2(270f, 338f);

        headingText = NewText("Heading", paperRect, bodyFont, 44f, Ink, TextAlignmentOptions.TopLeft);
        headingRect = headingText.rectTransform;
        TopBand(headingRect, 30f, 60f);

        pageSubText = NewText("PageSub", paperRect, bodyFont, 26f, InkSoft, TextAlignmentOptions.TopLeft);
        pageSubText.fontStyle = FontStyles.Italic;
        pageSubRect = pageSubText.rectTransform;
        TopBand(pageSubRect, 92f, 40f);

        // scrolling body
        var viewport = NewImage("Viewport", paperRect, new Color(1f, 1f, 1f, 0f));
        viewport.canvasRenderer.cullTransparentMesh = false; // stays a raycast target for the wheel
        viewport.gameObject.AddComponent<RectMask2D>();
        scrollRect = viewport.rectTransform;
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(50f, 30f);
        scrollRect.offsetMax = new Vector2(-60f, -140f);

        bodyText = NewText("Body", scrollRect, bodyFont, 27f, Ink, TextAlignmentOptions.TopLeft);
        bodyText.lineSpacing = 6f;
        bodyText.paragraphSpacing = 4f;
        var bodyRect = bodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.offsetMin = new Vector2(0f, 0f);
        bodyRect.offsetMax = new Vector2(0f, 0f);
        var fitter = bodyText.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = bodyRect;
        scroll.viewport = scrollRect;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        var bar = NewImage("Scrollbar", paperRect, new Color(0.30f, 0.25f, 0.20f, 0.25f));
        var barRect = bar.rectTransform;
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(1f, 0.5f);
        barRect.offsetMin = new Vector2(-34f, 30f);
        barRect.offsetMax = new Vector2(-24f, -140f);
        var handle = NewImage("Handle", barRect, new Color(0.30f, 0.25f, 0.20f, 0.8f));
        Stretch(handle.rectTransform);
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        // footer
        prevButton = NewButton("Previous", root, new Vector2(-300f, 80f), new Vector2(240f, 66f), 30f, () => ShowPage(pageIndex - 1));
        Anchor(prevButton, new Vector2(0.5f, 0f));
        nextButton = NewButton("Next", root, new Vector2(300f, 80f), new Vector2(240f, 66f), 30f, () => ShowPage(pageIndex + 1));
        Anchor(nextButton, new Vector2(0.5f, 0f));
        pageIndicator = NewText("PageIndicator", root, bodyFont, 28f, LabelOff, TextAlignmentOptions.Center);
        Place(pageIndicator.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(240f, 50f));
        closeButton = NewButton("Close", root, new Vector2(-220f, 80f), new Vector2(320f, 74f), 34f, Close);
        Anchor(closeButton, new Vector2(1f, 0f));
        closeLabel = closeButton.GetComponentInChildren<TMP_Text>(true);

        canvasRoot.SetActive(false);
    }

    private static void TopBand(RectTransform r, float top, float height)
    {
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.offsetMin = new Vector2(50f, -top - height);
        r.offsetMax = new Vector2(-50f, -top);
    }

    private static void Anchor(Button b, Vector2 anchor)
    {
        var r = (RectTransform)b.transform;
        Vector2 pos = r.anchoredPosition;
        r.anchorMin = r.anchorMax = anchor;
        r.anchoredPosition = pos;
    }

    private static void Stretch(RectTransform r, float inset = 0f)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset);
        r.offsetMax = new Vector2(-inset, -inset);
    }

    private static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    private TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        Material preset = font == bodyFont ? bodyMaterial : font == titleFont ? titleMaterial : null;
        if (preset != null) t.fontSharedMaterial = preset;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        t.richText = true;
        return t;
    }

    private Button NewButton(string label, Transform parent, Vector2 pos, Vector2 size, float fontSize, UnityEngine.Events.UnityAction onClick)
    {
        var img = NewImage(label + "Button", parent, Color.white);
        img.sprite = buttonSprite;
        img.type = Image.Type.Simple;
        var r = img.rectTransform;
        Place(r, new Vector2(0.5f, 0.5f), pos, size);

        var b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = img;
        b.onClick.AddListener(onClick);
        b.onClick.AddListener(PlayClick);

        var text = NewText("Label", r, bodyFont, fontSize, new Color(0.847f, 0.839f, 0.816f, 1f), TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 8f);
        text.text = label;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        // its Awake ran inside AddComponent, before these references existed, so it captured clear
        // resting colours; give it the real ones
        var punch = img.gameObject.AddComponent<ButtonHoverPunch>();
        punch.glowTarget = img;
        punch.hoverText = text;
        punch.SetBaseColors(Color.white, text.color);
        return b;
    }

    private static void MarkTab(Button b, bool on)
    {
        if (b == null) return;
        var punch = b.GetComponent<ButtonHoverPunch>();
        var label = b.GetComponentInChildren<TMP_Text>(true);
        if (punch != null) punch.SetBaseColors(on ? PlateOn : PlateOff, on ? LabelOn : LabelOff);
        else
        {
            b.image.color = on ? PlateOn : PlateOff;
            if (label != null) label.color = on ? LabelOn : LabelOff;
        }
    }

    private UIClickSound clickSound;

    private void PlayClick()
    {
        if (clickSound == null) clickSound = FindFirstObjectByType<UIClickSound>(FindObjectsInactive.Include);
        if (clickSound != null) clickSound.PlayClick();
    }

    // ---- the bag tab -----------------------------------------------------------------------------

    private GameObject bagPanel;

    // A tab hanging under the evidence bag panel (the panel itself reaches the top of the screen).
    private void AddBagTab()
    {
        var pm = FindFirstObjectByType<PauseManager>(FindObjectsInactive.Include);
        bagPanel = pm != null ? pm.bagMenuUI : null;
        if (bagPanel == null)
        {
            var toggle = FindFirstObjectByType<InventoryToggle>(FindObjectsInactive.Include);
            if (toggle != null) bagPanel = toggle.inventoryPanel;
        }
        if (bagPanel == null) return;

        bagTabButton = NewButton("Case File", bagPanel.transform, Vector2.zero, new Vector2(340f, 74f), 34f, OpenFromBag);
        bagTabButton.name = "CaseFileTab";
        var r = (RectTransform)bagTabButton.transform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = new Vector2(0f, -12f);
    }

    private void UpdateBagTab()
    {
        if (bagTabButton == null) return;
        bagTabButton.interactable = !IsOpen;
    }

    // ---- buttons on the Final Inspection ----------------------------------------------------------

    private void AddFinalInspectionButtons()
    {
        var fi = FinalInspection.Instance != null ? FinalInspection.Instance : FindFirstObjectByType<FinalInspection>(FindObjectsInactive.Include);
        if (fi == null || fi.rootPanel == null) return;

        wallFileButton = NewButton("Case File", fi.rootPanel.transform, Vector2.zero, new Vector2(230f, 62f), 28f, OpenFromFinalInspection);
        wallFileButton.name = "CaseFileButton";
        CornerTopRight(wallFileButton, new Vector2(-140f, -54f));

        wallGuideButton = NewButton("Guide", fi.rootPanel.transform, Vector2.zero, new Vector2(230f, 62f), 28f, OpenGuide);
        wallGuideButton.name = "GuideButton";
        CornerTopRight(wallGuideButton, new Vector2(-390f, -54f));

        wallFileButton.gameObject.SetActive(false);
        wallGuideButton.gameObject.SetActive(false);
    }

    private static void CornerTopRight(Button b, Vector2 pos)
    {
        var r = (RectTransform)b.transform;
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
    }

    private void OpenFromFinalInspection()
    {
        var fi = FinalInspection.Instance;
        Tab start = Tab.Evidence;
        if (fi != null && fi.CurrentStep == FinalInspection.Step.Accusation) start = Tab.People;
        Open(start);
    }

    private void UpdateFinalInspectionButtons()
    {
        if (wallFileButton == null) return;
        var fi = FinalInspection.Instance;
        bool show = fi != null && fi.IsOpen && (fi.CurrentStep == FinalInspection.Step.Wall
                    || fi.CurrentStep == FinalInspection.Step.Accusation
                    || fi.CurrentStep == FinalInspection.Step.Ruling
                    || fi.CurrentStep == FinalInspection.Step.Proof);
        if (wallFileButton.gameObject.activeSelf != show)
        {
            wallFileButton.gameObject.SetActive(show);
            wallGuideButton.gameObject.SetActive(show);
            if (show)
            {
                wallFileButton.transform.SetAsLastSibling();
                wallGuideButton.transform.SetAsLastSibling();
            }
        }
    }
}
