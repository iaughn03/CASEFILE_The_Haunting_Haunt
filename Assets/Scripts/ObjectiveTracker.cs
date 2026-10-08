using UnityEngine;
using TMPro;

// Always-visible HUD objective. Without this, hunting for evidence with no waypoints and no sense
// of progress reads as aimless wandering - this gives the player a running count and a location
// hint for whichever piece of evidence the case's narrative order (see EvidenceOrder.cs) says is
// next, plus the win condition once everything's been found.
public class ObjectiveTracker : MonoBehaviour
{
    [Header("References")]
    public EvidenceManager evidenceManager;
    public TMP_Text objectiveText;
    public TMP_Text hintText;

    [Header("Wording")]
    [Tooltip("Shown once every clue is held. Level 1 leaves by the Main Door; Level 2 by the Marsh house's front door, so the line can't name a fixed door.")]
    public string allFoundHint = "You have what you need. Go to the Main Door.";
    [Tooltip("Shown while clues are still missing.")]
    public string searchingHint = "Look for anything the report missed.";

    // Matches the case's narrative order 1-10 - the location each piece of evidence is described
    // as being found at in the story.
    private static readonly string[] LocationHints =
    {
        "Search the Director's Office",
        "Check the Pharmacy Counter",
        "Investigate Ward 1",
        "Search the Hanging Ward",
        "Look outside the Ward Door",
        "Shine your flashlight on the ceiling beam",
        "Search the Staff Breakroom",
        "Check a broken pipe in the Machine Room",
        "Search the Basement Safe",
        "Check the Doctor's Desk",
    };

    private void Start()
    {
        if (evidenceManager == null) evidenceManager = FindFirstObjectByType<EvidenceManager>();
        Refresh();
    }

    // Lets other systems (e.g. a cabinet's found-clue text) surface the same current hint the HUD
    // is already showing, without a second copy of the location list drifting out of sync with it.
    // Set by EvidenceHintDirector when the player has been stuck; cleared on the next pickup.
    private string hintOverride;

    public void SetHintOverride(string text)
    {
        hintOverride = text;
        Refresh();
    }

    public void ClearHintOverride()
    {
        if (hintOverride == null) return;
        hintOverride = null;
        Refresh();
    }

    // Level 1's evidence can be found in any order, so the fixed per-count location list below doesn't apply.
    private string CaseHint(int count, int max)
    {
        if (!string.IsNullOrEmpty(hintOverride)) return hintOverride;
        if (count >= max) return allFoundHint;
        return searchingHint;
    }

    public string CurrentHint()
    {
        if (evidenceManager == null) return "";

        int count = evidenceManager.currentEvidenceCount;
        int max = evidenceManager.maxEvidenceNeeded;

        if (CaseFile.HasData) return CaseHint(count, max);

        if (count >= max) return "All evidence found. Return to the Main Door.";
        if (count < LocationHints.Length) return LocationHints[count];
        return "";
    }

    public void Refresh()
    {
        if (evidenceManager == null) return;

        int count = evidenceManager.currentEvidenceCount;
        int max = evidenceManager.maxEvidenceNeeded;

        if (objectiveText != null)
            objectiveText.text = $"Evidence: {count}/{max}";

        if (hintText != null && CaseFile.HasData)
        {
            hintText.text = CaseHint(count, max);
        }
        else if (hintText != null)
        {
            if (count >= max)
            {
                hintText.text = "All evidence found. Return to the Main Door.";
            }
            else if (count < LocationHints.Length)
            {
                hintText.text = LocationHints[count];
            }
        }
    }
}
