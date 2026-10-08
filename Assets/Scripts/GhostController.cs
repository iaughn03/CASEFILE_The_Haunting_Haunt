using UnityEngine;
using UnityEngine.AI;
using System.Collections;

// The asylum's stalking presence. A NavMeshAgent-driven state machine (Roam / Chase / Search /
// Attack) built around one idea: she should feel like a real threat that reacts to what the player
// does - hides from a flashlight beam, loses a chase if you break line of sight for good, but never
// gives up a nearby vaulted obstacle as a free escape route.
public class GhostController : MonoBehaviour
{
    private enum State { Roaming, Chasing, Searching, Attacking, Stalking }

    [Header("Detection")]
    public Transform player;
    public float detectionRange = 15f;
    public LayerMask obstacleMask;
    [Range(0f, 360f)] public float fieldOfViewAngle = 110f;
    [Range(0f, 1f)] public float peripheralDetectionChance = 0.15f;
    public float peripheralRange = 5f;
    public float closeRangeAlwaysNotice = 2.8f;
    public float loseSightGracePeriod = 0.6f;

    [Header("Roaming")]
    public float roamRadius = 20f;
    public float roamWaitTime = 3f;
    public float roamWaitTimeJitter = 1.5f;

    [Header("Searching")]
    public float searchDuration = 4f;
    public float searchTurnSpeed = 80f;

    [Header("Movement Speeds")]
    public float roamSpeed = 1.5f;
    public float chaseSpeed = 7.6f;
    [Range(0f, 0.5f)] public float speedJitter = 0.08f;

    [Header("Flashlight Weakness")]
    public FlashlightController flashlight;
    [Range(0f, 1f)] public float flashlightSlowMultiplier = 0.4f;
    [Tooltip("Height above her feet the beam check aims at. transform.position sits on the floor, so aiming there measures the angle down to her feet rather than at her body - at close range that falls outside the cone even while she's plainly lit up.")]
    public float litAimHeight = 1.4f;

    [Header("Flashlight Risk - being lit up also reveals you to her")]
    public float litDetectionRangeBonus = 6f;
    [Range(0f, 180f)] public float litFovBonus = 40f;

    [Header("Glimpse - rare distant sighting that doesn't trigger a chase")]
    public bool glimpseEnabled = true;
    public float glimpseMinDistance = 10f;
    public float glimpseMaxDistance = 25f;
    [Range(0f, 1f)] public float glimpseChance = 0.35f;
    public float glimpseCheckInterval = 1f;
    public float glimpseDuration = 2.2f;
    public float glimpseCooldownMin = 60f;
    public float glimpseCooldownMax = 120f;
    public AudioClip[] glimpseClips;

    [Header("Attack")]
    public float attackRange = 2.3f;
    [Tooltip("Extra reach on the damage check only, so a player who drifts back half a step during the windup isn't cheated out of a hit that visually connected. Distances here are pivot-to-pivot: she's 1.5x scale with a 1.8m-wide collider and the player's is 1.2m wide, so their bodies already touch at ~1.5m - every 0.1 added here is another 0.1m of visible daylight between her swing and the player when the hit lands.")]
    public float attackReachTolerance = 1.15f;
    [Tooltip("The swing can only connect during this window after the windup. It used to stay open for the whole attackCooldown (~1.15s), which let a stationary swipe land on someone who had already walked well clear of it and then wandered back.")]
    public float attackHitWindow = 0.45f;
    [Tooltip("Total arc in front of her the swing can reach, in degrees. Without this the damage check was pure distance, so she could hit a player standing directly behind her.")]
    [Range(30f, 360f)] public float attackArcAngle = 150f;
    [Tooltip("How far above her pivot the player's pivot can be and still be hit. The player's pivot sits 1 to 1.25m above whatever they stand on, so the default covers someone up on a table, bed or crate (Game1's tables are ~1.2m: 2.4 measured) but not someone on the floor above (3.75m+ higher).")]
    public float attackMaxHeightAbove = 3.0f;
    [Tooltip("The player's usual pivot height over her feet when both stand on the same floor. Vertical offset beyond this (standing up on furniture) is ignored by the attack range, so climbing onto a table is no longer a safe spot.")]
    public float playerStandingHeight = 1.0f;
    public float attackDamage = 15f;
    public float attackWindup = 0.3f;
    [Tooltip("Also gates re-attacking while still in range - kept long enough that attackWindup + attackCooldown covers the full Attack clip at its Animator playback speed (2x, so ~1.32s effective), otherwise the Any State->Attack transition (no exit time) snaps the swing back to frame 0 mid-animation every cycle instead of letting it finish. Raising the clip's own playback speed (rather than just stretching this cooldown) is what keeps hits landing at a snappy pace without cutting the animation.")]
    public float attackCooldown = 1.15f;
    public AudioClip[] attackClips;
    [Tooltip("Plays instead of attackClips on the finishing blow - falls back to attackClips if left empty.")]
    public AudioClip[] killClips;
    [Range(0f, 1f)] [Tooltip("Separate volume multiplier for attack/kill clips specifically, on top of the master SFX slider - the point-blank scream was playing at full, unattenuated blast.")]
    public float attackVolume = 0.65f;

    [Header("Obstacle Vaulting - crosses the same low obstacles the player can jump, so vaulting one\n     never buys a clean escape")]
    public LayerMask vaultObstacleLayer;
    public float vaultRayDistance = 1.6f;
    public float vaultLandingOffset = 2.2f;
    public float vaultDuration = 0.5f;
    public float vaultArcHeight = 1.1f;
    public string vaultAnimTrigger = "Vault";

    [Header("Sound")]
    public AudioSource loopedSource;
    public AudioSource oneShotSource;
    public AudioClip movementLoopClip;
    public AudioClip[] chaseStartClips;
    public AudioClip[] loseTrackClips;
    public AudioClip[] ambientCues;
    public float ambientCueMinInterval = 6f;
    public float ambientCueMaxInterval = 16f;

    [Header("Doors - she forces open any closed door in her way, unless it's locked")]
    public float doorOpenRange = 1.8f;
    public LayerMask doorLayer;
    private float nextDoorCheckTime;

