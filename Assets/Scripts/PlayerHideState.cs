using UnityEngine;

// Single source of truth for whether the player is currently concealed in a hiding spot (locker,
// wardrobe, etc.). AI detection scripts (GhostController) check this before running their normal
// line-of-sight/proximity checks, so hiding actually works regardless of how exposed the player's
// collider/position technically still is underneath.
public class PlayerHideState : MonoBehaviour
{
    public bool IsHiding { get; private set; }

    public void SetHiding(bool hiding) => IsHiding = hiding;
}
