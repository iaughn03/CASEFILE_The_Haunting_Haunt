using System.Collections;
using UnityEngine;

// Climbing through an open WindowController window with the jump key. PlayerMovement asks TryClimb()
// before its own jump/vault, and stands down while IsClimbing (same pattern as PlayerVaultController).
// The player is lifted onto the sill, ducks through, and is set down on the far side: straight onto
// the floor/ground when it's close under the sill, otherwise released at sill height to fall, so
// jumping out of an upper floor window is a real drop (see PlayerFallDamage).
[RequireComponent(typeof(CharacterController))]
public class PlayerWindowClimb : MonoBehaviour
{
    [Header("Reach")]
    [Tooltip("Furthest the player's centre can be from the window (flat distance to the sash plane).")]
    public float reachDistance = 1.4f;
    [Tooltip("Sill height above the player's feet that can still be climbed onto.")]
    public float maxSillAboveFeet = 2.1f;
    [Tooltip("Below this the sill is under the player's knees, so it's no climb (stops a window from being 'climbed' from the floor above it).")]
    public float minSillAboveFeet = 0.4f;
    [Tooltip("How directly the player has to face the window (dot of the look direction and the way through).")]
    [Range(0f, 1f)] public float minFacing = 0.4f;
    [Tooltip("How far open the sashes must be before the gap counts as climbable.")]
    [Range(0f, 1f)] public float minOpenAmount = 0.8f;

