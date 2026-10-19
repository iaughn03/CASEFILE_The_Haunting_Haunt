using UnityEngine;

// Assigns this evidence's place in the case's narrative sequence (1-10, matching the story's
// "Suicide" setup clues 1-4 then the "Homicide" reality clues 5-10). EvidenceManager gates
// collection on this order so the case unfolds the way it's meant to be read regardless of which
// room the player wanders into first - finding the physician's notes before the suicide note would
// undercut the whole reveal.
public class EvidenceOrder : MonoBehaviour
{
    [Range(1, 10)]
    public int storyOrder = 1;
}
