using UnityEngine;

public class DoorController : MonoBehaviour
{
    [Header("Door Status")]
    public string doorName = "Main Door"; // Required for PlayerInteract UI
    public bool isLocked = true;
    public string requiredKeyID = "RedKey";
    private bool isOpen = false;
    public bool IsOpen => isOpen;

    [Header("Interaction")]
    [Tooltip("How close the player needs to be for this door's '[E] Open/Unlock' prompt to appear. Separate from the generic pickup interact range since doors are bigger and read naturally from further away.")]
    public float interactionRange = 3f;
    [Tooltip("For a double door split into two separate leaf GameObjects (each with its own DoorController and collider): the other leaf. Interacting with either one opens/closes both together, each swinging away from whoever opened it, instead of only the leaf the raycast happened to hit.")]
    public DoorController linkedDoor;

    [Header("Animation Settings")]
    [Tooltip("If true, the door slides along slideOffset instead of swinging on a hinge - for sliding glass/patio doors rather than rotating ones.")]
    public bool useSliding = false;
    public float openRotation = 90f;
    [Tooltip("Degrees per second the door swings. Uses RotateTowards rather than Slerp so the door always reaches the exact fully-open or fully-closed rotation in finite time, instead of Slerp's asymptotic approach which gets arbitrarily close but never truly arrives - the door (and its collider) could sit visibly ajar indefinitely even after isOpen/console state already said 'closed'.")]
    public float rotationSpeed = 180f;
    [Tooltip("Local-space offset from the closed position to the fully-open position, used when useSliding is true.")]
    public Vector3 slideOffset = new Vector3(-0.9f, 0f, 0f);
    [Tooltip("Units per second the door slides. Uses MoveTowards for the same reason rotationSpeed uses RotateTowards - guarantees the door actually reaches fully open/closed instead of asymptotically approaching it.")]
    public float slideSpeed = 1.5f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] openClips;
    public AudioClip[] closeClips;
    [Tooltip("Plays alongside openClips specifically when a locked door is unlocked with a key, so that moment gets its own key/lock cue instead of sounding identical to a door that was already unlocked.")]
    public AudioClip[] unlockClips;
    [Tooltip("Plays when the player tries this door without the required key - a rattling handle/latch cue so the door reads as physically locked instead of just silently refusing.")]
    public AudioClip[] lockedClips;
    [Range(0f, 0.5f)] public float pitchJitter = 0.06f;

    private Quaternion closedRot;
    private Quaternion openRot;
    private Vector3 closedPos;
    private Vector3 openPos;
    private UnityEngine.AI.NavMeshObstacle lockObstacle;

    // Tracks the in-progress swing/slide so Update() can ease it instead of moving at a constant
    // linear rate (which reads as robotic - real doors accelerate away from rest and settle into
    // the stop rather than moving at one flat speed the whole way and snapping still at the end).
    private Quaternion swingFromRot;
    private Quaternion swingToRot;
    private float swingDuration;
    private float swingElapsed;
    private Vector3 slideFromPos;
    private Vector3 slideToPos;
    private float slideDuration;
    private float slideElapsed;

    void Start()
    {
        // Store the initial rotation as "closed"
        closedRot = transform.localRotation;

        // The sign of openRotation is set per-door to swing toward whichever side actually has room
        // for it (a wall to fold back against) - it's fixed, not derived from which side someone
        // opens it from, since the correct side depends on the room layout, not the opener's position.
        openRot = Quaternion.Euler(0, openRotation, 0) * closedRot;

        closedPos = transform.localPosition;
        openPos = closedPos + slideOffset;

        CreateLockObstacle();
    }

    // The ghost's NavMesh is baked with doors left out, so her paths run straight through every
    // doorway. Now that she can't open a locked door, a path through one would leave her pressed
    // against it forever - so a locked door carves itself out of the NavMesh until it's unlocked.
    void CreateLockObstacle()
    {
        if (!isLocked) return;
        // The Level 2 exit marker carries a DoorController only so PlayerInteract finds it - it
        // isn't a doorway, and carving a hole in the pavement there would just strand her.
        if (GetComponent<DeductionGate>() != null) return;

        // The leaf's mesh/collider can sit on this object (Game2's house doors) or on a child under a
        // bare hinge pivot (Game1's asylum doors), so gather every solid piece into this transform's
        // local space.
        Bounds local = default;
        bool found = false;
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger) continue;
            Bounds b;
            if (c is MeshCollider mc && mc.sharedMesh != null) b = mc.sharedMesh.bounds;
            else if (c is BoxCollider bc) b = new Bounds(bc.center, bc.size);
            else continue;
            EncapsulateLocal(ref local, ref found, c.transform, b);
        }
        if (!found)
            foreach (MeshFilter mf in GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) EncapsulateLocal(ref local, ref found, mf.transform, mf.sharedMesh.bounds);
        if (!found) return;

        lockObstacle = gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
        lockObstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
        lockObstacle.center = local.center;
        lockObstacle.size = local.size;
        lockObstacle.carving = true;
    }

    void EncapsulateLocal(ref Bounds local, ref bool found, Transform owner, Bounds b)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 p = transform.InverseTransformPoint(owner.TransformPoint(corner));
            if (!found) { local = new Bounds(p, Vector3.zero); found = true; }
            else local.Encapsulate(p);
        }
    }

    // Smoothstep: slow-in, slow-out. Reads as a natural push/swing instead of a constant-speed slide.
    static float Ease(float t) => t * t * (3f - 2f * t);

    void Update()
    {
        // isLocked is public and flipped from outside too (a linked leaf unlocks its partner), so
        // follow it here rather than at each unlock site.
        if (lockObstacle != null && lockObstacle.enabled != isLocked) lockObstacle.enabled = isLocked;

        // Unscaled, not Time.deltaTime: PlayerInteract zeroes Time.timeScale whenever the cursor
        // unlocks (e.g. a menu opens), which would otherwise freeze a door mid-swing - isOpen and
        // the "[E] Open/Close" prompt already flipped instantly on interact, so the door would be
        // stuck reading the wrong state indefinitely instead of just finishing its animation.
        if (useSliding)
        {
            Vector3 target = isOpen ? openPos : closedPos;

            // (Re)start the eased slide whenever the target changed - including mid-slide, so
            // reversing direction (open then immediately close again) blends from wherever the
            // door currently sits instead of jumping back to a stale start point.
            if (target != slideToPos)
            {
                slideFromPos = transform.localPosition;
                slideToPos = target;
                float dist = Vector3.Distance(slideFromPos, slideToPos);
                slideDuration = slideSpeed > 0f ? dist / slideSpeed : 0f;
                slideElapsed = 0f;
            }

            slideElapsed += Time.unscaledDeltaTime;
            float t = slideDuration > 0f ? Mathf.Clamp01(slideElapsed / slideDuration) : 1f;
            transform.localPosition = Vector3.Lerp(slideFromPos, slideToPos, Ease(t));

            // Physics.autoSyncTransforms is off project-wide (perf), so a plain Transform move like
            // this one doesn't reach the collider PlayerInteract raycasts against until the next
            // physics step - for however many Update() frames land between FixedUpdate calls, the
            // door reads as still standing wherever it was last synced. Right up close, that stale
            // collider position is exactly wide/thin enough to make the door miss the interact ray
            // or block passage that the visible mesh no longer does. Only sync while actually moving.
            if (t < 1f) Physics.SyncTransforms();
        }
        else
        {
            Quaternion target = isOpen ? openRot : closedRot;

            if (target != swingToRot)
            {
                swingFromRot = transform.localRotation;
                swingToRot = target;
                float angle = Quaternion.Angle(swingFromRot, swingToRot);
                swingDuration = rotationSpeed > 0f ? angle / rotationSpeed : 0f;
                swingElapsed = 0f;
            }

            swingElapsed += Time.unscaledDeltaTime;
            float t = swingDuration > 0f ? Mathf.Clamp01(swingElapsed / swingDuration) : 1f;
            transform.localRotation = Quaternion.Slerp(swingFromRot, swingToRot, Ease(t));

            // Same stale-collider reasoning as the sliding branch above.
            if (t < 1f) Physics.SyncTransforms();
        }
    }

    /// <summary>
    /// Called by PlayerInteract.cs when the player presses 'E'
    /// </summary>
    public void Interaction(PlayerInteract player)
    {
        if (isLocked)
        {
            // Only 2 of the 10 inventory slots can ever be the "actively equipped" hotbar slot,
            // so requiring a key to be equipped (rather than just carried) would strand any key
            // picked up beyond the first two - it'd sit in the bag with no way to ever select it.
            // Simply carrying the matching key is enough to use it on its door.
            if (player.HasKeySomewhere(requiredKeyID))
            {
                UnlockPermanent(player);
            }
            else
            {
                player.DisplayNotification($"{requiredKeyID} required.");
                PlaySound(lockedClips);
            }
        }
        else
        {
            isOpen = !isOpen;
            PlaySound(isOpen ? openClips : closeClips);

            if (linkedDoor != null) linkedDoor.SyncOpenState(isOpen);
        }
    }

    // Lets a linked leaf (see linkedDoor) drive this door to match its open/closed state.
    public void SyncOpenState(bool open)
    {
        if (isLocked || isOpen == open) return;
        isOpen = open;
        PlaySound(isOpen ? openClips : closeClips);
    }

    // Called by non-player agents (e.g. GhostController) that need to pass through a door on their
    // own. A locked door stays shut - only the player's key opens it, so locking a door behind you
    // is a real escape. While locked, the carving NavMeshObstacle below makes her path around it.
    public void ForceOpen()
    {
        if (isOpen || isLocked) return;
        isOpen = true;
        PlaySound(openClips);
        if (linkedDoor != null) linkedDoor.SyncOpenState(true);
    }

    void UnlockPermanent(PlayerInteract player)
    {
        isLocked = false;
        isOpen = true; // Auto-open upon unlocking
        PlaySound(unlockClips);
        PlaySound(openClips);

        // Consumes the key from the player's inventory
        player.RemoveItem(requiredKeyID);

        player.DisplayNotification($"Used {requiredKeyID}: Door Unlocked.");

        if (linkedDoor != null)
        {
            linkedDoor.isLocked = false;
            linkedDoor.SyncOpenState(true);
        }
    }

    void PlaySound(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        audioSource.pitch = Random.Range(1f - pitchJitter, 1f + pitchJitter);
        audioSource.PlayOneShot(clip, SfxVolume.Multiplier);
    }
}