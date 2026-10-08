using UnityEngine;

public class InspectItem : MonoBehaviour
{
    // APPEND ONLY - serialized as an int on every EvidenceData asset and InspectItem in both levels.
    // Identity/Scene were added for Level 2: who the clue points at, and clues that only describe the
    // state of the scene itself (the staged break-in) without bearing on means, motive or opportunity.
    public enum EvidenceCategory { Uncategorized, Means, Motive, Opportunity, Identity, Scene }

    [Header("Item Data")]
    public string itemName = "Generic Item";

    [TextArea]
    public string itemDescription = "Just an ordinary object.";

    public Sprite itemIcon;

    public bool isKey = false;

    [Header("Case File")]
    public EvidenceCategory category = EvidenceCategory.Uncategorized;

    public void StartInspecting(Component caller = null)
    {
        Debug.Log("Started inspecting: " + itemName);
    }

    public void StopInspecting()
    {
        Debug.Log("Stopped inspecting: " + itemName);
    }
}