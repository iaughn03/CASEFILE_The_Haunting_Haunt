using UnityEngine;
using System.Collections.Generic;

public class EvidenceManager : MonoBehaviour
{
    [Header("Evidence Progress")]
    public int currentEvidenceCount = 0;
    public int maxEvidenceNeeded = 10;

    [Header("Collected Evidence")]
    public List<string> collectedEvidenceNames = new();
    public List<InspectItem.EvidenceCategory> collectedEvidenceCategories = new();

    // Lets the player only ever collect the NEXT piece of evidence in the case's narrative order -
    // see EvidenceOrder.cs. Free to explore anywhere, but the story reveals itself in sequence.
    [Tooltip("When off, evidence can be collected in any order (Level 1's free-search case). EvidenceOrder values are then ignored.")]
    public bool enforceStoryOrder = true;

    [Tooltip("When on, evidence that is still locked (story order or its data's unlock count) is fully hidden - no mesh, glow or collider. When off, it stays visible and glowing so the player can recognise it as evidence; examining it early just shows the locked message.")]
    public bool hideLockedEvidence = true;

    public bool CanCollect(int storyOrder) => !enforceStoryOrder || storyOrder <= currentEvidenceCount + 1;

    public bool ShouldReveal(bool unlocked) => unlocked || !hideLockedEvidence;

    // Both locks at once: the story order (EvidenceOrder) and the data's requiredCollectedToUnlock.
    public bool CanCollectNow(EvidenceItem item)
    {
        if (item == null) return false;
        EvidenceOrder order = item.GetComponent<EvidenceOrder>();
        return (order == null || CanCollect(order.storyOrder)) && item.IsUnlocked(this);
    }

    // Optional case evidence (EvidenceData.isRequired == false) is recorded in the bag but does not count
    // toward currentEvidenceCount, so the HUD's "Evidence: x/max" only tracks the required pieces.
    private static bool CountsTowardTotal(string evidenceName)
    {
        EvidenceData data = CaseFile.Find(evidenceName);
        return data == null || data.isRequired;
    }

    public string LockedMessage(int storyOrder)
    {
        return storyOrder <= 1
            ? "There's nothing to piece together here yet."
            : "This doesn't add up yet. There's something you need to find first.";
    }

    public void AddEvidence(string evidenceName, InspectItem.EvidenceCategory category = InspectItem.EvidenceCategory.Uncategorized)
    {
        if (collectedEvidenceNames.Contains(evidenceName))
        {
            Debug.LogWarning("Duplicate evidence detected: " + evidenceName);
            return;
        }

        collectedEvidenceNames.Add(evidenceName);
        collectedEvidenceCategories.Add(category);
        if (CountsTowardTotal(evidenceName)) currentEvidenceCount++;

        Debug.Log($"Evidence added: {evidenceName} ({currentEvidenceCount}/{maxEvidenceNeeded})");

        // TaskbarUI lives nested inside the bag panel, which is inactive whenever the bag isn't
        // open (the normal state during exploration) - the default FindFirstObjectByType skips
        // inactive objects, so evidence collected with the bag closed would silently fail to
        // refresh the on-screen count, leaving it stale until another lucky refresh happened to
        // land while the bag was open.
        TaskbarUI taskbar = FindFirstObjectByType<TaskbarUI>(FindObjectsInactive.Include);

        // a level without the old taskbar shows its count on the objective panel instead
        if (taskbar != null)
        {
            taskbar.RefreshTaskbarData();
        }

        // Tension ramp: ghost gets harder to escape once you're halfway there, world gets
        // darker in the final stretch. FindFirstObjectByType here (not cached) since AddEvidence
        // only fires a handful of times per playthrough - not worth a Start()-time lookup.
        // >= rather than == - DifficultyDirector already guards each threshold against firing
        // more than once, so this only needs to catch "has crossed" without depending on landing
        // on the exact count (evidence could conceivably be granted in a way that skips a value).
        DifficultyDirector director = FindFirstObjectByType<DifficultyDirector>();
        if (director != null)
        {
            if (currentEvidenceCount >= maxEvidenceNeeded / 2)
            {
                director.OnHalfEvidenceCollected();
            }

            if (currentEvidenceCount >= maxEvidenceNeeded - 3)
            {
                director.OnThreeEvidenceRemaining();
            }
        }

        ObjectiveTracker tracker = FindFirstObjectByType<ObjectiveTracker>(FindObjectsInactive.Include);
        if (tracker != null) tracker.Refresh();

        RefreshEvidenceLockStates();
        CaseFile.NotifyChanged();
    }

    public bool HasEvidence(string evidenceName)
    {
        return collectedEvidenceNames.Contains(evidenceName);
    }

    public void RemoveEvidence(string evidenceName)
    {
        if (!collectedEvidenceNames.Contains(evidenceName))
        {
            Debug.LogWarning("Cannot remove. Evidence not found: " + evidenceName);
            return;
        }

        int index = collectedEvidenceNames.IndexOf(evidenceName);
        collectedEvidenceNames.RemoveAt(index);
        collectedEvidenceCategories.RemoveAt(index);
        if (CountsTowardTotal(evidenceName)) currentEvidenceCount = Mathf.Max(0, currentEvidenceCount - 1);

        Debug.Log($"Evidence removed: {evidenceName} ({currentEvidenceCount}/{maxEvidenceNeeded})");

        // TaskbarUI lives nested inside the bag panel, which is inactive whenever the bag isn't
        // open (the normal state during exploration) - the default FindFirstObjectByType skips
        // inactive objects, so evidence collected with the bag closed would silently fail to
        // refresh the on-screen count, leaving it stale until another lucky refresh happened to
        // land while the bag was open.
        TaskbarUI taskbar = FindFirstObjectByType<TaskbarUI>(FindObjectsInactive.Include);

        if (taskbar != null)
        {
            taskbar.RefreshTaskbarData();
        }

        ObjectiveTracker tracker = FindFirstObjectByType<ObjectiveTracker>(FindObjectsInactive.Include);
        if (tracker != null) tracker.Refresh();

        RefreshEvidenceLockStates();
    }

    // Re-checks every not-yet-collected piece of evidence against the new count so whichever one
    // just became "next in line" reveals its glow/collider, and (on RemoveEvidence, e.g. a dropped
    // item never picked back up) anything that just fell out of reach hides again.
    private void RefreshEvidenceLockStates()
    {
        foreach (EvidencePickUp pickup in FindObjectsByType<EvidencePickUp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            pickup.RefreshLockState();
        }

        foreach (NoteObject note in FindObjectsByType<NoteObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            note.RefreshLockState();
        }
    }
}