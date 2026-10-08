using UnityEngine;

// Marks a validated floor position where random evidence may be placed. The transform position is the
// FLOOR point (EvidenceSpawner adds each item's own groundOffset). Points are tagged with a zone; the
// spawner only uses a point for items whose EvidenceData allows that zone.
public class EvidenceSpawnPoint : MonoBehaviour
{
    public EvidenceZone zone = EvidenceZone.None;

    [Tooltip("Walking distance (m) from the player's start to this point, around walls and through doors. 0 = not measured, and EvidenceSpawner falls back to straight-line distance, which badly underrates rooms that are close through a wall but a long way round on foot. Re-measure if the player start or the level layout changes.")]
    public float pathDistanceFromStart = 0f;

    [Tooltip("A hiding place (a shelf inside a cupboard). The spawner offers these to an item before the open-floor points of the same zone, so some evidence is always tucked away rather than only when the dice happen to land there.")]
    public bool fillFirst = false;

    [Tooltip("Reserve this point for one evidence item. That item then spawns only on its own reserved points (one picked at random each run, fixed scene clues included), placed with this point's rotation, and no other item ever uses the point. The position is still the floor point: the item's groundOffset is added on top.")]
    public EvidenceItem reservedFor;

    private static Color ZoneColor(EvidenceZone z)
    {
        switch (z)
        {
            case EvidenceZone.PharmacyCounter: return new Color(0.3f, 0.9f, 0.5f);
            case EvidenceZone.BackStorage: return new Color(0.9f, 0.7f, 0.3f);
            case EvidenceZone.Office: return new Color(0.4f, 0.6f, 1f);
            case EvidenceZone.Entrance: return new Color(1f, 0.5f, 0.9f);
            case EvidenceZone.StruggleArea: return new Color(1f, 0.35f, 0.3f);
            case EvidenceZone.HiddenSpot: return new Color(0.75f, 0.5f, 1f);
            default: return Color.white;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = ZoneColor(zone);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.15f, 0.15f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.6f);
    }
}
