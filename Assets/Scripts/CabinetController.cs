using UnityEngine;

// A furniture piece (medicine cabinet, locker, etc.) whose door(s) swing open on interact. The first
// time it's opened, it reveals a clue about where to find evidence, shown through the same
// full-screen note UI as readable notes so it feels consistent rather than inventing a second popup
// style. If no custom clueText is set, it falls back to the game's own current objective hint
// (ObjectiveTracker.CurrentHint()) wrapped in a bit of found-note framing - that way it's always
// pointing at whatever evidence is actually next, instead of a hardcoded clue that could go stale or
// point at evidence the player already found.
public class CabinetController : MonoBehaviour
{
    [Header("Cabinet Status")]
    public string cabinetName = "Cabinet"; // Shown in the PlayerInteract prompt
    private bool isOpen = false;
    public bool IsOpen => isOpen;

    [Header("Door(s)")]
    [Tooltip("The door mesh(es) that swing open. Fill just the first slot for a single door, or both for a left/right pair (e.g. a medicine cabinet) - they swing away from each other automatically.")]
    public Transform[] doors = new Transform[1];
    public float openRotation = 100f;
    [Tooltip("Local axis each door swings around - Y for a typical hinged cabinet door.")]
    public Vector3 rotationAxis = Vector3.up;
    public float rotationSpeed = 180f;
    [Tooltip("Drawers: slide each entry in `doors` out by slideOffset (in its parent's local space) instead of swinging it.")]
    public bool slide = false;
    public Vector3 slideOffset = new Vector3(0.4f, 0f, 0f);
    public float slideSpeed = 1.2f;
    [Tooltip("For parts whose pivot isn't on their hinge (pieces cut from one big level model all pivot at the model's origin): swing each door around hingePoint/hingeAxis instead of its own pivot. Both are in the door's PARENT space; openRotation's sign picks the direction.")]
    public bool useHinge = false;
    public Vector3 hingePoint;
    public Vector3 hingeAxis = Vector3.up;

    [Header("Interaction")]
    [Tooltip("How close the player needs to be for this cabinet's '[E] Open/Close' prompt to appear. Separate from the generic pickup interactRange, same reasoning as DoorController - furniture reads naturally from further away than small pickups.")]
    public float interactionRange = 3f;

    [Header("Clue")]
    [Tooltip("Off for plain furniture (wardrobe doors, drawers) that should just open and close - otherwise every one of them pops a hint note the first time it's opened.")]
    public bool revealClue = true;
    [Tooltip("Shown the first time this cabinet is opened. Leave blank to auto-fill from the game's current objective hint instead - the recommended default so every cabinet always points at whatever evidence is actually next.")]
    [TextArea] public string clueText;
    [Tooltip("Only reveals the clue once - reopening the cabinet afterwards just swings the door, no repeat popup.")]
    public bool revealOnce = true;
    [Tooltip("Only used when clueText is left blank.")]
    public ObjectiveTracker objectiveTracker;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] openClips;
    public AudioClip[] closeClips;
    [Range(0f, 0.5f)] public float pitchJitter = 0.06f;

    private bool hasRevealedClue = false;
    private Quaternion[] closedRotations;
    private Quaternion[] openRotations;
    private Vector3[] closedPositions;
    private float[] hingeAngles;

    void Start()
    {
        if (objectiveTracker == null) objectiveTracker = FindFirstObjectByType<ObjectiveTracker>();

        if (doors == null) return;

        closedRotations = new Quaternion[doors.Length];
        openRotations = new Quaternion[doors.Length];
        closedPositions = new Vector3[doors.Length];
        hingeAngles = new float[doors.Length];

        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] == null) continue;

            closedRotations[i] = doors[i].localRotation;
            closedPositions[i] = doors[i].localPosition;

            // A left/right pair should swing away from each other rather than both the same way -
            // alternate the sign per index so door 0 and door 1 open in opposite directions.
            float sign = (i % 2 == 0) ? 1f : -1f;
            openRotations[i] = Quaternion.AngleAxis(openRotation * sign, rotationAxis) * closedRotations[i];
        }
    }

    void Update()
    {
        if (doors == null) return;

        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] == null) continue;

            if (slide)
            {
                Vector3 targetPos = isOpen ? closedPositions[i] + slideOffset : closedPositions[i];
                if (doors[i].localPosition == targetPos) continue;
                doors[i].localPosition = Vector3.MoveTowards(doors[i].localPosition, targetPos, slideSpeed * Time.deltaTime);
                Physics.SyncTransforms();
                continue;
            }

            if (useHinge)
            {
                float targetAngle = isOpen ? openRotation : 0f;
                if (Mathf.Approximately(hingeAngles[i], targetAngle)) continue;
                hingeAngles[i] = Mathf.MoveTowards(hingeAngles[i], targetAngle, rotationSpeed * Time.deltaTime);
                Quaternion turn = Quaternion.AngleAxis(hingeAngles[i], hingeAxis);
                doors[i].localRotation = turn * closedRotations[i];
                doors[i].localPosition = hingePoint + turn * (closedPositions[i] - hingePoint);
                Physics.SyncTransforms();
                continue;
            }

            Quaternion target = isOpen ? openRotations[i] : closedRotations[i];
            if (doors[i].localRotation == target) continue;

            doors[i].localRotation = Quaternion.RotateTowards(doors[i].localRotation, target, rotationSpeed * Time.deltaTime);

            // Physics.autoSyncTransforms is off project-wide (perf), so this plain Transform move
            // doesn't reach the collider PlayerInteract raycasts against until the next physics step -
            // same stale-collider issue that made doors miss the interact ray up close (see DoorController).
            Physics.SyncTransforms();
        }
    }

    /// <summary>
    /// Called by PlayerInteract.cs when the player presses 'E'.
    /// </summary>
    public void Interaction(PlayerInteract player)
    {
        isOpen = !isOpen;
        PlaySound(isOpen ? openClips : closeClips);

        if (!isOpen || !revealClue) return;
        if (revealOnce && hasRevealedClue) return;

        string revealed = !string.IsNullOrEmpty(clueText) ? clueText : BuildDynamicClue();
        if (string.IsNullOrEmpty(revealed)) return;

        hasRevealedClue = true;
        if (NoteSystem.Instance != null) NoteSystem.Instance.InspectNoteInFace(revealed, null);
    }

    void PlaySound(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        audioSource.pitch = Random.Range(1f - pitchJitter, 1f + pitchJitter);
        audioSource.PlayOneShot(clip, SfxVolume.Multiplier);
    }

    string BuildDynamicClue()
    {
        if (objectiveTracker == null) return "";
        string hint = objectiveTracker.CurrentHint();
        if (string.IsNullOrEmpty(hint)) return "";
        return "A hurried note, tucked out of sight:\n\n\"" + hint + "\"";
    }
}
