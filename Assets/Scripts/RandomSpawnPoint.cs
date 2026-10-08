using UnityEngine;

// Attached to a world pickup (evidence or key) that should spawn at a different nearby spot each
// playthrough instead of always sitting in the exact same place. Picks one of several pre-placed
// candidate positions in Awake() - before Start()/raycasts/anything else can query this object's
// position - so every other system just sees the item already sitting at its chosen spot.
public class RandomSpawnPoint : MonoBehaviour
{
    [Tooltip("Candidate world positions this item can spawn at - one is picked at random each session.")]
    public Vector3[] candidatePositions;

    private void Awake()
    {
        // Awake still runs on a disabled component - disabling this is how an item opts out (e.g. it's
        // fixed in the scene, or EvidenceSpawner places it instead).
        if (!enabled) return;
        if (candidatePositions == null || candidatePositions.Length == 0) return;

        Vector3 chosen = candidatePositions[Random.Range(0, candidatePositions.Length)];
        transform.position = chosen;
    }
}
