using UnityEngine;
using TMPro;
using System.Collections;

// Opening hook shown once at game start, before the player has any sense of why they're here or
// what to do - without this the game just drops you into a dark hallway with zero context.
public class IntroSequence : MonoBehaviour
{
    [Header("UI")]
    public GameObject introPanel;
    public TMP_Text introText;

    [Header("Content")]
    [TextArea(2, 4)]
    public string[] lines = new string[]
    {
        "Grimwood Asylum, 1962.",
        "Dr. Edmund Vance, the Asylum's Director, was found dead in the East Wing.",
        "The official report says suicide.",
        "But something doesn't add up...",
        "Find the truth before the doors seal shut.",
    };

    [Header("Follow-up")]
    [Tooltip("Shown after this narrative sequence finishes, before the player gets control - kept as its own screen so it can't be skipped along with the story lines above.")]
    public HowToPlayScreen howToPlayScreen;

    public float secondsPerLine = 3f;
    public float fadeDuration = 0.5f;
    public float typewriterCharsPerSecond = 35f;

    // True from scene start until the briefing closes. PlayerMovement ignores all input while it's up
    // (mouse look isn't time-scaled, so the paused game alone still let the view turn behind it), and
    // the ghost can't hunt (see GhostController.StartGraceActive).
    public static bool IsPlaying { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsPlaying = false;

    private CanvasGroup canvasGroup;
    // Consumed per-line (fast-forwards the current line's typewriter/wait) rather than aborting the
    // whole sequence - a single SPACE press used to skip every remaining line outright, which meant
    // one habitual "skip the cutscene" press could blow straight past the flashlight/bag/interact
    // instructions and the "something you need to find first" line without ever showing them.
    private bool advance = false;

    private void Awake()
    {
        if (introPanel != null)
        {
            canvasGroup = introPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = introPanel.AddComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        if (introPanel == null) return;

        IsPlaying = true;
        introPanel.SetActive(true);
        canvasGroup.alpha = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        StartCoroutine(PlayIntro());
    }

    private void Update()
    {
        if (introPanel != null && introPanel.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape))
            {
                advance = true;
            }
        }
    }

    private IEnumerator PlayIntro()
    {
        // Wait a frame first - other scripts' own Start() calls (e.g. InventoryToggle, which
        // unconditionally sets Time.timeScale = 1f) all finish within this same first frame, and
        // Unity doesn't guarantee script execution order between unrelated components. Pausing
        // here instead of in Start() means nothing can stomp on it afterward but our own EndIntro().
        // The cursor needs the same treatment: PlayerMovement.Start() locks it, and PlayerInteract
        // forces timeScale back to 1 every frame while it's locked - so if the player's Start() ran
        // after ours (it does in Game2), the "paused" intro played over a live, running level.
        yield return null;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        foreach (string line in lines)
        {
            if (introText != null) introText.text = "";

            yield return StartCoroutine(Fade(0f, 1f));

            if (introText != null)
                yield return StartCoroutine(TypewriterEffect.Reveal(introText, line, typewriterCharsPerSecond, () => advance));
            advance = false;

            float t = 0f;
            while (t < secondsPerLine && !advance)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            advance = false;

            yield return StartCoroutine(Fade(1f, 0f));
        }

        EndIntro();
    }

    private IEnumerator Fade(float from, float to)
    {
        if (canvasGroup == null) yield break;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    private void OnDestroy() => IsPlaying = false;

    private void EndIntro()
    {
        IsPlaying = false;
        introPanel.SetActive(false);

        if (howToPlayScreen != null)
        {
            howToPlayScreen.Show();
        }
        else
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
