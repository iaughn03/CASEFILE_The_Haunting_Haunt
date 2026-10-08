using UnityEngine;

public class KeyItem : MonoBehaviour
{
    [Header("Item Details")]
    public string keyName = "RedKey";
    public Sprite keyIcon;

    [Header("Inventory Info")]
    [TextArea]
    public string itemDescription = "Describe the item here...";

    public string usageInstructions = "How do you use this?";

    [HideInInspector]
    public bool isCollected = false;

    public void PickUp(Transform inspectPoint)
    {
        if (isCollected)
            return;

        isCollected = true;

        Debug.Log(keyName + " collected!");

        foreach (MeshRenderer rend in GetComponentsInChildren<MeshRenderer>()) rend.enabled = false;
        foreach (Collider col in GetComponentsInChildren<Collider>()) col.enabled = false;

        // The glow outline is a "not-yet-picked-up" affordance - without disabling it here, it
        // would silently reappear later whenever the 3D inspection view re-enables this same
        // object's renderers to display it, since the outline mirrors renderer.enabled.
        foreach (ItemGlowOutline glow in GetComponentsInChildren<ItemGlowOutline>(true)) glow.enabled = false;
    }
}