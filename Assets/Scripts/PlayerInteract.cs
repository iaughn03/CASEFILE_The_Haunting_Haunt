using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PlayerInteract : MonoBehaviour
{
    [Header("Detection Settings")]
    public float interactRange = 3f;
    public LayerMask interactableLayer;
    public Camera playerCamera;
    public TextMeshProUGUI interactionText;

    [Header("Item Inspection Settings")]
    public Transform inspectPoint;
    [SerializeField] private float itemRotationSpeed = 400f;
   private InspectItem currentlyInspectingItem = null;
    private bool isInspecting = false;
    [Header("Inventory Slots Link")]
    [Tooltip("Drag your 16 slots here in order: Hotbar 1-4, then Bag 1-12")]
    public InventorySlot[] allInventorySlots = new InventorySlot[16];

    [Header("Inventory State")]
    [Tooltip("Hotbar slot whose item is in the player's hand (0-3), or -1 for nothing.")]
    public int currentlySelectedSlot = NoSlot;
    public const int NoSlot = -1;
    public const int HotbarSize = 4;
    // Pure runtime state, always rebuilt fresh in InitializeInventory() - NonSerialized so a scene
    // save mid-session can't bake a stale array length/contents into the saved scene and shadow
    // the fresh one Start() creates every play session (this is exactly how they ended up at the
    // old pre-expansion length of 10 in the saved scene despite the code here saying 16).
    [System.NonSerialized] public bool[] slotOccupied = new bool[16];
    [System.NonSerialized] public string[] heldItemNames = new string[16];

    [Header("Item Visuals (Child of Camera)")]
    public GameObject flashlightVisual;
    public GameObject handKeyVisual;

    [Header("Held Key Placement (camera space)")]
    [Tooltip("Where the held key sits relative to the player camera: right, up, forward (metres).")]
    public Vector3 heldKeyOffset = new Vector3(0.10f, -0.085f, 0.38f);
    [Tooltip("Key rotation relative to the camera: blade pointing forward and in toward the centre, flat face toward the player.")]
    public Vector3 heldKeyEuler = new Vector3(-55f, -25f, 15f);
    [Tooltip("Uniform scale of the key model (the model itself is a 5 cm key).")]
    public float heldKeyScale = 1.3f;

    [Header("Pickup Audio")]
    public AudioSource pickupAudioSource;
    public AudioClip[] pickupClips;
    [Range(0f, 0.5f)] public float pickupPitchJitter = 0.08f;

    [Header("UI Icons")]
    public Sprite handIcon;
    public Sprite flashlightIcon;
    [Tooltip("Shared icon used for every key, regardless of which door it unlocks - keeps inventory display consistent instead of depending on each KeyItem's own sprite.")]
    public Sprite keyIcon;

    [Header("Inventory UI")]
    public GameObject uiItemPrefab;

    public Transform hotbarPanel;
    public Transform bagPanel;

    private HotbarManager hotbarManager;
    private bool isShowingNotification = false;
    private PlayerMovement playerMovement;
    private FlashlightController flashlightController;
    private PlayerHealth playerHealth;
    private PlayerHideState hideState;
    private PlayerWindowClimb windowClimb;

    void Start()
    {
        hotbarManager = FindFirstObjectByType<HotbarManager>();
        playerMovement = GetComponent<PlayerMovement>();
        windowClimb = GetComponent<PlayerWindowClimb>();
        flashlightController = FindFirstObjectByType<FlashlightController>();
        playerHealth = GetComponent<PlayerHealth>();
        InitializeInventory();

        // The held key used to ride the ItemHolder under the player model, which carries the player's
        // non-uniform scale (0.8 x 1.2 x 1.2) plus its own 10x scale: it showed up as a huge, skewed
        // blob in the bottom-right corner. Detach it and place it from the camera instead (LateUpdate).
        if (handKeyVisual != null) handKeyVisual.transform.SetParent(null, true);
    }

    void LateUpdate()
    {
        if (handKeyVisual == null || !handKeyVisual.activeSelf || playerCamera == null) return;
        Transform cam = playerCamera.transform;
        // position + rotation only: cam.TransformPoint would also apply the camera's inherited non-uniform scale
        handKeyVisual.transform.SetPositionAndRotation(cam.position + cam.rotation * heldKeyOffset, cam.rotation * Quaternion.Euler(heldKeyEuler));
        handKeyVisual.transform.localScale = Vector3.one * heldKeyScale;
    }
void Update()
{
    // Once dead, PlayerHealth.DeathSequence() owns Time.timeScale exclusively - without this,
    // this method's own cursor-lock-driven timeScale management could race against it and leave
    // gameplay running behind the death screen instead of actually paused.
    if (playerHealth != null && playerHealth.IsDead) return;

    // PlayerHideState is only added the first time the player gets near a hiding spot (see
    // LockerHidingSpot.CachePlayerRefs), so it won't exist yet at scene start - keep retrying
    // the lookup lazily until it does, then stop.
    if (hideState == null) hideState = FindFirstObjectByType<PlayerHideState>();

    // While hidden, this script's own raycast-driven E handling (item pickup, doors, etc.) was
    // firing on the exact same E press LockerHidingSpot uses to come back out - picking up
    // whatever happened to be in view at the same moment the player tried to exit, and stomping
    // its own interactionText/notification over the locker's "[E] Come out" prompt.
    if (hideState != null && hideState.IsHiding)
    {
        HideInteractionUI();
        return;
    }

    if (isInspecting && currentlyInspectingItem != null)
    {
        HandleInspectionRotation();
        return;
    }

    if (NoteSystem.Instance != null &&
        NoteSystem.Instance.notePanel != null &&
        NoteSystem.Instance.notePanel.activeSelf)
    {
        HideInteractionUI();
        return;
    }

    if (Cursor.lockState == CursorLockMode.None &&
        !isInspecting)
    {
        Time.timeScale = 0f;
        HideInteractionUI();
        return;
    }
    else
    {
        Time.timeScale = 1f;
    }

    HandleHotbarInput();
    HandleDropInput();
    HandleInteraction();
    UpdateHandVisuals();
}

    // Names of the level's real door keys (KeyItem), normalised. Only these show the key in the
    // player's hand. Matching "key" in the item name made evidence like Game3's "Spare Key Under Mat"
    // or Game2's "Family Only Key" look like a usable key when it is only something to inspect.
    private readonly System.Collections.Generic.List<string> doorKeyNames = new System.Collections.Generic.List<string>();

    private void RememberDoorKey(string keyName)
    {
        string n = Normalize(keyName);
        if (n.Length > 0 && !doorKeyNames.Contains(n)) doorKeyNames.Add(n);
    }

    private void InitializeInventory()
    {
        doorKeyNames.Clear();
        foreach (KeyItem k in FindObjectsByType<KeyItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            RememberDoorKey(k.keyName);

        slotOccupied = new bool[16];
        heldItemNames = new string[16];

        // All 16 slots hold items: hotbar 0-3, bag 4-15. There is no Hand / Flashlight slot any more:
        // the flashlight is always carried and toggled with its own key (F by default), and
        // currentlySelectedSlot = NoSlot means nothing is in the player's hand.
        currentlySelectedSlot = NoSlot;
        for (int i = 0; i < slotOccupied.Length; i++)
        {
            slotOccupied[i] = false;
            heldItemNames[i] = "";
            if (i < HotbarSize && hotbarManager != null) hotbarManager.ClearSlot(i);
        }
        if (hotbarManager != null) hotbarManager.HighlightSlot(NoSlot);

        // FIXED: Safely cleans item data without touching parent slot panels
       for (int i = 0; i < allInventorySlots.Length; i++)
{
    if (allInventorySlots[i] == null)
        continue;

    DraggableItem item = GetDirectDraggableItem(allInventorySlots[i]);

   if (item != null)
{
    item.itemName = "";
    item.itemDescription = "";
    item.itemUsage = "";

    if (item.image != null)
    {
        item.image.sprite = null;
        item.image.enabled = false;
    }
}
}}

   private void HandleHotbarInput()
{
    // Keys 1-4 take the item in that hotbar slot in hand; the same key again puts it away (NoSlot).
    // Scroll cycles empty hand -> 1 -> 2 -> 3 -> 4 -> empty hand.
    int previousSlot = currentlySelectedSlot;
    float scroll = Input.GetAxisRaw("Mouse ScrollWheel");

    if (scroll > 0f) currentlySelectedSlot--;
    else if (scroll < 0f) currentlySelectedSlot++;
    if (currentlySelectedSlot >= HotbarSize) currentlySelectedSlot = NoSlot;
    if (currentlySelectedSlot < NoSlot) currentlySelectedSlot = HotbarSize - 1;

    for (int i = 0; i < HotbarSize; i++)
    {
        if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            currentlySelectedSlot = currentlySelectedSlot == i ? NoSlot : i;
    }

    if (previousSlot != currentlySelectedSlot)
    {
        if (hotbarManager != null)
            hotbarManager.HighlightSlot(currentlySelectedSlot);
    }
} 
    private void HandleDropInput()
    {
        if (currentlySelectedSlot < 0 || currentlySelectedSlot >= HotbarSize) return;

        if (GameKeys.Down(GameKeys.Action.DropItem) && slotOccupied[currentlySelectedSlot])
        {
            string itemToDropName = heldItemNames[currentlySelectedSlot];
            GameObject worldObjectToDrop = null;

            if (currentlySelectedSlot < allInventorySlots.Length && allInventorySlots[currentlySelectedSlot] != null)
            {
                DraggableItem item = GetDirectDraggableItem(allInventorySlots[currentlySelectedSlot]);
                if (item != null)
                {
                    worldObjectToDrop = item.sourceWorldObject;

                    if (item.image != null)
                    {
                        item.image.sprite = null;
                        item.image.enabled = false;
                    }
                    ClearItemData(item);
                }
            }

            heldItemNames[currentlySelectedSlot] = "";
            slotOccupied[currentlySelectedSlot] = false;

            if (hotbarManager != null)
            {
                hotbarManager.ClearSlot(currentlySelectedSlot);
                currentlySelectedSlot = NoSlot;
                hotbarManager.HighlightSlot(NoSlot);
            }

            if (worldObjectToDrop != null)
            {
                DropItemInWorld(worldObjectToDrop);
            }

            DisplayNotification("Dropped " + itemToDropName);
        }
    }

    // Puts a picked-up item's original world object (kept hidden, not destroyed, since collection
    // - see EvidencePickUp/KeyItem) back in front of the player and re-enables it so it can be
    // interacted with and collected again, instead of the item just vanishing from the game.
    private void DropItemInWorld(GameObject worldObject)
    {
        // Flattened, camera-pitch-independent forward direction - using the camera's raw forward
        // here sent the drop point flying up near ceiling height (or under the floor) whenever the
        // player was looking up or down while pressing G, since the raycast below could then miss
        // the actual floor entirely and fall back to that pitched-up/down position.
        Vector3 forwardFlat = playerCamera != null ? playerCamera.transform.forward : transform.forward;
        forwardFlat.y = 0f;
        if (forwardFlat.sqrMagnitude < 0.0001f) forwardFlat = transform.forward;
        forwardFlat.Normalize();

        // transform (the player root, which carries the CharacterController) sits at ground/feet
        // level rather than at camera height, so this stays grounded regardless of look pitch.
        Vector3 originPoint = transform.position + forwardFlat * 1.2f;
        Vector3 dropPosition = originPoint;

        // Rest it on whatever floor is beneath the drop point rather than leaving it floating
        // mid-air or embedded in the ground.
        if (Physics.Raycast(originPoint + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f))
        {
            dropPosition = hit.point + Vector3.up * 0.05f;
        }

        worldObject.transform.position = dropPosition;

        // Sprite notes (Level 1) are drawn in their local XY plane, so they need 90 on X to lie
        // face-up - identity rotation stood them up on edge instead of flat. Mesh documents (Level 2's
        // paper quads, folders) are already modelled lying flat and only get a random yaw.
        if (worldObject.GetComponent<NoteObject>() != null)
            worldObject.transform.rotation = worldObject.GetComponent<SpriteRenderer>() != null
                ? Quaternion.Euler(90f, Random.Range(0f, 360f), 0f)
                : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        else
            worldObject.transform.rotation = Quaternion.identity;

        if (worldObject.TryGetComponent<EvidencePickUp>(out EvidencePickUp evidence))
        {
            evidence.isCollected = false;
            evidence.ReleaseBodies();
        }

        if (worldObject.TryGetComponent<KeyItem>(out KeyItem key))
        {
            key.isCollected = false;
        }

        if (worldObject.TryGetComponent<NoteObject>(out NoteObject note))
        {
            note.isCollected = false;
        }

        foreach (Collider col in worldObject.GetComponentsInChildren<Collider>(true)) col.enabled = true;
        foreach (Renderer rend in worldObject.GetComponentsInChildren<Renderer>(true)) rend.enabled = true;
        foreach (ItemGlowOutline glow in worldObject.GetComponentsInChildren<ItemGlowOutline>(true)) glow.enabled = true;
    }

    void HandleInteraction()
{
    if (isShowingNotification) return;

    RaycastHit hit;
    Ray ray = playerCamera.ViewportPointToRay(
        new Vector3(0.5f, 0.5f, 0)
    );

    // Cast out past the generic interactRange so doors - which use their own, separately
    // tunable range - are still detected; every branch below checks its own range against
    // hit.distance before treating anything as actually in reach.
    float castDistance = Mathf.Max(interactRange, 10f);

    bool gotHit = Physics.Raycast(ray, out hit, castDistance, interactableLayer);

    if (!gotHit)
    {
        // A raycast never registers a hit if its origin already starts inside the collider it
        // would've hit - standing pressed right up against something thin like a door leaf is
        // enough to embed the camera in (or right against the inside of) its collider, silently
        // dropping every prompt with no distance check ever coming into it. Retrying from a point
        // pulled back along the same look direction moves the origin to just outside any such
        // paper-thin geometry, so the very next cast can still find it normally.
        // Try progressively farther pull-backs - a single fixed 0.6m retry still fails to clear
        // thicker geometry (a doorframe, a corner, another collider behind the door) that the
        // player is pressed into, which is exactly when this retry matters most.
        float[] pullbacks = { 0.6f, 1.2f, 2f };
        foreach (float pullback in pullbacks)
        {
            Vector3 retryOrigin = ray.origin - ray.direction.normalized * pullback;
            gotHit = Physics.Raycast(retryOrigin, ray.direction, out hit, castDistance + pullback, interactableLayer);
            if (gotHit)
            {
                hit.distance = Mathf.Max(0f, hit.distance - pullback);
                break;
            }
        }
    }

    // The casts above only see the Interact layer, so walls, floors and furniture were invisible
    // to them: from outside a house the crosshair could open doors and take clues through the wall.
    if (gotHit && IsBehindWorldGeometry(ray.origin, hit)) gotHit = false;

    if (gotHit)
    {
        // Doors (checked first - own configurable range, independent of interactRange).
        // GetComponentInParent, not TryGetComponent: some door leaves keep their collider on a
        // separate child mesh object (its geometry always transforms correctly, including doors
        // with unusual baked-in rotations from how they were originally mirrored/authored) while
        // DoorController itself stays on the parent pivot - same reasoning as the cabinet lookup below.
        DoorController door = hit.collider.GetComponentInParent<DoorController>();
        if (door != null)
        {
            if (hit.distance <= door.interactionRange)
            {
                // The Map 1 exit door swaps its normal open/unlock behavior for the
                // Suicide-or-Homicide ending choice once all evidence has been found.
                if (door.TryGetComponent<DeductionGate>(out DeductionGate gate))
                {
                    if (gate.HasAllEvidence)
                    {
                        SetInteractionUI("[E] Confront the Truth");

                        if (GameKeys.Down(GameKeys.Action.Interact))
                        {
                            // Classification quiz (if present) runs first and opens the whodunit
                            // choice itself once answered - see CaseClassificationQuiz.Open()/Proceed().
                            // Level 1's case ends in the Final Inspection (case wall, ruling, accusation,
                            // signed report, ending).
                            if (FinalInspection.Instance != null)
                                FinalInspection.Instance.Open();
                            else if (door.TryGetComponent<CaseClassificationQuiz>(out CaseClassificationQuiz quiz))
                                quiz.Open();
                            else
                                gate.OpenChoice();
                        }
                    }
                    else
                    {
                        SetInteractionUI(gate.ProgressText);
                    }

                    return;
                }

                string status =
                    door.isLocked
                    ? "[E] Unlock " + door.doorName
                    : (door.IsOpen ? "[E] Close Door" : "[E] Open Door");

                SetInteractionUI(status);

                if (GameKeys.Down(GameKeys.Action.Interact))
                {
                    door.Interaction(this);
                }
            }
            else
            {
                HideOrOfferClimb();
            }

            return;
        }

        // Cabinets (own configurable range, same treatment as doors above - checked before the
        // generic interactRange gate below since furniture reads naturally from further away).
        // Uses GetComponentInParent rather than TryGetComponent like the other branches, because a
        // cabinet's case and each door are separate sibling meshes with their own colliders - the
        // component lives on their shared parent, not on whichever one the raycast happens to hit.
        CabinetController cabinet = hit.collider.GetComponentInParent<CabinetController>();
        if (cabinet != null)
        {
            if (hit.distance <= cabinet.interactionRange)
            {
                SetInteractionUI(cabinet.IsOpen ? "[E] Close " + cabinet.cabinetName : "[E] Open " + cabinet.cabinetName);

                if (GameKeys.Down(GameKeys.Action.Interact))
                {
                    cabinet.Interaction(this);
                }
            }
            else
            {
                HideOrOfferClimb();
            }

            return;
        }

        // Windows (Game2): two sash colliders per window, looked up through WindowController's own
        // sash registry since a corner piece carries two windows under one parent.
        WindowController window = WindowController.FromCollider(hit.collider);
        if (window != null)
        {
            if (hit.distance <= window.interactionRange)
            {
                SetInteractionUI(window.IsOpen ? "[E] Close " + window.windowName : "[E] Open " + window.windowName);

                if (GameKeys.Down(GameKeys.Action.Interact))
                {
                    window.Interaction(this);
                }
            }
            else
            {
                HideOrOfferClimb();
            }

            return;
        }

        if (hit.distance > interactRange)
        {
            HideOrOfferClimb();
            return;
        }

        // Readable documents that are not evidence (witness statements): read in place, never collected
        if (hit.collider.TryGetComponent<ReadableNote>(out ReadableNote readable))
        {
            SetInteractionUI("[E] Read " + readable.title);

            if (GameKeys.Down(GameKeys.Action.Interact))
            {
                HideInteractionUI();
                readable.Read();
            }

            return;
        }

        // Notes
        if (hit.collider.TryGetComponent<NoteObject>(
            out NoteObject noteObj))
        {
            if (IsHiddenCaseEvidence(noteObj)) { HideInteractionUI(); return; }

            SetInteractionUI(ExaminePrompt(noteObj, "[E] Read Note Fragment"));

            if (GameKeys.Down(GameKeys.Action.Interact))
            {
                HideInteractionUI();
                noteObj.Interact();
            }

            return;
        }

        // Evidence Pickups
        if (hit.collider.TryGetComponent<EvidencePickUp>(
            out EvidencePickUp evidence))
        {
            // Flashlight-only case evidence (the hidden tape) can't be prompted for until it's lit.
            if (IsHiddenCaseEvidence(evidence)) { HideInteractionUI(); return; }

            if (evidence.requiresFlashlight &&
                (flashlightController == null || !flashlightController.isOn))
            {
                SetInteractionUI("Too dark to make out anything here.");
                return;
            }

            SetInteractionUI(ExaminePrompt(evidence, "[E] Collect " + evidence.EvidenceName));

            if (GameKeys.Down(GameKeys.Action.Interact))
            {
                evidence.Interact();
            }

            return;
        }

        // Inspectable Objects
        if (hit.collider.TryGetComponent<InspectItem>(
            out InspectItem inspectable))
        {
            SetInteractionUI(
                "[E] Examine " + inspectable.itemName
            );

            if (GameKeys.Down(GameKeys.Action.Interact))
            {
                StartInspectingObject(inspectable);
            }

            return;
        }

        // Keys
        if (hit.collider.TryGetComponent<KeyItem>(
            out KeyItem key))
        {
            SetInteractionUI(
                "[E] Pick up " + key.keyName
            );

            if (GameKeys.Down(GameKeys.Action.Interact))
            {
                CollectKey(key);
            }

            return;
        }

        // Batteries (consumed immediately, not stored in inventory)
        if (hit.collider.TryGetComponent<BatteryPickup>(
            out BatteryPickup battery))
        {
            SetInteractionUI(
                "[E] Pick up " + battery.pickupName
            );

            if (GameKeys.Down(GameKeys.Action.Interact))
            {
                CollectBattery(battery);
            }

            return;
        }

    }

    HideOrOfferClimb();
}

    // Nothing to interact with under the crosshair: if the player is squared up to an open window
    // they can climb through, say so (the climb itself runs from PlayerMovement's jump key).
    private void HideOrOfferClimb()
    {
        if (windowClimb != null && windowClimb.CanClimb())
            SetInteractionUI("[" + GameKeys.KeyName(GameKeys.Get(GameKeys.Action.Jump)) + "] Climb Through Window");
        else
            HideInteractionUI();
    }

    private readonly RaycastHit[] sightHits = new RaycastHit[16];

    // True when solid non-Interact geometry sits between the camera and the interactable the ray
    // found. Measured from the camera itself (not a pulled-back retry origin, which could start
    // behind a wall at the player's back). The player's own colliders and other parts of the same
    // door/cabinet/object don't count, and 5 cm of slack keeps a clue lying flush on a floor or
    // shelf from being hidden by that surface.
    private bool IsBehindWorldGeometry(Vector3 eye, RaycastHit target)
    {
        Vector3 toTarget = target.point - eye;
        float distance = toTarget.magnitude - 0.05f;
        if (distance <= 0f) return false;

        int count = Physics.RaycastNonAlloc(eye, toTarget / toTarget.magnitude, sightHits, distance,
            ~interactableLayer, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = sightHits[i].collider;
            if (c == null || c == target.collider) continue;
            if (c.transform.IsChildOf(transform.root)) continue; // the player's own body
            if (c.transform.IsChildOf(target.collider.transform)) continue;
            if (SameOwner<DoorController>(c, target.collider) || SameOwner<CabinetController>(c, target.collider)) continue;
            if (Houses(c, target.collider.bounds.center)) continue;
            return true;
        }
        return false;
    }

    // Furniture wrapped around the thing being aimed at (a wardrobe body around its drawers, a rack
    // frame around its file boxes, a fridge around the battery inside) isn't a wall in the way: its
    // collider shell sits a few cm in front of them. Walls are thin slabs, so whatever stands behind
    // one is never inside its bounds; the size cap keeps a whole building mesh from counting.
    private static bool Houses(Collider blocker, Vector3 point)
    {
        Bounds local;
        if (blocker is BoxCollider box) local = new Bounds(box.center, box.size);
        else if (blocker is MeshCollider mesh && mesh.sharedMesh != null) local = mesh.sharedMesh.bounds;
        else return false;

        Vector3 size = Vector3.Scale(local.size, blocker.transform.lossyScale);
        if (Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) > 5f) return false;
        return local.Contains(blocker.transform.InverseTransformPoint(point));
    }

    private static bool SameOwner<T>(Collider a, Collider b) where T : Component
    {
        T owner = b.GetComponentInParent<T>();
        return owner != null && a.GetComponentInParent<T>() == owner;
    }

    private void StartInspectingObject(InspectItem item)
    {
        if (inspectPoint == null) return;
        isInspecting = true;
        currentlyInspectingItem = item;
        HideInteractionUI();
        currentlyInspectingItem.StartInspecting(inspectPoint);
    }

    private void HandleInspectionRotation()
    {
        if (currentlyInspectingItem == null) return;

        float rotateX = Input.GetAxis("Mouse X") * itemRotationSpeed * Time.deltaTime;
        float rotateY = Input.GetAxis("Mouse Y") * itemRotationSpeed * Time.deltaTime;

        currentlyInspectingItem.transform.Rotate(Vector3.up, -rotateX, Space.World);
        currentlyInspectingItem.transform.Rotate(Vector3.right, rotateY, Space.World);

        if (GameKeys.Down(GameKeys.Action.Interact))
        {
            currentlyInspectingItem.StopInspecting();
            displayNotificationLog();
        }
    }

    private void displayNotificationLog()
    {
        DisplayNotification("Logged: " + currentlyInspectingItem.itemName);
        currentlyInspectingItem = null;
        isInspecting = false;
    }

   private void UpdateHandVisuals()
{
    // The flashlight is always carried (it is no longer a hotbar slot); F turns it on and off.
    if (flashlightVisual != null && !flashlightVisual.activeSelf)
        flashlightVisual.SetActive(true);

    // Key visual
    if (handKeyVisual != null)
    {
        bool showKey = false;

        if (currentlySelectedSlot >= 0 &&
            currentlySelectedSlot < heldItemNames.Length)
        {
            string itemName = heldItemNames[currentlySelectedSlot];

            showKey =
                slotOccupied[currentlySelectedSlot] &&
                doorKeyNames.Contains(Normalize(itemName));
        }

        handKeyVisual.SetActive(showKey);
    }
}

  void CollectKey(KeyItem key)
{
    if (playerMovement != null) playerMovement.PlayGrabPulse();

    for (int i = 0; i < HotbarSize; i++)
    {
        if (!slotOccupied[i])
        {
            AssignKeyToSlotIndex(i, key);
            PlayPickupSound();
            DisplayNotification("Picked up " + key.keyName);
            return;
        }
    }

    for (int i = 4; i < slotOccupied.Length; i++)
    {
        if (!slotOccupied[i])
        {
            AssignKeyToSlotIndex(i, key);
            PlayPickupSound();
            DisplayNotification("Stored in bag slot");
            return;
        }
    }

    Debug.LogWarning("Inventory full - could not pick up " + key.keyName);
}

    void CollectBattery(BatteryPickup battery)
    {
        if (playerMovement != null) playerMovement.PlayGrabPulse();

        battery.PickUp();
        PlayPickupSound();
        DisplayNotification("Battery Restored (+" + battery.batteryRestoreAmount + "%)");
    }

    private void AssignKeyToSlotIndex(int index, KeyItem key)
{
    slotOccupied[index] = true;
    heldItemNames[index] = key.keyName;
    RememberDoorKey(key.keyName); // in case it was spawned after the start-up scan

    if (index >= allInventorySlots.Length ||
        allInventorySlots[index] == null)
    {
        Debug.LogError(
            $"Inventory slot {index} missing!"
        );
        return;
    }

    InventorySlot targetSlot =
        allInventorySlots[index];

    DraggableItem drag = GetOrCreateDraggableItem(targetSlot);

    if (drag != null)
    {
        drag.SetupItem(
            key.keyName,
            key.itemDescription,
            keyIcon
        );

        drag.itemUsage =
            key.usageInstructions;

        drag.sourceSlot =
            targetSlot;

        drag.parentAfterDrag =
            targetSlot.transform;

        drag.isReadableNote = false;
        drag.sourceWorldObject = key.gameObject;
        drag.inspectCopySource = null;

        // Force the icon box itself and compensate for the slot's own scale - see the matching
        // comments in CollectEvidence(). This was previously missing the scale compensation
        // entirely, which would show a key the same "small icon with a bigger duplicate behind it"
        // way the flashlight/note did before that got fixed.
        RectTransform keyRect = drag.GetComponent<RectTransform>();
        if (keyRect != null)
        {
            Vector3 parentLossyScale = UiScale.WithinCanvas(targetSlot.transform);
            keyRect.localScale = new Vector3(
                parentLossyScale.x != 0f ? 1f / parentLossyScale.x : 1f,
                parentLossyScale.y != 0f ? 1f / parentLossyScale.y : 1f,
                parentLossyScale.z != 0f ? 1f / parentLossyScale.z : 1f
            );

            keyRect.sizeDelta = GetSlotIconSize(targetSlot);
            keyRect.anchoredPosition = Vector2.zero;
        }
    }
    else
    {
        Debug.LogError(
            "uiItemPrefab missing DraggableItem!"
        );
    }

    // Hotbar icon - false: the DraggableItem set up above already renders this icon, so the
    // permanent display isn't needed (and would otherwise draw a second, separately-sized copy
    // behind it).
    if (index < 4 && hotbarManager != null)
    {
        hotbarManager.UpdateSlot(
            index,
            keyIcon,
            key.keyName,
            false
        );
    }

    key.PickUp(inspectPoint);

   // StartCoroutine(ForceCameraRefresh()
    ;
}
    // The bag's slots are children of a GridLayoutGroup that only actually lays them out once its
    // GameObject hierarchy is active - and the bag starts closed, so a slot's own RectTransform.rect
    // reads as (0,0) (never laid out) for any item collected before the bag has been opened at least
    // once this session. GridLayoutGroup.cellSize itself is a plain field, unaffected by active
    // state, so read the icon's target size from there when the slot belongs to one; the hotbar
    // isn't grid-managed and its slots' rects are always valid (it's on screen the whole session).
    private Vector2 GetSlotIconSize(InventorySlot slot) => slot.IconSize();

    // Every slot reuses one DraggableItem for its whole life (see GetOrCreateDraggableItem), so an
    // emptied slot has to forget everything about its last item. Leftovers used to carry over to the
    // next pickup: a key landing where a dropped note had been opened the note reader with the key's
    // text when clicked, and evidence could show the previous key's "Usage" line.
    private static void ClearItemData(DraggableItem item)
    {
        item.itemName = "";
        item.itemDescription = "";
        item.itemUsage = "";
        item.isReadableNote = false;
        item.sourceWorldObject = null;
        item.inspectCopySource = null;
    }

    private DraggableItem GetDirectDraggableItem(InventorySlot slot)
    {
        if (slot == null) return null;

        DraggableItem directComp = slot.GetComponentInChildren<DraggableItem>(true);
        if (directComp != null) return directComp;

        foreach (Transform child in slot.transform)
        {
            DraggableItem item = child.GetComponent<DraggableItem>();
            if (item != null) return item;
        }
        return null;
    }

    // Every slot keeps exactly one DraggableItem for its whole lifetime, reused across pickups
    // instead of destroyed-and-reinstantiated each time. The old pattern (Destroy() the empty
    // placeholder, Instantiate() a fresh one) let stale copies linger since Destroy() only takes
    // effect at end of frame - a same-slot lookup in that window could grab the leftover empty
    // one instead of the newly populated one, which is how a picked-up item could end up with no
    // visible icon, and how a used/removed item's icon could fail to actually clear.
    private DraggableItem GetOrCreateDraggableItem(InventorySlot slot)
    {
        if (slot == null) return null;

        DraggableItem[] existing = slot.GetComponentsInChildren<DraggableItem>(true);
        if (existing.Length > 0)
        {
            // Dedupe by GameObject first - destroying existing[i].gameObject when it's the SAME
            // object as existing[0] (multiple DraggableItem components stacked on one GameObject,
            // rather than separate sibling items) would destroy the one we're about to return too.
            GameObject kept = existing[0].gameObject;
            for (int i = 1; i < existing.Length; i++)
            {
                if (existing[i].gameObject != kept) Destroy(existing[i].gameObject);
            }

            // Then strip any extra DraggableItem components left stacked on the kept GameObject.
            DraggableItem[] compsOnKept = kept.GetComponents<DraggableItem>();
            for (int i = 1; i < compsOnKept.Length; i++) Destroy(compsOnKept[i]);

            return compsOnKept[0];
        }

        if (uiItemPrefab == null) return null;

        GameObject newItem = Instantiate(uiItemPrefab, slot.transform);
        RectTransform rect = newItem.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.localPosition = Vector3.zero;
            rect.localScale = Vector3.one;
        }

        return newItem.GetComponent<DraggableItem>();
    }

   IEnumerator ForceCameraRefresh()
{
    yield return null;
}

    public bool IsHoldingItem(string itemName) =>
        currentlySelectedSlot >= 0 && currentlySelectedSlot < heldItemNames.Length &&
        Normalize(heldItemNames[currentlySelectedSlot]) == Normalize(itemName);
    
    public bool HasKeySomewhere(string itemName)
    {
        for (int i = 0; i < slotOccupied.Length; i++)
            if (slotOccupied[i] && Normalize(heldItemNames[i]) == Normalize(itemName)) return true;
        return false;
    }

   public void RemoveItem(string itemName)
{
    for (int i = 0; i < slotOccupied.Length; i++)
    {
        if (slotOccupied[i] &&
            Normalize(heldItemNames[i]) == Normalize(itemName))
        {
            // Clear inventory data
            heldItemNames[i] = "";
            slotOccupied[i] = false;

            // Clear inventory UI
            if (i < allInventorySlots.Length &&
                allInventorySlots[i] != null)
            {
                DraggableItem item =
                    GetDirectDraggableItem(allInventorySlots[i]);

                if (item != null)
                {
                    ClearItemData(item);

                    if (item.image != null)
                    {
                        item.image.sprite = null;
                        item.image.enabled = false;
                    }
                }
            }

            // Clear hotbar icon
            if (hotbarManager != null && i < 4)
            {
                hotbarManager.ClearSlot(i);
            }

            // Nothing in hand any more
            currentlySelectedSlot = NoSlot;

            if (hotbarManager != null)
            {
                hotbarManager.HighlightSlot(NoSlot);
            }

            return;
        }
    }
}
    void PlayPickupSound()
    {
        if (pickupAudioSource == null || pickupClips == null || pickupClips.Length == 0) return;
        AudioClip clip = pickupClips[Random.Range(0, pickupClips.Length)];
        pickupAudioSource.pitch = Random.Range(1f - pickupPitchJitter, 1f + pickupPitchJitter);
        pickupAudioSource.PlayOneShot(clip, SfxVolume.Multiplier);
    }

    // Case evidence (anything with an EvidenceItem) reads "[E] Examine <name>"; older pickups keep their own wording.
    private static string ExaminePrompt(Component c, string legacyPrompt)
    {
        var item = c.GetComponent<EvidenceItem>();
        return item != null ? "[E] Examine " + item.DisplayName : legacyPrompt;
    }

    private static bool IsHiddenCaseEvidence(Component c)
    {
        var item = c.GetComponent<EvidenceItem>();
        return item != null && !item.IsVisibleNow;
    }

    public void DisplayNotification(string message) { StopCoroutine("ShowMessage"); StartCoroutine(ShowMessage(message)); }
    private string Normalize(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(" ", "").ToLower();
    // Prompts are written with "[E]"; FormatPrompt swaps in the key Interact is actually bound to.
    private void SetInteractionUI(string text) { if (interactionText != null) { interactionText.text = GameKeys.FormatPrompt(text); interactionText.gameObject.SetActive(true); } }
    private void HideInteractionUI() { if (interactionText != null && !isShowingNotification) interactionText.gameObject.SetActive(false); }

    IEnumerator ShowMessage(string msg)
    {
        isShowingNotification = true;
        interactionText.text = msg;
        interactionText.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(2.5f); 
        isShowingNotification = false;
        interactionText.gameObject.SetActive(false);
    }
public void CollectEvidence(InspectItem item)
{
    if (item == null)
    {
        Debug.LogError("InspectItem is NULL!");
        return;
    }

    if (playerMovement != null) playerMovement.PlayGrabPulse();

    for (int i = 0; i < slotOccupied.Length; i++)
    {
        if (!slotOccupied[i])
        {
            heldItemNames[i] = item.itemName;
            slotOccupied[i] = true;

            InventorySlot targetSlot = allInventorySlots[i];

            if (targetSlot == null)
            {
                Debug.LogError("Inventory slot " + i + " is missing!");
                return;
            }

            DraggableItem drag = GetOrCreateDraggableItem(targetSlot);

            if (drag != null)
            {
                RectTransform rect = drag.GetComponent<RectTransform>();

                if (rect != null)
                {
                    rect.localPosition = Vector3.zero;

                    // Slots can sit under differently-scaled ancestors (e.g. the bag vs the hotbar),
                    // so compensate here to keep the icon's actual on-screen size consistent everywhere.
                    Vector3 parentLossyScale = UiScale.WithinCanvas(targetSlot.transform);
                    rect.localScale = new Vector3(
                        parentLossyScale.x != 0f ? 1f / parentLossyScale.x : 1f,
                        parentLossyScale.y != 0f ? 1f / parentLossyScale.y : 1f,
                        parentLossyScale.z != 0f ? 1f / parentLossyScale.z : 1f
                    );

                    // Force the icon box itself instead of trusting whatever size it happened to
                    // come with - a stale/pre-existing "itemprefab" template child (bag slots) or a
                    // freshly Instantiate()'d uiItemPrefab (hotbar slots, which have no pre-existing
                    // template child) can otherwise be much bigger than intended. Sized off the
                    // slot's own actual rect (hotbar and bag slots are different sizes, and either
                    // can be resized later) using the same fill ratio HotbarManager's iconDisplay
                    // uses for hotbar slots specifically - that separate image already renders the
                    // same icon on top of this one there, so matching sizes makes the two layers
                    // overlap invisibly instead of showing as a small icon with a bigger duplicate.
                    rect.sizeDelta = targetSlot.IconSize(item.GetComponent<NoteObject>() != null);
                    rect.anchoredPosition = Vector2.zero;
                }

                drag.SetupItem(
                    item.itemName,
                    item.itemDescription,
                    item.itemIcon,
                    hotbarPanel,
                    bagPanel
                );

                drag.sourceSlot = targetSlot;
                drag.parentAfterDrag = targetSlot.transform;
                drag.itemUsage = "";
                drag.isReadableNote = item.GetComponent<NoteObject>() != null;

                // Kept for notes too (not just evidence/keys) now that NoteSystem hides rather than
                // destroys the original NoteObject on pickup - lets a dropped note be placed back
                // in the world instead of vanishing. DraggableItem.OnPointerClick still routes
                // isReadableNote items to the note-reading overlay before it ever checks this, so
                // setting it here doesn't change how notes behave when clicked in the bag.
                // Scene evidence that stays in the world (EvidenceItem.keepInWorld) has no hidden pickup to
                // borrow or to put back when dropped, so it keeps no world reference; the 3D view shows
                // a copy of it instead.
                bool keepInWorld = item.TryGetComponent<EvidenceItem>(out EvidenceItem caseEvidence) && caseEvidence.keepInWorld;
                drag.sourceWorldObject = keepInWorld ? null : item.gameObject;
                drag.inspectCopySource = keepInWorld ? item.gameObject : null;
            }

            // Update hotbar if placed in hotbar - false: the DraggableItem set up above already
            // renders this icon, so the permanent display isn't needed (and would otherwise draw
            // a second, separately-sized copy behind it).
            if (i < 4 && hotbarManager != null)
            {
                hotbarManager.UpdateSlot(
                    i,
                    item.itemIcon,
                    item.itemName,
                    false
                );
            }

            // Find EvidenceManager
            EvidenceManager evidenceManager =
                FindFirstObjectByType<EvidenceManager>();

            if (evidenceManager == null)
            {
                Debug.LogError("EvidenceManager not found in the scene!");
            }
            // Dropping an item (see DropItemInWorld) deliberately re-enables its world object so it
            // can be picked back up rather than vanishing - that re-pickup already went through
            // CollectEvidence once before, so the evidence itself was already recorded. Skip the
            // redundant AddEvidence call (and its "duplicate" warning, which would otherwise fire on
            // every ordinary drop-then-recollect) rather than re-reporting evidence already counted.
            else if (!evidenceManager.HasEvidence(item.itemName))
            {
                evidenceManager.AddEvidence(item.itemName, item.category);
            }

            PlayPickupSound();
            return;
        }
    }

    Debug.LogWarning("Inventory Full!");
}}