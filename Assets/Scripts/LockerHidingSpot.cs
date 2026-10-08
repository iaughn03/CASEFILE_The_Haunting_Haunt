using System.Collections;
using UnityEngine;
using TMPro;

// Attach to a locker/wardrobe/hiding-spot object alongside a trigger Collider sized around it (see
// Reset() - it forces isTrigger on for you). Player walks into the trigger, a prompt appears, and
// pressing the interact key snaps them inside: normal movement freezes (mouse look still works, so
// hiding still lets you peek around), their own colliders/renderers turn off so nothing physically
// or visually gives them away, and GhostController-style AI is told they're unreachable via
// PlayerHideState. Pressing the same key again reverses all of it and puts them back just outside.
[RequireComponent(typeof(Collider))]
public class LockerHidingSpot : MonoBehaviour
{
    [Header("Prompt")]
    [Tooltip("Dedicated UI text for this hiding spot's prompt - kept separate from PlayerInteract's own raycast-driven prompt so the two never fight over the same Text component on the same frame.")]
    public TextMeshProUGUI interactionText;
    public string enterPrompt = "[E] Hide";
    public string exitPrompt = "[E] Come out";
    public KeyCode interactKey = KeyCode.E;

    [Header("Hiding Position")]
    [Tooltip("Empty child transform placed just inside the locker, facing out through the door/slats. The player is snapped here on entry, with the camera reset to standing eye height and a level view - so set its height so that eye height (hidePoint Y + the player camera's local Y) lands on the door's vent slits.")]
    public Transform hidePoint;
    [Tooltip("Where the player lands on exit, standing just outside the locker. Defaults to this object's own transform if left empty.")]
    public Transform exitPoint;

    [Header("Obstructed View")]
    [Tooltip("A dark/slatted overlay covering most of the screen while hidden - a cheap stand-in for actually swapping cameras. Needs a CanvasGroup on a full-screen Image.")]
    public CanvasGroup slatOverlay;
    public float overlayFadeSpeed = 8f;
    [Tooltip("HUD elements (hotbar, health/energy/battery bars, objective panel, crosshair, etc.) switched off while hidden so peeking through the slats doesn't still show the full HUD on top of it. The hide prompt itself (interactionText) is handled separately and should NOT be listed here.")]
    public GameObject[] hudElementsToHide;

    private bool[] hudElementsPreviousState;

    [Header("Interior Liner")]
    [Tooltip("Most locker models are a thin outward-facing shell with no real interior geometry, so the camera sitting at hidePoint (which is geometrically inside the model's bounds) sees straight through it to whatever is behind/outside - backface culling makes the shell invisible from its own inside. This generates a plain dark box, sized to fit just inside the model, that's shown only while hiding so the player's POV actually reads as enclosed instead of floating in empty space. Turn off if this locker's model already has real modeled interior geometry.")]
    public bool useInteriorLiner = true;
    [Tooltip("World-space size of the generated interior box. Leave at zero to auto-size from this object's combined renderer bounds (shrunk slightly so it sits just inside the model's walls).")]
    public Vector3 interiorLinerSize = Vector3.zero;
    public Color interiorLinerColor = new Color(0.04f, 0.03f, 0.03f);
    [Tooltip("Leaves the liner's front wall (the side hidePoint faces) open, so the slits in slatOverlay show the outside world while the other walls keep the view feeling enclosed. Turn off to get the old fully-closed black box.")]
    public bool openLinerFront = true;

    private GameObject interiorLiner;

