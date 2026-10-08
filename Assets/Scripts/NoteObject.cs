using UnityEngine;

public class NoteObject : MonoBehaviour
{
    [Header("Written Contents")]
    [TextArea(8, 12)]
    public string noteContent = "Type your mystery text clue here...";

    [HideInInspector]
    public bool isCollected = false;

    private EvidenceOrder order;
    private EvidenceItem caseItem;
    private EvidenceManager evidenceManager;

    private void Awake()
    {
        order = GetComponent<EvidenceOrder>();
        caseItem = GetComponent<EvidenceItem>();
    }

    private void Start()
    {
        RefreshLockState();
    }

    // Mirrors EvidencePickUp.RefreshLockState() - a note further ahead in the case's narrative
    // order (see EvidenceOrder.cs) than the story has actually reached stays fully hidden (no glow,
    // no collider) instead of being readable/visible before its turn.
    public void RefreshLockState()
    {
        if (isCollected || (order == null && caseItem == null)) return;

        if (evidenceManager == null) evidenceManager = FindFirstObjectByType<EvidenceManager>();
        if (evidenceManager == null) return;

        bool unlocked = order == null || evidenceManager.CanCollect(order.storyOrder);
        if (caseItem != null) unlocked &= caseItem.IsUnlocked(evidenceManager);
        bool revealed = evidenceManager.ShouldReveal(unlocked);

        // Queried fresh every call rather than cached - ItemGlowOutline destroys and rebuilds its
        // own outline-mesh Renderer children every time its `enabled` flips, so a cached renderer
        // list here would go stale after the first lock/unlock and throw MissingReferenceException
        // on the next call.
        foreach (Collider col in GetComponentsInChildren<Collider>(true)) col.enabled = revealed;
        foreach (Renderer rend in GetComponentsInChildren<Renderer>(true)) rend.enabled = revealed;
        foreach (ItemGlowOutline glow in GetComponentsInChildren<ItemGlowOutline>(true)) glow.enabled = revealed;
    }

    public void Interact()
    {
        if (isCollected) return;

        if (caseItem != null)
        {
            EvidenceManager caseManager = FindFirstObjectByType<EvidenceManager>();
            if (!caseItem.IsUnlocked(caseManager))
            {
                PlayerInteract p = FindFirstObjectByType<PlayerInteract>();
                if (p != null) p.DisplayNotification(caseItem.LockedMessage);
                return;
            }
        }

        // Case unfolds in narrative order (see EvidenceOrder.cs) - block reading this ahead of
        // where the story has actually gotten to, regardless of which room it was found in.
        EvidenceOrder order = GetComponent<EvidenceOrder>();
        if (order != null)
        {
            EvidenceManager em = FindFirstObjectByType<EvidenceManager>();
            if (em != null && !em.CanCollect(order.storyOrder))
            {
                PlayerInteract player = FindFirstObjectByType<PlayerInteract>();
                if (player != null) player.DisplayNotification(em.LockedMessage(order.storyOrder));
                return;
            }
        }

        if (NoteSystem.Instance != null)
        {
            // Sends the text and this object's reference directly to the overlay system
            NoteSystem.Instance.InspectNoteInFace(noteContent, this);
        }
    }

    // Hides the physical note (kept alive, not destroyed, so it can be placed back in the world if
    // the player later drops it from their inventory - mirrors EvidencePickUp/KeyItem).
    public void Hide()
    {
        isCollected = true;

        foreach (Collider col in GetComponentsInChildren<Collider>()) col.enabled = false;
        foreach (Renderer rend in GetComponentsInChildren<Renderer>()) rend.enabled = false;
        foreach (ItemGlowOutline glow in GetComponentsInChildren<ItemGlowOutline>(true)) glow.enabled = false;
    }
}