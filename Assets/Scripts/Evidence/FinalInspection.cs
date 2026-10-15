using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The Level 1 finale, opened from the unlocked Main Door. One calm screen at a time, each with a Next
// button: confirm -> Case Wall (red strings between clues) -> ruling -> accusation -> sign and stamp the
// report -> ending (best / partial / wrong) -> Case Summary with score, Retry and Main Menu.
// A wrong ruling or a wrong accused never reaches the summary: the ending says why the player's own pick
// doesn't fit, without naming the right one, and Try Again goes back to the ruling with the wall kept.
// The game is paused, movement and the gameplay HUD are off, and nothing is explained until the ending.
public class FinalInspection : MonoBehaviour
{
    public static FinalInspection Instance { get; private set; }

    // Lets HUD elements (e.g. ExitWaypoint) stand down while this screen owns the display.
    public bool IsOpen => open;

    [System.Serializable]
    public class Suspect
    {
        public string suspectName;
        public string role;
        [TextArea(2, 4)] public string dossier;
        public Sprite portrait;
        public bool isCulprit;
        [Tooltip("Shown when the player accuses this suspect wrongly: why the evidence clears them. Must not name the real culprit.")]
        [TextArea(2, 5)] public string wrongReason;
    }

    // Story was added last (the order on screen is set in Next(), not by these values).
    public enum Step { Confirm, Wall, Ruling, Accusation, Proof, Sign, Ending, Summary, Lesson, Story }
    private enum Tier { Best, Partial, Worst }

    // The three rulings the player picks between on the Ruling screen. The game teaches the real
    // Philippine Revised Penal Code meanings (the Guide and every lesson text use them), checked in order:
    //   Parricide (Art. 246) - killer is the victim's spouse, parent, child, or other direct line relative
    //   Murder    (Art. 248) - not parricide, but with a qualifying circumstance (treachery, evident
    //                          premeditation, superior strength, reward, fire/poison, cruelty)
    //   Homicide  (Art. 249) - neither of the above
    // Motive, or whether the killer was a stranger, never decides it. Level 1 is Murder (premeditated),
    // Level 2 is Parricide (Colette's own child), Level 3 is Homicide (one blow in a struggle, no tie).
    public enum Verdict { Parricide, Murder, Homicide }

    [Tooltip("The ruling this case's evidence actually supports. Level 1: Wren's recorded threat and his return that night show evident premeditation - Murder. Level 2: the key ledger, the alibi and the diary put the killing on Colette's own child - Parricide.")]
    public Verdict correctVerdict = Verdict.Murder;

    [Header("Case")]
    [Tooltip("Named on the signed report. Level 1: Dr. E. Vance. Level 2: Colette Marsh.")]
    public string deceasedName = "Dr. E. Vance";
    public Suspect[] suspects;
    public CaseLinkSet linkSet;
    [TextArea(4, 14)] public string whatReallyHappened;

    [Header("Endings")]
    public string bestTitle = "Case closed";
    [TextArea(3, 8)] public string bestBody;
    public string partialTitle = "Case unresolved";
    [Tooltip("Right ruling and right accused, but too few connections on the wall.")]
    [TextArea(3, 8)] public string partialFewLinksBody;

    [Header("Wrong report - explained, then Try Again")]
    public string worstTitle = "Wrong call";
    [Tooltip("Why a Parricide ruling doesn't fit this case. Shown only when the player ruled Parricide wrongly; must not name the right ruling.")]
    [TextArea(3, 8)] public string wrongParricideReason;
    [Tooltip("Why a Murder ruling doesn't fit this case. Must not name the right ruling.")]
    [TextArea(3, 8)] public string wrongMurderReason;
    [Tooltip("Why a Homicide ruling doesn't fit this case. Must not name the right ruling.")]
    [TextArea(3, 8)] public string wrongHomicideReason;
    [Tooltip("Closes every wrong report, over the Retry button (which restarts the level).")]
    public string retryLine = "Press Retry to start the case again from the beginning.";

    [Header("Score")]
    public int rulingPoints = 30;
    public int suspectPoints = 30;
    public int pointsPerLink = 8;
    [Tooltip("Each wrong string on the Case Wall costs this share of a link's points (rounded up). 0.5 = half a link: 5 when a link is worth 10, 2 when it is worth 4.")]
    [Range(0f, 1f)] public float wrongLinkPenaltyShare = 0.5f;
    [Tooltip("Each report sent back (wrong ruling or wrong accused) costs this share of the case's maximum score, rounded. 0.1 = 10 points on a 100 point case. The final score never goes below 0.")]
    [Range(0f, 1f)] public float wrongReportPenaltyShare = 0.1f;
    [Tooltip("Valid links needed, with the right ruling and suspect, for the best ending.")]
    public int bestMinLinks = 3;

    [Header("Screens")]
    public GameObject rootPanel;
    public TMP_Text titleText;
    public TMP_Text promptText;
    public GameObject confirmScreen;
    public GameObject wallScreen;
    public GameObject rulingScreen;
    public GameObject accusationScreen;
    public GameObject signScreen;
    public GameObject endingScreen;
    public GameObject summaryScreen;

    [Header("Footer")]
    public Button nextButton;
    public TMP_Text nextLabel;
    public Button backButton;
    public TMP_Text backLabel;
    public Button retryButton;
    public Button menuButton;

    [Header("Case Wall")]
    public CaseWall wall;

    [Header("Ruling")]
    [Tooltip("On for a case where the cause of death isn't in question (Level 2): the three verdict plates are hidden and the step becomes a read-and-confirm. The ruling then carries no score and the grade turns entirely on the accusation, proof and links.")]
    public bool fixedRuling = false;
    [Tooltip("Shown on the confirmation, e.g. \"Cause of Death: Homicide\".")]
    public string fixedRulingLabel = "Cause of Death: Homicide";
    [Tooltip("Holds the fixed-ruling text. Shown only when fixedRuling is on; the verdict plates below are hidden in that mode.")]
    public GameObject fixedRulingGroup;
    public TMP_Text fixedRulingText;
    [Tooltip("Parent of the three verdict plates, hidden wholesale when fixedRuling is on.")]
    public GameObject verdictButtonGroup;
    [Tooltip("The question over the three verdict plates.")]
    public string rulingPrompt = "How do you rule Dr. Vance's death?";
    public Button parricideButton;
    public Button murderButton;
    public Button homicideButton;

