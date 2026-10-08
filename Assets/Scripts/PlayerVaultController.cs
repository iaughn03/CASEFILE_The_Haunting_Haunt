using UnityEngine;
using System.Collections;

// Vaulting only - core walk/sprint/jump/gravity belongs entirely to PlayerMovement, which calls
// TryVault() before falling back to a normal jump. This used to also reimplement movement itself
// (its own walkSpeed/sprintSpeed/gravity, its own controller.Move() calls), which meant both
// scripts drove the CharacterController independently every frame outside of an active vault -
// PlayerMovement only ever deferred to isVaulting, not to VaultController's own movement pass.
[RequireComponent(typeof(CharacterController))]
public class PlayerVaultController : MonoBehaviour
{
    [Header("Vault Settings")]
    public float slowVaultSpeed = 2.5f;
    public float fastVaultSpeed = 5f;
    public float rayDistance = 1.8f;
    public float landingOffset = 2.2f;
    public LayerMask obstacleLayer;
    [Tooltip("Height above the character's actual feet the detection ray fires at - tuned for knee-to-chest obstacles. Measured from feet (via the controller's own center/height), not from transform.position directly, since that pivot isn't guaranteed to sit at ground level.")]
    public float vaultCheckHeight = 0.5f;

    [Header("Camera Tilt")]
    public Transform playerCamera;
    public float vaultTiltAngle = 15f;
    public float tiltSmoothTime = 5f;

    [Header("Status")]
    public bool isVaulting = false;

    private CharacterController controller;
    private float currentTiltX = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>().transform;
    }

    // Called by PlayerMovement's own jump handling - vaulting takes priority over a normal jump
    // when there's an obstacle directly ahead within rayDistance.
    public bool TryVault()
    {
        if (isVaulting || controller == null || !controller.enabled) return false;

        // transform.position + a flat offset assumed the pivot sat at ground level, but this
        // controller's center/height put it at the capsule's mid-body instead - that put the ray at
        // roughly chest/shoulder height, so it would only ever catch obstacles nearly as tall as the
        // player. Deriving from the controller's actual feet position keeps the check tuned for the
        // knee-to-chest obstacles vaulting is meant for, regardless of how the pivot is configured.
        float feetY = transform.position.y + controller.center.y - controller.height * 0.5f;
        Vector3 rayOrigin = new Vector3(transform.position.x, feetY + vaultCheckHeight, transform.position.z);

        if (Physics.Raycast(rayOrigin, transform.forward, out RaycastHit hit, rayDistance, obstacleLayer))
        {
            float speed = GameKeys.Held(GameKeys.Action.Sprint) ? fastVaultSpeed : slowVaultSpeed;
            Vector3 vaultTarget = hit.point + (transform.forward * landingOffset);
            vaultTarget.y = transform.position.y;

            StartCoroutine(VaultRoutine(vaultTarget, speed));
            return true;
        }
        return false;
    }

    IEnumerator VaultRoutine(Vector3 targetPos, float speed)
    {
        isVaulting = true;
        float elapsed = 0;
        Vector3 startPos = transform.position;

        // Disable the controller so the vault can move the transform directly instead of fighting
        // CharacterController's own collision resolution mid-arc.
        controller.enabled = false;

        while (elapsed < 1)
        {
            elapsed += Time.deltaTime * speed;
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed);

            currentTiltX = Mathf.Lerp(0, vaultTiltAngle, elapsed * 2);
            playerCamera.localEulerAngles = new Vector3(currentTiltX, playerCamera.localEulerAngles.y, playerCamera.localEulerAngles.z);

            yield return null;
        }

        transform.position = targetPos;

        // Re-enable so PlayerMovement's gravity/walking resumes normally next frame.
        controller.enabled = true;

        float resetElapsed = 0;
        while (resetElapsed < 1)
        {
            resetElapsed += Time.deltaTime * tiltSmoothTime;
            currentTiltX = Mathf.Lerp(vaultTiltAngle, 0, resetElapsed);
            playerCamera.localEulerAngles = new Vector3(currentTiltX, playerCamera.localEulerAngles.y, playerCamera.localEulerAngles.z);
            yield return null;
        }

        isVaulting = false;
    }
}
