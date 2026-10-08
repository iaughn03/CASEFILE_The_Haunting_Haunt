using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Death")]
    public GameObject youDiedPanel;
    public string mainMenuSceneName = "MainMenu";
    public Button deathRestartButton;
    public Button deathExitButton;
    [Tooltip("Same GameOverSubtitle text DeductionGate fills in for a wrong deduction ending - reused here so dying to the ghost also leaves the case explicitly unsolved instead of a blank line under YOU DIED.")]
    public TextMeshProUGUI deathSubtitleText;
    [TextArea]
    [Tooltip("Shown under YOU DIED when the ghost kills the player outright, before the case could be solved.")]
    public string killedByGhostSubtitle = "The case goes cold. The truth dies with you, and Grimwood keeps its secrets.";
    [Tooltip("Real-time delay before the death screen/pause kicks in, so the ghost's kill animation is actually visible instead of freezing instantly. Kept above the Kill clip's own length (1.8s) so the stab is never frozen mid-swing when the pause kicks in.")]
    public float deathAnimationDelay = 2f;
    [Tooltip("How long YouDiedPanel takes to fade in (its own CanvasGroup) once the collapse finishes, instead of just popping onto screen at full opacity.")]
    public float deathFadeDuration = 0.9f;

    [Header("Death Cinematic (kill-cam)")]
    [Tooltip("Black bars that slide in for a widescreen/cinematic feel while the kill animation plays.")]
    public RectTransform letterboxTop;
    public RectTransform letterboxBottom;
    public float letterboxHeight = 90f;
    [Tooltip("How long the camera takes to swing from the first-person view into the kill-cam angle.")]
    public float killCamSwingDuration = 0.5f;
    [Tooltip("Sideways distance from the player/killer midpoint the kill-cam settles at.")]
    public float killCamSideDistance = 2.2f;
    [Tooltip("How far below the killer's actual face position the kill-cam settles, in world units - a small positive value gives a slight, flattering upward angle without dipping down toward her knees.")]
    public float killCamHeight = 0.1f;
    [Tooltip("How far back from the midpoint the kill-cam pulls, along the player-to-killer line.")]
    public float killCamPullback = 0.4f;
    [Tooltip("Field of view the kill-cam eases into as it settles on the shot - zoomed in from the normal playing FOV, but wide enough to keep both her and the player (and the stab actually connecting) in frame rather than cropping down to just her face.")]
    public float killCamZoomFOV = 36f;
    [Tooltip("Fallback height above the killer's feet used only if she has no humanoid Head bone to read directly - the kill-cam otherwise finds her actual head bone at runtime so this never has to match her real height by hand.")]
    public float killCamFaceHeight = 1.7f;
    [Range(0f, 1f)]
    [Tooltip("How much the kill-cam's focus point is pulled from the player/killer midpoint toward the killer's face - 0 frames both evenly, 1 centers entirely on her face. Kept moderate so the shot still reads as her stabbing the player rather than a pure face portrait that crops the stab itself out of frame.")]
    public float killCamFaceBias = 0.4f;
    [Tooltip("How long the initial stagger/recoil from the stab lasts - a sharp, quick reaction to the hit before gravity actually takes over, rather than the body going limp instantly.")]
    public float staggerDuration = 0.2f;
    [Tooltip("How far the camera reels back and to the side during the initial stagger.")]
    public float staggerOffset = 0.15f;
    [Tooltip("How long the actual fall - accelerating downward like gravity, after the stagger - takes.")]
    public float collapseDuration = 0.55f;
    [Tooltip("World height above the player's own feet the camera ends up at once fully collapsed - near the floor, like actually lying down. Deliberately absolute rather than a delta from wherever the kill-cam happened to be, since that can sit well above head height (see killCamHeight) and a fixed drop from there would never reach the ground.")]
    public float collapseGroundHeight = 0.35f;
    [Tooltip("How far the camera pitches forward/down (degrees) by the end of the fall, like the head dropping as the body hits the floor.")]
    public float collapseTiltAngle = 75f;
    [Tooltip("Random left/right roll (degrees) applied to the fall, so she doesn't collapse in a perfectly symmetrical straight line - randomized fresh each death.")]
    public float collapseSideRollJitter = 18f;
    [Tooltip("Size of the short, sharply-decaying camera shake on impact, once the fall reaches the ground.")]
    public float landingShakeMagnitude = 0.06f;
    [Tooltip("How long the landing shake takes to decay to nothing.")]
    public float landingShakeDuration = 0.25f;

    [Header("Health Bar UI")]
    public Image healthBarFill;       // the real, current-health bar
    public Image healthBarTrailFill;  // lighter "afterimage" bar that lags behind on damage
    public float healthBarFillSpeed = 2.8f;   // fillAmount/sec the main bar can move
    public float healthBarTrailSpeed = 0.8f;  // fillAmount/sec the trail bar can move (slower = visible lag)

    [Header("Damage Flash (scratch marks)")]
    public CanvasGroup damageFlashGroup; // wraps the claw-scratch graphic so its whole group fades as one
    public RectTransform scratchCluster; // parent of the 4 claw lines - randomized as one unit per hit
    public float flashPeakAlpha = 0.8f;
    public float flashFadeDuration = 0.4f;
    public Vector2 scratchPositionJitter = new Vector2(90f, 60f); // max +/- screen offset from center
    public float scratchRotationJitter = 20f; // max +/- degrees added on top of the claws' baked-in rake angle
    public Vector2 scratchScaleRange = new Vector2(0.8f, 1.2f);

    [Header("Hit Audio")]
    public AudioSource hurtAudioSource;
    public AudioClip[] hurtClips;

    [Header("Hit Impact")]
    public float shakeDuration = 0.3f;
    public float shakeMagnitude = 0.15f;

    [Header("Recovery - health slowly comes back once the ghost has stopped hunting you")]
    [Tooltip("Seconds after the last hit before any health returns.")]
    public float regenDelay = 10f;
    [Tooltip("Health regained per second while it's safe (no ghost chasing or attacking).")]
    public float regenPerSecond = 2f;

    public bool IsDead { get; private set; }

    private float lastHitTime = -999f;
    private GhostController[] ghosts;
    private Coroutine flashRoutine;
    private PlayerMovement playerMovement;
    private CharacterController characterController;
    private bool isDeathTransitioning; // guards Restart/Exit against double-clicks firing a second scene load
    private Transform killer; // whoever last hit us - used to frame the kill-cam on the finishing blow

    void Start()
    {
        currentHealth = maxHealth;
        playerMovement = GetComponent<PlayerMovement>();
        characterController = GetComponent<CharacterController>();

        if (healthBarFill != null) healthBarFill.fillAmount = 1f;
        if (healthBarTrailFill != null) healthBarTrailFill.fillAmount = 1f;

        if (damageFlashGroup != null) damageFlashGroup.alpha = 0f;
    }

    void Update()
    {
        if (!IsDead && currentHealth < maxHealth && Time.time - lastHitTime >= regenDelay && !AnyGhostHunting())
            currentHealth = Mathf.Min(maxHealth, currentHealth + regenPerSecond * Time.deltaTime);

        float targetFill = currentHealth / maxHealth;

        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = Mathf.MoveTowards(healthBarFill.fillAmount, targetFill, healthBarFillSpeed * Time.deltaTime);
        }

        if (healthBarTrailFill != null)
        {
            healthBarTrailFill.fillAmount = Mathf.MoveTowards(healthBarTrailFill.fillAmount, targetFill, healthBarTrailSpeed * Time.deltaTime);
        }
    }

    private bool AnyGhostHunting()
    {
        if (ghosts == null) ghosts = FindObjectsByType<GhostController>(FindObjectsSortMode.None);
        foreach (var g in ghosts)
            if (g != null && g.IsChasing) return true;
        return false;
    }

    public void TakeDamage(float amount, Transform source = null)
    {
        if (IsDead) return;

        if (source != null) killer = source;

        lastHitTime = Time.time;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        Debug.Log($"Player took {amount} damage, health now {currentHealth}/{maxHealth}");

        FlashDamage();
        PlayHurtSound();

        if (playerMovement != null)
        {
            playerMovement.Shake(shakeDuration, shakeMagnitude);
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    void FlashDamage()
    {
        if (damageFlashGroup == null) return;

        RandomizeScratchCluster();

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    // Moves/rotates/scales the whole claw-mark cluster as one unit so each hit rakes
    // a different spot at a different angle, instead of always showing the same scratch.
    void RandomizeScratchCluster()
    {
        if (scratchCluster == null) return;

        float x = Random.Range(-scratchPositionJitter.x, scratchPositionJitter.x);
        float y = Random.Range(-scratchPositionJitter.y, scratchPositionJitter.y);
        scratchCluster.anchoredPosition = new Vector2(x, y);

        float rot = Random.Range(-scratchRotationJitter, scratchRotationJitter);
        scratchCluster.localRotation = Quaternion.Euler(0f, 0f, rot);

        float scale = Random.Range(scratchScaleRange.x, scratchScaleRange.y);
        bool mirror = Random.value < 0.5f;
        scratchCluster.localScale = new Vector3(mirror ? -scale : scale, scale, 1f);
    }

    IEnumerator FlashRoutine()
    {
        damageFlashGroup.alpha = flashPeakAlpha;

        float t = 0f;
        while (t < flashFadeDuration)
        {
            t += Time.deltaTime;
            damageFlashGroup.alpha = Mathf.Lerp(flashPeakAlpha, 0f, t / flashFadeDuration);
            yield return null;
        }

        damageFlashGroup.alpha = 0f;
    }

    void PlayHurtSound()
    {
        if (hurtAudioSource == null || hurtClips == null || hurtClips.Length == 0) return;
        hurtAudioSource.PlayOneShot(hurtClips[Random.Range(0, hurtClips.Length)], SfxVolume.Multiplier);
    }

    void Die()
    {
        IsDead = true;
        Debug.Log("Player died.");

        // Stops player input immediately (can't walk/look away mid-kill-animation) without touching
        // Time.timeScale yet - that happens after the delay below, once the animation has actually
        // had a chance to play. Freezing timeScale here would freeze the ghost's Kill animation too,
        // on frame one, before it's visible.
        if (playerMovement != null) playerMovement.enabled = false;

        StartCoroutine(DeathSequence());
    }

    IEnumerator DeathSequence()
    {
        yield return StartCoroutine(PlayKillCam());
        yield return StartCoroutine(PlayCollapse());

        if (deathSubtitleText != null) deathSubtitleText.text = killedByGhostSubtitle;

        // Paused/unlocked immediately (the world shouldn't keep moving once the body's hit the
        // floor) - the fade itself still plays smoothly afterward since it runs on unscaled time.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;

        yield return StartCoroutine(FadeInDeathScreen());

        // Waits here for the player to choose Restart or Exit on the YouDiedPanel buttons
    }

    // YouDiedPanel already carries a full-screen black background - fading its own CanvasGroup in
    // (rather than a separate overlay) doubles as both the "vision fades to black" beat and the
    // death screen's own entrance, instead of the panel just popping on at full opacity.
    IEnumerator FadeInDeathScreen()
    {
        if (youDiedPanel == null) yield break;

        youDiedPanel.SetActive(true);
        CanvasGroup group = youDiedPanel.GetComponent<CanvasGroup>();
        if (group == null) yield break; // no CanvasGroup to fade - already shown instantly above, same as before

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        float t = 0f;
        while (t < deathFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(t / deathFadeDuration);
            yield return null;
        }

        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    // Reads the killer's actual head bone so the kill-cam's "face" target tracks her real height
    // instead of a guessed offset - she's a good deal taller than a default human, so a flat
    // killCamFaceHeight number used to land the zoom around her chest instead of her face.
    Vector3 GetKillerFacePos()
    {
        Animator killerAnim = killer.GetComponent<Animator>();
        if (killerAnim != null && killerAnim.isHuman)
        {
            Transform head = killerAnim.GetBoneTransform(HumanBodyBones.Head);
            if (head != null) return head.position;
        }
        return killer.position + Vector3.up * killCamFaceHeight;
    }

    // Swings the first-person camera out to a third-person angle so the ghost's Kill animation
    // actually reads as a stabbing rather than playing off to the side of a frozen first-person view,
    // with letterbox bars easing in for a cinematic feel. Spends the same real-time budget
    // deathAnimationDelay always reserved for the kill animation to play before the death screen -
    // the swing eats the front of it, the rest is spent holding the shot.
    IEnumerator PlayKillCam()
    {
        Camera cam = playerMovement != null ? playerMovement.playerCamera : null;
        Transform camT = cam != null ? cam.transform : null;
        bool haveShot = camT != null && killer != null;

        Vector3 startPos = Vector3.zero, targetPos = Vector3.zero;
        Quaternion startRot = Quaternion.identity, targetRot = Quaternion.identity;
        float startFOV = cam != null ? cam.fieldOfView : 60f;

        if (haveShot)
        {
            startPos = camT.position;
            startRot = camT.rotation;
            camT.SetParent(null, true); // freed from the (now-stationary) player rig so it can move independently for the shot

            Vector3 playerPos = transform.position + Vector3.up * 1.5f;
            Vector3 killerPos = killer.position + Vector3.up * 1.5f;
            Vector3 killerFacePos = GetKillerFacePos();
            Vector3 toKiller = killerPos - playerPos;
            Vector3 flatDir = toKiller.sqrMagnitude > 0.001f ? toKiller.normalized : transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, flatDir);
            if (side.sqrMagnitude < 0.001f) side = transform.right;
            side.Normalize();

            Vector3 midpoint = Vector3.Lerp(playerPos, killerPos, 0.5f);
            // Biased toward the killer's face rather than dead-center between the two, so the FOV
            // zoom below actually lands on her face instead of the empty space between them.
            Vector3 focusPoint = Vector3.Lerp(midpoint, killerFacePos, killCamFaceBias);
            targetPos = midpoint + side * killCamSideDistance - flatDir * killCamPullback;
            // Pegged to her actual (head-bone-derived) face height, not a flat absolute number - she's
            // considerably taller than a default human, so a fixed height used to sit near her torso.
            targetPos.y = killerFacePos.y - killCamHeight;
            targetRot = Quaternion.LookRotation((focusPoint - targetPos).normalized, Vector3.up);
        }

        float swing = Mathf.Clamp(killCamSwingDuration, 0f, deathAnimationDelay);
        float t = 0f;
        while (t < swing)
        {
            t += Time.unscaledDeltaTime;
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / swing), 2f); // ease-out - settles rather than snapping into place

            if (haveShot)
            {
                camT.position = Vector3.Lerp(startPos, targetPos, eased);
                camT.rotation = Quaternion.Slerp(startRot, targetRot, eased);
                if (cam != null) cam.fieldOfView = Mathf.Lerp(startFOV, killCamZoomFOV, eased);
            }
            SetLetterboxHeight(Mathf.Lerp(0f, letterboxHeight, eased));
            yield return null;
        }

        if (haveShot)
        {
            camT.position = targetPos;
            camT.rotation = targetRot;
            if (cam != null) cam.fieldOfView = killCamZoomFOV;
        }
        SetLetterboxHeight(letterboxHeight);

        float holdTime = deathAnimationDelay - swing;
        if (holdTime > 0f) yield return new WaitForSecondsRealtime(holdTime);
    }

    void SetLetterboxHeight(float height)
    {
        if (letterboxTop != null) letterboxTop.sizeDelta = new Vector2(letterboxTop.sizeDelta.x, height);
        if (letterboxBottom != null) letterboxBottom.sizeDelta = new Vector2(letterboxBottom.sizeDelta.x, height);
    }

    // The CharacterController's own transform sits at its capsule CENTER, not the feet (center.y is
    // typically 0, height 2, so the root pivot is a full meter above actual ground). Using the root
    // directly as a floor reference left an earlier version of the collapse settling roughly a meter
    // above the real floor instead of on it.
    float GetFeetWorldY()
    {
        if (characterController != null)
            return transform.position.y + characterController.center.y - characterController.height * 0.5f;
        return transform.position.y;
    }

    // Runs right after PlayKillCam()'s hold, so the stab has already been seen in full before this
    // starts. Three beats, like a body actually reacting to a fatal wound rather than one smooth
    // camera move: a sharp involuntary stagger from the impact, an accelerating fall (gravity, not
    // a flat ease), then a short decaying shake as it actually hits the floor.
    IEnumerator PlayCollapse()
    {
        Camera cam = playerMovement != null ? playerMovement.playerCamera : null;
        Transform camT = cam != null ? cam.transform : null;
        if (camT == null) yield break;

        Vector3 stabPos = camT.position;
        Quaternion stabRot = camT.rotation;

        // Phase 1: stagger - a quick, sharp reel back and to a random side, the body's first
        // involuntary reaction to the wound, before it actually starts going down.
        Vector3 staggerAxis = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
        if (staggerAxis.sqrMagnitude < 0.001f) staggerAxis = camT.right;
        staggerAxis.Normalize();
        Vector3 staggerPos = stabPos - camT.forward * staggerOffset * 0.5f + staggerAxis * staggerOffset;
        Quaternion staggerRot = stabRot * Quaternion.Euler(Random.Range(-8f, 4f), Random.Range(-10f, 10f), Random.Range(-12f, 12f));

        float t = 0f;
        while (t < staggerDuration)
        {
            t += Time.unscaledDeltaTime;
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / staggerDuration), 3f); // sharp ease-out - snaps into the recoil rather than drifting into it
            camT.position = Vector3.Lerp(stabPos, staggerPos, eased);
            camT.rotation = Quaternion.Slerp(stabRot, staggerRot, eased);
            yield return null;
        }

        // Phase 2: the actual fall - accelerates like gravity rather than a flat lerp, and settles at
        // an absolute ground height (not a delta from the stagger position, which can sit well above
        // head height depending on how high the kill-cam left off - see killCamHeight).
        Vector3 fallTargetPos = staggerPos;
        fallTargetPos.y = GetFeetWorldY() + collapseGroundHeight;
        float sideRoll = Random.Range(-collapseSideRollJitter, collapseSideRollJitter);
        Quaternion fallTargetRot = staggerRot * Quaternion.Euler(collapseTiltAngle, 0f, sideRoll);

        t = 0f;
        while (t < collapseDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / collapseDuration);
            float eased = p * p * p; // accelerating - gravity, not a flat ease
            camT.position = Vector3.Lerp(staggerPos, fallTargetPos, eased);
            camT.rotation = Quaternion.Slerp(staggerRot, fallTargetRot, eased);
            yield return null;
        }

        camT.position = fallTargetPos;
        camT.rotation = fallTargetRot;

        // Phase 3: landing impact - a short, sharply-decaying shake, like the body actually hitting
        // the floor rather than the camera just gliding to a silent stop.
        t = 0f;
        while (t < landingShakeDuration)
        {
            t += Time.unscaledDeltaTime;
            float remaining = 1f - Mathf.Clamp01(t / landingShakeDuration);
            float intensity = landingShakeMagnitude * remaining * remaining;
            camT.position = fallTargetPos + Random.insideUnitSphere * intensity;
            yield return null;
        }

        camT.position = fallTargetPos;
        camT.rotation = fallTargetRot;
    }

    // Wired to the YouDiedPanel's Restart button
    public void RestartLevel()
    {
        if (!BeginDeathTransition()) return;
        StartCoroutine(LoadSceneRoutine(SceneManager.GetActiveScene().name));
    }

    // Wired to the YouDiedPanel's Exit button
    public void ExitToMainMenu()
    {
        if (!BeginDeathTransition()) return;
        StartCoroutine(LoadSceneRoutine(mainMenuSceneName));
    }

    // Locks out further clicks so a double-click (very plausible right after dying) can't
    // fire two overlapping scene loads. Returns false if a transition is already underway.
    bool BeginDeathTransition()
    {
        if (isDeathTransitioning) return false;
        isDeathTransitioning = true;

        if (deathRestartButton != null) deathRestartButton.interactable = false;
        if (deathExitButton != null) deathExitButton.interactable = false;

        Time.timeScale = 1f;
        return true;
    }

    IEnumerator LoadSceneRoutine(string sceneName)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (op != null && !op.isDone)
        {
            yield return null;
        }
    }
}
