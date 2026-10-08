using UnityEngine;

// Links an existing evidence object (one that already has EvidencePickUp or NoteObject plus an
// InspectItem) to its EvidenceData. Everything else - pickup, bag slot, HUD counter - keeps using the
// existing scripts; this only adds the case data, the unlock rule, and the "is it visible right now"
// state the glow/hidden-tape logic needs.
// Runs before EvidenceSpawner (-100) and the pickups, which all look up EvidenceOrder in their own
// Awake - so an order added from the data below is there for them to find.
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class EvidenceItem : MonoBehaviour
{
    public EvidenceData data;

    [Tooltip("Height of this object's pivot above the floor. Applied when the spawner places it on a spawn point, so items with different pivots all rest on the floor.")]
    public float groundOffset = 0f;

    [Tooltip("Scene evidence (a toppled chair, floor marks): examining it records it in the journal, but the object stays where it is instead of being 'picked up' and hidden.")]
    public bool keepInWorld = false;

    [Tooltip("For items that stay fixed in the scene: which zone their area counts as, used in hint text.")]
    public EvidenceZone fixedZone = EvidenceZone.None;

    public EvidenceZone CurrentZone { get; set; } = EvidenceZone.None;

    // Extra glow strength (0..1) set by EvidenceHintDirector when the player has been stuck for a while.
    public float HintBoost { get; set; }

    // Written by EvidenceGlow each frame; read by pickups/PlayerInteract for flashlight-only evidence.
    public bool IsLit { get; set; }

    private InspectItem inspect;
    private EvidencePickUp pickup;
    private NoteObject note;

    public string DisplayName => data != null ? data.displayName : (inspect != null ? inspect.itemName : name);
    public bool IsRequired => data != null && data.isRequired;
    public bool VisibleOnlyWhenLit => data != null && data.visibleOnlyWhenLit;
    // Set once the player has held it, so flashlight-only evidence can still be found again if dropped.
    public bool HasBeenCollected { get; set; }

    // The EvidenceOrder on the object, else the data's fallback; 0 = no place in the story order.
    public int StoryOrder
    {
        get
        {
            var order = GetComponent<EvidenceOrder>();
            if (order != null) return order.storyOrder;
            return data != null ? data.storyOrder : 0;
        }
    }

    public bool IsVisibleNow => !VisibleOnlyWhenLit || IsLit || HasBeenCollected;

    public bool IsCollected =>
        (pickup != null && pickup.isCollected) || (note != null && note.isCollected);

    private void Awake()
    {
        inspect = GetComponent<InspectItem>();
        pickup = GetComponent<EvidencePickUp>();
        note = GetComponent<NoteObject>();

        // The bag, prompts and EvidenceManager all key off InspectItem.itemName, so the data's display
        // name is the single source of truth. The description doubles as the neutral observation so
        // the existing bag description already shows something sensible.
        if (data != null && inspect != null)
        {
            inspect.itemName = data.displayName;
            inspect.category = data.category;

            // Notes are re-read from the bag using itemDescription as the note's actual text, so they
            // keep their written content there; the observation shows in the description panel.
            inspect.itemDescription = note != null ? note.noteContent : data.observation;
        }

        if (CurrentZone == EvidenceZone.None) CurrentZone = fixedZone;

        if (data != null && data.storyOrder > 0 && GetComponent<EvidenceOrder>() == null)
            gameObject.AddComponent<EvidenceOrder>().storyOrder = data.storyOrder;

        CaseFile.Register(this);
    }

    private void OnDestroy()
    {
        CaseFile.Unregister(this);
    }

    // requiredCollectedToUnlock is measured against EvidenceManager.currentEvidenceCount, which counts
    // required evidence only.
    public bool IsUnlocked(EvidenceManager manager)
    {
        if (data == null || data.requiredCollectedToUnlock <= 0) return true;
        return manager != null && manager.currentEvidenceCount >= data.requiredCollectedToUnlock;
    }

    public string LockedMessage =>
        data != null && !string.IsNullOrEmpty(data.lockedMessage) ? data.lockedMessage : "You don't know what to look for here yet.";
}