    [Header("Proof - cite up to N clues backing the accusation (optional step, after the accusation)")]
    public bool useProofStep = false;
    public GameObject proofScreen;
    [Tooltip("Parent with a layout group. One toggle per collected clue is generated in here.")]
    public RectTransform proofList;
    [Tooltip("An inactive template under proofList, cloned once per collected clue. Needs a Button and a TMP_Text somewhere beneath it.")]
    public GameObject proofEntryTemplate;
    public TMP_Text proofCountText;
    public int maxProofSelections = 3;
    [Tooltip("Score for each cited clue, by its EvidenceData.proofStrength. Citing a misleading clue scores nothing.")]
    public int pointsPerStrongProof = 10;
    public int pointsPerSupportingProof = 5;

    [Header("Accusation")]
    public Button[] suspectButtons;
    public TMP_Text[] suspectNames;
    public TMP_Text[] suspectRoles;
    public TMP_Text[] suspectDossiers;
    public Image[] suspectPortraits;

    [Header("Sign the report")]
    public TMP_Text reportText;
    public HoldToSignButton signButton;
    public Image signatureImage;
    public Button stampButton;
    public RectTransform stampGroup;
    public RectTransform paper;
    public TMP_Text signHint;

    [Header("Ending")]
    public Image ghostImage;
    public Image redFlash;
    public CanvasGroup endingTextGroup;
    public TMP_Text endingTitle;
    public TMP_Text endingBody;

    [Header("Summary")]
    public TMP_Text summaryText;
    public ScrollRect summaryScroll;
    public TMP_Text scoreText;

    [Header("Lesson - shown after the summary, on the way to the Main Menu (reuses the summary screen)")]
    public string lessonTitle = "What this case teaches";
    [Tooltip("Why this case was the ruling it was, and why not the other two. Leave empty to skip the lesson and go straight to the Main Menu.")]
    [TextArea(6, 24)] public string lessonBody;
    [Tooltip("Shown in the side panel: the three rulings at a glance. The case's ruling and the player's own are added underneath.")]
    [TextArea(6, 24)] public string lessonSideNote;

    [Header("Background")]
    public Image vignetteImage;

    [Header("Audio")]
    public AudioSource uiAudio;
    public AudioSource scratchAudio;

    private Step step;
    public Step CurrentStep => step;
    private bool open;
    private Verdict verdict;
    private bool ruled;
    private int suspectIndex = -1;
    private bool signed;
    private bool stamped;
    private bool locked;
    private bool endingReady;
    private bool wallBuilt; // Back can return to the wall; rebuilding it would wipe the pinned strings
    private Tier tier;
    private int score;
    private int wrongReports; // reports sent back since the level loaded (backing out does not clear it); costs points
    private Coroutine running;
    private readonly List<EvidenceData> citedProof = new List<EvidenceData>();
    private readonly List<GameObject> proofEntries = new List<GameObject>();
    private int proofScore;

    private Vector2 paperHome;
    private PauseManager pauseManager;
    private PlayerMovement playerMovement;
    private PlayerInteract playerInteract;
    private bool movementWas, interactWas;

    private void Awake()
    {
        Instance = this;
        if (rootPanel != null) rootPanel.SetActive(false);

        if (vignetteImage != null && vignetteImage.sprite == null) vignetteImage.sprite = FinalInspectionArt.Vignette();
        if (paper != null) paperHome = paper.anchoredPosition;

        // The world is muted while the inspection is up; this screen's own sounds must still play.
        if (uiAudio != null) { uiAudio.ignoreListenerPause = true; uiAudio.spatialBlend = 0f; uiAudio.playOnAwake = false; }
        if (scratchAudio != null) { scratchAudio.ignoreListenerPause = true; scratchAudio.spatialBlend = 0f; scratchAudio.playOnAwake = false; }
        if (wall != null && wall.audioSource == null) wall.audioSource = uiAudio;

        // The summary text isn't a raycast target, so nothing under the mouse caught the scroll wheel
        // except the thin scrollbar. An invisible image over the viewport takes the wheel everywhere.
        if (summaryScroll != null && summaryScroll.viewport != null && summaryScroll.viewport.GetComponent<Graphic>() == null)
        {
            var catcher = summaryScroll.viewport.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            catcher.canvasRenderer.cullTransparentMesh = false; // a culled (fully clear) image is skipped by raycasts
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        FillSuspects();

        if (nextButton != null) nextButton.onClick.AddListener(Next);
        if (backButton != null) backButton.onClick.AddListener(Back);
        if (retryButton != null) retryButton.onClick.AddListener(Retry);
        if (menuButton != null) menuButton.onClick.AddListener(MainMenu);
        if (parricideButton != null) parricideButton.onClick.AddListener(() => Rule(Verdict.Parricide));
        if (murderButton != null) murderButton.onClick.AddListener(() => Rule(Verdict.Murder));
        if (homicideButton != null) homicideButton.onClick.AddListener(() => Rule(Verdict.Homicide));
        if (suspectButtons != null)
            for (int i = 0; i < suspectButtons.Length; i++)
            {
                int index = i;
                if (suspectButtons[i] != null) suspectButtons[i].onClick.AddListener(() => Accuse(index));
            }
        if (stampButton != null) stampButton.onClick.AddListener(Stamp);
        if (signButton != null)
        {
            signButton.Progress += OnSignProgress;
            signButton.Completed += OnSignCompleted;
        }
    }

    private void FillSuspects()
    {
        if (suspects == null) return;
        for (int i = 0; i < suspects.Length; i++)
        {
            if (suspectNames != null && i < suspectNames.Length && suspectNames[i] != null) suspectNames[i].text = suspects[i].suspectName;
            if (suspectRoles != null && i < suspectRoles.Length && suspectRoles[i] != null) suspectRoles[i].text = suspects[i].role;
            if (suspectDossiers != null && i < suspectDossiers.Length && suspectDossiers[i] != null) suspectDossiers[i].text = suspects[i].dossier;
            // No art yet (Level 2) -> a silhouette rather than a blank white frame.
            if (suspectPortraits != null && i < suspectPortraits.Length && suspectPortraits[i] != null)
                suspectPortraits[i].sprite = suspects[i].portrait != null ? suspects[i].portrait : FinalInspectionArt.Silhouette();
        }
    }

    // ---- opening / closing ------------------------------------------------------------------

    // Called by the Main Door once every required clue has been found.
    public void Open()
    {
        if (open) return;
        open = true;

        verdict = Verdict.Parricide; ruled = false; suspectIndex = -1;
        signed = stamped = locked = endingReady = false;
        // wallBuilt is NOT reset: strings pinned before the player backed out ("Not yet") stay on the wall
        citedProof.Clear();
        proofScore = 0;

        if (pauseManager == null) pauseManager = FindFirstObjectByType<PauseManager>();
        if (playerMovement == null) playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerInteract == null) playerInteract = FindFirstObjectByType<PlayerInteract>();

        // The bag, if open, is closed first so nothing sits between the player and the inspection.
        if (PauseManager.isBagOpen && pauseManager != null) pauseManager.ToggleBag();

        if (playerMovement != null) { movementWas = playerMovement.enabled; playerMovement.enabled = false; }
        if (playerInteract != null) { interactWas = playerInteract.enabled; playerInteract.enabled = false; }

        if (rootPanel != null)
        {
            rootPanel.SetActive(true);
            rootPanel.transform.SetAsLastSibling();
        }

        if (pauseManager != null) pauseManager.SetCaseUiOpen(true);
        else
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        // The world (and the ghost) goes silent for the inspection; this UI's own audio ignores the pause.
        AudioListener.pause = true;

        ShowStep(Step.Confirm);
    }

