using UnityEngine;

// How much a clue is worth when the player cites it as proof at the ruling. Never shown while playing.
public enum ProofStrength
{
    Weak = 0,
    Supporting = 1,
    Strong = 2,
}

// One clue in the case, as data. Attached to a world object through EvidenceItem. The text is kept
// deliberately non-graphic and never describes a method. While playing, the player only ever sees the
// neutral `observation`; what a clue really means (endingMeaning) is revealed only in the ending.
[CreateAssetMenu(menuName = "Case/Evidence Data", fileName = "EvidenceData")]
public class EvidenceData : ScriptableObject
{
    public string id;
    [Tooltip("Shown in prompts, the bag and the ruling screen. Must match InspectItem.itemName on the world object (EvidenceItem keeps them in sync).")]
    public string displayName;
    public InspectItem.EvidenceCategory category = InspectItem.EvidenceCategory.Uncategorized;
    [Tooltip("Required evidence counts toward the HUD's Evidence: x/max (EvidenceManager.maxEvidenceNeeded). Optional evidence is recorded in the bag but doesn't count.")]
    public bool isRequired = true;
    [Tooltip("Zones this item may randomly spawn in. Leave empty for an item that stays fixed at its place in the scene.")]
    public EvidenceZone[] allowedZones;
    [Tooltip("Fallback story order (1-10) for a scene object that has no EvidenceOrder component - EvidenceItem adds one at startup. 0 = none. An EvidenceOrder already on the object wins.")]
    public int storyOrder = 0;

    [Header("What the player sees")]
    [Tooltip("Only what is physically visible - no conclusions, no hints about what it means. Shown under \"Observation\" in the bag.")]
    [TextArea(2, 12)] public string observation;
    [Tooltip("A plain fact the investigator would know about this kind of clue, plus a leading question. Points toward what it could connect to without naming the answer. Shown under the observation in the bag and in the Case File.")]
    [TextArea(2, 8)] public string clueNote;

    [Header("Revealed only in the ending")]
    [Tooltip("What the clue really meant. Shown on the ending screen after the ruling has been graded.")]
    [TextArea(2, 6)] public string endingMeaning;
    [Tooltip("How good a piece of proof this is when the player cites it at the ruling.")]
    public ProofStrength proofStrength = ProofStrength.Supporting;
    [Tooltip("A clue that looks like it points somewhere but doesn't hold up. Marked as such in the ending.")]
    public bool misleading = false;

    [Header("Availability")]
    [Tooltip("Number of REQUIRED items the player must already hold before this can be examined. 0 = always available.")]
    public int requiredCollectedToUnlock = 0;
    [TextArea(1, 3)] public string lockedMessage = "You don't know what to look for here yet.";
    [Tooltip("Only visible (and interactable) while the player's flashlight is on it.")]
    public bool visibleOnlyWhenLit = false;

    public bool IsPortable => allowedZones != null && allowedZones.Length > 0;

    public bool AllowsZone(EvidenceZone zone)
    {
        if (allowedZones == null) return false;
        foreach (var z in allowedZones) if (z == zone) return true;
        return false;
    }
}
