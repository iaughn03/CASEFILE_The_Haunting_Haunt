using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Once every clue is in the bag the player has to walk back to one specific door, and on an open
// exterior map there is nothing on screen telling them which one or which way. This puts a marker on
// the exit: it sits over the door while the door is in view, and pins itself to the edge of the
// screen with an arrow when it isn't, with a live distance readout either way.
//
// Deliberately silent until the case is actually complete - showing it earlier would point at the
// exit during the search, which is the one thing the search phase is not supposed to do.
public class ExitWaypoint : MonoBehaviour
{
    [Header("What it points at")]
    [Tooltip("The exit itself. Left empty, it finds the DeductionGate in the scene (which is the object PlayerInteract treats as the exit) and uses that.")]
    public Transform target;
    [Tooltip("Nudges the marker up off the floor so it sits on the door rather than at its pivot.")]
    public Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);

    [Header("When it appears")]
    public EvidenceManager evidenceManager;
    [Tooltip("Shown only once this many clues are held. Left at 0 it uses EvidenceManager.maxEvidenceNeeded, i.e. the full set.")]
    public int showAtCount = 0;

    [Header("UI")]
    public Camera worldCamera;
    public CanvasGroup group;
    public RectTransform markerRoot;
    [Tooltip("Rotates to point at the exit, and is shown only while the marker is pinned to a screen edge.")]
    public RectTransform arrow;
    public TMP_Text label;
    public TMP_Text distanceText;
    [Tooltip("How far inside the screen edge the marker is allowed to sit, in canvas pixels.")]
    public float edgePadding = 110f;
    public string labelText = "Leave the scene";

    [Header("Feel")]
    public float fadeSpeed = 3f;
    [Tooltip("Gentle breathing scale so the marker reads as live rather than as a static HUD decal.")]
    public float pulseAmount = 0.06f;
    public float pulseSpeed = 2.2f;

    [Header("World beacon - optional")]
    [Tooltip("Enabled alongside the marker, so the exit is visible in the world too and not only on the HUD.")]
    public Light beacon;
    public float beaconMinIntensity = 2f;
    public float beaconMaxIntensity = 7f;

    private Transform player;
    private RectTransform canvasRect;
    private float alpha;

    private void Start()
    {
        if (evidenceManager == null) evidenceManager = FindFirstObjectByType<EvidenceManager>();
        if (worldCamera == null)
        {
            var pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) { worldCamera = pm.playerCamera; player = pm.transform; }
        }
        if (worldCamera == null) worldCamera = Camera.main;
        if (player == null && worldCamera != null) player = worldCamera.transform;

        if (target == null)
        {
            var gate = FindFirstObjectByType<DeductionGate>(FindObjectsInactive.Include);
            if (gate != null) target = gate.transform;
        }

        if (markerRoot != null) canvasRect = markerRoot.parent as RectTransform;
        if (label != null) label.text = labelText;

        alpha = 0f;
        if (group != null) group.alpha = 0f;
        if (beacon != null) beacon.enabled = false;
    }

    private bool ShouldShow()
    {
        if (target == null || worldCamera == null || evidenceManager == null) return false;

        int need = showAtCount > 0 ? showAtCount : evidenceManager.maxEvidenceNeeded;
        if (evidenceManager.currentEvidenceCount < need) return false;

        // The marker is HUD furniture - it has no business sitting on top of the pause menu, the
        // evidence bag, or the Final Inspection the player just opened.
        if (PauseManager.isPaused || PauseManager.isBagOpen) return false;
        if (FinalInspection.Instance != null && FinalInspection.Instance.IsOpen) return false;

        return true;
    }

    private void LateUpdate()
    {
        bool show = ShouldShow();

        alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
        if (group != null) group.alpha = alpha;
        if (beacon != null) beacon.enabled = alpha > 0.01f;
        if (alpha <= 0.001f) return;

        Vector3 worldPoint = target.position + targetOffset;
        Vector3 viewport = worldCamera.WorldToViewportPoint(worldPoint);

        // Behind the camera comes back with a positive-looking x/y and negative z; mirror it so the
        // clamp below pushes the marker to the correct edge instead of the opposite one.
        bool behind = viewport.z < 0f;
        if (behind) { viewport.x = 1f - viewport.x; viewport.y = 1f - viewport.y; }

        Vector2 canvasSize = canvasRect != null ? canvasRect.rect.size : new Vector2(Screen.width, Screen.height);
        Vector2 pos = new Vector2((viewport.x - 0.5f) * canvasSize.x, (viewport.y - 0.5f) * canvasSize.y);

        Vector2 limit = canvasSize * 0.5f - Vector2.one * edgePadding;
        bool offScreen = behind || Mathf.Abs(pos.x) > limit.x || Mathf.Abs(pos.y) > limit.y;

        if (offScreen)
        {
            // Keep the direction and push the marker out to whichever edge it crosses first, so the
            // arrow still reads as "the door is that way" rather than snapping to a corner.
            Vector2 dir = pos.sqrMagnitude > 0.0001f ? pos.normalized : Vector2.up;
            float scale = Mathf.Min(
                Mathf.Abs(dir.x) > 0.0001f ? limit.x / Mathf.Abs(dir.x) : float.MaxValue,
                Mathf.Abs(dir.y) > 0.0001f ? limit.y / Mathf.Abs(dir.y) : float.MaxValue);
            pos = dir * scale;

            if (arrow != null)
            {
                arrow.gameObject.SetActive(true);
                arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            }
        }
        else if (arrow != null) arrow.gameObject.SetActive(false);

        if (markerRoot != null)
        {
            markerRoot.anchoredPosition = pos;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
            markerRoot.localScale = Vector3.one * pulse;
        }

        if (distanceText != null && player != null)
        {
            float d = Vector3.Distance(player.position, target.position);
            distanceText.text = Mathf.RoundToInt(d) + "m";
        }

        if (beacon != null)
            beacon.intensity = Mathf.Lerp(beaconMinIntensity, beaconMaxIntensity,
                                          (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f);
    }
}
