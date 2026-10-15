using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float exhaustedRunSpeed = 6f; // run speed once energy is depleted
    public float crouchSpeed = 2.5f;
    public float mouseSensitivity = 2f;
    public float gravity = -19.62f;
    public float jumpHeight = 1.5f;

    [Header("Energy Settings")]
    public float maxEnergy = 100f;
    public float currentEnergy;
    public float energyDrainRate = 20f;
    public float energyRegenRate = 15f;

    [Header("Energy Bar UI")]
    public Image energyBarFill;       // the real, current-energy bar
    public Image energyBarTrailFill;  // lighter "afterimage" bar that lags behind
    public float energyBarFillSpeed = 2.8f;
    public float energyBarTrailSpeed = 0.8f;

    [Header("Crouch Settings")]
    public float standHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchSmoothTime = 10f;
    // Standing collider shape, captured at Start. Crouching shrinks the capsule from the top down -
    // the centre moves with it so the feet stay planted (see SetBodyHeight).
    private Vector3 standCenter;
    private CapsuleCollider bodyCapsule;
    private float bodyCapsuleStandHeight;
    private Vector3 bodyCapsuleStandCenter;

    [Header("Head Bob Settings")]
    public float walkBobSpeed = 10f;
    public float walkBobAmount = 0.05f;
    private float defaultYPos = 0;
    private float timer;
    private Vector3 bobOffset; // head-bob's own contribution, tracked separately so shake can never contaminate it

    [Header("References")]
    public Camera playerCamera;

    private CharacterController controller;
    private PlayerVaultController vaultScript; // Reference to your vault script
    private PlayerWindowClimb windowClimb;     // climbing through open windows (Game2 only)
    private Animator bodyAnimator; // drives the visible character's Idle/Walk/Run states
    private float verticalRotation = 0f;
    private Vector3 velocity;
    private bool isGrounded;
    private Vector2 moveInput;                   // eased movement input, see the MOVEMENT section
    private const float moveInputResponse = 6f;  // how fast input eases to full (units/sec), like an axis' sensitivity

    private float shakeDuration;
    private float shakeTimer;
    private float shakeMagnitude;

    [Header("Item Holding")]
    public float holdBlendSpeed = 6f; // how fast the arm blends into/out of the holding pose
    private int itemHoldLayerIndex = -1;
    private bool isHoldingItem;      // persistent - true while an equipped item (e.g. flashlight) is active
    private float grabPulseTimer;    // counts down during a one-off "reaching to grab" pulse
    private Transform itemHolder;    // holds the flashlight/key visuals

    [Header("Held Item Viewmodel Offset")]
    [Tooltip("Fixed position (relative to the camera) the held item sits at, like a standard FPS viewmodel.")]
    public float itemHolderForwardOffset = 0.6f;
    public float itemHolderRightOffset = 0.15f;
    public float itemHolderUpOffset = -0.05f;

    [Header("Arm Stabilizer")]
    public float armStabilizeBlendSpeed = 8f; // how fast the arm locks/releases from the stable pose
    private int armStabilizeLayerIndex = -1;  // Override layer masked to RightArm only

    [Header("Footstep Audio")]
    public AudioSource footstepSource;
    public AudioClip[] footstepClips;
    [Tooltip("Played instead of footstepClips while actually sprinting (isActuallyRunning) - falls back to footstepClips if left empty.")]
    public AudioClip[] runFootstepClips;
    [Range(0f, 1f)] public float footstepVolume = 0.55f;
    public Vector2 footstepPitchRange = new Vector2(0.93f, 1.07f);
    [Tooltip("Pitch range for runFootstepClips specifically - the source recording already sounds like a brisk sprint, so this defaults lower than footstepPitchRange instead of reusing it, which made running audio sound sped-up/chipmunked.")]
    public Vector2 runFootstepPitchRange = new Vector2(0.78f, 0.88f);
    private bool footstepSourceIsRunClip;

    [Header("Jump & Land Audio")]
    public AudioSource jumpAudioSource;
    public AudioClip[] jumpClips;
    public AudioClip[] landClips;
    [Range(0f, 1f)] public float jumpVolume = 0.7f;
    [Tooltip("Fall speed (units/sec) below which landing is silent - so a small step-down doesn't play a full impact thud.")]
    public float minLandSpeedForSound = 1.5f;
    [Tooltip("Fall speed at which a landing plays at full volume.")]
    public float maxLandSpeedForFullVolume = 12f;

    [Header("Hiding")]
    [Tooltip("Set by a hiding spot (e.g. LockerHidingSpot) while the player is concealed. Freezes movement, crouch, run, jump, footsteps, head bob and gravity - mouse look keeps working so hiding still lets you peek around.")]
    [HideInInspector] public bool movementLocked = false;

    // Called by PlayerHealth (or anything else) to punch the camera on impact
    public void Shake(float duration, float magnitude)
    {
        shakeDuration = duration;
        shakeTimer = duration;
        shakeMagnitude = magnitude;
    }

    // Called by FlashlightController (or any equip logic) while an item is actively held/in-use
    public void SetHoldingItem(bool holding)
    {
        isHoldingItem = holding;
    }

    // Called on pickup (evidence, notes, keys, anything grabbed from the world) for a brief reach gesture
    public void PlayGrabPulse(float duration = 0.4f)
    {
        grabPulseTimer = duration;
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        vaultScript = GetComponent<PlayerVaultController>(); // Link the scripts
        windowClimb = GetComponent<PlayerWindowClimb>();

        standCenter = controller.center;
        // The player also carries a plain CapsuleCollider the same size as the controller. It has to
        // shrink with the crouch too, or the head stays 2 m tall to everything else that tests it.
        bodyCapsule = GetComponent<CapsuleCollider>();
        if (bodyCapsule != null)
        {
            bodyCapsuleStandHeight = bodyCapsule.height;
            bodyCapsuleStandCenter = bodyCapsule.center;
        }

        // Lives on the CharacterModel wrapper, not this object. GetComponentInChildren<Animator>()
        // is ambiguous here - other props (e.g. the flashlight) also carry an Animator, and it can
        // match one of those instead, silently leaving the body's Animator never driven.
        Transform modelWrapper = transform.Find("CharacterModel");
        if (modelWrapper != null) bodyAnimator = modelWrapper.GetComponent<Animator>();

        if (bodyAnimator != null) itemHoldLayerIndex = bodyAnimator.GetLayerIndex("ItemHold");
        if (bodyAnimator != null) armStabilizeLayerIndex = bodyAnimator.GetLayerIndex("ArmStabilizer");

        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "ItemHolder") { itemHolder = t; break; }
        }


        if (playerCamera != null)
            defaultYPos = playerCamera.transform.localPosition.y;

        currentEnergy = maxEnergy;
        if (energyBarFill != null) energyBarFill.fillAmount = 1f;
        if (energyBarTrailFill != null) energyBarTrailFill.fillAmount = 1f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Lights freshly instantiated by a runtime scene reload (e.g. after clicking
        // Restart) can end up not properly registered with the render pipeline - the
        // scene renders solid black even though the camera, gameplay, and UI all work
        // fine. Toggling every light off/on one frame after Start forces URP to
        // re-register them. (The camera itself is left alone - disabling it, even for
        // one frame, makes the Game view show "No cameras rendering" instead.)
        StartCoroutine(RefreshLightsNextFrame());
    }

    IEnumerator RefreshLightsNextFrame()
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights) l.enabled = false;

        yield return null;

        foreach (var l in lights) if (l != null) l.enabled = true;
    }

    void Update()
    {
        // --- THE FIX ---
        // If Mark is currently vaulting, we stop the movement script entirely
        // to prevent the "Inactive Controller" error and movement glitches.
        if (vaultScript != null && vaultScript.isVaulting)
        {
            return;
        }

        // Same for a window climb: it drives the transform with the controller disabled.
        if (windowClimb != null && windowClimb.IsClimbing) return;

        // No walking or looking around while the opening briefing is on screen.
        if (IntroSequence.IsPlaying) return;

        // While hidden in a locker/hiding spot: mouse look still works (so you can peek through the
        // slats), but everything else - movement, crouch, run, jump, footsteps, head bob, gravity -
        // is frozen. The CharacterController is disabled for the same duration by the hiding spot,
        // so nothing below this point may call controller.Move() while this is true.
        if (movementLocked)
        {
            ApplyMouseLook();
            return;
        }

        // 1. GROUND CHECK
        float fallSpeedBeforeLanding = -velocity.y;
        bool wasGrounded = isGrounded;
        isGrounded = controller.isGrounded;

        if (isGrounded && !wasGrounded)
        {
            PlayLandSound(fallSpeedBeforeLanding);
        }

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // 2. MOUSE LOOK (Horizontal & Vertical)
        ApplyMouseLook();

        // 3. CROUCH LOGIC
        // Letting go of Ctrl under a table or a low beam keeps the player down until there is room to
        // stand - growing the capsule into the ceiling used to wedge the player inside it.
        bool wantsCrouch = GameKeys.Held(GameKeys.Action.Crouch);
        bool isCrouching = wantsCrouch || (controller.height < standHeight - 0.01f && !HasHeadroomToStand());
        float targetHeight = isCrouching ? crouchHeight : standHeight;
        float newHeight = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchSmoothTime);
        if (Mathf.Abs(newHeight - targetHeight) < 0.002f) newHeight = targetHeight;
        SetBodyHeight(newHeight);

        // Camera Y base tracks the same smoothed height the collider just moved to, so the player's
        // actual view lowers when crouching instead of only the collider shrinking underneath them -
        // previously the camera stayed pinned at defaultYPos regardless of crouch state, so crouching
        // changed speed/collider size but was invisible to the player looking through the camera.
        float crouchProgress = Mathf.InverseLerp(standHeight, crouchHeight, controller.height);
        float cameraYBase = Mathf.Lerp(defaultYPos, defaultYPos - (standHeight - crouchHeight), crouchProgress);

        // 4. MOVEMENT SPEED (Walk, Run, or Crouch)
        bool wantsToRun = !isCrouching && GameKeys.Held(GameKeys.Action.Sprint);
        bool isActuallyRunning = wantsToRun && currentEnergy > 0f;

        float currentSpeed = walkSpeed;
        if (isCrouching) currentSpeed = crouchSpeed;
        else if (isActuallyRunning) currentSpeed = runSpeed;
        else if (wantsToRun) currentSpeed = exhaustedRunSpeed; // out of energy - sprint is slowed, not full speed

        if (isActuallyRunning)
        {
            currentEnergy = Mathf.Max(0f, currentEnergy - energyDrainRate * Time.deltaTime);
        }
        else if (!wantsToRun)
        {
            currentEnergy = Mathf.Min(maxEnergy, currentEnergy + energyRegenRate * Time.deltaTime);
        }

        UpdateEnergyBar();

        // Rebindable movement keys (Settings > Keybinds), eased in/out the way Input.GetAxis used to -
        // snapping to zero first when a direction reverses, so changing direction never drifts.
        Vector2 rawMove = GameKeys.MoveRaw();
        if (rawMove.x != 0f && Mathf.Sign(rawMove.x) != Mathf.Sign(moveInput.x)) moveInput.x = 0f;
        if (rawMove.y != 0f && Mathf.Sign(rawMove.y) != Mathf.Sign(moveInput.y)) moveInput.y = 0f;
        moveInput = Vector2.MoveTowards(moveInput, rawMove, moveInputResponse * Time.deltaTime);
        float x = moveInput.x;
        float z = moveInput.y;

        Vector3 move = transform.right * x + transform.forward * z;

        // Execute Move 1 (Horizontal)
        controller.Move(move * currentSpeed * Time.deltaTime);

        // The CharacterController's actual resulting velocity, not the raw input vector - using input
        // alone kept both the animator and footsteps thinking the player was moving at full speed while
        // walking into a wall or closed door (input held, but controller.Move() couldn't displace them).
        float actualHorizontalSpeed = new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;

        // Drive the visible character's Idle/Walk/Run blend - normalized against runSpeed so
        // Walk sits around ~0.6 and full sprint reaches 1.0, matching the controller's thresholds.
        if (bodyAnimator != null)
        {
            float normalizedSpeed = (isGrounded && actualHorizontalSpeed > 0.1f) ? currentSpeed / runSpeed : 0f;
            bodyAnimator.SetFloat("Speed", normalizedSpeed);
        }

        // Blend the "holding an item" arm pose in/out - stays on while an item is actively
        // equipped (e.g. flashlight on), and also flashes briefly on pickup as a reach/grab gesture.
        if (bodyAnimator != null && itemHoldLayerIndex >= 0)
        {
            if (grabPulseTimer > 0f) grabPulseTimer -= Time.deltaTime;

            float targetWeight = (isHoldingItem || grabPulseTimer > 0f) ? 1f : 0f;
            float currentWeight = bodyAnimator.GetLayerWeight(itemHoldLayerIndex);
            float newWeight = Mathf.MoveTowards(currentWeight, targetWeight, holdBlendSpeed * Time.deltaTime);
            bodyAnimator.SetLayerWeight(itemHoldLayerIndex, newWeight);
        }

        // Keep the held item (flashlight/key) locked into its stable forward-facing pose at all
        // times, regardless of movement or hold state - the visible arm/body mesh itself is no
        // longer rendered in first person, so there's no "looks robotic" tradeoff to weigh against
        // this; leaving it conditional just meant the item pointed the wrong way whenever standing
        // still with nothing actively toggled on.
        if (bodyAnimator != null && armStabilizeLayerIndex >= 0)
        {
            float currentArmWeight = bodyAnimator.GetLayerWeight(armStabilizeLayerIndex);
            float newArmWeight = Mathf.MoveTowards(currentArmWeight, 1f, armStabilizeBlendSpeed * Time.deltaTime);
            bodyAnimator.SetLayerWeight(armStabilizeLayerIndex, newArmWeight);
        }

        // 5. JUMPING - climbing through an open window, then vaulting, take priority over a plain jump
        if (GameKeys.Down(GameKeys.Action.Jump))
        {
            // Like TryVault(), a climb disables the controller at once, so skip this frame's Move() below.
            if (windowClimb != null && windowClimb.TryClimb()) return;
            bool vaulted = vaultScript != null && vaultScript.TryVault();
            // TryVault() disables the CharacterController the instant it starts a vault (so the vault
            // coroutine can move the transform directly) - continuing on into this same frame's gravity
            // controller.Move() below would call Move() on that now-disabled controller and throw.
            if (vaulted) return;
            if (isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                PlayRandomOneShot(jumpAudioSource, jumpClips, jumpVolume, 0.96f, 1.04f);
            }
        }

        // 6. HEAD BOB
        // Tracks its own offset (not the transform) so camera shake below can never bleed
        // into it and accumulate - that drift was letting the camera visually punch through
        // nearby walls after repeated hits.
        // Gated on the same actualHorizontalSpeed computed above (the CharacterController's real
        // resulting velocity) - using raw input alone kept the bob playing at full strength while
        // walking into a closed door or wall, which read as the camera continuously vibrating in place.
        if (isGrounded && actualHorizontalSpeed > 0.1f)
        {
            float speedMultiplier = isCrouching ? 0.5f : (GameKeys.Held(GameKeys.Action.Sprint) ? 1.5f : 1f);
            timer += Time.deltaTime * walkBobSpeed * speedMultiplier;

            bobOffset = new Vector3(0f, Mathf.Sin(timer) * walkBobAmount, 0f);

            // footstepClips is a multi-second continuous walking loop rather than a single footfall,
            // so it's driven as a loop tied to movement state instead of PlayOneShot-per-interval -
            // that used to fire a fresh overlapping copy of the whole clip every stepInterval, which
            // then had no way to be cancelled and kept audibly trailing long after the player stopped.
            // Uses runFootstepClips instead of footstepClips while actually sprinting, restarting the
            // loop on the exact frame the run state changes (not just when it stops) so breaking into
            // a sprint mid-stride swaps the loop immediately instead of waiting for the walking clip
            // to finish.
            bool useRunClips = isActuallyRunning && runFootstepClips != null && runFootstepClips.Length > 0;
            AudioClip[] activeFootstepClips = useRunClips ? runFootstepClips : footstepClips;
            Vector2 activePitchRange = useRunClips ? runFootstepPitchRange : footstepPitchRange;

            if (footstepSource != null && activeFootstepClips != null && activeFootstepClips.Length > 0)
            {
                if (!footstepSource.isPlaying || isActuallyRunning != footstepSourceIsRunClip)
                {
                    footstepSource.clip = activeFootstepClips[Random.Range(0, activeFootstepClips.Length)];
                    footstepSource.loop = true;
                    footstepSource.pitch = Random.Range(activePitchRange.x, activePitchRange.y);
                    footstepSource.Play();
                    footstepSourceIsRunClip = isActuallyRunning;
                }
                footstepSource.volume = footstepVolume * SfxVolume.Multiplier;
            }
        }
        else
        {
            timer = 0;
            bobOffset = Vector3.Lerp(bobOffset, Vector3.zero, Time.deltaTime * walkBobSpeed);

            if (footstepSource != null && footstepSource.isPlaying) footstepSource.Stop();
        }

        // 6b. CAMERA SHAKE (decays over shakeDuration, recomputed fresh each frame)
        Vector3 shakeOffset = Vector3.zero;
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            float intensity = shakeMagnitude * (shakeTimer / shakeDuration);
            shakeOffset = Random.insideUnitSphere * intensity;
            shakeOffset.z = 0f;
        }

        playerCamera.transform.localPosition = new Vector3(0f, cameraYBase, 0f) + bobOffset + shakeOffset;

        // 7. APPLY GRAVITY
        velocity.y += gravity * Time.deltaTime;
        
        // Execute Move 2 (Vertical/Gravity)
        controller.Move(velocity * Time.deltaTime);
    }

    // Resizes the collider from the top down. Changing only the height (as this used to) shrank the
    // capsule around its middle: the feet lifted half the difference off the floor and the player
    // dropped, so on top of the camera's own crouch offset the view sank to knee height - and standing
    // back up grew the capsule down into the floor.
    void SetBodyHeight(float height)
    {
        // Only touch the colliders when the height actually changes - rewriting them every frame
        // rebuilds the physics shape each time, which wakes any Rigidbody on the player.
        if (Mathf.Approximately(controller.height, height)) return;
        float lost = standHeight - height;
        controller.height = height;
        controller.center = new Vector3(standCenter.x, standCenter.y - lost * 0.5f, standCenter.z);
        if (bodyCapsule != null)
        {
            bodyCapsule.height = Mathf.Max(bodyCapsule.radius * 2f, bodyCapsuleStandHeight - lost);
            bodyCapsule.center = new Vector3(bodyCapsuleStandCenter.x, bodyCapsuleStandCenter.y - lost * 0.5f, bodyCapsuleStandCenter.z);
        }
    }

    // Sweeps the head from where it is now up to standing height; anything in the way (other than the
    // player's own colliders) means there is no room to stand yet.
    bool HasHeadroomToStand()
    {
        Vector3 scale = transform.lossyScale;
        float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float scaleY = Mathf.Abs(scale.y);
        Vector3 up = transform.up;
        Vector3 feet = transform.TransformPoint(controller.center) - up * (controller.height * 0.5f * scaleY);
        Vector3 headNow = feet + up * Mathf.Max(radius, controller.height * scaleY - radius);
        float rise = (standHeight - controller.height) * scaleY + controller.skinWidth;
        if (rise <= 0f) return true;

        foreach (var hit in Physics.SphereCastAll(headNow, radius * 0.95f, up, rise, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            return false;
        }
        return true;
    }

    // Called by a hiding spot on entry. movementLocked skips the per-frame camera placement below, so
    // without this the camera stays wherever it was when the player stepped in - down at crouch
    // height, mid head-bob, or pitched at the floor - instead of level with the locker's vent slits.
    public void ResetViewForHiding()
    {
        verticalRotation = 0f;
        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = new Vector3(0f, defaultYPos, 0f);
            playerCamera.transform.localRotation = Quaternion.identity;
        }
    }

    void ApplyMouseLook()
    {
        // Sensitivity and invert come from Settings > Controls (GameSettings), not the inspector field.
        float sensitivity = GameSettings.MouseSensitivity;
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity * (GameSettings.InvertY ? -1f : 1f);

        transform.Rotate(Vector3.up * mouseX);

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -80f, 80f);
        if (playerCamera != null)
            playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    // Runs after Mecanim has applied the frame's pose. Both position AND rotation are locked to a
    // fixed camera-relative spot, like a standard FPS viewmodel - this used to only lock rotation,
    // leaving position riding the animated hand bone for a bit of natural sway. That was fine while
    // the arm was visible, but once the arm/body mesh was excluded from rendering entirely (nothing
    // left to see the "sway" against), the bone's raw position had no guarantee of sitting anywhere
    // near the camera's view - Idle/Walk/Run each pose the hidden arm differently, so the held item
    // could end up almost completely out of frame instead of visible in front of the player.
    void LateUpdate()
    {
        if (itemHolder != null && playerCamera != null)
        {
            itemHolder.position = playerCamera.transform.position
                + playerCamera.transform.forward * itemHolderForwardOffset
                + playerCamera.transform.right * itemHolderRightOffset
                + playerCamera.transform.up * itemHolderUpOffset;
            itemHolder.rotation = Quaternion.LookRotation(playerCamera.transform.forward, playerCamera.transform.up);
        }
    }

    void PlayLandSound(float fallSpeed)
    {
        if (fallSpeed < minLandSpeedForSound) return;

        float volume = Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(minLandSpeedForSound, maxLandSpeedForFullVolume, fallSpeed)) * jumpVolume;
        PlayRandomOneShot(jumpAudioSource, landClips, volume, 0.94f, 1.02f);
    }

    // Shared by footsteps/jump/land - randomizes pitch per play so a single repeated clip doesn't
    // read as an obviously looping sample.
    void PlayRandomOneShot(AudioSource source, AudioClip[] clips, float volume, float pitchMin, float pitchMax)
    {
        if (source == null || clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        source.pitch = Random.Range(pitchMin, pitchMax);
        source.PlayOneShot(clip, volume * SfxVolume.Multiplier);
    }

    void UpdateEnergyBar()
    {
        float targetFill = currentEnergy / maxEnergy;

        if (energyBarFill != null)
        {
            energyBarFill.fillAmount = Mathf.MoveTowards(energyBarFill.fillAmount, targetFill, energyBarFillSpeed * Time.deltaTime);
        }

        if (energyBarTrailFill != null)
        {
            energyBarTrailFill.fillAmount = Mathf.MoveTowards(energyBarTrailFill.fillAmount, targetFill, energyBarTrailSpeed * Time.deltaTime);
        }
    }
}