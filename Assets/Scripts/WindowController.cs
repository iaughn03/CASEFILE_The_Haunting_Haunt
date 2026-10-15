using System.Collections.Generic;
using UnityEngine;

// A house window whose casement sashes swing open and shut on interact (Game2's ModularHousePack
// windows). Once open, PlayerWindowClimb lets the player climb through it with the jump key.
// The opening's shape (centre, facing, width, sill and top) is measured from the closed sashes'
// colliders at Awake, so one component fits every window size and facing without hand tuning.
public class WindowController : MonoBehaviour
{
    [Header("Window")]
    public string windowName = "Window"; // Shown in the PlayerInteract prompt
    [Tooltip("The sashes that swing. Each pivots on its hinge (its own local Y axis) by its entry in openAngles; the signs are picked per sash so it swings into the room.")]
    public Transform[] sashes = new Transform[2];
    public float[] openAngles = { 90f, -90f };
    [Tooltip("Degrees per second.")]
    public float rotationSpeed = 220f;

    [Header("Interaction")]
    public float interactionRange = 3f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] openClips;
    public AudioClip[] closeClips;
    [Range(0f, 0.5f)] public float pitchJitter = 0.06f;

    public static readonly List<WindowController> All = new List<WindowController>();
    private static readonly Dictionary<Collider, WindowController> bySash = new Dictionary<Collider, WindowController>();

    public bool IsOpen { get; private set; }
    // 0 = shut, 1 = swung fully open.
    public float OpenAmount { get; private set; }

    // The opening, in world space. Center sits in the plane of the closed sashes; Normal is level and
    // points to one side of the wall (which side doesn't matter, the climb works both ways).
    public Vector3 Center { get; private set; }
    public Vector3 Normal { get; private set; }
    public Vector3 Right { get; private set; }
    public float HalfWidth { get; private set; }
    public float SillY { get; private set; }
    public float TopY { get; private set; }

    private Quaternion[] closedRotations;
    private readonly List<Collider> ownColliders = new List<Collider>();

    void Awake()
    {
        closedRotations = new Quaternion[sashes.Length];
        for (int i = 0; i < sashes.Length; i++)
        {
            if (sashes[i] == null) continue;
            closedRotations[i] = sashes[i].localRotation;
            foreach (Collider c in sashes[i].GetComponentsInChildren<Collider>(true))
            {
                ownColliders.Add(c);
                bySash[c] = this;
            }
        }
        MeasureOpening();
    }

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void OnDestroy()
    {
        foreach (Collider c in ownColliders)
            if (c != null && bySash.TryGetValue(c, out WindowController w) && w == this) bySash.Remove(c);
    }

    // PlayerInteract's lookup: which window (if any) owns the sash collider the crosshair hit.
    public static WindowController FromCollider(Collider c)
    {
        return c != null && bySash.TryGetValue(c, out WindowController w) ? w : null;
    }

    public bool OwnsCollider(Collider c) => ownColliders.Contains(c);

    void MeasureOpening()
    {
        var corners = new List<Vector3>();
        Vector3 normal = Vector3.zero;
        foreach (Collider c in ownColliders)
        {
            // The collider's box in its own local space (sashes use either a BoxCollider or a convex mesh).
            Bounds local;
            if (c is BoxCollider box) local = new Bounds(box.center, box.size);
            else if (c is MeshCollider mesh && mesh.sharedMesh != null) local = mesh.sharedMesh.bounds;
            else continue;

            Transform t = c.transform;
            Vector3 half = local.extents;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        corners.Add(t.TransformPoint(local.center + Vector3.Scale(half, new Vector3(x, y, z))));

            if (normal == Vector3.zero)
            {
                // The sash is a thin slab: its thinnest axis is the wall's facing.
                Vector3 ax = t.TransformVector(Vector3.right * local.size.x);
                Vector3 ay = t.TransformVector(Vector3.up * local.size.y);
                Vector3 az = t.TransformVector(Vector3.forward * local.size.z);
                Vector3 thin = ax;
                if (az.sqrMagnitude < thin.sqrMagnitude) thin = az;
                if (ay.sqrMagnitude < thin.sqrMagnitude) thin = ay;
                thin.y = 0f;
                if (thin.sqrMagnitude > 1e-8f) normal = thin.normalized;
            }
        }

        if (corners.Count == 0 || normal == Vector3.zero)
        {
            Center = transform.position;
            Normal = transform.forward;
            Right = transform.right;
            return;
        }

        Vector3 sum = Vector3.zero;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (Vector3 p in corners)
        {
            sum += p;
            minY = Mathf.Min(minY, p.y);
            maxY = Mathf.Max(maxY, p.y);
        }
        Center = sum / corners.Count;
        Normal = normal;
        Right = Vector3.Cross(Vector3.up, normal).normalized;

        float halfWidth = 0f;
        foreach (Vector3 p in corners) halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(p - Center, Right)));
        HalfWidth = halfWidth;
        SillY = minY;
        TopY = maxY;
    }

    void Update()
    {
        float target = IsOpen ? 1f : 0f;
        if (Mathf.Approximately(OpenAmount, target)) return;

        float widest = 1f;
        foreach (float a in openAngles) widest = Mathf.Max(widest, Mathf.Abs(a));
        OpenAmount = Mathf.MoveTowards(OpenAmount, target, rotationSpeed / widest * Time.deltaTime);

        // Ease in and out so the sashes don't start and stop dead.
        float eased = Mathf.SmoothStep(0f, 1f, OpenAmount);
        for (int i = 0; i < sashes.Length; i++)
        {
            if (sashes[i] == null) continue;
            float angle = i < openAngles.Length ? openAngles[i] : 0f;
            sashes[i].localRotation = closedRotations[i] * Quaternion.AngleAxis(angle * eased, Vector3.up);
        }

        // Physics.autoSyncTransforms is off project wide, same as CabinetController/DoorController.
        Physics.SyncTransforms();
    }

    /// <summary>
    /// Called by PlayerInteract.cs when the player presses 'E'.
    /// </summary>
    public void Interaction(PlayerInteract player)
    {
        IsOpen = !IsOpen;
        PlaySound(IsOpen ? openClips : closeClips);
    }

    void PlaySound(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        audioSource.pitch = Random.Range(1f - pitchJitter, 1f + pitchJitter);
        audioSource.PlayOneShot(clip, SfxVolume.Multiplier);
    }
}
