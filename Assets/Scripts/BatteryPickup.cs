using UnityEngine;

public class BatteryPickup : MonoBehaviour
{
    [Header("Battery Settings")]
    public string pickupName = "Battery";
    public float batteryRestoreAmount = 50f; // flat amount added to the flashlight's current charge

    [Header("Pickup Feedback")]
    public AudioClip pickupSound;

    [HideInInspector]
    public bool isCollected = false;

    public void PickUp()
    {
        if (isCollected) return;
        isCollected = true;

        FlashlightController flashlight = FindFirstObjectByType<FlashlightController>(FindObjectsInactive.Include);
        if (flashlight != null)
        {
            flashlight.AddBattery(batteryRestoreAmount);
        }

        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, SfxVolume.Multiplier);
        }

        foreach (MeshRenderer mr in GetComponentsInChildren<MeshRenderer>()) mr.enabled = false;
        foreach (Collider col in GetComponentsInChildren<Collider>()) col.enabled = false;

        // Give the pickup sound a moment to finish before the object disappears entirely
        Destroy(gameObject, pickupSound != null ? pickupSound.length : 0.1f);
    }
}