    private void Cancel()
    {
        if (!open) return;
        open = false;
        if (rootPanel != null) rootPanel.SetActive(false);

        AudioListener.pause = false;
        if (playerMovement != null) playerMovement.enabled = movementWas;
        if (playerInteract != null) playerInteract.enabled = interactWas;
        if (pauseManager != null) pauseManager.SetCaseUiOpen(false);
        else
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // ---- flow -------------------------------------------------------------------------------

    private void ShowStep(Step next)
    {
        step = next;
        if (running != null) { StopCoroutine(running); running = null; }
        if (scratchAudio != null) scratchAudio.Stop();

        Show(confirmScreen, next == Step.Confirm);
        Show(wallScreen, next == Step.Wall);
        Show(rulingScreen, next == Step.Ruling);
        Show(accusationScreen, next == Step.Accusation);
        Show(proofScreen, next == Step.Proof);
        Show(signScreen, next == Step.Sign);
        Show(endingScreen, next == Step.Ending);
        // the lesson is laid out on the summary screen: reading text on the left, a side panel on the right
        Show(summaryScreen, next == Step.Summary || next == Step.Lesson || next == Step.Story);

        // Every step before signing can step back one screen; picks made there are kept.
        bool showBack = next == Step.Confirm || next == Step.Lesson || next == Step.Wall
                        || next == Step.Ruling || next == Step.Accusation || next == Step.Proof;
        Show(backButton != null ? backButton.gameObject : null, showBack);
        if (backLabel != null) backLabel.text = next == Step.Confirm ? "Not yet" : "Back";

        bool summary = next == Step.Summary;
        Show(nextButton != null ? nextButton.gameObject : null, !summary);
        Show(retryButton != null ? retryButton.gameObject : null, summary);
        Show(menuButton != null ? menuButton.gameObject : null, summary);
        // the summary's menu button leads on to the lesson when there is one, so it reads Next there
        if (summary && menuButton != null)
        {
            var menuLabel = menuButton.GetComponentInChildren<TMP_Text>(true);
            if (menuLabel != null) menuLabel.text = string.IsNullOrWhiteSpace(lessonBody) ? "Main Menu" : "Next";
        }

        switch (next)
        {
            case Step.Confirm:
                SetTitle("Final Inspection");
                SetPrompt("");
                SetNext("Begin", true);
                break;

            case Step.Wall:
                SetTitle("The Case Wall");
                SetNext("Next", true);
                if (!wallBuilt || WallCluesChanged()) { BuildWall(); wallBuilt = true; }
                UpdateWallPrompt();
                break;

            case Step.Ruling:
                SetTitle("Your ruling");
                Show(verdictButtonGroup, !fixedRuling);
                Show(fixedRulingGroup, fixedRuling);
                if (fixedRuling)
                {
                    // Nothing to decide - the cause of death is established, the player just reads and
                    // moves on. ruled stays true so the grade below treats the ruling as settled.
                    ruled = true;
                    if (fixedRulingText != null) fixedRulingText.text = fixedRulingLabel;
                    SetPrompt("The cause of death isn't in question here.");
                    SetNext("Next", true);
                }
                else
                {
                    SetPrompt(rulingPrompt);
                    Mark(parricideButton, ruled && verdict == Verdict.Parricide);
                    Mark(murderButton, ruled && verdict == Verdict.Murder);
                    Mark(homicideButton, ruled && verdict == Verdict.Homicide);
                    SetNext("Next", ruled);
                }
                break;

            case Step.Proof:
                SetTitle("Your proof");
                SetPrompt("Cite up to " + maxProofSelections + " clues that back the accusation.");
                BuildProofList();
                SetNext("Next", citedProof.Count > 0);
                break;

            case Step.Accusation:
                SetTitle("The accused");
                SetPrompt("Who did it?");
                for (int i = 0; i < (suspectButtons != null ? suspectButtons.Length : 0); i++) Mark(suspectButtons[i], i == suspectIndex);
                SetNext("Next", suspectIndex >= 0);
                break;

            case Step.Sign:
                SetTitle("Sign the report");
                SetPrompt("");
                PrepareSignScreen();
                SetNext("Next", locked);
                break;

            case Step.Ending:
                SetPrompt("");
                SetNext("Next", false);
                running = StartCoroutine(EndingRoutine());
                break;

            case Step.Summary:
                SetTitle("Case Summary");
                SetPrompt("");
                BuildSummary();
                break;

            case Step.Story:
                SetTitle("What really happened");
                SetPrompt("");
                BuildStory();
                SetNext("Next", true);
                break;

            case Step.Lesson:
                SetTitle(lessonTitle);
                SetPrompt("");
                BuildLesson();
                SetNext("Main Menu", true);
                break;
        }
    }

    private void Next()
    {
        switch (step)
        {
            case Step.Confirm: ShowStep(Step.Wall); break;
            // Who did it comes before which crime it was: Parricide turns on who the killer is to the
            // victim, so the ruling can only be judged once the accused is named.
            case Step.Wall: ShowStep(Step.Accusation); break;
            case Step.Accusation: ShowStep(Step.Ruling); break;
            case Step.Ruling: ShowStep(useProofStep ? Step.Proof : Step.Sign); break;
            case Step.Proof: ShowStep(Step.Sign); break;
            case Step.Sign: if (locked) ShowStep(Step.Ending); break;
            // a correct report first tells the whole story, then the summary scores it
            case Step.Ending: if (endingReady && tier != Tier.Worst) ShowStep(HasStory ? Step.Story : Step.Summary); break;
            case Step.Story: ShowStep(Step.Summary); break;
            case Step.Lesson: LoadMainMenu(); break;
        }
    }

    // A wrong report offers the summary's own two buttons: Retry restarts the level, Main Menu leaves.
    private void ShowWrongReportFooter()
    {
        Show(nextButton != null ? nextButton.gameObject : null, false);
        Show(retryButton != null ? retryButton.gameObject : null, true);
        Show(menuButton != null ? menuButton.gameObject : null, true);
        SetButtonLabel(retryButton, "Retry");
        SetButtonLabel(menuButton, "Main Menu");
    }

    private static void SetButtonLabel(Button b, string label)
    {
        if (b == null) return;
        var text = b.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;
    }

    private void Back()
    {
        switch (step)
        {
            case Step.Confirm: Cancel(); break;
            case Step.Wall: ShowStep(Step.Confirm); break;
            case Step.Accusation: ShowStep(Step.Wall); break;
            case Step.Ruling: ShowStep(Step.Accusation); break;
            case Step.Proof: ShowStep(Step.Ruling); break;
            case Step.Lesson: ShowStep(Step.Summary); break;
        }
    }

    private static void Show(GameObject go, bool on) { if (go != null && go.activeSelf != on) go.SetActive(on); }
    private void SetTitle(string s) { if (titleText != null) titleText.text = s; }
    private void SetPrompt(string s) { if (promptText != null) promptText.text = s; }

    private void SetNext(string label, bool enabled)
    {
        if (nextLabel != null) nextLabel.text = label;
        if (nextButton != null) nextButton.interactable = enabled;
    }

    // Neutral "this is the one I picked" look for plate buttons.
    private static void Mark(Button b, bool on)
    {
        if (b == null) return;
        var card = b.GetComponent<SelectableCard>();
        if (card != null) { card.SetSelected(on); return; }
        var punch = b.GetComponent<ButtonHoverPunch>();
        Color plate = on ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
        Color text = on ? new Color(0.96f, 0.90f, 0.72f, 1f) : new Color(0.72f, 0.70f, 0.66f, 1f);
        if (punch != null) punch.SetBaseColors(plate, text);
        else
        {
            var img = b.GetComponent<Image>();
            if (img != null) img.color = plate;
        }
    }

    // ---- case wall --------------------------------------------------------------------------

    private List<EvidenceItem> CollectedClues()
    {
        var list = new List<EvidenceItem>();
        foreach (var item in CaseFile.Items)
            if (item != null && item.data != null && CaseFile.IsInBag(item.DisplayName)) list.Add(item);
        list.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
        return list;
    }

    private readonly List<EvidenceData> wallClues = new List<EvidenceData>();

    // Rebuilding lays the cards out again; the strings already pinned are carried over onto it.
    private void BuildWall()
    {
        if (wall == null) return;
        var keepLinks = new List<CaseLinkSet.Link>(wall.MadeLinks);
        var keepWrong = new List<(EvidenceData a, EvidenceData b)>(wall.WrongPairs);
        var clues = CollectedClues();

        wall.linkSet = linkSet;
        Canvas.ForceUpdateCanvases();
        wall.Build(clues);
        wallClues.Clear();
        foreach (var c in clues) wallClues.Add(c.data);
        wall.LinksChanged -= UpdateWallPrompt;
        wall.LinksChanged += UpdateWallPrompt;
        if (keepLinks.Count > 0 || keepWrong.Count > 0) wall.RestoreStrings(keepLinks, keepWrong);
    }

    // The player can leave the inspection and come back with more clues in the bag; the board then
    // needs a card for each new one.
    private bool WallCluesChanged()
    {
        var clues = CollectedClues();
        if (clues.Count != wallClues.Count) return true;
        for (int i = 0; i < clues.Count; i++) if (clues[i].data != wallClues[i]) return true;
        return false;
    }

    private int WrongLinkPenalty => Mathf.CeilToInt(pointsPerLink * wrongLinkPenaltyShare);

    private int MaxScore
    {
        get
        {
            int maxLinks = linkSet != null ? linkSet.links.Count : 5;
            int maxProof = useProofStep ? maxProofSelections * pointsPerStrongProof : 0;
            return (fixedRuling ? 0 : rulingPoints) + suspectPoints + maxLinks * pointsPerLink + maxProof;
        }
    }

    private int WrongReportPenalty => Mathf.RoundToInt(MaxScore * wrongReportPenaltyShare);

    // Tells the player how many connections exist, so they know the rest of the clues (the red herrings)
    // aren't meant to connect, and how many strings they've pinned. Right strings show their proof tag on
    // the wall; which ones were wrong, and what they cost, is only shown on the Case Summary.
    private void UpdateWallPrompt()
    {
        if (wall == null) { SetPrompt("Drag a string from one clue to another."); return; }
        int total = wall.AvailableLinks;
        int pinned = wall.ValidLinks + wall.InvalidAttempts;

        // One short goal line, then the how and the cost on a smaller, quieter line under it, so the
        // goal reads at a glance instead of one run on sentence wrapping across the screen.
        string goal = total > 0
            ? "Find the " + total + " connections. Not every clue connects."
            : "Connect the clues that belong together.";
        var details = new List<string> { "Drag between two clues to pin a string", "Click a clue to read it" };
        if (WrongLinkPenalty > 0) details.Add("Wrong string: " + WrongLinkPenalty + " points off");
        if (pinned > 0) details.Add("Pinned: " + pinned);

        SetPrompt("<size=30>" + goal + "</size>\n<size=22><color=#9C958A>"
                  + string.Join("     •     ", details) + "</color></size>");
    }

    // ---- ruling / accusation ----------------------------------------------------------------

    private void Rule(Verdict chosen)
    {
        ruled = true;
        verdict = chosen;
        Mark(parricideButton, chosen == Verdict.Parricide);
        Mark(murderButton, chosen == Verdict.Murder);
        Mark(homicideButton, chosen == Verdict.Homicide);
        SetNext("Next", true);
        Deselect();
    }

    // fixedRulingLabel reads "Cause of Death: Homicide" on screen; the report wants just the finding.
    private static string StripLabel(string labelled)
    {
        if (string.IsNullOrEmpty(labelled)) return "Homicide";
        int colon = labelled.LastIndexOf(':');
        return colon >= 0 && colon < labelled.Length - 1 ? labelled.Substring(colon + 1).Trim() : labelled.Trim();
    }

    private bool RuledCorrectly => fixedRuling || (ruled && verdict == correctVerdict);

    private bool SuspectRight =>
        suspects != null && suspectIndex >= 0 && suspectIndex < suspects.Length && suspects[suspectIndex].isCulprit;

    private void Accuse(int index)
    {
        suspectIndex = index;
        for (int i = 0; i < suspectButtons.Length; i++) Mark(suspectButtons[i], i == index);
        SetNext("Next", true);
        Deselect();
    }

    private static void Deselect()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    }