    [Header("Physical Safety")]
    [Tooltip("Hard floor on how close she's ever allowed to physically be, independent of state/timing - her collider is a trigger, so nothing else stops her occupying the player's space. Must stay clearly below the chase/attack standoff distance (stoppingDistance derives from attackRange) - equal or close values make EnforceMinDistance's per-frame Warp fight the NavMeshAgent's own deceleration right at that boundary, which reads as her running in place in slow motion instead of closing in cleanly.")]
    public float minPhysicalDistance = 1f;

    [Header("Stalking - after losing a chase she may not fully give up, following at a distance\n     instead. Freezes solid the instant she's looked at and only creeps closer while unwatched.")]
    [Range(0f, 1f)] public float stalkChance = 0.5f;
    public float stalkDuration = 14f;
    public float stalkSpeed = 1.1f;
    [Tooltip("Won't creep closer than this while unwatched - getting any closer requires her own normal detection (CanSeePlayer) to trigger a real chase.")]
    public float stalkApproachDistance = 3f;
    [Tooltip("Falls back to ambientCues if left empty.")]
    public AudioClip[] stalkClips;
    public float stalkClipMinInterval = 4f;
    public float stalkClipMaxInterval = 10f;

    [Header("Proximity Dread - her breathing gets louder/faster the closer she is, whether or not\n     she's currently visible. Always maxed out while actively chasing.")]
    public AudioSource heartbeatSource;
    public float heartbeatStartDistance = 14f;
    [Range(0f, 1f)] public float heartbeatMinVolume = 0f;
    [Range(0f, 1f)] public float heartbeatMaxVolume = 0.85f;
    public float heartbeatMinPitch = 0.85f;
    public float heartbeatMaxPitch = 1.3f;
    public float heartbeatSmoothing = 2.5f;

    [Header("Escalation - each time she loses the player she gets a little faster and a little more\n     resistant to the flashlight slow on subsequent chases, so she can't be cheesed forever.")]
    public int maxEscalationStacks = 4;
    public float chaseSpeedPerStack = 0.5f;
    [Range(0f, 0.3f)] public float flashlightResistancePerStack = 0.06f;

    [Header("Flashlight Interference - close range during an active chase drains the battery faster,\n     coaxing it toward its own flicker threshold right when it matters most.")]
    public float interferenceRange = 4f;
    public float chaseBatteryDrainBonus = 4f;

    private bool initialized;
    private NavMeshAgent agent;
    private Animator anim;
    private State state = State.Roaming;
    private float waitTimer;
    private float currentRoamWait;
    private float searchTimer;
    private Vector3 lastKnownPlayerPosition;
    private float nextAmbientCueTime;
    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;
    private PlayerHideState playerHideState;
    private bool debugLastLoggedHidden; // TEMP diagnostic, remove with other [LockerDebug] lines
    private float attackTimer;
    private bool attackDamageApplied;
    private float lastSeenTimer;
    private float currentChaseSpeed; // the jittered speed EnterChase() rolled for this chase, reapplied each frame so the flashlight slow multiplier below has a stable base instead of compounding onto itself
    private float chaseStallTimer; // how long she's been trying to chase without actually covering ground - see UpdateChaseStallWatchdog()
    private float roamStallTimer; // same idea as chaseStallTimer, but for an unreachable roam destination - see RoamBehavior()
    private float searchStallTimer; // same idea, but for an unreachable lastKnownPlayerPosition - see SearchBehavior()
    private Vector3 lastChaseDestination;
    private float nextChaseRepathTime;
    private Camera playerCamera;
    private bool isGlimpsing;
    private float glimpseTimer;
    private float nextGlimpseCheckTime;
    private float glimpseCooldownUntil;
    private bool hasKilledPlayer;
    private bool isVaulting;
    private float loopedSourceBaseVolume = 1f;
    private float stalkTimer;
    private float nextStalkClipTime;
    private int escalationStacks;
    private readonly RaycastHit[] litProbeHits = new RaycastHit[8]; // reused by IsLitByFlashlight() so the per-frame beam check doesn't allocate

    // Lets external systems (ambient/proximity layers) back off while she's an active threat instead
    // of stacking on top of it and diluting the signal.
    public bool IsChasing => state == State.Chasing || state == State.Attacking;

    // The opening briefing and the start tips are for reading: while either is up she keeps
    // wandering but can't spot, chase, stalk, glimpse at or attack the player.
    public static bool StartGraceActive => IntroSequence.IsPlaying || StartReminders.TipsShowing;

    void Start() => EnsureInitialized();

    void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();