    [Header("Climb")]
    public float mountDuration = 0.3f;
    public float crossDuration = 0.5f;
    [Tooltip("Seconds per metre for the short step down onto the floor/ground on the far side.")]
    public float dropSecondsPerMetre = 0.18f;
    [Tooltip("How far past the window the player is set down; further spots are tried if this one is blocked.")]
    public float landingDistance = 0.9f;
    [Tooltip("If the floor/ground on the far side is within this distance under the sill, the player is set down on it. Further down, they're released at sill height and fall.")]
    public float stepDownLimit = 1.9f;
    [Tooltip("How far the view ducks while squeezing through the frame.")]
    public float crouchDip = 0.9f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] climbClips;
    [Range(0f, 1f)] public float climbVolume = 0.6f;

    public bool IsClimbing { get; private set; }

    private CharacterController controller;
    private PlayerMovement movement;
    private PlayerHealth health;
    private readonly Collider[] overlapHits = new Collider[16];
    private readonly RaycastHit[] groundHits = new RaycastHit[16];

    private struct Plan
    {
        public WindowController window;
        public Vector3 into;        // level direction through the window, away from the player's side
        public Vector3 mount;       // transform positions (capsule centre), not feet
        public Vector3 land;
        public Vector3 settle;
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        movement = GetComponent<PlayerMovement>();
        health = GetComponent<PlayerHealth>();
    }

    // Used by PlayerInteract to show the climb prompt.
    public bool CanClimb()
    {
        return !IsClimbing && controller.enabled && FindPlan(out _);
    }

    public bool TryClimb()
    {
        if (IsClimbing || !controller.enabled) return false;
        if (!FindPlan(out Plan plan)) return false;
        StartCoroutine(ClimbRoutine(plan));
        return true;
    }

    float FeetY() => transform.TransformPoint(controller.center).y - controller.height * 0.5f * Mathf.Abs(transform.lossyScale.y);

    float Radius()
    {
        Vector3 s = transform.lossyScale;
        return controller.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
    }

    bool FindPlan(out Plan plan)
    {
        plan = default;
        float feetY = FeetY();
        float pivotAboveFeet = transform.position.y - feetY;
        float radius = Radius();
        Vector3 look = transform.forward;
        look.y = 0f;
        look.Normalize();

        WindowController best = null;
        float bestDistance = float.MaxValue;
        Vector3 bestInto = Vector3.zero;
        float bestLateral = 0f;

        foreach (WindowController w in WindowController.All)
        {
            if (w == null || w.OpenAmount < minOpenAmount) continue;

            Vector3 offset = transform.position - w.Center;
            offset.y = 0f;
            float side = Vector3.Dot(offset, w.Normal);
            float distance = Mathf.Abs(side);
            if (distance > reachDistance || distance >= bestDistance) continue;

            float lateral = Vector3.Dot(offset, w.Right);
            if (Mathf.Abs(lateral) > w.HalfWidth - radius) continue;

            float sillAbove = w.SillY - feetY;
            if (sillAbove < minSillAboveFeet || sillAbove > maxSillAboveFeet) continue;

            Vector3 into = side >= 0f ? -w.Normal : w.Normal;
            if (Vector3.Dot(look, into) < minFacing) continue;

            best = w;
            bestDistance = distance;
            bestInto = into;
            bestLateral = lateral;
        }

        if (best == null) return false;

        float laneLimit = Mathf.Max(0f, best.HalfWidth - radius - 0.1f);
        Vector3 lane = best.Center + best.Right * Mathf.Clamp(bestLateral, -laneLimit, laneLimit);
        lane.y = 0f;
        float sillFeet = best.SillY + 0.05f;

        plan.window = best;
        plan.into = bestInto;
        plan.mount = lane - bestInto * 0.25f + Vector3.up * (sillFeet + pivotAboveFeet);

        bool haveFallback = false;
        Vector3 fallbackLand = Vector3.zero;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            Vector3 landFeet = lane + bestInto * (landingDistance + attempt * 0.35f) + Vector3.up * sillFeet;
            Vector3 restFeet = landFeet;

            bool hasGround = GroundBelow(landFeet, 30f, out float groundY);
            bool stepDown = hasGround && sillFeet - groundY <= stepDownLimit;
            if (stepDown) restFeet.y = groundY + 0.02f;

            if (!CapsuleFree(restFeet, radius, best)) continue;

            // A long drop should reach the ground, not catch on a lamp post or fence top beside
            // the wall: try further out first, and only use a snagged spot if nothing else fits.
            if (!stepDown && hasGround && !DropIsClear(landFeet, groundY, radius))
            {
                if (!haveFallback) { haveFallback = true; fallbackLand = landFeet; }
                continue;
            }

            plan.land = landFeet + Vector3.up * pivotAboveFeet;
            plan.settle = restFeet + Vector3.up * pivotAboveFeet;
            return true;
        }

        if (!haveFallback) return false;
        plan.land = plan.settle = fallbackLand + Vector3.up * pivotAboveFeet;
        return true;
    }

    bool DropIsClear(Vector3 feet, float groundY, float radius)
    {
        Vector3 origin = feet + Vector3.up * (radius + 0.05f);
        float distance = feet.y - groundY;
        int count = Physics.SphereCastNonAlloc(origin, radius * 0.95f, Vector3.down, groundHits, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = groundHits[i].collider;
            if (c == null || c.transform.IsChildOf(transform)) continue;
            if (groundHits[i].point.y > groundY + 0.3f) return false;
        }
        return true;
    }

    bool GroundBelow(Vector3 feet, float range, out float groundY)
    {
        groundY = float.MinValue;
        Vector3 origin = feet + Vector3.up * 0.3f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, range + 0.3f, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider c = groundHits[i].collider;
            if (c == null || c.transform.IsChildOf(transform)) continue;
            if (groundHits[i].distance < nearest)
            {
                nearest = groundHits[i].distance;
                groundY = groundHits[i].point.y;
            }
        }
        return nearest < float.MaxValue;
    }

    bool CapsuleFree(Vector3 feet, float radius, WindowController window)
    {
        float height = controller.height * Mathf.Abs(transform.lossyScale.y);
        Vector3 bottom = feet + Vector3.up * (radius + 0.05f);
        Vector3 top = feet + Vector3.up * Mathf.Max(radius + 0.05f, height - radius - 0.05f);
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius * 0.95f, overlapHits, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = overlapHits[i];
            if (c == null || c.transform.IsChildOf(transform)) continue;
            if (window.OwnsCollider(c)) continue;
            return false;
        }
        return true;
    }

    IEnumerator ClimbRoutine(Plan plan)
    {
        IsClimbing = true;
        controller.enabled = false;

        if (audioSource != null && climbClips != null && climbClips.Length > 0)
            audioSource.PlayOneShot(climbClips[Random.Range(0, climbClips.Length)], climbVolume * SfxVolume.Multiplier);

        Transform cam = movement != null && movement.playerCamera != null ? movement.playerCamera.transform : null;
        Vector3 camStart = cam != null ? cam.localPosition : Vector3.zero;
        Vector3 camDucked = camStart + Vector3.down * crouchDip;

        Vector3 start = transform.position;
        Quaternion startRot = transform.rotation;
        Quaternion faceRot = Quaternion.LookRotation(plan.into, Vector3.up);

        // 1. Up onto the sill, turning square to the window and ducking under the frame.
        float t = 0f;
        while (t < 1f)
        {
            if (Interrupted()) break;
            t = Mathf.Min(1f, t + Time.deltaTime / mountDuration);
            float e = Mathf.SmoothStep(0f, 1f, t);
            transform.position = Vector3.Lerp(start, plan.mount, e);
            transform.rotation = Quaternion.Slerp(startRot, faceRot, e);
            if (cam != null) cam.localPosition = Vector3.Lerp(camStart, camDucked, e);
            yield return null;
        }

        // 2. Through the frame, straightening up once clear of the wall.
        t = 0f;
        while (t < 1f && !Interrupted())
        {
            t = Mathf.Min(1f, t + Time.deltaTime / crossDuration);
            transform.position = Vector3.Lerp(plan.mount, plan.land, Mathf.SmoothStep(0f, 1f, t));
            if (cam != null) cam.localPosition = Vector3.Lerp(camDucked, camStart, Mathf.InverseLerp(0.55f, 1f, t));
            yield return null;
        }

        // 3. Short step down onto the floor/ground. A long drop is left to gravity instead.
        float drop = plan.land.y - plan.settle.y;
        if (drop > 0.01f && !Interrupted())
        {
            float duration = Mathf.Max(0.08f, drop * dropSecondsPerMetre);
            t = 0f;
            while (t < 1f && !Interrupted())
            {
                t = Mathf.Min(1f, t + Time.deltaTime / duration);
                transform.position = Vector3.Lerp(plan.land, plan.settle, t * t);
                yield return null;
            }
        }

        if (cam != null) cam.localPosition = camStart;
        controller.enabled = true;
        IsClimbing = false;
    }

    bool Interrupted() => health != null && health.IsDead;
}