    // ---- proof ------------------------------------------------------------------------------

    // Generated from whatever the player actually collected rather than authored per case, so the
    // list can't offer a clue they never found (or miss one they did).
    private void BuildProofList()
    {
        foreach (var go in proofEntries) if (go != null) Destroy(go);
        proofEntries.Clear();

        if (proofList == null || proofEntryTemplate == null) return;
        proofEntryTemplate.SetActive(false);

        foreach (var item in CollectedClues())
        {
            EvidenceData data = item.data;
            var entry = Instantiate(proofEntryTemplate, proofList);
            entry.name = "Proof_" + data.displayName;
            entry.SetActive(true);

            var label = entry.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = data.displayName;

            var button = entry.GetComponent<Button>();
            if (button == null) button = entry.GetComponentInChildren<Button>(true);
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => ToggleProof(data, button));
                Mark(button, citedProof.Contains(data));
            }

            proofEntries.Add(entry);
        }

        UpdateProofCount();
    }

    private void ToggleProof(EvidenceData data, Button button)
    {
        if (citedProof.Contains(data)) citedProof.Remove(data);
        else if (citedProof.Count < maxProofSelections) citedProof.Add(data);
        else return; // at the cap - deselect something first rather than silently swapping

        Mark(button, citedProof.Contains(data));
        UpdateProofCount();
        SetNext("Next", citedProof.Count > 0);
        Deselect();
    }

    private void UpdateProofCount()
    {
        if (proofCountText != null)
            proofCountText.text = citedProof.Count + " / " + maxProofSelections + " cited";
    }

    // A misleading clue proves nothing no matter how confidently it's cited.
    private int ScoreProof()
    {
        int total = 0;
        foreach (var data in citedProof)
        {
            if (data == null || data.misleading) continue;
            if (data.proofStrength == ProofStrength.Strong) total += pointsPerStrongProof;
            else if (data.proofStrength == ProofStrength.Supporting) total += pointsPerSupportingProof;
        }
        return total;
    }

    // ---- sign and stamp ---------------------------------------------------------------------

    private void PrepareSignScreen()
    {
        signed = stamped = locked = false;

        var sb = new StringBuilder();
        sb.Append("CASE REPORT\n\n");
        sb.Append("Deceased:  ").Append(deceasedName).Append('\n');
        sb.Append("Finding:  ").Append(fixedRuling ? StripLabel(fixedRulingLabel) : verdict.ToString()).Append('\n');
        if (suspectIndex >= 0 && suspects != null) sb.Append("Accused:  ").Append(suspects[suspectIndex].suspectName).Append('\n');
        int links = wall != null ? wall.ValidLinks : 0;
        sb.Append("Connections made:  ").Append(links).Append('\n');
        if (useProofStep && citedProof.Count > 0)
        {
            sb.Append("Cited:  ");
            for (int i = 0; i < citedProof.Count; i++)
            {
                if (i > 0) sb.Append(",  ");
                sb.Append(citedProof[i].displayName);
            }
            sb.Append('\n');
        }
        sb.Append('\n');
        sb.Append("I certify that this report is complete and accurate.");
        if (reportText != null) reportText.text = sb.ToString();

        if (signatureImage != null) { signatureImage.fillAmount = 0f; signatureImage.gameObject.SetActive(true); }
        if (stampGroup != null) stampGroup.gameObject.SetActive(false);
        Show(signButton != null ? signButton.gameObject : null, true);
        Show(stampButton != null ? stampButton.gameObject : null, false);
        if (signHint != null) signHint.text = "Hold to sign.";
        if (paper != null) paper.anchoredPosition = paperHome;
    }

    private void OnSignProgress(float p)
    {
        if (signatureImage != null) signatureImage.fillAmount = p;
        if (scratchAudio != null)
        {
            if (signButton != null && signButton.Holding && !scratchAudio.isPlaying)
            {
                scratchAudio.clip = FinalInspectionArt.Scratch();
                scratchAudio.loop = true;
                scratchAudio.volume = 0.6f * SfxVolume.Multiplier;
                scratchAudio.Play();
            }
            else if (signButton != null && !signButton.Holding && scratchAudio.isPlaying) scratchAudio.Stop();
        }
    }

    private void OnSignCompleted()
    {
        if (step != Step.Sign) return;
        signed = true;
        if (scratchAudio != null) scratchAudio.Stop();
        if (signatureImage != null) signatureImage.fillAmount = 1f;
        Show(signButton != null ? signButton.gameObject : null, false);
        Show(stampButton != null ? stampButton.gameObject : null, true);
        if (signHint != null) signHint.text = "Now stamp it.";
    }

    private void Stamp()
    {
        if (!signed || stamped) return;
        stamped = true;
        Show(stampButton != null ? stampButton.gameObject : null, false);
        running = StartCoroutine(StampRoutine());
    }

    private IEnumerator StampRoutine()
    {
        if (stampGroup != null)
        {
            stampGroup.gameObject.SetActive(true);
            float t = 0f;
            const float slam = 0.22f;
            while (t < slam)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / slam);
                stampGroup.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, k * k);
                yield return null;
            }
            stampGroup.localScale = Vector3.one;
        }

        if (uiAudio != null) uiAudio.PlayOneShot(FinalInspectionArt.Thud(), SfxVolume.Multiplier);

        // the paper jolts once under the stamp
        if (paper != null)
        {
            float t = 0f;
            while (t < 0.28f)
            {
                t += Time.unscaledDeltaTime;
                float decay = 1f - t / 0.28f;
                paper.anchoredPosition = paperHome + new Vector2(Mathf.Sin(t * 70f) * 7f * decay, Mathf.Cos(t * 55f) * 5f * decay);
                yield return null;
            }
            paper.anchoredPosition = paperHome;
        }

        yield return new WaitForSecondsRealtime(0.7f);

        locked = true;
        if (signHint != null) signHint.text = "The report is filed.";
        SetNext("Next", true);
        running = null;
    }

    // ---- ending -----------------------------------------------------------------------------

    private void ComputeResult()
    {
        int links = wall != null ? wall.ValidLinks : 0;
        bool ruledRight = RuledCorrectly;
        bool suspectRight = SuspectRight;
        proofScore = useProofStep ? ScoreProof() : 0;

        // A wrong ruling or a wrong accused is sent back rather than filed, so it scores and records nothing.
        if (!ruledRight || !suspectRight)
        {
            tier = Tier.Worst;
            wrongReports++;
            return;
        }
        tier = links >= bestMinLinks ? Tier.Best : Tier.Partial;

        int maxLinks = linkSet != null ? linkSet.links.Count : 5;
        int rulingScore = fixedRuling ? 0 : (ruledRight ? rulingPoints : 0);
        int wrongCost = (wall != null ? wall.InvalidAttempts : 0) * WrongLinkPenalty;
        int reportCost = wrongReports * WrongReportPenalty;
        score = Mathf.Max(0, rulingScore + (suspectRight ? suspectPoints : 0) + Mathf.Min(links, maxLinks) * pointsPerLink + proofScore - wrongCost - reportCost);
        AssessmentResults.RecordClassification(tier == Tier.Best);
        // "Case closed" marks the level solved on the map selection.
        LevelProgress.RecordResult(SceneManager.GetActiveScene().name, tier == Tier.Best, score);
    }

    // Says why the player's own picks don't fit, one paragraph for each wrong part, and never which ruling
    // or which suspect would: the player has to go back to the evidence to find that.
    private string WrongReportBody()
    {
        var sb = new StringBuilder();
        if (!RuledCorrectly)
        {
            string reason = verdict == Verdict.Parricide ? wrongParricideReason
                          : verdict == Verdict.Murder ? wrongMurderReason
                          : wrongHomicideReason;
            sb.Append("<b>Your ruling: ").Append(verdict.ToString()).Append("</b>\n")
              .Append(string.IsNullOrWhiteSpace(reason) ? "The evidence doesn't fit that ruling." : reason);
        }
        if (!SuspectRight)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            Suspect accused = suspects != null && suspectIndex >= 0 && suspectIndex < suspects.Length ? suspects[suspectIndex] : null;
            sb.Append("<b>Your accused: ").Append(accused != null ? accused.suspectName : "nobody").Append("</b>\n")
              .Append(accused == null || string.IsNullOrWhiteSpace(accused.wrongReason) ? "The evidence doesn't hold against them." : accused.wrongReason);
        }
        if (!string.IsNullOrWhiteSpace(retryLine)) sb.Append("\n\n<i>").Append(retryLine).Append("</i>");
        if (WrongReportPenalty > 0)
            sb.Append("\n<i>Each wrong report takes ").Append(WrongReportPenalty).Append(" points off the case score. Wrong reports so far: ")
              .Append(wrongReports).Append(".</i>");
        return sb.ToString();
    }

    private Vector2 ghostHome;
    private bool ghostHomeKnown;

    private IEnumerator EndingRoutine()
    {
        ComputeResult();

        string title, body;
        if (tier == Tier.Best) { title = bestTitle; body = bestBody; }
        else if (tier == Tier.Worst) { title = worstTitle; body = WrongReportBody(); }
        else { title = partialTitle; body = partialFewLinksBody; }

        SetTitle("");
        if (endingTitle != null) endingTitle.text = title;
        if (endingBody != null)
        {
            // a wrong report can carry two reasons, more than the box was sized for - shrink to fit, never grow
            if (!endingBody.enableAutoSizing)
            {
                endingBody.fontSizeMax = endingBody.fontSize;
                endingBody.fontSizeMin = 20f;
                endingBody.enableAutoSizing = true;
            }
            endingBody.text = body;
        }
        if (endingTextGroup != null) endingTextGroup.alpha = 0f;
        if (redFlash != null) { var c = redFlash.color; c.a = 0f; redFlash.color = c; }
        if (ghostImage != null)
        {
            // Back to where the scene puts it (left of the ending text), not the screen centre - zero
            // sat the ghost right under the title and body.
            if (!ghostHomeKnown) { ghostHome = ghostImage.rectTransform.anchoredPosition; ghostHomeKnown = true; }
            ghostImage.gameObject.SetActive(true);
            ghostImage.rectTransform.localScale = Vector3.one;
            ghostImage.rectTransform.anchoredPosition = ghostHome;
            SetGhost(0f, new Color(0.78f, 0.90f, 1f));
        }

        yield return new WaitForSecondsRealtime(0.5f);

        if (tier == Tier.Best)
        {
            if (uiAudio != null) uiAudio.PlayOneShot(FinalInspectionArt.Chime(), SfxVolume.Multiplier);
            yield return Fade(a => SetGhost(a, new Color(0.78f, 0.90f, 1f)), 0f, 0.85f, 1.4f);
            yield return Fade(a => { if (endingTextGroup != null) endingTextGroup.alpha = a; }, 0f, 1f, 1.6f);
            yield return new WaitForSecondsRealtime(1.2f);
            // the ghost calmly fades and drifts upward
            float t = 0f;
            const float dur = 6f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = t / dur;
                SetGhost(Mathf.Lerp(0.85f, 0f, k), Color.Lerp(new Color(0.78f, 0.90f, 1f), Color.white, k));
                if (ghostImage != null) ghostImage.rectTransform.anchoredPosition = ghostHome + new Vector2(0f, Mathf.Lerp(0f, 60f, k));
                yield return null;
            }
        }
        else if (tier == Tier.Partial)
        {
            yield return Fade(a => SetGhost(a, new Color(0.70f, 0.78f, 0.86f)), 0f, 0.45f, 1.6f);
            yield return Fade(a => { if (endingTextGroup != null) endingTextGroup.alpha = a; }, 0f, 1f, 1.4f);
            // it lingers - a slow, uneasy flicker for as long as the player reads
            running = null;
            StartCoroutine(Flicker());
        }
        else
        {
            yield return Fade(a => SetGhost(a, new Color(0.55f, 0.62f, 0.72f)), 0f, 0.55f, 1.6f);
            yield return new WaitForSecondsRealtime(0.6f);
            // it turns: red, closer, then the lunge with the ghost's own attack sound
            float t = 0f;
            const float turn = 0.9f;
            while (t < turn)
            {
                t += Time.unscaledDeltaTime;
                float k = t / turn;
                SetGhost(Mathf.Lerp(0.55f, 0.9f, k), Color.Lerp(new Color(0.55f, 0.62f, 0.72f), new Color(0.85f, 0.08f, 0.08f), k));
                if (ghostImage != null) ghostImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.25f, k);
                yield return null;
            }

            PlayGhostAttack();

            t = 0f;
            const float lunge = 0.22f;
            while (t < lunge)
            {
                t += Time.unscaledDeltaTime;
                float k = t / lunge;
                if (ghostImage != null) ghostImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 2.6f, k * k);
                if (redFlash != null) { var c = redFlash.color; c.a = Mathf.Lerp(0f, 0.8f, k); redFlash.color = c; }
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.25f);

            t = 0f;
            while (t < 1.2f)
            {
                t += Time.unscaledDeltaTime;
                float k = t / 1.2f;
                if (redFlash != null) { var c = redFlash.color; c.a = Mathf.Lerp(0.8f, 0.25f, k); redFlash.color = c; }
                SetGhost(Mathf.Lerp(0.9f, 0.35f, k), new Color(0.6f, 0.06f, 0.06f));
                yield return null;
            }
            yield return Fade(a => { if (endingTextGroup != null) endingTextGroup.alpha = a; }, 0f, 1f, 1.4f);
        }

        endingReady = true;
        if (tier == Tier.Worst) ShowWrongReportFooter();
        else SetNext("Next", true);
        running = null;
    }

    private IEnumerator Flicker()
    {
        while (step == Step.Ending && tier == Tier.Partial)
        {
            float a = 0.28f + 0.22f * Mathf.PerlinNoise(Time.unscaledTime * 0.9f, 3f);
            SetGhost(a, new Color(0.70f, 0.78f, 0.86f));
            yield return null;
        }
    }

    private void PlayGhostAttack()
    {
        // Reuse the ghost's own scream, and the game's existing full-screen jumpscare slam when present.
        AudioClip clip = null;
        var ghost = FindFirstObjectByType<GhostController>();
        if (ghost != null && ghost.attackClips != null && ghost.attackClips.Length > 0)
            clip = ghost.attackClips[Random.Range(0, ghost.attackClips.Length)];

        if (Jumpscare2D.Instance != null)
        {
            var src = Jumpscare2D.Instance.GetComponent<AudioSource>();
            if (src != null) src.ignoreListenerPause = true;
            Jumpscare2D.Instance.Show(null, clip);
        }
        else if (clip != null && uiAudio != null) uiAudio.PlayOneShot(clip, SfxVolume.Multiplier);
    }

    private void SetGhost(float alpha, Color tint)
    {
        if (ghostImage == null) return;
        tint.a = alpha;
        ghostImage.color = tint;
    }

    private static IEnumerator Fade(System.Action<float> apply, float from, float to, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            apply(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds)));
            yield return null;
        }
        apply(to);
    }

    // ---- summary ----------------------------------------------------------------------------

    private void BuildSummary()
    {
        var sb = new StringBuilder();

        // The wall never said which strings were right; this is where the player finds out.
        if (wall != null && (wall.MadeLinks.Count > 0 || wall.WrongPairs.Count > 0))
        {
            sb.Append("<size=120%>Your strings</size>\n\n");
            foreach (var l in wall.MadeLinks)
                sb.Append("<b>").Append(l.clueA.displayName).Append(" + ").Append(l.clueB.displayName).Append("</b>   (right)\n")
                  .Append(l.proves).Append("\n\n");
            foreach (var p in wall.WrongPairs)
                sb.Append("<b>").Append(p.a.displayName).Append(" + ").Append(p.b.displayName).Append("</b>   (wrong, lost ")
                  .Append(WrongLinkPenalty).Append(")\n").Append("These two clues don't connect.\n\n");
            sb.Append('\n');
        }

        sb.Append("<size=120%>What each clue meant</size>\n\n");

        var items = new List<EvidenceItem>();
        foreach (var item in CaseFile.Items) if (item != null && item.data != null) items.Add(item);
        items.Sort((a, b) =>
        {
            int c = b.data.proofStrength.CompareTo(a.data.proofStrength);
            return c != 0 ? c : string.CompareOrdinal(a.DisplayName, b.DisplayName);
        });

        foreach (var item in items)
        {
            sb.Append("<b>").Append(item.DisplayName).Append("</b>");
            if (item.data.misleading) sb.Append("   (a false lead)");
            if (!CaseFile.IsInBag(item.DisplayName)) sb.Append("   (you never found this)");
            sb.Append('\n').Append(item.data.endingMeaning).Append("\n\n");
        }
        sb.Append("\n<size=120%>What really happened</size>\n\n").Append(whatReallyHappened).Append("\n");
        if (summaryText != null) summaryText.text = sb.ToString();
        if (summaryScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            summaryScroll.verticalNormalizedPosition = 1f;
        }

        if (scoreText != null)
        {
            int links = wall != null ? wall.ValidLinks : 0;
            int invalid = wall != null ? wall.InvalidAttempts : 0;
            int maxLinks = linkSet != null ? linkSet.links.Count : 5;
            bool ruledRight = RuledCorrectly;
            bool suspectRight = SuspectRight;
            int maxProof = useProofStep ? maxProofSelections * pointsPerStrongProof : 0;
            int max = (fixedRuling ? 0 : rulingPoints) + suspectPoints + maxLinks * pointsPerLink + maxProof;
            string rank = LevelProgress.Rank(score);

            var s = new StringBuilder();
            s.Append("<size=130%>Score</size>\n\n");
            if (fixedRuling) s.Append("Ruling\n   ").Append(StripLabel(fixedRulingLabel)).Append("   (established)\n\n");
            else s.Append("Ruling\n   ").Append(verdict.ToString()).Append("   ").Append(ruledRight ? rulingPoints : 0).Append(" / ").Append(rulingPoints).Append("\n\n");
            s.Append("Accusation\n   ").Append(suspectRight ? suspectPoints : 0).Append(" / ").Append(suspectPoints).Append("\n\n");
            s.Append("Links made\n   ").Append(links).Append(" of ").Append(maxLinks).Append("   ").Append(Mathf.Min(links, maxLinks) * pointsPerLink).Append(" / ").Append(maxLinks * pointsPerLink).Append("\n\n");
            s.Append("Wrong strings\n   ").Append(invalid);
            if (invalid > 0) s.Append("   lost ").Append(invalid * WrongLinkPenalty);
            s.Append("\n\n");
            if (wrongReports > 0) s.Append("Wrong reports\n   ").Append(wrongReports).Append("   lost ").Append(wrongReports * WrongReportPenalty).Append("\n\n");
            if (useProofStep)
                s.Append("Proof cited\n   ").Append(citedProof.Count).Append(" of ").Append(maxProofSelections)
                 .Append("   ").Append(proofScore).Append(" / ").Append(maxProofSelections * pointsPerStrongProof).Append("\n\n");
            s.Append('\n');
            s.Append("<size=130%>").Append(score).Append(" / ").Append(max).Append("</size>\n").Append(rank);
            scoreText.text = s.ToString();
        }
    }

    // ---- retry / menu -----------------------------------------------------------------------

    private static void LeaveScene()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    // After a wrong report, Retry goes back to the accusation with the wall and the picks kept (marked), so
    // the player can rethink the accused and the ruling from the evidence. On the summary it restarts the
    // level: reloading the scene resets every clue, spawn, counter and marker.
    private void Retry()
    {
        if (step == Step.Ending && tier == Tier.Worst)
        {
            SendBackToAccusation();
            return;
        }
        LeaveScene();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void SendBackToAccusation()
    {
        if (running != null) { StopCoroutine(running); running = null; }
        signed = stamped = locked = endingReady = false;
        if (redFlash != null) { var c = redFlash.color; c.a = 0f; redFlash.color = c; }
        if (ghostImage != null) { ghostImage.rectTransform.localScale = Vector3.one; SetGhost(0f, Color.white); }
        if (endingTextGroup != null) endingTextGroup.alpha = 0f;
        SetButtonLabel(menuButton, "Main Menu");
        ShowStep(Step.Accusation);
    }

    // ---- what really happened ---------------------------------------------------------------

    private CaseStoryData Story => CaseFileUI.Instance != null ? CaseFileUI.Instance.story : null;
    private bool HasStory => Story != null && !string.IsNullOrWhiteSpace(Story.storyReveal);

    // The full account, on its own screen right after "Case closed": the story on the left, the case in
    // brief on the right. Only reached from a correct report, so it never gives away an answer.
    private void BuildStory()
    {
        if (summaryText != null) summaryText.text = Story != null ? Story.storyReveal : whatReallyHappened;
        if (summaryScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            summaryScroll.verticalNormalizedPosition = 1f;
        }

        if (scoreText != null)
        {
            var s = new StringBuilder();
            s.Append("<size=130%>The case</size>\n\n");
            s.Append("Deceased\n   ").Append(deceasedName).Append("\n\n");
            Suspect culprit = null;
            if (suspects != null) foreach (var x in suspects) if (x != null && x.isCulprit) { culprit = x; break; }
            if (culprit != null) s.Append("Killer\n   ").Append(culprit.suspectName).Append("\n   ").Append(culprit.role).Append("\n\n");
            s.Append("Ruling\n   ").Append(fixedRuling ? StripLabel(fixedRulingLabel) : correctVerdict.ToString());
            if (!scoreText.enableAutoSizing)
            {
                scoreText.fontSizeMax = scoreText.fontSize;
                scoreText.fontSizeMin = 16f;
                scoreText.enableAutoSizing = true;
            }
            scoreText.text = s.ToString();
        }
    }

    // The summary's menu button (labelled Next then) stops at the lesson first, when the case has one.
    private void MainMenu()
    {
        if (step == Step.Summary && !string.IsNullOrWhiteSpace(lessonBody)) ShowStep(Step.Lesson);
        else LoadMainMenu();
    }

    private void LoadMainMenu()
    {
        LeaveScene();
        SceneManager.LoadScene("MainMenu");
    }

    // ---- lesson -----------------------------------------------------------------------------

    private void BuildLesson()
    {
        if (summaryText != null) summaryText.text = lessonBody;
        if (summaryScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            summaryScroll.verticalNormalizedPosition = 1f;
        }

        if (scoreText != null)
        {
            var s = new StringBuilder();
            s.Append("This case:  <b>").Append(correctVerdict.ToString()).Append("</b>\n");
            if (!fixedRuling) s.Append("Your ruling:  <b>").Append(verdict.ToString()).Append("</b>\n");
            s.Append('\n').Append(lessonSideNote);
            // the note is longer than the score sheet this panel was sized for - shrink to fit, never grow
            if (!scoreText.enableAutoSizing)
            {
                scoreText.fontSizeMax = scoreText.fontSize;
                scoreText.fontSizeMin = 16f;
                scoreText.enableAutoSizing = true;
            }
            scoreText.text = s.ToString();
        }
    }
}
