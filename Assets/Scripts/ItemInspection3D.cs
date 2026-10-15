using UnityEngine;
using UnityEngine.UI;
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

    [Header("Notes")]
    [Tooltip("The rotate/zoom hint under the view. Found by name (InspectHint) when left empty. Swapped for noteHint while a note's page is shown.")]
    public TMP_Text inspectHintText;
    public string noteHint = "Click the note again to read it up close.";
    [Tooltip("How much of the view the note page fills.")]
    [Range(0.5f, 1f)] public float notePageFill = 0.96f;

    private float defaultFov;
    private GameObject currentItem;
    private Transform originalParent;
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;
    private Vector3 originalLocalScale;
    private int originalLayer;
    private bool isInspecting = false;
    // True while currentItem is a throwaway copy (see InspectCopy), destroyed rather than put back.
    private bool currentIsCopy;
    // The item hangs off this, offset so its visual centre sits on the anchor - rotating the pivot
    // turns the item about its middle instead of about wherever its model origin happens to be.
    private Transform pivot;

    // A note shows the same page the reader shows (paper art + its text), not the 3D paper prop,
    // which sits edge-on or blank and never carries the writing. Built once from a copy of
    // NoteSystem's NotePanel, then scaled down into the item view.
    private RectTransform notePageHolder;
    private RectTransform notePage;
    private TMP_Text notePageText;
    private RawImage itemViewImage;
    private string defaultHint;
    private bool showingNotePage;

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
        StopInspecting();
        if (worldObject == null || itemAnchor == null) return;

        currentIsCopy = false;
        currentItem = worldObject;
        originalParent = worldObject.transform.parent;
        originalLocalPos = worldObject.transform.localPosition;
        originalLocalRot = worldObject.transform.localRotation;
        originalLocalScale = worldObject.transform.localScale;
        originalLayer = worldObject.layer;

        Show(worldObject, displayName);
    }

    // Scene evidence that stays where it was found (EvidenceItem.keepInWorld: floor marks, a forced
    // latch) has no hidden pickup to borrow - the real one is still standing in the level. Shows a
    // look-only copy of it instead (meshes and sprites, no scripts, colliders or physics), thrown away
    // when inspection ends, so the bag still shows the very thing that was examined.
    public void InspectCopy(GameObject source, string displayName)
    {
        StopInspecting();
        if (source == null || itemAnchor == null) return;

        GameObject copy = BuildCopy(source);
        if (copy == null) return;

        currentIsCopy = true;
        currentItem = copy;
        originalLocalScale = copy.transform.localScale;
        originalLayer = copy.layer;

        Show(copy, displayName);
    }

    // Rebuilt from the source's meshes and sprites rather than Instantiate()d and stripped: a clone
    // carries the clue's scripts (EvidenceItem would register the copy as a second clue, and some of
    // them depend on each other so they can't be removed one by one).
    private GameObject BuildCopy(GameObject source)
    {
        GameObject copy = CopyLook(source.transform, itemAnchor);
        copy.name = source.name + "_InspectCopy";
        copy.transform.localPosition = Vector3.zero;
        copy.transform.localRotation = Quaternion.identity;
        copy.transform.localScale = source.transform.lossyScale;
        return copy;
    }

    private static GameObject CopyLook(Transform src, Transform parent)
    {
        GameObject go = new GameObject(src.name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = src.localPosition;
        go.transform.localRotation = src.localRotation;
        go.transform.localScale = src.localScale;

        MeshFilter mf = src.GetComponent<MeshFilter>();
        MeshRenderer mr = src.GetComponent<MeshRenderer>();
        if (mf != null && mr != null && mf.sharedMesh != null)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
        }

        SkinnedMeshRenderer smr = src.GetComponent<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMesh != null)
        {
            go.AddComponent<MeshFilter>().sharedMesh = smr.sharedMesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
        }

        SpriteRenderer sr = src.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            SpriteRenderer s = go.AddComponent<SpriteRenderer>();
            s.sprite = sr.sprite;
            s.color = sr.color;
            s.flipX = sr.flipX;
            s.flipY = sr.flipY;
            s.drawMode = sr.drawMode;
            s.size = sr.size;
            s.sharedMaterial = sr.sharedMaterial;
        }

        // Only what is actually showing in the level: hidden children stay out, and so do the glow
        // shell and dust motes, which are effects around the clue rather than part of it.
        foreach (Transform child in src)
        {
            if (!child.gameObject.activeSelf || child.name.EndsWith("_Outline") ||
                child.GetComponent<ParticleSystem>() != null) continue;
            CopyLook(child, go.transform);
        }
        return go;
    }

    private void Show(GameObject worldObject, string displayName)
    {
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

    // False when there is no reader page to copy; the caller then falls back to the 3D prop.
    public bool InspectNote(string noteText, string displayName)
    {
        StopInspecting();
        if (inspectionPanel == null || !BuildNotePage()) return false;

        notePageText.text = noteText;
        notePageText.maxVisibleCharacters = int.MaxValue;

        if (itemNameText != null) itemNameText.text = displayName;
        if (itemViewImage != null) itemViewImage.enabled = false;
        notePageHolder.gameObject.SetActive(true);
        if (inspectHintText != null) inspectHintText.text = noteHint;
        inspectionPanel.SetActive(true);
        showingNotePage = true;

        FitNotePage();
        return true;
    }

    private bool BuildNotePage()
    {
        if (notePage != null) return true;

        NoteSystem notes = NoteSystem.Instance;
        if (notes == null || notes.notePanel == null || notes.noteTextMesh == null) return false;

        RectTransform view = FindItemView();
        if (view == null) return false;
        itemViewImage = view.GetComponent<RawImage>();

        if (inspectHintText == null)
        {
            Transform hint = FindDeep(inspectionPanel.transform, "InspectHint");
            if (hint != null) inspectHintText = hint.GetComponent<TMP_Text>();
        }
        if (inspectHintText != null) defaultHint = inspectHintText.text;

        // The holder stands in for the reader page's own parent, so the copy lays itself out at the
        // reader's real size and proportions; FitNotePage then shrinks the whole thing into the view.
        RectTransform source = (RectTransform)notes.notePanel.transform;
        RectTransform sourceParent = source.parent as RectTransform;

        notePageHolder = new GameObject("NotePageHolder", typeof(RectTransform)).GetComponent<RectTransform>();
        notePageHolder.SetParent(view, false);
        notePageHolder.anchorMin = notePageHolder.anchorMax = notePageHolder.pivot = new Vector2(0.5f, 0.5f);
        notePageHolder.sizeDelta = sourceParent != null ? sourceParent.rect.size : source.rect.size;

        string textPath = PathFrom(source, notes.noteTextMesh.transform);
        GameObject copy = Instantiate(notes.notePanel, notePageHolder, false);
        copy.name = "NotePage";
        notePage = (RectTransform)copy.transform;
        notePage.localScale = Vector3.one;

        Transform textT = textPath != null ? notePage.Find(textPath) : null;
        notePageText = textT != null ? textT.GetComponent<TMP_Text>() : null;
        if (notePageText == null)
        {
            Destroy(notePageHolder.gameObject);
            notePageHolder = null;
            notePage = null;
            return false;
        }

        // Only the paper and its writing: no close hint, no reader scripts (typewriter etc.), no clicks.
        // Immediate, since the copy is switched on below in this same frame and a deferred Destroy
        // would still let those scripts run Awake/OnEnable first.
        foreach (TMP_Text t in copy.GetComponentsInChildren<TMP_Text>(true))
            if (t != null && t != notePageText) DestroyImmediate(t.gameObject);
        foreach (MonoBehaviour mb in copy.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string ns = mb.GetType().Namespace;
            if (ns != "UnityEngine.UI" && ns != "TMPro") DestroyImmediate(mb);
        }
        foreach (Graphic g in copy.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

        copy.SetActive(true);
        notePageHolder.gameObject.SetActive(false);
        return true;
    }

    private void FitNotePage()
    {
        RectTransform view = notePageHolder.parent as RectTransform;
        notePageHolder.localScale = Vector3.one;
        notePageHolder.anchoredPosition = Vector2.zero;
        LayoutRebuilder.ForceRebuildLayoutImmediate(notePageHolder);

        Vector2 page = notePage.rect.size;
        if (view == null || page.x <= 0f || page.y <= 0f) return;

        // A page is taller than the square item view, so let it use the panel's full height between
        // the name and the hint (the view sits centred in that gap) for bigger, easier writing.
        Vector2 room = view.rect.size;
        RectTransform panel = inspectionPanel.transform as RectTransform;
        if (panel != null && itemNameText != null && inspectHintText != null)
        {
            float gap = itemNameText.rectTransform.anchorMin.y - inspectHintText.rectTransform.anchorMax.y;
            if (gap > 0f) room = new Vector2(panel.rect.width * 0.9f, Mathf.Max(room.y, panel.rect.height * gap));
        }

        float s = Mathf.Min(room.x / page.x, room.y / page.y) * notePageFill;
        Vector2 pageCentre = (Vector2)notePage.localPosition + notePage.rect.center;
        notePageHolder.localScale = new Vector3(s, s, 1f);
        notePageHolder.anchoredPosition = -pageCentre * s;
    }

    private RectTransform FindItemView()
    {
        // The RawImage showing the inspection camera's render texture.
        foreach (RawImage ri in inspectionPanel.GetComponentsInChildren<RawImage>(true))
            if (inspectionCamera == null || ri.texture == inspectionCamera.targetTexture) return ri.rectTransform;
        return null;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static string PathFrom(Transform root, Transform target)
    {
        string path = null;
        for (Transform t = target; t != null; t = t.parent)
        {
            if (t == root) return path ?? "";
            path = path == null ? t.name : t.name + "/" + path;
        }
        return null;
    }

    public void StopInspecting()
    {
        if (showingNotePage)
        {
            if (notePageHolder != null) notePageHolder.gameObject.SetActive(false);
            if (itemViewImage != null) itemViewImage.enabled = true;
            if (inspectHintText != null && defaultHint != null) inspectHintText.text = defaultHint;
            showingNotePage = false;
        }

        if (currentItem != null && currentIsCopy)
        {
            currentItem.SetActive(false);
            Destroy(currentItem);
            currentItem = null;
            currentIsCopy = false;
        }
        else if (currentItem != null)
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
        // Dust motes and the glow shell are effects around a clue, not part of it - never switch them
        // on for the stage. (The shell is only Destroy()ed at the end of the pickup frame.)
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            bool effect = r is ParticleSystemRenderer || r.gameObject.name.EndsWith("_Outline");
            if (!(isEnabled && effect)) r.enabled = isEnabled;
        }
    }
}
