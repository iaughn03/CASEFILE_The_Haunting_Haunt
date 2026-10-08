using UnityEngine;
using UnityEngine.AI;

// The scene bakes its walkable area through a proper Unity.AI.Navigation.NavMeshSurface component
// (matching Game1's setup), which loads its own baked data automatically on enable - no manual
// NavMesh.AddNavMeshData() needed here. This script's only remaining job: some NavMeshAgents are kept
// disabled in the saved scene so they don't try to place themselves on the navmesh before it's loaded
// (a disabled agent skips that entirely until re-enabled). This has to run in Awake(), not Start() -
// GhostController's own Start() calls agent.SetDestination() on this same agent, and Unity does NOT
// guarantee Start() order between different objects (that bit Start() once already: the agent was still
// disabled when GhostController.Start() ran). Awake() is what Unity DOES guarantee: every object's
// Awake() finishes, for every object in the scene, before any object's Start() runs - so as long as this
// enables the agent in Awake(), GhostController.Start() is guaranteed to see it already enabled,
// regardless of which of the two Awake()s (this one or NavMeshSurface's own) happens to run first.
public class SceneNavMeshLoader : MonoBehaviour
{
    public NavMeshAgent[] agentsToActivateAfterLoad;

    void Awake()
    {
        if (agentsToActivateAfterLoad == null) return;
        foreach (var a in agentsToActivateAfterLoad)
            if (a != null) a.enabled = true;
    }
}
