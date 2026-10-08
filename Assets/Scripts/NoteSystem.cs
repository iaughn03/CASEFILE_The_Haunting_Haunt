using UnityEngine;
using TMPro;
using System.Collections;

public class NoteSystem : MonoBehaviour
{
    public static NoteSystem Instance;

    [Header("UI Display Setup")]
    public GameObject notePanel;
    public TextMeshProUGUI noteTextMesh;
    [Tooltip("Full-screen dim backdrop shown behind the note so the game world/other UI doesn't show through around its edges.")]
    public GameObject noteBackdrop;

    [Header("Animation Settings")]
    [SerializeField] private float animationSpeed = 0.2f;

    [Header("Input Controls")]
    public KeyCode closeKey = KeyCode.E;

    private bool isReading = false;
    private bool isAnimating = false;
    private string pendingNoteText = "";
    private NoteObject currentNoteInHand = null;
    // True when this read was opened by clicking/re-reading a note already sitting in the bag -
    // closing should return the player to the (paused) bag, not drop them back into live gameplay.
    private bool reopenBagOnClose = false;
    // Runs once the bag has reopened after a bag re-read - lets the item fill the bag's detail and
    // 3D inspection panels then, instead of drawing them over the page while it's being read.
    private System.Action onReturnedToBag;
    private PlayerMovement playerMovement;
    private PlayerInteract playerInteract;
    private InventoryToggle inventoryToggle;
    private DescriptionPanel descriptionPanel;

    public bool IsReading
    {
        get { return isReading; }
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        if (notePanel != null)
        {
            notePanel.SetActive(false);
            notePanel.transform.localScale = Vector3.zero;
        }

        if (noteBackdrop != null) noteBackdrop.SetActive(false);

        playerMovement = FindFirstObjectByType<PlayerMovement>();
        playerInteract = FindFirstObjectByType<PlayerInteract>();
        inventoryToggle = FindFirstObjectByType<InventoryToggle>();
        descriptionPanel = FindFirstObjectByType<DescriptionPanel>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        // closeKey left at its default E follows the rebindable Interact key
        bool closePressed = closeKey == KeyCode.E ? GameKeys.Down(GameKeys.Action.Interact) : Input.GetKeyDown(closeKey);
        if (isReading && !isAnimating && closePressed)
        {
            StartCoroutine(AnimateClose());
        }
    }

    public void InspectNoteInFace(string textData, NoteObject sourceObject, System.Action onReturnedToBag = null)
    {
        if (isAnimating)
            return;

        this.onReturnedToBag = onReturnedToBag;

        // Bail out loudly here, before Time.timeScale gets touched, rather than letting AnimateOpen()
        // hit a null notePanel mid-coroutine - a coroutine exception on its first segment gets logged
        // by Unity's own coroutine runner but never propagates back to this call, so without this
        // check a misconfigured panel would silently leave the game frozen at timeScale=0 forever
        // (only AnimateClose() ever resets it, and it would never be reached).
        if (notePanel == null || noteTextMesh == null)
        {
            Debug.LogError("NoteSystem is missing notePanel or noteTextMesh - cannot open note.");
            return;
        }

        // Close the bag first if it's open - otherwise the note panel opens on top of it and the
        // two backgrounds overlap/bleed into each other.
        if (inventoryToggle != null && inventoryToggle.IsInventoryOpen())
        {
            inventoryToggle.ToggleInventory();

            // Only a bag re-read (no world object to collect) should reopen the bag on close -
            // a note picked up fresh off the ground should drop the player back into gameplay.
            reopenBagOnClose = sourceObject == null;
        }
        else
        {
            reopenBagOnClose = false;
        }

        // The bag's detail panel sits above the note in the Canvas and closing the bag doesn't hide
        // it, so it would cover the left side of the page.
        if (descriptionPanel != null) descriptionPanel.Hide();

        pendingNoteText = textData;
        currentNoteInHand = sourceObject;

        if (currentNoteInHand != null)
        {
            currentNoteInHand.Hide();
        }

        if (playerMovement != null) playerMovement.PlayGrabPulse();

        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        StartCoroutine(AnimateOpen());
    }

    private IEnumerator AnimateOpen()
    {
        isAnimating = true;

        if (noteBackdrop != null) noteBackdrop.SetActive(true);
        notePanel.SetActive(true);
        notePanel.transform.localScale = Vector3.zero;

        float timer = 0f;

        while (timer < animationSpeed)
        {
            timer += Time.unscaledDeltaTime;

            // Ease-out: the page settles into place instead of stopping dead.
            float progress = 1f - Mathf.Pow(1f - Mathf.Clamp01(timer / animationSpeed), 3f);

            notePanel.transform.localScale =
                Vector3.Lerp(Vector3.zero, Vector3.one, progress);

            yield return null;
        }

        notePanel.transform.localScale = Vector3.one;
        isAnimating = false;

        // A found note already has its text written on it - show it in full immediately rather than
        // animating a live typewriter reveal, which read as the note being typed out in real time.
        noteTextMesh.text = pendingNoteText;
        noteTextMesh.maxVisibleCharacters = int.MaxValue;

        isReading = true;
    }

    private IEnumerator AnimateClose()
    {
        isAnimating = true;
        isReading = false;

        float timer = 0f;

        while (timer < animationSpeed)
        {
            timer += Time.unscaledDeltaTime;

            float progress = Mathf.Pow(Mathf.Clamp01(timer / animationSpeed), 3f);

            notePanel.transform.localScale =
                Vector3.Lerp(Vector3.one, Vector3.zero, progress);

            yield return null;
        }

        notePanel.transform.localScale = Vector3.zero;
        notePanel.SetActive(false);
        if (noteBackdrop != null) noteBackdrop.SetActive(false);

        if (currentNoteInHand != null)
        {
            // Routes through the same evidence pipeline as every other evidence pickup (PillBottle,
            // etc.) so the note counts toward evidence progress AND actually shows up as a draggable
            // icon in the bag - the old path (NoteEvidence.CollectNote -> EvidenceManager.AddEvidence
            // directly) only incremented the counter with nothing visible to show for it.
            InspectItem inspectItem = currentNoteInHand.GetComponent<InspectItem>();

            if (inspectItem != null && playerInteract != null)
            {
                playerInteract.CollectEvidence(inspectItem);
            }
            else
            {
                Debug.LogWarning("InspectItem or PlayerInteract missing - note not added to bag!");
            }

            // Kept alive (already hidden via Hide() above) rather than destroyed, so dropping this
            // note later from the bag can place the same physical object back in the world.
            currentNoteInHand = null;
        }

        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        isAnimating = false;

        System.Action returned = onReturnedToBag;
        onReturnedToBag = null;

        if (reopenBagOnClose)
        {
            reopenBagOnClose = false;
            if (inventoryToggle != null) inventoryToggle.ToggleInventory();
            // After ToggleInventory: opening the bag clears the detail/inspection panels.
            if (returned != null) returned();
        }
    }

    public void SetPaused(bool paused)
    {
        if (!isReading)
            return;

        if (paused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}