    [Header("Locker Animation/Audio")]
    [Tooltip("Optional: an Animator on the locker, driven via openTrigger/closeTrigger, if it already has open/close clips authored.")]
    public Animator lockerAnimator;
    public string openTrigger = "Open";
    public string closeTrigger = "Close";
    [Tooltip("Optional: a simple door panel to swing open/closed by script - no Animator needed. Same eased swing as DoorController. Leave empty to skip (e.g. if using lockerAnimator instead, or no visible door movement at all).")]
    public Transform doorTransform;
    public float doorOpenAngle = 90f;
    public Vector3 doorRotationAxis = Vector3.up;
    public float doorRotationSpeed = 320f;
    [Tooltip("Stepping in or out swings the door open, then shut behind you after this many seconds - so it stays closed the whole time you're hidden. Keep it just above the time the door takes to open (90 deg / doorRotationSpeed, or the Open clip's length).")]
    public float doorStayOpenTime = 0.35f;
    public AudioSource audioSource;
    public AudioClip[] openClips;
    public AudioClip[] closeClips;
    [Range(0f, 0.5f)] public float pitchJitter = 0.05f;
    [Tooltip("The locker clip runs long (several seconds of clang/rattle tail) - this cuts it off after just the initial hit instead of playing it out.")]
    public float soundCutoffDuration = 1.5f;
    [Tooltip("Quick fade applied right before the cutoff so it doesn't end in an audible click/pop.")]
    public float soundFadeOutDuration = 0.15f;

    private Coroutine audioCutoffRoutine;

    public bool IsHiding { get; private set; }

    private bool playerInRange;
    private PlayerInteract playerInteract;
    private PlayerMovement playerMovement;
    private CharacterController playerController;
    private Rigidbody playerRigidbody;
    private Collider[] playerColliders;
    private Renderer[] playerRenderers;
    private bool[] playerRenderersPreviousState;
    private PlayerHideState hideState;
    private float targetOverlayAlpha;

    private Quaternion doorClosedRot;
    private Quaternion doorOpenRot;
    private bool doorOpen;
    private Coroutine doorSwingRoutine;

    // Deliberately no Reset() here: this is commonly added to furniture that already has its own
    // solid (often concave MeshCollider) collider for physical blocking, and blindly flipping
    // "the first collider found" to a trigger corrupts that - it fails outright on a concave mesh,
    // and even when it doesn't, it would silently turn the locker's own body non-solid. The trigger
    // collider for proximity detection should always be added explicitly, separate from whatever
    // collider the model already ships with.

    void Start()
    {
        if (doorTransform != null)
        {
            doorClosedRot = doorTransform.localRotation;
            doorOpenRot = Quaternion.AngleAxis(doorOpenAngle, doorRotationAxis) * doorClosedRot;
        }

        BuildInteriorLiner();
    }

    // Builds a plain box that only renders from the inside (Cull Front, so the outward faces are
    // invisible and it never pops in over the model from outside) and stays inactive except while
    // hiding - see the useInteriorLiner tooltip for why this exists.
    void BuildInteriorLiner()
    {
        if (!useInteriorLiner) return;

        Vector3 size = interiorLinerSize;
        Vector3 center;

        if (size == Vector3.zero)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            b.Expand(-0.08f);

            if (b.size.x <= 0f || b.size.y <= 0f || b.size.z <= 0f) return;

            size = b.size;
            center = b.center;
        }
        else
        {
            center = hidePoint != null ? hidePoint.position : transform.position;
        }

        interiorLiner = GameObject.CreatePrimitive(PrimitiveType.Cube);
        interiorLiner.name = "InteriorLiner (generated)";

        if (openLinerFront && hidePoint != null)
        {
            // The liner keeps an identity world rotation, so its local axes are the world axes -
            // drop the face whose normal is the world axis hidePoint is mostly looking along.
            Vector3 f = hidePoint.forward;
            Vector3 omit = Mathf.Abs(f.x) >= Mathf.Abs(f.y) && Mathf.Abs(f.x) >= Mathf.Abs(f.z)
                ? new Vector3(Mathf.Sign(f.x), 0f, 0f)
                : (Mathf.Abs(f.z) >= Mathf.Abs(f.y) ? new Vector3(0f, 0f, Mathf.Sign(f.z)) : new Vector3(0f, Mathf.Sign(f.y), 0f));

            MeshFilter mf = interiorLiner.GetComponent<MeshFilter>();
            mf.sharedMesh = BuildBoxWithoutFace(mf.sharedMesh, omit);
        }
        interiorLiner.transform.SetParent(transform, true);
        interiorLiner.transform.position = center;
        interiorLiner.transform.rotation = Quaternion.identity;

