using UnityEngine;
using TMPro;

// Drives the middle "3D inspection" panel shown when clicking a bag item. Rather than spawning a
// fresh copy per item type, this borrows the item's own original world pickup object (kept hidden,
// not destroyed, on collection - see EvidencePickUp/KeyItem) by temporarily reparenting it onto a
// dedicated off-map stage that only a dedicated inspection camera can see, then restores it to
// exactly where it was when inspection ends.
public class ItemInspection3D : MonoBehaviour
{
    public static ItemInspection3D Instance;

    [Header("Stage")]
    public Transform itemAnchor;

    [Header("UI")]
    public GameObject inspectionPanel;
    public TMP_Text itemNameText;

    [Header("Rotation")]
    public float rotationSpeed = 150f;
    public float keyRotationSpeed = 90f;

    [Header("Zoom")]
    public Camera inspectionCamera;
    public float zoomSpeed = 15f;
    public float minFov = 12f;
    public float maxFov = 45f;

    [Header("Framing")]
    [Tooltip("How much of the view the item fills when it opens (its bounding sphere vs the camera's half-height at the stage). Items are scaled to this on the stage and put back at their real size afterwards, so a 1 m vanity and a 5 cm key both open fully in frame.")]
    [Range(0.2f, 1f)] public float fitFraction = 0.8f;
    [Tooltip("How far flat items (papers, keys, floor marks) are tipped up to face the camera when they open: 1 = fully flat-on, 0 = left lying flat (edge-on).")]
    [Range(0f, 1f)] public float flatTiltAmount = 0.8f;