        if (flashlight == null)
            flashlight = FindFirstObjectByType<FlashlightController>(FindObjectsInactive.Include);

        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
            playerMovement = player.GetComponent<PlayerMovement>();
            if (playerMovement != null) playerCamera = playerMovement.playerCamera;
            playerHideState = player.GetComponent<PlayerHideState>();
        }

        glimpseCooldownUntil = Time.time + Random.Range(glimpseCooldownMin, glimpseCooldownMax);

        if (loopedSource != null && movementLoopClip != null)
        {
            loopedSourceBaseVolume = loopedSource.volume;
            loopedSource.clip = movementLoopClip;
            loopedSource.loop = true;
            loopedSource.Play();
        }

        currentRoamWait = roamWaitTime;
        ScheduleNextAmbientCue();
        SetNewRoamDestination();
    }

    void Update()
    {
        EnsureInitialized();

        // PlayerHideState is added lazily by LockerHidingSpot the first time the player enters a
        // hiding spot's trigger - if that hasn't happened yet when EnsureInitialized() ran (e.g. she
        // spawned and started roaming before the player ever walked near a locker), the one-time
        // GetComponent in EnsureInitialized() permanently missed it and every hidden check below
        // would silently no-op forever. Keep retrying until it's actually found.
        if (playerHideState == null && player != null)
            playerHideState = player.GetComponent<PlayerHideState>();

        if (hasKilledPlayer) return;
        // Mid-vault, VaultOverObstacle() owns transform.position directly and the agent is disabled -
        // every navmesh-dependent call below is unsafe to touch until it hands control back.
        if (isVaulting) return;

        // Safety net: if she's ever ended up enabled but off the navmesh (a bad vault landing, a
        // EnforceMinDistance push into solid geometry, anything), every navmesh call below either
        // throws or silently no-ops, which used to leave her frozen in place for good with no way
        // back. Try to recover onto the nearest valid point immediately; if that's not possible this
        // frame, skip the rest rather than spamming NavMeshAgent exceptions.
        if (agent != null && agent.enabled && !agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit recoveryHit, 6f, NavMesh.AllAreas))
                agent.Warp(recoveryHit.position);
            else
                return;
        }

        UpdateAmbientCues();
        TryOpenDoorsAhead();
        if (player == null) return;

        EnforceMinDistance();
        UpdateHeartbeat();

        bool litByFlashlight = IsLitByFlashlight();
        bool seesPlayer = CanSeePlayer(litByFlashlight);

        if (StartGraceActive)
        {
            seesPlayer = false;
            if (isGlimpsing)
            {
                isGlimpsing = false;
                agent.isStopped = false;
                agent.updateRotation = true;
            }
            if (state != State.Roaming)
            {
                if (state == State.Attacking && anim != null) anim.ResetTrigger("Attack");
                EnterRoam();
            }
        }

        // A successful hide should read as her actually giving up, not as a multi-second
        // investigate-the-doorway routine - drop straight into Roaming instead of running the
        // normal lose-sight grace period into Searching's scan-in-place-then-maybe-Stalk pipeline,
        // which left her lingering right outside the locker for several seconds either way.
        // Attacking is included: AttackBehavior() re-enters another swing after each cooldown whenever
        // she's still in range, with no hidden check, so hiding next to her used to leave her swinging
        // at the locker forever (the hit itself was already blocked, so it never did damage).
        bool hiddenForRoamCheck = playerHideState != null && playerHideState.IsHiding;
        if (hiddenForRoamCheck && (state == State.Chasing || state == State.Searching || state == State.Stalking || state == State.Attacking))
        {
            if (state == State.Attacking && anim != null) anim.ResetTrigger("Attack");
            EnterRoam();
        }

        switch (state)
        {
            case State.Roaming:
                if (isGlimpsing)
                {
                    HoldGlimpse();
                    if (seesPlayer) { isGlimpsing = false; EnterChase(); }
                    break;
                }
                RoamBehavior();
                if (seesPlayer) EnterChase();
                else if (!StartGraceActive) TryTriggerGlimpse();
                break;

            case State.Chasing:
                // A well-timed hide beats a chase even at point-blank range - without this, closing
                // the locker door one frame too late still let proximity alone trigger an attack.
                bool hiddenNow = playerHideState != null && playerHideState.IsHiding;
                if (hiddenNow) Debug.Log("[LockerDebug] Chase attack-range check bypassed - player hidden.");
                if (!hiddenNow && AttackDistance() <= attackRange)
                {
                    Debug.Log("[LockerDebug] EnterAttack() firing! hiddenNow=" + hiddenNow + " dist=" + AttackDistance());
                    EnterAttack();
                    break;
                }
                // Reset to the chase's base speed every frame, same as Roaming/Searching do - otherwise
                // the flashlight slow multiplier below compounds onto an already-slowed value each frame
                // it stays lit, converging to a near-total freeze within a few frames instead of a flat 0.4x.
                agent.speed = currentChaseSpeed;
                RepathTowardPlayer();
                ApplyChaseInterference();
                UpdateChaseStallWatchdog();

                if (seesPlayer)
                {
                    lastKnownPlayerPosition = GetPlayerNavTarget();
                    lastSeenTimer = 0f;
                }
                else
                {
                    lastSeenTimer += Time.deltaTime;
                    if (lastSeenTimer >= loseSightGracePeriod) EnterSearch();
                }
                break;

            case State.Searching:
                SearchBehavior(seesPlayer);
                break;

            case State.Attacking:
                AttackBehavior();
                break;

            case State.Stalking:
                StalkBehavior(seesPlayer);
                break;
        }

        // Starting a vault disables the agent synchronously - bail out immediately so nothing below
        // touches it again this frame.
        if (TryGhostVault()) return;

        if (litByFlashlight)
        {
            // Resistance climbs with each escalation stack, capped at 1 (no slow at all) - the
            // flashlight stays a real tool but stops being a permanent "hold to neutralize her" button.
            float effectiveSlow = Mathf.Min(1f, flashlightSlowMultiplier + escalationStacks * flashlightResistancePerStack);
            agent.speed *= effectiveSlow;

            // Lowering agent.speed alone only changes her target: the agent eases down to it at its
            // acceleration (10 m/s^2), so from a full chase she kept closing at ~9 m/s for the first
            // half-second of being lit and was on the player before the slow ever showed. Cap her actual
            // velocity the moment the beam lands so it reads as her being held back by the light.
            if (agent.enabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude > agent.speed * agent.speed)
                agent.velocity = agent.velocity.normalized * agent.speed;
        }

        UpdateAudioAndAnimation();
    }

    bool IsLitByFlashlight()
    {
        if (flashlight == null || !flashlight.isOn || flashlight.lightSource == null) return false;
        if (!flashlight.gameObject.activeInHierarchy || !flashlight.lightSource.activeInHierarchy) return false;

        Light lightComp = flashlight.lightSource.GetComponent<Light>();
        if (lightComp == null) return false;

        Transform beam = flashlight.lightSource.transform;
        Vector3 toGhost = (transform.position + Vector3.up * litAimHeight) - beam.position;
        float distance = toGhost.magnitude;
        if (distance > lightComp.range) return false;

        float angle = Vector3.Angle(beam.forward, toGhost);
        if (angle > lightComp.spotAngle * 0.5f) return false;

        // QueryTriggerInteraction.Ignore, and skipping her own colliders, is what makes this work at
        // all: her collider is a trigger sitting on the same layer obstacleMask covers, so the ray hit
        // *her* on the way to the aim point and reported her as occluded every single frame - the slow
        // below never applied once. The level's scare/evidence trigger volumes share that layer too and
        // would shadow her just as wrongly.
        int count = Physics.RaycastNonAlloc(beam.position, toGhost.normalized, litProbeHits,
            distance - 0.1f, obstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (litProbeHits[i].collider == null) continue;
            if (litProbeHits[i].transform.IsChildOf(transform)) continue;
            return false;
        }

        return true;
    }

    void RoamBehavior()
    {
        agent.speed = roamSpeed;
        // Same class of bug as EnterChase()/EnterSearch(): Roaming can be entered straight out of
        // Stalking (timeout) while agent.isStopped happens to be true from StalkBehavior()'s per-frame
        // "frozen while watched" toggle - left uncleared, she'd stand roaming-in-place forever.
        agent.isStopped = false;

        // SetNewRoamDestination() only checks NavMesh.SamplePosition succeeding near the random point -
        // that doesn't guarantee the point is actually reachable from here (a navmesh seam/gap between
        // her and it is enough to strand her). Without this she'd just stand at her current spot forever,
        // waiting on a remainingDistance that can never shrink - reads as her being permanently "stuck"
        // in an ordinary open area with nothing obviously blocking her, not mid-chase, just idle-frozen.
        if (AgentStuckEnRoute())
        {
            roamStallTimer += Time.deltaTime;
            if (roamStallTimer >= 1.5f)
            {
                roamStallTimer = 0f;
                SetNewRoamDestination(); // abandon the unreachable point rather than standing here forever
            }
        }
        else
        {
            roamStallTimer = 0f;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= currentRoamWait)
            {
                SetNewRoamDestination();
                waitTimer = 0f;
                currentRoamWait = roamWaitTime + Random.Range(-roamWaitTimeJitter, roamWaitTimeJitter);
            }
        }
    }

    void SearchBehavior(bool seesPlayer)
    {
        if (seesPlayer)
        {
            EnterChase();
            return;
        }

        agent.speed = roamSpeed;

        // Same unreachable-destination failure as RoamBehavior()/UpdateChaseStallWatchdog(), but here
        // it's worse: reachedLastKnownPosition (and therefore searchTimer, and therefore ever leaving
        // Search) never even starts if lastKnownPlayerPosition itself turns out to be unreachable - she'd
        // hang in Search forever rather than eventually timing out into Stalk/Roam like normal. Force the
        // destination back to where she's actually standing so reachedLastKnownPosition trips next frame
        // and the ordinary scan-turn/timeout flow below takes over instead of stalling ahead of it.
        if (AgentStuckEnRoute())
        {
            searchStallTimer += Time.deltaTime;
            if (searchStallTimer >= 1.5f)
            {
                searchStallTimer = 0f;
                agent.SetDestination(transform.position);
            }
        }
        else
        {
            searchStallTimer = 0f;
        }

        bool reachedLastKnownPosition = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance;

        if (reachedLastKnownPosition)
        {
            // Stationary and turning in place to scan the area - the agent has nowhere left to walk
            // to, so its own steering has no heading to offer; take rotation over manually instead of
            // leaving it fighting a spin it didn't request.
            agent.updateRotation = false;
            transform.Rotate(Vector3.up, searchTurnSpeed * Time.deltaTime);
            searchTimer += Time.deltaTime;

            if (searchTimer >= searchDuration)
            {
                agent.updateRotation = true; // was left false from the manual scan-turn above - without this the ghost slides along her roam path without turning to face it

                // She's slipping away from a chase she almost won - escalate for next time, and roll
                // whether she actually gives up or keeps following at a distance instead.
                escalationStacks = Mathf.Min(maxEscalationStacks, escalationStacks + 1);

                if (Random.value < stalkChance)
                {
                    EnterStalk();
                }
                else
                {
                    state = State.Roaming;
                    SetNewRoamDestination();
                }
            }
        }
    }

    float ChaseStoppingDistance => Mathf.Max(0.3f, attackRange - 0.5f);

    // Pivot-to-pivot reach used to start and repeat a swing. Plain 3D distance counted the height of
    // whatever the player climbed onto, so standing on a table put them out of range from right beside
    // it. Height up to the normal standing offset counts as before (same reach on flat floor); anything
    // above that is ignored, up to attackMaxHeightAbove, past which she can't reach at all.
    float AttackDistance()
    {
        Vector3 d = player.position - transform.position;
        if (d.y > attackMaxHeightAbove) return float.PositiveInfinity;
        float dy = Mathf.Min(Mathf.Abs(d.y), playerStandingHeight);
        d.y = 0f;
        return Mathf.Sqrt(d.sqrMagnitude + dy * dy);
    }

    // player.position sits at the CharacterController's pivot height, not its feet - handing that
    // Y straight to SetDestination() can push NavMesh's internal "closest point" search into a bad
    // spot in tight/cluttered rooms, producing a partial path that leaves her stuck a step from her
    // spawn instead of actually closing in. Snapping to the nearest real navmesh point first keeps
    // the destination on solid, walkable ground regardless of exactly where the player's pivot sits.
    Vector3 GetPlayerNavTarget()
    {
        if (NavMesh.SamplePosition(player.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            return hit.position;
        return player.position;
    }

    // SetDestination() kicks off an async path query. Calling it fresh every single Update() frame
    // (as this used to) never lets one of those queries finish before the next frame cancels and
    // replaces it - pathPending stays permanently true, she keeps serving whatever path last actually
    // completed (which she may have already fully walked), and the animator/velocity read as running
    // in place forever since no new path ever lands. Only actually re-request a path when the target
    // has moved meaningfully or a short throttle window has passed - frequent enough to track a moving
    // player, infrequent enough for each query to resolve.
    void RepathTowardPlayer()
    {
        Vector3 target = GetPlayerNavTarget();
        bool targetMoved = Vector3.Distance(target, lastChaseDestination) > 1f;
        bool throttleElapsed = Time.time >= nextChaseRepathTime;
        if (!targetMoved && !throttleElapsed) return;

        // Player up on a table/crate has no navmesh under them, so the target snaps to the floor beside
        // it. The normal chase standoff would then stop her ~1.8m short of that floor spot - out of
        // swing range of someone already a step away from it - and she'd stand there forever. Walk all
        // the way to the edge instead; the attack check takes over as soon as she's in reach.
        Vector3 offMesh = target - player.position;
        offMesh.y = 0f;
        agent.stoppingDistance = offMesh.sqrMagnitude > 0.25f ? 0.2f : ChaseStoppingDistance;

        agent.SetDestination(target);
        lastChaseDestination = target;
        nextChaseRepathTime = Time.time + 0.2f;
    }

    void SetNewRoamDestination()
    {
        agent.stoppingDistance = 0f;
        Vector3 randomDir = Random.insideUnitSphere * roamRadius;
        randomDir += transform.position;
        if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, roamRadius, 1))
        {
            agent.SetDestination(hit.position);
        }
    }

    bool CanSeePlayer(bool litByFlashlight)
    {
        // Concealed in a locker/hiding spot - undetectable regardless of distance, flashlight, or
        // line of sight. Checked first so nothing below (including the close-range/FOV bypasses)
        // can override it.
        bool debugHiddenNow = playerHideState != null && playerHideState.IsHiding;
        if (debugHiddenNow != debugLastLoggedHidden)
        {
            Debug.Log("[LockerDebug] GhostController hidden-state changed to: " + debugHiddenNow + " (playerHideState null? " + (playerHideState == null) + ")");
            debugLastLoggedHidden = debugHiddenNow;
        }
        if (debugHiddenNow) return false;

        float effectiveRange = litByFlashlight ? detectionRange + litDetectionRangeBonus : detectionRange;
        float effectiveFov = litByFlashlight ? Mathf.Min(360f, fieldOfViewAngle + litFovBonus) : fieldOfViewAngle;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer > effectiveRange) return false;

        Vector3 eyePos = transform.position + Vector3.up * 1.5f;
        Vector3 targetPos = player.position + Vector3.up * 1f;
        Vector3 directionToPlayer = (targetPos - eyePos).normalized;

        if (Physics.Raycast(eyePos, directionToPlayer, out RaycastHit hit, distanceToPlayer, obstacleMask))
        {
            if (!hit.collider.CompareTag("Player")) return false;
        }

        if (distanceToPlayer <= closeRangeAlwaysNotice) return true;

        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        if (angle <= effectiveFov * 0.5f) return true;

        if (distanceToPlayer <= peripheralRange)
            return Random.value < peripheralDetectionChance * Time.deltaTime;

        return false;
    }

    void EnterChase()
    {
        state = State.Chasing;
        lastSeenTimer = 0f;
        chaseStallTimer = 0f;
        nextChaseRepathTime = 0f; // force an immediate repath this frame rather than waiting out the throttle window below
        // Each stack (one per chase she's lost) permanently raises her base chase speed for the rest
        // of the level - she "remembers" every close call and gets harder to shake off the more times
        // the player evades her.
        float escalatedBaseSpeed = chaseSpeed + escalationStacks * chaseSpeedPerStack;
        currentChaseSpeed = escalatedBaseSpeed * Random.Range(1f - speedJitter, 1f + speedJitter);
        agent.speed = currentChaseSpeed;
        // EnterAttack() leaves this true for the whole windup+cooldown - without clearing it here,
        // breaking out of attack range straight into a chase left her frozen in place forever, still
        // "chasing" on paper (SetDestination() below runs every frame) but never actually moving.
        agent.isStopped = false;
        agent.stoppingDistance = ChaseStoppingDistance;
        agent.autoBraking = false;
        // Hand facing back to the agent's own steering - it should track the movement direction
        // during an active chase, not whatever a previous state (attack windup, glimpse) last set.
        agent.updateRotation = true;
        PlayRandomClip(oneShotSource, chaseStartClips);
    }

    // Player successfully hid - she gives up on the spot rather than investigating the doorway
    // first. Same agent setup EnterSearch()/SetNewRoamDestination() use, just skipping straight to
    // Roaming instead of scanning in place for searchDuration and then rolling Stalk vs Roam.
    void EnterRoam()
    {
        state = State.Roaming;
        lastSeenTimer = 0f;
        searchTimer = 0f;
        agent.speed = roamSpeed;
        agent.isStopped = false;
        agent.stoppingDistance = 0f;
        agent.autoBraking = true;
        agent.updateRotation = true; // Search's scan-in-place leaves this false - restore it before walking off
        SetNewRoamDestination();
    }

    void EnterSearch()
    {
        state = State.Searching;
        searchTimer = 0f;
        agent.speed = roamSpeed;
        agent.isStopped = false; // same fix as EnterChase() - EnterAttack() can leave this stuck true otherwise
        agent.stoppingDistance = 0f;
        agent.autoBraking = true;
        agent.updateRotation = true; // walking to the last-known position - let the agent face its own path
        agent.SetDestination(lastKnownPlayerPosition);
        PlayRandomClip(oneShotSource, loseTrackClips);
    }

    // True whenever she has somewhere to be but isn't actually getting there - a repath that never
    // resolves, or a destination sitting on the far side of a navmesh seam/gap. Shared by every state
    // that sets a destination and waits (Roaming, Chasing, ...) since NavMeshAgent gives no built-in
    // signal for "this destination will never be reached" - SetDestination() fails silently either way.
    bool AgentStuckEnRoute()
    {
        return !agent.pathPending
            && agent.velocity.sqrMagnitude < 0.05f
            && agent.remainingDistance > agent.stoppingDistance + 0.5f;
    }

    // A chase target can go unreachable mid-pursuit - through a doorway edge the navmesh doesn't quite
    // connect, a corner the player rounds that puts her destination on the far side of a wall the mesh
    // treats as disjoint, anywhere pathfinding comes back PathInvalid/PathPartial with no way forward.
    // SetDestination() fails silently in that case: state stays Chasing, IsRunning stays true so the
    // Run clip keeps playing, but agent.velocity never leaves ~0 - she reads as running in place forever
    // instead of ever recovering. Catch that and force a recovery rather than let it stand indefinitely.
    void UpdateChaseStallWatchdog()
    {
        // Two distinct ways a chase can get stuck: (a) AgentStuckEnRoute() - a repath request that never
        // actually resolves, or (b) she successfully reaches the end of a NavMeshPathStatus.PathPartial
        // route - the navmesh genuinely has no walkable connection any further (a bad seam between
        // separately-baked tiles, a doorway gap, anything) - and just stands there having "arrived" at
        // that dead end while the real player is still well beyond it. (b) doesn't trip (a) at all since
        // she did reach her (partial) destination, so it needs its own check against her actual distance
        // to the player rather than to agent.destination.
        bool notMoving = !agent.pathPending && agent.velocity.sqrMagnitude < 0.05f;
        bool stillFarFromDestination = agent.remainingDistance > agent.stoppingDistance + 0.5f;
        bool arrivedAtDeadEnd = agent.pathStatus == NavMeshPathStatus.PathPartial
            && agent.remainingDistance <= agent.stoppingDistance + 0.5f
            && Vector3.Distance(transform.position, player.position) > attackRange * 2f;

        bool stalled = notMoving && (stillFarFromDestination || arrivedAtDeadEnd);

        if (!stalled)
        {
            chaseStallTimer = 0f;
            return;
        }

        chaseStallTimer += Time.deltaTime;
        if (chaseStallTimer < 1.2f) return;

        // First response: try a fresh, wider sample around the player in case the exact point she was
        // aiming for was the unreachable part of a room (e.g. just past a wall corner) while the room
        // itself is fine. If even that gives no path forward, treat it like losing the player rather
        // than sitting stuck - EnterSearch() cuts loose to the last position that actually worked, and
        // stalkChance keeps her nearby afterward instead of this reading as her giving up.
        chaseStallTimer = 0f;
        if (NavMesh.SamplePosition(player.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            var testPath = new UnityEngine.AI.NavMeshPath();
            if (agent.CalculatePath(hit.position, testPath) && testPath.status == NavMeshPathStatus.PathComplete)
                return;
        }

        lastKnownPlayerPosition = transform.position;
        EnterSearch();
    }

    void EnterStalk()
    {
        state = State.Stalking;
        stalkTimer = 0f;
        agent.isStopped = false;
        agent.speed = stalkSpeed;
        agent.stoppingDistance = stalkApproachDistance;
        agent.autoBraking = true;
        agent.updateRotation = true;
        nextStalkClipTime = Time.time + Random.Range(stalkClipMinInterval, stalkClipMaxInterval);
    }

    // Weeping-Angel style pursuit: creeps closer while unwatched, freezes solid the instant the
    // player's camera can actually see her (reusing the same check the glimpse system uses). Her own
    // normal detection (seesPlayer, passed in) can still promote this straight into a real chase -
    // that's the "gotcha" moment where creeping too close pays off for her.
    void StalkBehavior(bool seesPlayer)
    {
        if (seesPlayer)
        {
            EnterChase();
            return;
        }

        stalkTimer += Time.deltaTime;
        if (stalkTimer >= stalkDuration)
        {
            state = State.Roaming;
            agent.stoppingDistance = 0f;
            SetNewRoamDestination();
            return;
        }

        bool watched = PlayerCanSeeGhost();
        agent.isStopped = watched;

        if (watched)
        {
            Vector3 lookDir = player.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 6f);
        }
        else
        {
            agent.SetDestination(GetPlayerNavTarget());
        }

        if (Time.time >= nextStalkClipTime)
        {
            AudioClip[] clips = stalkClips != null && stalkClips.Length > 0 ? stalkClips : ambientCues;
            PlayRandomClip(oneShotSource, clips);
            nextStalkClipTime = Time.time + Random.Range(stalkClipMinInterval, stalkClipMaxInterval);
        }
    }

    void EnterAttack()
    {
        state = State.Attacking;
        agent.isStopped = true;
        // The agent auto-faces its movement direction by default (updateRotation=true) - left on
        // during the windup, it fights the manual look-at Slerp below every frame, which reads as
        // jittery/disconnected facing right when the attack should read as a clean, deliberate lunge.
        agent.updateRotation = false;
        attackTimer = 0f;
        attackDamageApplied = false;
        PlayRandomClip(oneShotSource, attackClips, attackVolume);
        if (anim != null) anim.SetTrigger("Attack");
    }

    void AttackBehavior()
    {
        // Stays rooted in place and just turns to face the player for the whole attack, windup
        // included - the NavMeshAgent used to be released mid-swing to lunge and close any gap
        // that opened during the windup, but the Attack clip is a stationary swipe with no
        // matching footwork, so the script-driven translation read as gliding across the floor.
        // EnterAttack() only fires once she's already within attackRange, and the damage check
        // below already has a 1.3x range tolerance for a player who steps back during the windup,
        // so the lunge wasn't actually load-bearing for landing the hit.
        Vector3 lookDir = player.position - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);

        // lastKnownPlayerPosition otherwise only updates in the Chasing case - left stale through an
        // entire attack windup+cooldown, a player who breaks away and rounds a corner mid-attack sends
        // the eventual EnterSearch() (below) back to wherever she last saw them during the chase, not
        // where they actually went, instead of the fresher position she's had eyes on this whole time.
        lastKnownPlayerPosition = player.position;

        attackTimer += Time.deltaTime;

        if (!attackDamageApplied && attackTimer >= attackWindup)
        {
            // Only marks the swing as "resolved" once it actually connects - she's rooted in place
            // for the whole windup (no lunge, see the comment above AttackBehavior()), so a player
            // backing away can easily drift outside range by the exact frame windup elapses. Used to
            // mark attackDamageApplied here unconditionally, which permanently whiffed the swing the
            // instant that one frame's distance check failed. The window below keeps the hit live a
            // little longer than one frame, but no longer stays open for the entire cooldown.
            bool withinHitWindow = attackTimer <= attackWindup + attackHitWindow;

            // Horizontal only: her pivot sits at her feet and the player's at roughly their waist, so
            // the raw 3D distance carried a constant ~0.6m of vertical offset that had nothing to do
            // with whether the swing reached them.
            Vector3 toPlayer = player.position - transform.position;
            bool withinHeight = toPlayer.y <= attackMaxHeightAbove;
            toPlayer.y = 0f;
            float distanceToPlayer = toPlayer.magnitude;

            // She turns to face the player throughout the windup, so this only rules out someone who
            // actually got around behind her - it doesn't rob her of a legitimate frontal hit.
            bool inFrontOfHer = toPlayer.sqrMagnitude < 0.0001f ||
                                Vector3.Angle(transform.forward, toPlayer.normalized) <= attackArcAngle * 0.5f;

            bool hiddenDuringSwing = playerHideState != null && playerHideState.IsHiding;
            if (withinHitWindow && inFrontOfHer && withinHeight && !hiddenDuringSwing &&
                distanceToPlayer <= attackRange * attackReachTolerance && playerHealth != null)
            {
                attackDamageApplied = true;
                if (playerHealth.currentHealth <= attackDamage && !playerHealth.IsDead)
                {
                    hasKilledPlayer = true;
                    agent.isStopped = true;
                    if (anim != null) anim.SetTrigger("Kill");
                    PlayRandomClip(oneShotSource, killClips != null && killClips.Length > 0 ? killClips : attackClips, attackVolume);
                }
                playerHealth.TakeDamage(attackDamage, transform);
            }
        }

        if (attackTimer < attackWindup + attackCooldown) return;

        if (AttackDistance() <= attackRange)
        {
            EnterAttack();
        }
        else if (CanSeePlayer(IsLitByFlashlight()))
        {
            // Route through EnterChase() rather than setting state directly - it resets speed,
            // stoppingDistance, autoBraking and (critically) hands rotation control back to the agent,
            // which the attack windup just took over manually. Skipping that left her facing frozen
            // at whatever angle the last attack ended on instead of turning to track the player.
            EnterChase();
        }
        else
        {
            EnterSearch();
        }
    }

    void TryTriggerGlimpse()
    {
        if (!glimpseEnabled || playerCamera == null) return;
        if (Time.time < nextGlimpseCheckTime) return;
        nextGlimpseCheckTime = Time.time + glimpseCheckInterval;

        if (Time.time < glimpseCooldownUntil) return;
        if (Time.time - JumpscareFlash.LastScareTime < 20f) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        if (distanceToPlayer < glimpseMinDistance || distanceToPlayer > glimpseMaxDistance) return;

        if (!PlayerCanSeeGhost()) return;
        if (Random.value > glimpseChance) return;

        isGlimpsing = true;
        glimpseTimer = 0f;
        agent.isStopped = true;
        agent.updateRotation = false; // holding a manual look-at toward the player for the sighting
        PlayRandomClip(oneShotSource, glimpseClips);
    }

    void HoldGlimpse()
    {
        Vector3 lookDir = player.position - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 4f);

        glimpseTimer += Time.deltaTime;
        if (glimpseTimer >= glimpseDuration)
        {
            isGlimpsing = false;
            agent.isStopped = false;
            agent.updateRotation = true; // back to roaming - let the agent face its own path again
            glimpseCooldownUntil = Time.time + Random.Range(glimpseCooldownMin, glimpseCooldownMax);
            SetNewRoamDestination();
        }
    }

    bool PlayerCanSeeGhost()
    {
        Vector3 targetPos = transform.position + Vector3.up * 1f;
        Vector3 viewportPoint = playerCamera.WorldToViewportPoint(targetPos);

        if (viewportPoint.z <= 0f) return false;
        if (viewportPoint.x < 0f || viewportPoint.x > 1f || viewportPoint.y < 0f || viewportPoint.y > 1f) return false;

        Vector3 eyePos = playerCamera.transform.position;
        float distance = Vector3.Distance(eyePos, targetPos);

        if (Physics.Raycast(eyePos, (targetPos - eyePos).normalized, out RaycastHit hit, distance, obstacleMask))
        {
            if (!hit.transform.IsChildOf(transform) && hit.transform != transform) return false;
        }
        return true;
    }

    void EnforceMinDistance()
    {
        Vector3 toGhost = transform.position - player.position;
        toGhost.y = 0f;
        float dist = toGhost.magnitude;

        if (dist < minPhysicalDistance && dist > 0.001f)
        {
            Vector3 corrected = player.position + toGhost.normalized * minPhysicalDistance;
            corrected.y = transform.position.y;

            // Pushing blindly in world space can shove her into a wall/prop with no navmesh under
            // it - sample near the corrected point first so this can never itself be what strands her.
            if (NavMesh.SamplePosition(corrected, out NavMeshHit hit, minPhysicalDistance + 1f, NavMesh.AllAreas))
                agent.Warp(hit.position);
        }
    }

    // Throttled rather than checked every frame - a physics overlap is comparatively expensive and
    // door state only actually needs to catch up a few times a second to read as instant to a player.
    void TryOpenDoorsAhead()
    {
        if (Time.time < nextDoorCheckTime) return;
        nextDoorCheckTime = Time.time + 0.15f;

        Vector3 center = transform.position + transform.forward * (doorOpenRange * 0.5f) + Vector3.up * 1f;
        var hits = Physics.OverlapSphere(center, doorOpenRange, doorLayer, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            DoorController door = h.GetComponentInParent<DoorController>();
            if (door != null && !door.IsOpen) door.ForceOpen();
        }
    }

    // Checked every frame she has an active heading. A NavMeshAgent alone would just path around a
    // low obstacle the player can vault over - fine while roaming, but during a chase that detour is
    // exactly the kind of gap a vault is supposed to open up.
    bool TryGhostVault()
    {
        // Attacking sets agent.isStopped = false during its post-windup lunge (see AttackBehavior),
        // so without this an obstacle in that lunge path could trigger a vault mid-attack and pause
        // attackTimer, glitching a kill attempt into a vault animation.
        if (isVaulting || hasKilledPlayer || isGlimpsing || state == State.Attacking) return false;
        if (agent == null || !agent.enabled || agent.isStopped) return false;
        if (agent.velocity.sqrMagnitude < 0.01f) return false;

        Vector3 dir = agent.velocity.normalized;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.6f;

        if (Physics.Raycast(rayOrigin, dir, out RaycastHit hit, vaultRayDistance, vaultObstacleLayer))
        {
            Vector3 landing = hit.point + dir * vaultLandingOffset;
            landing.y = transform.position.y;
            StartCoroutine(VaultOverObstacle(landing));
            return true;
        }
        return false;
    }

    IEnumerator VaultOverObstacle(Vector3 landingPos)
    {
        isVaulting = true;
        Vector3 startPos = transform.position;
        agent.enabled = false;

        if (anim != null && !string.IsNullOrEmpty(vaultAnimTrigger)) anim.SetTrigger(vaultAnimTrigger);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / vaultDuration;
            Vector3 flatPos = Vector3.Lerp(startPos, landingPos, t);
            float arc = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * vaultArcHeight;
            transform.position = flatPos + Vector3.up * arc;
            yield return null;
        }

        transform.position = landingPos;
        agent.enabled = true;

        // The fixed landing offset doesn't know what's actually there - it can overshoot into a wall,
        // corner, or cluttered prop with no navmesh nearby. Widen the search before giving up, and if
        // even that fails, fall back to the vault's own start point (which was on the mesh) rather
        // than leaving her enabled but off-mesh, which used to strand her frozen and unrecoverable
        // for the rest of the level.
        if (NavMesh.SamplePosition(landingPos, out NavMeshHit navHit, 4f, NavMesh.AllAreas))
        {
            transform.position = navHit.position;
            agent.Warp(navHit.position);
        }
        else if (NavMesh.SamplePosition(startPos, out NavMeshHit startHit, 4f, NavMesh.AllAreas))
        {
            transform.position = startHit.position;
            agent.Warp(startHit.position);
        }

        isVaulting = false;
    }

    void UpdateAudioAndAnimation()
    {
        if (anim != null)
        {
            bool running = state == State.Chasing;
            anim.SetBool("IsRunning", running);

            if (state == State.Attacking)
            {
                anim.speed = 1f;
            }
            else
            {
                float baseline = running ? chaseSpeed : (state == State.Stalking ? stalkSpeed : roamSpeed);
                float speedRatio = agent.velocity.magnitude / Mathf.Max(baseline, 0.01f);
                // No dedicated idle clip exists, so a full stop (waiting between roam destinations,
                // the Searching scan-turn, frozen mid-stalk) used to still play the walk cycle at 30%
                // speed - reads as endlessly walking in place instead of actually standing still.
                // 0.05 keeps just enough motion to avoid freezing on an awkward mid-stride pose.
                float targetAnimSpeed = Mathf.Clamp(speedRatio, 0.05f, 1.6f);
                // Smoothed rather than snapped straight to targetAnimSpeed - without this, the instant
                // a state change swaps the baseline (e.g. Roaming -> Chasing), the ratio jumps hard
                // before her actual velocity has caught up to the new speed, popping the animation's
                // playback rate in a single frame. That reads as a visible glitch rather than her
                // accelerating into a sprint, which undercuts the "real, physical threat" feel.
                anim.speed = Mathf.MoveTowards(anim.speed, targetAnimSpeed, Time.deltaTime * 4f);
            }
        }

        if (loopedSource != null)
        {
            loopedSource.pitch = state == State.Chasing ? 1.3f : 1f;
            bool actuallyMoving = !hasKilledPlayer && !isGlimpsing && state != State.Attacking && agent.velocity.magnitude > 0.15f;
            float targetVolume = actuallyMoving ? loopedSourceBaseVolume * SfxVolume.Multiplier : 0f;
            loopedSource.volume = Mathf.MoveTowards(loopedSource.volume, targetVolume, Time.deltaTime * 3f);
        }
    }

    // Her breathing, audible any time she's in play regardless of state or line of sight - the closer
    // she is, the louder and faster it gets, so proximity reads as dread even when she's never seen.
    void UpdateHeartbeat()
    {
        if (heartbeatSource == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        float proximity = Mathf.Clamp01(1f - (distance / heartbeatStartDistance));

        // Actively hunting always reads as maximum dread, regardless of the exact current distance.
        if (state == State.Chasing || state == State.Attacking) proximity = 1f;

        float targetVolume = Mathf.Lerp(heartbeatMinVolume, heartbeatMaxVolume, proximity) * SfxVolume.Multiplier;
        float targetPitch = Mathf.Lerp(heartbeatMinPitch, heartbeatMaxPitch, proximity);

        heartbeatSource.volume = Mathf.MoveTowards(heartbeatSource.volume, targetVolume, Time.deltaTime * heartbeatSmoothing);
        heartbeatSource.pitch = Mathf.MoveTowards(heartbeatSource.pitch, targetPitch, Time.deltaTime * heartbeatSmoothing);

        if (!heartbeatSource.isPlaying && heartbeatSource.volume > 0.001f) heartbeatSource.Play();
    }

    // Close range during an active chase drains the battery harder, on top of its own normal drain -
    // pushes it toward FlashlightController's existing flicker threshold right as it matters most,
    // without needing any new visual/flicker code of our own.
    void ApplyChaseInterference()
    {
        if (flashlight == null || !flashlight.isOn) return;
        if (Vector3.Distance(transform.position, player.position) > interferenceRange) return;

        flashlight.batteryLife = Mathf.Max(0f, flashlight.batteryLife - chaseBatteryDrainBonus * Time.deltaTime);
    }

    void ScheduleNextAmbientCue()
    {
        nextAmbientCueTime = Time.time + Random.Range(ambientCueMinInterval, ambientCueMaxInterval);
    }

    void UpdateAmbientCues()
    {
        if (ambientCues == null || ambientCues.Length == 0) return;
        if (Time.time < nextAmbientCueTime) return;

        PlayRandomClip(oneShotSource, ambientCues);
        ScheduleNextAmbientCue();
    }

    void PlayRandomClip(AudioSource source, AudioClip[] clips, float volumeScale = 1f)
    {
        if (source == null || clips == null || clips.Length == 0) return;
        source.PlayOneShot(clips[Random.Range(0, clips.Length)], SfxVolume.Multiplier * volumeScale);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.yellow;
        Vector3 forward = transform.forward * detectionRange;
        Quaternion leftRayRotation = Quaternion.AngleAxis(-fieldOfViewAngle * 0.5f, Vector3.up);
        Quaternion rightRayRotation = Quaternion.AngleAxis(fieldOfViewAngle * 0.5f, Vector3.up);
        Gizmos.DrawRay(transform.position, leftRayRotation * forward);
        Gizmos.DrawRay(transform.position, rightRayRotation * forward);
    }
}
