using System.Collections.Generic;
using UnityEngine;

// At level start, randomly places every portable evidence item (EvidenceData.allowedZones is
// not empty, required or optional) on an EvidenceSpawnPoint in one of its allowed zones. No two items share a point, and
// items are kept a minimum distance apart when the pool allows it. With placeByStoryOrder, earlier
// clues land nearer the player's start and later ones farther in. An item (portable or fixed) that
// has its own reserved points (EvidenceSpawnPoint.reservedFor) skips all that and lands on one of
// them at random. Runs before other scripts' Start() so pickups, glow and prompts all see the final
// position.
[DefaultExecutionOrder(-100)]
public class EvidenceSpawner : MonoBehaviour
{
    [Header("Debug")]
    [Tooltip("Use a fixed seed so a specific layout can be reproduced while testing.")]
    public bool useDebugSeed = false;
    public int debugSeed = 12345;

    [Header("Placement")]
    [Tooltip("Preferred minimum distance between two spawned items. Ignored automatically if the point pool is too tight to satisfy it.")]
    public float minSeparation = 3f;
    [Tooltip("Random yaw applied around the vertical axis so items don't all face the same way.")]
    public bool randomYaw = true;
    public bool logPlacements = true;

    [Header("Story order")]
    [Tooltip("Earlier clues (lower EvidenceOrder.storyOrder) prefer spawn points nearer the player's start, later ones points farther away, so the case leads the player deeper into the level.")]
    public bool placeByStoryOrder = true;
    [Tooltip("Random slack in metres on each point's distance, so the layout still varies between runs.")]
    public float orderJitter = 2f;

    public int LastSeed { get; private set; }

    private System.Random rng;
    private Vector3 playerStart;
    private float minPointDist, maxPointDist;
    private int minOrder, maxOrder;

    private void Awake()
    {
        LastSeed = useDebugSeed ? debugSeed : System.Environment.TickCount;
        rng = new System.Random(LastSeed);
        PlaceAll();
    }

