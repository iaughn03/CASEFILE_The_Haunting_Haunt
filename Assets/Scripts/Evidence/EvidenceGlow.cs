using UnityEngine;

// Visibility aid for evidence the player hasn't collected yet: a soft pale pulse (the existing
// ItemGlowOutline, recolored) plus faint drifting dust. Both strengthen when the flashlight is on the
// item or the player is within nearRange, and both stop the moment the item is collected. Strength is
// scaled by DifficultyDirector so the escalation later in the level also makes clues harder to spot.
// Evidence whose data says visibleOnlyWhenLit (the hidden tape) has its meshes switched off until the
// flashlight actually hits it.
[RequireComponent(typeof(EvidenceItem))]
public class EvidenceGlow : MonoBehaviour
{
    [Header("Look")]
    public Color paleColor = new Color(0.86f, 0.91f, 0.98f);
    [Range(0f, 1f)] public float restIntensity = 0.45f;
    [Range(0f, 1f)] public float pulseAmount = 0.35f;
    public float pulseSpeed = 1.6f;

    [Header("Ready vs locked")]
    [Tooltip("Glow color while this clue can be collected right now. Locked clues (story order / unlock count) keep paleColor.")]
    public Color readyColor = new Color(1f, 0.78f, 0.42f);
    [Tooltip("Glow and dust strength of a locked-but-visible clue, relative to a collectible one.")]
    [Range(0f, 1f)] public float lockedStrength = 0.4f;

    [Header("Boosts")]
    public float nearRange = 6f;
    public float nearBoost = 0.5f;
    public float flashlightBoost = 0.9f;

    [Header("Flashlight reveal")]
    [Tooltip("How far inside the spot cone (fraction of its half-angle) the item must be to count as newly lit. The cone's outer edge is barely lit on screen, so revealing there made flashlight-only evidence pop in out of the dark fringe.")]
    [Range(0.2f, 1f)] public float revealConeFraction = 0.6f;
    [Tooltip("Once lit, stays lit this long after the beam moves off it, so the 0.1s check doesn't flicker it on and off at the beam's edge or with head bob.")]
    public float litHoldSeconds = 0.75f;

    [Header("Dust")]
    public bool enableDust = true;
    public float dustRadius = 0.35f;
    public float dustMaxRate = 14f;

    private EvidenceItem item;
    private EvidenceManager manager;
    private ItemGlowOutline[] outlines;
    private Renderer[] meshRenderers;
    private ParticleSystem dust;
    private float smoothed;
    private float nextLitCheck;
    private float lastInBeamTime;
    private bool lit;
    private bool stopped;

    private static Transform playerTransform;
    private static FlashlightController flashlight;
    private static Texture2D dotTexture;

    private void Awake()
    {
        item = GetComponent<EvidenceItem>();
    }

    private void Start()
    {
        outlines = GetComponentsInChildren<ItemGlowOutline>(true);
        foreach (var o in outlines)
        {
            o.glowColor = paleColor;
            o.emissionIntensity = 1f;
            // A story-locked item's outline is disabled by its pickup; building here would leave an
            // untracked full-bright shell floating over the hidden item. OnEnable rebuilds it, in
            // the recolored pale, once the item unlocks.
            if (o.isActiveAndEnabled) o.BuildOutlines();
        }

        meshRenderers = GetComponentsInChildren<Renderer>(true);
        if (enableDust) BuildDust();
    }

    private void Update()
    {
        if (stopped) return;

        if (item.IsCollected)
        {
            StopGlow();
            return;
        }

        CacheStatics();

        if (Time.unscaledTime >= nextLitCheck)
        {
            float now = Time.unscaledTime;
            nextLitCheck = now + 0.1f;

            // Hysteresis: a tighter cone to become lit than to stay lit, plus a short hold once the
            // beam leaves - a single yes/no test at the cone edge flipped flashlight-only evidence
            // (GarageLockPryMarks, the hidden tape) in and out every check. Switching the flashlight
            // off still drops it immediately.
            bool inBeam = ComputeLit(lit ? 1f : revealConeFraction);
            if (inBeam) lastInBeamTime = now;
            lit = inBeam || (lit && FlashlightOn() && now - lastInBeamTime < litHoldSeconds);
        }
        item.IsLit = lit;

        // Story-locked evidence is hidden by its pickup (unless the manager keeps locked evidence
        // visible); it must not give itself away with dust or, for flashlight-only evidence, by
        // switching its own meshes back on.
        if (manager == null) manager = FindFirstObjectByType<EvidenceManager>();
        bool collectible = manager == null || manager.CanCollectNow(item);
        bool revealed = manager == null || manager.ShouldReveal(collectible);
        SetReady(collectible);

        // Hidden evidence: meshes (and therefore the outline, which mirrors them) only exist while lit.
        if (item.VisibleOnlyWhenLit && revealed) SetMeshesVisible(lit);

        float near = 0f;
        if (playerTransform != null)
        {
            float d = Vector3.Distance(playerTransform.position, transform.position);
            near = Mathf.Clamp01(1f - d / Mathf.Max(0.01f, nearRange));
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed + transform.position.x);
        float strength = restIntensity + pulseAmount * pulse;
        strength += nearBoost * near;
        if (lit) strength += flashlightBoost;
        strength += item.HintBoost;

        var director = FindDirector();
        if (director != null) strength *= director.EvidenceGlowMultiplier;
        if (!collectible) strength *= lockedStrength;

        // The hidden tape shows nothing at all (outline or motes) until the flashlight actually hits it.
        bool suppressed = !revealed || (item.VisibleOnlyWhenLit && !lit);
        float outlineStrength = suppressed ? 0f : strength;
        float dustStrength = suppressed ? 0f : strength;

        smoothed = Mathf.Lerp(smoothed, outlineStrength, 8f * Time.unscaledDeltaTime);
        foreach (var o in outlines)
        {
            if (o != null && o.enabled) o.SetIntensityMultiplier(Mathf.Clamp(smoothed, 0f, 3f));
        }

        UpdateDust(dustStrength);
    }

