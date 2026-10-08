using System;
using System.Collections.Generic;
using UnityEngine;

// The valid red-string connections on the Final Inspection's Case Wall, kept as data so the case can be
// tuned without touching code. A link is valid whichever clue the player drags from.
[CreateAssetMenu(menuName = "Case/Case Link Set", fileName = "CaseLinks")]
public class CaseLinkSet : ScriptableObject
{
    [Serializable]
    public class Link
    {
        public EvidenceData clueA;
        public EvidenceData clueB;
        [Tooltip("The one line shown on the string once the link is made.")]
        [TextArea(1, 2)] public string proves;

        public bool Matches(EvidenceData a, EvidenceData b)
        {
            if (a == null || b == null || clueA == null || clueB == null) return false;
            return (clueA == a && clueB == b) || (clueA == b && clueB == a);
        }
    }

    public List<Link> links = new List<Link>();

    public Link Find(EvidenceData a, EvidenceData b)
    {
        foreach (var l in links) if (l.Matches(a, b)) return l;
        return null;
    }
}