    private float defaultFov;
    private GameObject currentItem;
    private Transform originalParent;
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;
    private Vector3 originalLocalScale;
    private int originalLayer;
    private bool isInspecting = false;
    // The item hangs off this, offset so its visual centre sits on the anchor - rotating the pivot
    // turns the item about its middle instead of about wherever its model origin happens to be.
    private Transform pivot;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (inspectionPanel != null) inspectionPanel.SetActive(false);
        if (inspectionCamera != null) defaultFov = inspectionCamera.fieldOfView;
    }

    public void Inspect(GameObject worldObject, string displayName)
    {
        if (worldObject == null || itemAnchor == null) return;

        StopInspecting();

        currentItem = worldObject;
        originalParent = worldObject.transform.parent;
        originalLocalPos = worldObject.transform.localPosition;
        originalLocalRot = worldObject.transform.localRotation;
        originalLocalScale = worldObject.transform.localScale;
        originalLayer = worldObject.layer;

        if (pivot == null)
        {
            pivot = new GameObject("InspectionPivot").transform;
            pivot.SetParent(itemAnchor, false);
        }
        pivot.localPosition = Vector3.zero;
        pivot.localRotation = Quaternion.identity;
        pivot.localScale = Vector3.one;

        worldObject.transform.SetParent(pivot, false);
        worldObject.transform.localPosition = Vector3.zero;
        worldObject.transform.localRotation = Quaternion.identity;

        int inspectionLayer = LayerMask.NameToLayer("ItemInspection");
        SetLayerRecursively(worldObject, inspectionLayer);
        SetRenderersEnabled(worldObject, true);

        if (itemNameText != null) itemNameText.text = displayName;
        if (inspectionPanel != null) inspectionPanel.SetActive(true);

        // Reset zoom each time a new item is opened, so zooming in on one item doesn't carry
        // over and start the next one weirdly close or far away.
        if (inspectionCamera != null) inspectionCamera.fieldOfView = defaultFov;

        FitToView(worldObject);

        isInspecting = true;
    }

    // Evidence ranges from a 5 cm key to a metre-tall vanity and a 1.5 m trail of boot prints, and
    // model origins are rarely at the model's middle - so a single fixed placement framed some items
    // fine, filled the whole panel with others, and swung the rest around off-centre when rotated.
    // Scales the item (uniformly) so its bounding sphere fills fitFraction of the view, then centres
    // that sphere on the pivot.
    private void FitToView(GameObject item)
    {
        Bounds b = default;
        bool any = false;
        foreach (var r in item.GetComponentsInChildren<Renderer>(true))
        {
            // The glow shell and dust motes aren't part of the object.
            if (r is ParticleSystemRenderer || r.gameObject.name.EndsWith("_Outline")) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!any) return;

        float radius = b.extents.magnitude;
        if (radius < 0.0001f) return;

        float distance = inspectionCamera != null ? Vector3.Distance(inspectionCamera.transform.position, pivot.position) : 1f;
        float fov = inspectionCamera != null ? inspectionCamera.fieldOfView : 30f;
        float targetRadius = distance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * fitFraction;
        float s = targetRadius / radius;

        item.transform.localScale = originalLocalScale * s;
        // The item's origin sits on the pivot, so scaling about it scales the centre's offset too.
        item.transform.position -= (b.center - pivot.position) * s;

        // Things that lie flat (papers, keys, a trail of boot prints) open edge-on to a camera looking
        // straight ahead - a thin line, or nothing at all for a floor decal. Tip them up to face the
        // camera, stopping a little short of flat-on so they still read as an object.
        float thickness = b.size.y;
        float span = Mathf.Max(b.size.x, b.size.z);
        if (inspectionCamera != null && thickness < span * 0.25f)
        {
            Quaternion faceCamera = Quaternion.FromToRotation(Vector3.up, -inspectionCamera.transform.forward);
            pivot.rotation = Quaternion.Slerp(Quaternion.identity, faceCamera, flatTiltAmount) * pivot.rotation;
        }
    }

    public void StopInspecting()
    {
        if (currentItem != null)
        {
            SetRenderersEnabled(currentItem, false);
            SetLayerRecursively(currentItem, originalLayer);
            currentItem.transform.SetParent(originalParent, false);
            currentItem.transform.localPosition = originalLocalPos;
            currentItem.transform.localRotation = originalLocalRot;
            currentItem.transform.localScale = originalLocalScale;
            currentItem = null;
        }
        if (pivot != null) pivot.localRotation = Quaternion.identity;

        if (inspectionPanel != null) inspectionPanel.SetActive(false);
        isInspecting = false;
    }

    private void Update()
    {
        if (!isInspecting || currentItem == null) return;

        // Turn the pivot, not the item, so it spins about its visual centre (see FitToView).
        Transform turn = pivot != null ? pivot : currentItem.transform;

        if (Input.GetMouseButton(0))
        {
            float rotX = Input.GetAxis("Mouse X") * rotationSpeed * Time.unscaledDeltaTime;
            float rotY = Input.GetAxis("Mouse Y") * rotationSpeed * Time.unscaledDeltaTime;
            turn.Rotate(Vector3.up, -rotX, Space.World);
            turn.Rotate(Vector3.right, rotY, Space.World);
        }

        // Arrow keys turn the item as a keyboard-only alternative to mouse-drag rotation.
        float keyStep = keyRotationSpeed * Time.unscaledDeltaTime;
        if (Input.GetKey(KeyCode.LeftArrow)) turn.Rotate(Vector3.up, keyStep, Space.World);
        if (Input.GetKey(KeyCode.RightArrow)) turn.Rotate(Vector3.up, -keyStep, Space.World);
        if (Input.GetKey(KeyCode.UpArrow)) turn.Rotate(Vector3.right, keyStep, Space.World);
        if (Input.GetKey(KeyCode.DownArrow)) turn.Rotate(Vector3.right, -keyStep, Space.World);

        // Scroll wheel zooms by narrowing/widening the inspection camera's FOV, clamped so the
        // item can't be zoomed through or pushed out to nothing.
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f && inspectionCamera != null)
        {
            inspectionCamera.fieldOfView = Mathf.Clamp(
                inspectionCamera.fieldOfView - scroll * zoomSpeed,
                minFov,
                maxFov
            );
        }
    }

    private void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
    }

    private void SetRenderersEnabled(GameObject go, bool isEnabled)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = isEnabled;
    }
}
