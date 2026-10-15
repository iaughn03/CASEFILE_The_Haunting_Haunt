using UnityEngine;

// Hurts the player for landing after a long drop (jumping out of an upper floor window, off a
// stairwell edge, a balcony). Used in every game.
//
// The fall height is the height lost between leaving the ground and landing (so a jump's own arc never
// counts: Game1's 2.5 m jump off a table is a 1 m fall, not 3.5), capped by the most open air ever
// measured under the feet on the way down. The cap is what keeps stairs harmless: sprinting down a
// steep flight is a chain of hops that can each lose 3 m+, but the steps are never more than about a
// metre below the feet. A real drop has the whole height of open air underneath.
//
// Anything that moves the player by hand (vaults, window climbs, hiding spots, the intro) resets the
// measurement, so a teleport is never mistaken for a fall.
[DefaultExecutionOrder(100)] // after PlayerMovement has moved the controller this frame
[RequireComponent(typeof(CharacterController))]
public class PlayerFallDamage : MonoBehaviour
{
    [Tooltip("Drops up to this height (metres) are harmless.")]
    public float safeFallHeight = 3f;
    [Tooltip("Damage for a drop just past safeFallHeight.")]
    public float minDamage = 15f;
    [Tooltip("Extra damage for every metre fallen beyond safeFallHeight.")]
    public float damagePerMetre = 15f;
    [Tooltip("How far down the open air under the feet is measured. Nothing below within this range (a hole in the map) counts as this much.")]
    public float clearanceProbeRange = 60f;

    private CharacterController controller;
    private PlayerMovement movement;
    private PlayerVaultController vault;
    private PlayerWindowClimb windowClimb;
    private PlayerHealth health;

    private bool airborne;
    private float takeoffFeetY;
    private float lastGroundedFeetY;
    private bool hasGroundedFeet;
    private float maxClearance;
    private readonly RaycastHit[] probeHits = new RaycastHit[16];

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        movement = GetComponent<PlayerMovement>();
        vault = GetComponent<PlayerVaultController>();
        windowClimb = GetComponent<PlayerWindowClimb>();
        health = GetComponent<PlayerHealth>();
    }

    float FeetY() => transform.TransformPoint(controller.center).y - controller.height * 0.5f * Mathf.Abs(transform.lossyScale.y);

    // Open air between the feet and whatever is straight below them.
    float ClearanceBelow(float feetY)
    {
        Vector3 origin = new Vector3(transform.position.x, feetY + 0.3f, transform.position.z);
        int count = Physics.SphereCastNonAlloc(origin, 0.15f, Vector3.down, probeHits, clearanceProbeRange, ~0, QueryTriggerInteraction.Ignore);
        float nearest = clearanceProbeRange;
        for (int i = 0; i < count; i++)
        {
            Collider c = probeHits[i].collider;
            if (c == null || c.transform.IsChildOf(transform)) continue;
            if (probeHits[i].distance <= 0f) continue; // started inside it (a wall the capsule is brushing)
            nearest = Mathf.Min(nearest, probeHits[i].distance);
        }
        return Mathf.Max(0f, nearest - 0.3f);
    }

    void Update()
    {
        bool handMoved = !controller.enabled
            || (movement != null && (!movement.enabled || movement.movementLocked))
            || (vault != null && vault.isVaulting)
            || (windowClimb != null && windowClimb.IsClimbing)
            || IntroSequence.IsPlaying
            || (health != null && health.IsDead);

        if (handMoved)
        {
            airborne = false;
            hasGroundedFeet = false;
            return;
        }

        float feet = FeetY();

        if (!controller.isGrounded)
        {
            if (!airborne)
            {
                airborne = true;
                // Where the feet left the ground, or (after a window climb / teleport) where the
                // player was let go in the air.
                takeoffFeetY = hasGroundedFeet ? lastGroundedFeetY : feet;
                maxClearance = 0f;
            }
            maxClearance = Mathf.Max(maxClearance, ClearanceBelow(feet));
            hasGroundedFeet = false;
            return;
        }

        lastGroundedFeetY = feet;
        hasGroundedFeet = true;

        if (!airborne) return;
        airborne = false;

        float fallen = Mathf.Min(takeoffFeetY - feet, maxClearance);
        if (fallen <= safeFallHeight || health == null) return;

        float damage = minDamage + (fallen - safeFallHeight) * damagePerMetre;
        health.TakeFallDamage(damage);
    }
}