        // The primitive cube is 1x1x1 in local space - dividing the desired world size by the
        // parent's lossy scale gives a localScale that lands on that world size regardless of how
        // the locker itself is scaled in the scene.
        Vector3 parentScale = transform.lossyScale;
        interiorLiner.transform.localScale = new Vector3(
            size.x / Mathf.Max(parentScale.x, 0.0001f),
            size.y / Mathf.Max(parentScale.y, 0.0001f),
            size.z / Mathf.Max(parentScale.z, 0.0001f));

        Collider generatedCollider = interiorLiner.GetComponent<Collider>();
        if (generatedCollider != null) Destroy(generatedCollider);

        Renderer linerRenderer = interiorLiner.GetComponent<MeshRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        Material mat = new Material(shader);
        mat.color = interiorLinerColor;
        if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Front);
        linerRenderer.sharedMaterial = mat;
        linerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linerRenderer.receiveShadows = false;

        interiorLiner.SetActive(false);
    }

    // Copies the built-in cube mesh and drops the triangles of the one face whose normal matches
    // `omitNormal`, reusing the primitive's own winding so the remaining faces still render
    // correctly with the liner's Cull Front material.
    static Mesh BuildBoxWithoutFace(Mesh cube, Vector3 omitNormal)
    {
        Vector3[] verts = cube.vertices;
        Vector3[] normals = cube.normals;
        int[] tris = cube.triangles;
        var kept = new System.Collections.Generic.List<int>();

        for (int i = 0; i < tris.Length; i += 3)
        {
            if (Vector3.Dot(normals[tris[i]], omitNormal) > 0.9f) continue;
            kept.Add(tris[i]); kept.Add(tris[i + 1]); kept.Add(tris[i + 2]);
        }

        Mesh m = new Mesh();
        m.name = "LinerOpenBox";
        m.vertices = verts;
        m.normals = normals;
        m.uv = cube.uv;
        m.triangles = kept.ToArray();
        m.RecalculateBounds();
        return m;
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("[LockerDebug] OnTriggerEnter by " + other.gameObject.name);
        if (IsHiding) return;
        PlayerInteract pi = other.GetComponentInParent<PlayerInteract>();
        if (pi == null) { Debug.Log("[LockerDebug] no PlayerInteract found in parent chain"); return; }

        Debug.Log("[LockerDebug] player entered range");
        CachePlayerRefs(pi);
        playerInRange = true;
        UpdatePrompt();
    }

    void OnTriggerExit(Collider other)
    {
        PlayerInteract pi = other.GetComponentInParent<PlayerInteract>();
        if (pi == null || pi != playerInteract) return;

        playerInRange = false;
        if (!IsHiding) HideInteractionUI();
    }

    // Player colliders are disabled while hidden (see SetPlayerVisible), which would normally stop
    // OnTriggerExit from ever firing when they walk back out - re-enabling happens in Exit() itself
    // instead of waiting on a trigger event, so this isn't load-bearing for that case, only for the
    // normal walk-away-without-hiding path.
    void CachePlayerRefs(PlayerInteract pi)
    {
        if (playerInteract == pi) return;

        playerInteract = pi;
        playerMovement = pi.GetComponent<PlayerMovement>();
        playerController = pi.GetComponent<CharacterController>();
        playerRigidbody = pi.GetComponent<Rigidbody>();
        playerColliders = pi.GetComponentsInChildren<Collider>(true);
        playerRenderers = pi.GetComponentsInChildren<Renderer>(true);

        hideState = pi.GetComponent<PlayerHideState>();
        if (hideState == null) hideState = pi.gameObject.AddComponent<PlayerHideState>();
    }

    void Update()
    {
        if (slatOverlay != null)
        {
            slatOverlay.alpha = Mathf.MoveTowards(slatOverlay.alpha, targetOverlayAlpha, overlayFadeSpeed * Time.deltaTime);
            slatOverlay.blocksRaycasts = slatOverlay.alpha > 0.01f;
        }

        if (doorTransform != null)
        {
            Quaternion target = doorOpen ? doorOpenRot : doorClosedRot;
            if (doorTransform.localRotation != target)
                doorTransform.localRotation = Quaternion.RotateTowards(doorTransform.localRotation, target, doorRotationSpeed * Time.deltaTime);
        }

        // Re-enforced every frame rather than only once in Enter(), so nothing else (another HUD
        // script re-activating its own panel, a stray SetActive elsewhere) can pop these back on
        // for even a frame while hidden - SetActive on an already-inactive object is a cheap no-op.
        if (IsHiding && hudElementsToHide != null)
        {
            foreach (var el in hudElementsToHide)
            {
                if (el != null && el.activeSelf) el.SetActive(false);
            }
        }

        if (!playerInRange && !IsHiding) return;
        if (playerInteract == null) return;

        // Watchdog: catches anything ELSE moving the player away while supposedly hidden.
        if (IsHiding && hidePoint != null && Vector3.Distance(playerInteract.transform.position, hidePoint.position) > 0.5f)
            Debug.Log("[LockerDebug] WATCHDOG: player drifted away while hiding! now at " + playerInteract.transform.position + " expected near " + hidePoint.position);

        // Menus own the keyboard: E behind the pause menu, bag, a note or a case panel must not step
        // in or out. Those all stop time, so check that rather than trust a flag that can drift.
        if (PauseManager.isPaused || PauseManager.isCaseUiOpen || Time.timeScale == 0f) return;

        // interactKey left at its default E follows the rebindable Interact key
        if (interactKey == KeyCode.E ? GameKeys.Down(GameKeys.Action.Interact) : Input.GetKeyDown(interactKey))
        {
            Debug.Log("[LockerDebug] E pressed. IsHiding=" + IsHiding + " playerInRange=" + playerInRange);
            if (IsHiding) Exit();
            else if (playerInRange) Enter();
        }
    }

    // Public so other systems (a scripted scare, the ghost finding you, a cutscene) can force the
    // player into or out of a hiding spot without faking a keypress - and so the hide/exit cycle is
    // testable without driving Input.
    public void Enter()
    {
        IsHiding = true;
        Debug.Log("[LockerDebug] Enter() called. Player pos before=" + playerInteract.transform.position + " hidePoint=" + (hidePoint != null ? hidePoint.position.ToString() : "NULL"));

        if (hidePoint != null)
        {
            playerInteract.transform.position = hidePoint.position;
            playerInteract.transform.rotation = hidePoint.rotation;
        }
        Debug.Log("[LockerDebug] Player pos after set=" + playerInteract.transform.position);

        // Disabling the CharacterController (rather than just leaving movementLocked to do the work)
        // matches the same pattern PlayerVaultController already uses while it drives the transform
        // directly during a vault - guarantees nothing can call controller.Move() on it mid-hide.
        if (playerController != null) playerController.enabled = false;

        // With the CharacterController off, a non-kinematic Rigidbody is free to fall/drift under
        // physics - and if a CharacterJoint sits on the same object with no connectedBody, it's
        // anchored to a fixed point in world space and will yank the player straight back toward
        // wherever that anchor was last configured (this is what caused the "teleports to spawn"
        // bug: hiding disabled the controller, and the joint pulled the rigidbody back to its
        // anchor before anything else could resist it). Going kinematic stops all of that - Joint
        // has no enabled flag to toggle (it's a plain Component, not a Behaviour), but a kinematic
        // Rigidbody simply isn't affected by joints or forces at all, which covers it either way.
        if (playerRigidbody != null) playerRigidbody.isKinematic = true;

        if (playerMovement != null)
        {
            playerMovement.movementLocked = true;
            playerMovement.ResetViewForHiding();
            if (playerMovement.footstepSource != null && playerMovement.footstepSource.isPlaying)
                playerMovement.footstepSource.Stop();
        }

        SetPlayerVisible(false);
        SetHudVisible(false);
        if (interiorLiner != null) interiorLiner.SetActive(true);
        if (hideState != null) hideState.SetHiding(true);

        targetOverlayAlpha = 1f;
        SwingDoor();

        UpdatePrompt();
        Debug.Log("[LockerDebug] Enter() finished. Final pos=" + playerInteract.transform.position + " controller.enabled=" + (playerController != null ? playerController.enabled.ToString() : "null") + " movementLocked=" + (playerMovement != null ? playerMovement.movementLocked.ToString() : "null") + " hideState.IsHiding=" + (hideState != null ? hideState.IsHiding.ToString() : "null(!)"));
    }

    public void Exit()
    {
        IsHiding = false;

        Quaternion exitRot = exitPoint != null ? exitPoint.rotation : transform.rotation;

        // Resolved BEFORE the CharacterController is switched back on, so the overlap tests inside
        // don't trip over the player's own capsule sitting at the hide position.
        Vector3 exitPos = ResolveExitPosition(exitPoint != null ? exitPoint.position : transform.position);

        // CharacterController must be re-enabled before moving the transform through it, otherwise
        // the position set below is exactly the kind of teleport CharacterController normally
        // resists - re-enable first, then place, matches how PlayerVaultController lands its own
        // vaults (see VaultOverObstacle's agent.Warp after agent.enabled = true).
        if (playerController != null) playerController.enabled = true;
        playerInteract.transform.position = exitPos;
        playerInteract.transform.rotation = exitRot;

        // Restore physics only after the transform is back in place, same ordering reasoning as
        // the CharacterController re-enable above - otherwise the joint could yank the rigidbody
        // before the position assignment above has a chance to land.
        if (playerRigidbody != null) playerRigidbody.isKinematic = false;

        if (playerMovement != null) playerMovement.movementLocked = false;

        SetPlayerVisible(true);
        SetHudVisible(true);
        if (interiorLiner != null) interiorLiner.SetActive(false);
        if (hideState != null) hideState.SetHiding(false);

        targetOverlayAlpha = 0f;
        SwingDoor();

        // Standing right at exitPoint, beside the locker - still in range without needing to wait
        // for a fresh OnTriggerEnter (their collider was off a moment ago and is only just now
        // being switched back on this same frame).
        playerInRange = true;
        UpdatePrompt();
    }

    // Opens the door, then pulls it shut behind the player - used for both stepping in and stepping
    // out. The door used to open on Enter() and only close again on Exit(), so it hung wide open the
    // whole time the player was hiding.
    void SwingDoor()
    {
        if (doorSwingRoutine != null) StopCoroutine(doorSwingRoutine);
        doorSwingRoutine = StartCoroutine(DoorSwingRoutine());
    }

    IEnumerator DoorSwingRoutine()
    {
        doorOpen = true;
        if (lockerAnimator != null && !string.IsNullOrEmpty(openTrigger))
        {
            if (!string.IsNullOrEmpty(closeTrigger)) lockerAnimator.ResetTrigger(closeTrigger);
            lockerAnimator.SetTrigger(openTrigger);
        }
        PlaySound(openClips);

        yield return new WaitForSeconds(doorStayOpenTime);

        doorOpen = false;
        if (lockerAnimator != null && !string.IsNullOrEmpty(closeTrigger))
        {
            if (!string.IsNullOrEmpty(openTrigger)) lockerAnimator.ResetTrigger(openTrigger);
            lockerAnimator.SetTrigger(closeTrigger);
        }
        PlaySound(closeClips);
        doorSwingRoutine = null;
    }

    // An exitPoint is placed by eye in the scene view, where the natural thing to do is drop it on
    // the floor beside the locker - but the player's transform origin is the CENTRE of their
    // CharacterController capsule, not their feet. A floor-level exitPoint therefore buries the
    // bottom half of the capsule in the floor slab (and in the terrain underneath it), and the
    // controller's depenetration pass ejects the player sideways out of that overlap on the next
    // frame - which is how "come out of the locker" was launching the player through the wall into
    // the next room. So rather than trusting the authored Y, this finds the actual floor under the
    // exit spot and sits the capsule on top of it, then sanity-checks that the spot is clear.
    Vector3 ResolveExitPosition(Vector3 desired)
    {
        if (playerController == null || playerInteract == null) return desired;

        Vector3 scale = playerInteract.transform.lossyScale;
        float halfHeight = Mathf.Max(playerController.height * 0.5f * scale.y,
                                     playerController.radius * Mathf.Max(scale.x, scale.z));
        // Distance from the transform origin down to the bottom of the capsule.
        float feetOffset = halfHeight - playerController.center.y * scale.y;

        Vector3 grounded = desired;
        RaycastHit floor;
        if (Physics.Raycast(desired + Vector3.up * (halfHeight + 0.5f), Vector3.down, out floor,
                            halfHeight + 3.5f, ~0, QueryTriggerInteraction.Ignore))
            grounded.y = floor.point.y + feetOffset + playerController.skinWidth;

        if (IsSpotClear(grounded, halfHeight)) return grounded;

        // Blocked (exitPoint sunk into a wall, furniture moved since it was placed, etc.) - walk
        // outwards along the locker's front face and take the first clear spot, so a bad placement
        // degrades into "standing slightly further out" rather than into a wall.
        Vector3 outward = transform.forward;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.001f) outward = Vector3.forward;
        outward.Normalize();

        for (float step = 0.4f; step <= 2.0f; step += 0.4f)
        {
            Vector3 candidate = grounded + outward * step;
            if (Physics.Raycast(candidate + Vector3.up * (halfHeight + 0.5f), Vector3.down, out floor,
                                halfHeight + 3.5f, ~0, QueryTriggerInteraction.Ignore))
                candidate.y = floor.point.y + feetOffset + playerController.skinWidth;

            if (IsSpotClear(candidate, halfHeight)) return candidate;
        }

        Debug.LogWarning("[" + name + "] No clear exit spot found near exitPoint - placing the player at the grounded exit position anyway. Check that exitPoint sits in open floor in front of the locker.", this);
        return grounded;
    }

    bool IsSpotClear(Vector3 position, float halfHeight)
    {
        float radius = playerController.radius * Mathf.Max(playerInteract.transform.lossyScale.x,
                                                           playerInteract.transform.lossyScale.z);
        Vector3 centre = position + Vector3.Scale(playerController.center, playerInteract.transform.lossyScale);
        float capsuleOffset = Mathf.Max(halfHeight - radius, 0f);

        // Triggers are ignored on purpose: the locker's own proximity trigger surrounds the exit
        // spot by design, and evidence/objective zones shouldn't count as "blocked" either.
        return Physics.OverlapCapsule(centre + Vector3.up * capsuleOffset,
                                      centre - Vector3.up * capsuleOffset,
                                      radius, ~0, QueryTriggerInteraction.Ignore).Length == 0;
    }

    // Caches each element's own prior active state (rather than just assuming they were all on)
    // so a HUD piece some other system had already hidden for its own reason doesn't get forced
    // back on when we come out of the locker.
    void SetHudVisible(bool visible)
    {
        if (hudElementsToHide == null) return;

        if (!visible)
        {
            hudElementsPreviousState = new bool[hudElementsToHide.Length];
            for (int i = 0; i < hudElementsToHide.Length; i++)
            {
                if (hudElementsToHide[i] == null) continue;
                hudElementsPreviousState[i] = hudElementsToHide[i].activeSelf;
                hudElementsToHide[i].SetActive(false);
            }
        }
        else
        {
            for (int i = 0; i < hudElementsToHide.Length; i++)
            {
                if (hudElementsToHide[i] == null) continue;
                bool restore = hudElementsPreviousState != null && i < hudElementsPreviousState.Length && hudElementsPreviousState[i];
                hudElementsToHide[i].SetActive(restore);
            }
        }
    }

    void SetPlayerVisible(bool visible)
    {
        if (playerColliders != null)
        {
            foreach (var c in playerColliders)
            {
                // The CharacterController is handled separately (Enter/Exit above) - toggling it
                // here too would race with that and could leave it in the wrong state.
                if (c == null || c is CharacterController) continue;
                c.enabled = visible;
            }
        }

        // The player's own body mesh renderer is already off by default (first-person, nothing to
        // see of yourself) - this mainly matters for held-item visuals (flashlight, key) that could
        // otherwise still poke out into view/onto a ghost's raycast while tucked in the locker.
        //
        // Restoring must go back to each renderer's own PRIOR state, not just blanket-enable
        // everything: blindly setting every renderer to true on exit was switching the flashlight
        // mesh back on even when it hadn't been equipped/switched on before hiding.
        if (playerRenderers != null)
        {
            if (!visible)
            {
                playerRenderersPreviousState = new bool[playerRenderers.Length];
                for (int i = 0; i < playerRenderers.Length; i++)
                {
                    if (playerRenderers[i] == null) continue;
                    playerRenderersPreviousState[i] = playerRenderers[i].enabled;
                    playerRenderers[i].enabled = false;
                }
            }
            else
            {
                for (int i = 0; i < playerRenderers.Length; i++)
                {
                    if (playerRenderers[i] == null) continue;
                    bool restore = playerRenderersPreviousState != null && i < playerRenderersPreviousState.Length && playerRenderersPreviousState[i];
                    playerRenderers[i].enabled = restore;
                }
            }
        }
    }

    void UpdatePrompt()
    {
        if (interactionText == null) return;

        if (IsHiding)
        {
            interactionText.text = GameKeys.FormatPrompt(exitPrompt);
            interactionText.gameObject.SetActive(true);
        }
        else if (playerInRange)
        {
            interactionText.text = GameKeys.FormatPrompt(enterPrompt);
            interactionText.gameObject.SetActive(true);
        }
        else
        {
            HideInteractionUI();
        }
    }

    void HideInteractionUI()
    {
        if (interactionText != null) interactionText.gameObject.SetActive(false);
    }

    void PlaySound(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        audioSource.pitch = Random.Range(1f - pitchJitter, 1f + pitchJitter);

        // PlayOneShot can't be stopped individually, and the source clip runs several seconds
        // longer than the actual clang we want - so this plays it on the dedicated AudioSource
        // instead and cuts it off short with a quick fade (same fix as Jumpscare2D's audio).
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.volume = SfxVolume.Multiplier;
        audioSource.Play();

        if (audioCutoffRoutine != null) StopCoroutine(audioCutoffRoutine);
        audioCutoffRoutine = StartCoroutine(AudioCutoffRoutine());
    }

    IEnumerator AudioCutoffRoutine()
    {
        yield return new WaitForSecondsRealtime(soundCutoffDuration);

        float startVolume = audioSource.volume;
        float t = 0f;
        while (t < soundFadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, t / soundFadeOutDuration);
            yield return null;
        }
        audioSource.Stop();
        audioSource.volume = SfxVolume.Multiplier;
    }
}
