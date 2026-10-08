using System;
using System.Collections.Generic;
using UnityEngine;

// Session-scoped record of the case: which evidence objects exist in the level (registered by
// EvidenceItem). Static so the UI (bag, Final Inspection) can read it without lookups. Everything
// here resets when the scene reloads, which is what makes Retry a clean restart.
public static class CaseFile
{
    public static readonly List<EvidenceItem> Items = new List<EvidenceItem>();

    public static event Action Changed;

    private static EvidenceManager manager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Items.Clear();
        manager = null;
        Changed = null;
    }

    public static bool HasData => Items.Count > 0;

    public static void Register(EvidenceItem item)
    {
        if (item != null && !Items.Contains(item)) Items.Add(item);
    }

    public static void Unregister(EvidenceItem item)
    {
        Items.Remove(item);
    }

    public static EvidenceItem FindItem(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return null;
        foreach (var item in Items)
            if (item != null && item.DisplayName == displayName) return item;
        return null;
    }

    public static EvidenceData Find(string displayName)
    {
        var item = FindItem(displayName);
        return item != null ? item.data : null;
    }

    // True once the clue has actually been added to the evidence bag.
    public static bool IsInBag(string displayName)
    {
        if (manager == null) manager = UnityEngine.Object.FindFirstObjectByType<EvidenceManager>();
        return manager != null && manager.HasEvidence(displayName);
    }

    public static void NotifyChanged()
    {
        Changed?.Invoke();
    }
}
