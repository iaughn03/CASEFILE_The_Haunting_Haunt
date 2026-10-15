using UnityEngine;

public class EvidencePickUp : MonoBehaviour
{
    private InspectItem inspectItem;

    // Single source of truth for this evidence's name lives on InspectItem.itemName
    public string EvidenceName =>
        inspectItem != null ? inspectItem.itemName : "Unknown Evidence";

    [HideInInspector]
    public bool isCollected = false;

    [Tooltip("Only interactable while the player's flashlight is on - for evidence like faint marks that are only visible under direct light.")]
    public bool requiresFlashlight = false;

    private EvidenceOrder order;
    private EvidenceItem caseItem;
    private EvidenceManager evidenceManager;

    private void Awake()
    {
        inspectItem = GetComponent<InspectItem>();
        order = GetComponent<EvidenceOrder>();
        caseItem = GetComponent<EvidenceItem>();
    }

    private void Start()
    {
        RefreshLockState();
    }

    // Only the next piece of evidence the case's narrative order (see EvidenceOrder.cs) has
    // actually reached should be visible/glowing in the world - everything further ahead stays
    // fully hidden (no glow, no collider) instead of letting the player see or click evidence the
    // story hasn't gotten to yet. EvidenceManager calls this on every piece whenever the collected
    // count changes, so the next item in line reveals itself the moment it unlocks.
    public void RefreshLockState()
    {
        if (isCollected || (order == null && caseItem == null)) return;

        if (evidenceManager == null) evidenceManager = FindFirstObjectByType<EvidenceManager>();
        if (evidenceManager == null) return;

        bool unlocked = order == null || evidenceManager.CanCollect(order.storyOrder);
        if (caseItem != null) unlocked &= caseItem.IsUnlocked(evidenceManager);
        // With hideLockedEvidence off, locked evidence stays visible and clickable; Interact() then
        // shows the locked message instead of collecting it.
        bool revealed = evidenceManager.ShouldReveal(unlocked);
        // Flashlight-only evidence (the hidden tape) stays invisible until EvidenceGlow says it's lit.
        bool shown = revealed && (caseItem == null || caseItem.IsVisibleNow);

        // Queried fresh every call rather than cached - ItemGlowOutline destroys and rebuilds its
        // own outline-mesh Renderer children every time its `enabled` flips, so a cached renderer
        // list here would go stale after the first lock/unlock and throw MissingReferenceException
        // on the next call.
        foreach (Collider col in GetComponentsInChildren<Collider>(true)) col.enabled = revealed;
        foreach (Renderer rend in GetComponentsInChildren<Renderer>(true))
        {
            if (rend is ParticleSystemRenderer) continue; // EvidenceGlow's dust manages itself
            rend.enabled = shown;
        }
        foreach (ItemGlowOutline glow in GetComponentsInChildren<ItemGlowOutline>(true)) glow.enabled = revealed;
    }

    public void Interact()
    {
        if (isCollected) return;

        PlayerInteract player =
            FindFirstObjectByType<PlayerInteract>();

        if (caseItem != null)
        {
            EvidenceManager caseManager = FindFirstObjectByType<EvidenceManager>();
            if (!caseItem.IsUnlocked(caseManager))
            {
                if (player != null) player.DisplayNotification(caseItem.LockedMessage);
                return;
            }
            if (!caseItem.IsVisibleNow) return;
        }

        // Case unfolds in narrative order (see EvidenceOrder.cs) - block collecting ahead of
        // where the story has actually gotten to, regardless of which room the player found this in.
        EvidenceOrder order = GetComponent<EvidenceOrder>();
        if (order != null)
        {
            EvidenceManager em = FindFirstObjectByType<EvidenceManager>();
            if (em != null && !em.CanCollect(order.storyOrder))
            {
                if (player != null) player.DisplayNotification(em.LockedMessage(order.storyOrder));
                return;
            }
        }

        isCollected = true;

        if (player != null && inspectItem != null)
        {
            // Add to inventory (and count as evidence inside CollectEvidence)
            player.CollectEvidence(inspectItem);

            // Disable (not destroy) the collider/renderer immediately - a second E-press (easy to
            // do while the prompt is still showing) would otherwise hit this same object again
            // before Destroy() takes effect at end of frame, firing Interact() twice and logging a
            // spurious "duplicate evidence" warning. Keeping the object alive (just hidden) rather
            // than destroying it also lets the bag's 3D inspection view borrow this same object
            // later instead of needing a separate display copy of every evidence type.
            // GetComponentsInChildren, not GetComponent - the actual mesh/collider on evidence like
            // PillBottle lives on a child object, not the root, so a root-only lookup silently does
            // nothing and leaves the picked-up item fully visible in the world.
            foreach (Collider col in GetComponentsInChildren<Collider>()) col.enabled = false;

            // Scene evidence (a toppled chair, floor marks) stays visible where it is - it is only
            // recorded in the journal, not carried away.
            bool stayVisible = caseItem != null && caseItem.keepInWorld;
            if (!stayVisible)
            {
                foreach (Renderer rend in GetComponentsInChildren<Renderer>()) rend.enabled = false;
                FreezeBodies();
            }

            // The glow outline is a "not-yet-picked-up" affordance - without disabling it here,
            // it would silently reappear later whenever the 3D inspection view re-enables this
            // same object's renderers to display it, since the outline mirrors renderer.enabled.
            foreach (ItemGlowOutline glow in GetComponentsInChildren<ItemGlowOutline>(true)) glow.enabled = false;
        }
        else
        {
            Debug.LogWarning(
                "PlayerInteract or InspectItem missing!"
            );
        }
    }

    // A falling body (Game1's pill bottle has gravity on) with its colliders switched off dropped
    // through the floor for as long as it sat in the bag, gathering speed the whole time - so the
    // 3D view borrowed it from somewhere far below the map, and dropping it put it back in the world
    // still moving at that speed. Held still while collected; ReleaseBodies hands it back on drop.
    private Rigidbody[] frozenBodies;

    private void FreezeBodies()
    {
        var bodies = new System.Collections.Generic.List<Rigidbody>();
        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb.isKinematic) continue;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            bodies.Add(rb);
        }
        frozenBodies = bodies.ToArray();
    }

    public void ReleaseBodies()
    {
        if (frozenBodies == null) return;
        foreach (Rigidbody rb in frozenBodies)
            if (rb != null) rb.isKinematic = false;
        frozenBodies = null;
    }
}