    private void PlaceAll()
    {
        var points = new List<EvidenceSpawnPoint>(FindObjectsByType<EvidenceSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        var items = new List<EvidenceItem>();

        // Items with their own reserved points pick one of those and skip the shared zone pool.
        var reserved = PlaceReserved(points);
        points.RemoveAll(p => p.reservedFor != null);

        foreach (var item in FindObjectsByType<EvidenceItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (item.data != null && item.data.IsPortable && !reserved.Contains(item)) items.Add(item);
        }

        if (items.Count == 0) return;
        if (points.Count == 0)
        {
            Debug.LogWarning("[EvidenceSpawner] No EvidenceSpawnPoints in the scene - portable evidence stays where it was placed.");
            return;
        }

        PrepareStoryOrder(points);

        Shuffle(items);
        // Most constrained (fewest allowed zones) first so backtracking rarely has to undo anything.
        items.Sort((a, b) => a.data.allowedZones.Length.CompareTo(b.data.allowedZones.Length));

        var assignment = new EvidenceSpawnPoint[items.Count];
        bool ok = Assign(items, points, assignment, 0, minSeparation) || Assign(items, points, assignment, 0, 0f);

        if (!ok)
        {
            Debug.LogWarning("[EvidenceSpawner] Could not give every portable item its own valid point - leaving items where they were placed.");
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var point = assignment[i];

            item.transform.position = point.transform.position + Vector3.up * item.groundOffset;
            if (point.fillFirst)
                AlignToShelf(item, point);
            else if (randomYaw)
                item.transform.rotation = Quaternion.AngleAxis((float)(rng.NextDouble() * 360.0), Vector3.up) * item.transform.rotation;
            item.CurrentZone = point.zone;

            if (logPlacements)
                Debug.Log($"[EvidenceSpawner] {item.DisplayName} -> {point.name} ({point.zone}) at {item.transform.position} (seed {LastSeed})");
        }
    }

    // Each reserved point belongs to one item, so there is nothing to share out: every item with
    // reserved points lands on one of them at random, posed exactly as the point is (scene clues like
    // a toppled chair or boot prints keep their lie that way).
    private HashSet<EvidenceItem> PlaceReserved(List<EvidenceSpawnPoint> points)
    {
        var byItem = new Dictionary<EvidenceItem, List<EvidenceSpawnPoint>>();
        foreach (var p in points)
        {
            if (p.reservedFor == null) continue;
            if (!byItem.TryGetValue(p.reservedFor, out var list)) byItem[p.reservedFor] = list = new List<EvidenceSpawnPoint>();
            list.Add(p);
        }

        foreach (var pair in byItem)
        {
            var item = pair.Key;
            var point = pair.Value[rng.Next(pair.Value.Count)];
            item.transform.SetPositionAndRotation(point.transform.position + Vector3.up * item.groundOffset, point.transform.rotation);
            item.CurrentZone = point.zone;

            if (logPlacements)
                Debug.Log($"[EvidenceSpawner] {item.DisplayName} -> reserved {point.name} ({point.zone}) at {item.transform.position} (seed {LastSeed})");
        }
        return new HashSet<EvidenceItem>(byItem.Keys);
    }

    // A shelf inside a cupboard is shallow: a random yaw could poke a long item (a page, the note)
    // through the door or the back. Lay the item's longer side along the shelf instead - the spawn
    // point's local X is the shelf's width.
    private static void AlignToShelf(EvidenceItem item, EvidenceSpawnPoint point)
    {
        Vector3 size = WorldSize(item);
        float shelfYaw = point.transform.eulerAngles.y;
        float extra = size.z > size.x ? 90f : 0f;   // long side currently along world Z -> turn it onto X first
        item.transform.rotation = Quaternion.AngleAxis(shelfYaw + extra, Vector3.up) * item.transform.rotation;
    }

    private static Vector3 WorldSize(EvidenceItem item)
    {
        var renderers = item.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return Vector3.zero;
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b.size;
    }

    private bool Assign(List<EvidenceItem> items, List<EvidenceSpawnPoint> points, EvidenceSpawnPoint[] assignment, int index, float separation)
    {
        if (index >= items.Count) return true;

        var item = items[index];
        var candidates = new List<EvidenceSpawnPoint>();
        foreach (var p in points)
        {
            if (!item.data.AllowsZone(p.zone)) continue;

            bool taken = false;
            for (int i = 0; i < index; i++)
            {
                if (assignment[i] == p) { taken = true; break; }
                if (separation > 0f && Vector3.Distance(assignment[i].transform.position, p.transform.position) < separation) { taken = true; break; }
            }
            if (!taken) candidates.Add(p);
        }

        Shuffle(candidates);
        SortByStoryDistance(item, candidates);
        // hiding places first (stable: keeps the order above within each group)
        var hidden = candidates.FindAll(p => p.fillFirst);
        if (hidden.Count > 0)
        {
            candidates.RemoveAll(p => p.fillFirst);
            candidates.InsertRange(0, hidden);
        }

        foreach (var c in candidates)
        {
            assignment[index] = c;
            if (Assign(items, points, assignment, index + 1, separation)) return true;
        }

        assignment[index] = null;
        return false;
    }

    // Maps the scene's story-order range onto the spawn points' distance range from the player start:
    // the first clue aims for the nearest point, the last for the farthest.
    private void PrepareStoryOrder(List<EvidenceSpawnPoint> points)
    {
        if (!placeByStoryOrder) return;

        var player = FindFirstObjectByType<PlayerMovement>();
        if (player == null) { placeByStoryOrder = false; return; }
        playerStart = player.transform.position;

        minPointDist = float.MaxValue; maxPointDist = 0f;
        foreach (var p in points)
        {
            float d = DistanceFromStart(p);
            minPointDist = Mathf.Min(minPointDist, d);
            maxPointDist = Mathf.Max(maxPointDist, d);
        }

        minOrder = int.MaxValue; maxOrder = int.MinValue;
        foreach (var o in FindObjectsByType<EvidenceOrder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            minOrder = Mathf.Min(minOrder, o.storyOrder);
            maxOrder = Mathf.Max(maxOrder, o.storyOrder);
        }
        // Inactive items never ran EvidenceItem.Awake, so their data fallback isn't a component yet.
        foreach (var e in FindObjectsByType<EvidenceItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            int s = e.StoryOrder;
            if (s <= 0) continue;
            minOrder = Mathf.Min(minOrder, s);
            maxOrder = Mathf.Max(maxOrder, s);
        }
    }

    // Runs after the shuffle, so points at similar distances keep a random order among themselves.
    // The backtracking in Assign() still falls through to farther-off points if the best ones are taken.
    private void SortByStoryDistance(EvidenceItem item, List<EvidenceSpawnPoint> candidates)
    {
        if (!placeByStoryOrder || maxOrder <= minOrder) return;

        int storyOrder = item.StoryOrder;
        if (storyOrder <= 0) return;

        float t = Mathf.InverseLerp(minOrder, maxOrder, storyOrder);
        float target = Mathf.Lerp(minPointDist, maxPointDist, t);

        var score = new Dictionary<EvidenceSpawnPoint, float>();
        foreach (var c in candidates)
        {
            float d = DistanceFromStart(c);
            score[c] = Mathf.Abs(d - target) + (float)rng.NextDouble() * orderJitter;
        }
        candidates.Sort((a, b) => score[a].CompareTo(score[b]));
    }

    // The point's measured walking distance when it has one, else straight-line from the player start.
    private float DistanceFromStart(EvidenceSpawnPoint p)
    {
        return p.pathDistanceFromStart > 0f ? p.pathDistanceFromStart : Vector3.Distance(playerStart, p.transform.position);
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