    private void StopGlow()
    {
        stopped = true;
        item.IsLit = false;
        item.HasBeenCollected = true;
        foreach (var o in outlines) if (o != null) o.enabled = false;
        if (dust != null)
        {
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            dust.gameObject.SetActive(false);
        }
    }

    private void SetMeshesVisible(bool visible)
    {
        foreach (var r in meshRenderers)
        {
            if (r == null || r is ParticleSystemRenderer || r.gameObject.name.EndsWith("_Outline")) continue;
            if (r.enabled != visible) r.enabled = visible;
        }
    }

    // Amber for the clue(s) the player can take now, pale for the ones still locked, so the next
    // step stands out among everything that's visible.
    private bool? readyState;
    private void SetReady(bool ready)
    {
        if (readyState == ready) return;
        readyState = ready;

        Color c = ready ? readyColor : paleColor;
        foreach (var o in outlines) if (o != null) o.glowColor = c;
        if (dust != null)
        {
            var main = dust.main;
            main.startColor = new Color(c.r, c.g, c.b, 0.55f);
        }
    }

    private static DifficultyDirector cachedDirector;
    private static DifficultyDirector FindDirector()
    {
        if (cachedDirector == null) cachedDirector = FindFirstObjectByType<DifficultyDirector>();
        return cachedDirector;
    }

    private static void CacheStatics()
    {
        if (playerTransform == null)
        {
            var pi = FindFirstObjectByType<PlayerInteract>();
            if (pi != null) playerTransform = pi.transform;
        }
        if (flashlight == null) flashlight = FindFirstObjectByType<FlashlightController>();
    }

    private static Light FlashlightLight()
    {
        if (flashlight == null || !flashlight.isOn || flashlight.lightSource == null) return null;
        if (!flashlight.lightSource.activeInHierarchy) return null;

        Light l = flashlight.lightSource.GetComponent<Light>();
        return l != null && l.enabled ? l : null;
    }

    private static bool FlashlightOn() => FlashlightLight() != null;

    private bool ComputeLit(float coneFraction)
    {
        Light l = FlashlightLight();
        if (l == null) return false;

        Vector3 target = CenterPoint();
        Vector3 origin = l.transform.position;
        Vector3 to = target - origin;
        float dist = to.magnitude;
        if (dist > l.range) return false;

        if (l.type == LightType.Spot)
        {
            float angle = Vector3.Angle(l.transform.forward, to);
            if (angle > l.spotAngle * 0.5f * coneFraction) return false;
        }

        // Something solid between the light and the item (a wall, a shelf) blocks it.
        if (Physics.Linecast(origin, target, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(transform) && hit.transform != transform) return false;
        }
        return true;
    }

    private Vector3 CenterPoint()
    {
        var col = GetComponentInChildren<Collider>(true);
        if (col != null) return col.bounds.center;
        return transform.position;
    }

    // ---- dust motes -------------------------------------------------------------------------

    private void BuildDust()
    {
        if (dotTexture == null) dotTexture = MakeDotTexture();

        var go = new GameObject("EvidenceDust");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        dust = go.AddComponent<ParticleSystem>();
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = dust.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
        main.startColor = new Color(paleColor.r, paleColor.g, paleColor.b, 0.55f);
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.005f;

        var emission = dust.emission;
        emission.rateOverTime = 0f;

        var shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = dustRadius;

        var col = dust.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var noise = dust.noise;
        noise.enabled = true;
        noise.strength = 0.05f;
        noise.frequency = 0.6f;

        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.sharedMaterial = GlowPipeline.CreateParticleMaterial(dotTexture);
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;

        dust.Play();
    }

    private void UpdateDust(float strength)
    {
        if (dust == null) return;
        var emission = dust.emission;
        emission.rateOverTime = Mathf.Clamp01(strength) * dustMaxRate;
    }

    private static Texture2D MakeDotTexture()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }
}
