using UnityEngine;
using System.Collections.Generic;

// Inverted-hull outline: for every mesh under this object, spawns a slightly larger copy with its
// triangle winding order reversed, so Unity's DEFAULT face culling (which normally hides the back
// side of a mesh) now hides what used to be its front and shows only what used to be its back.
// The original mesh's front faces occlude the outline's now-hidden-front over the object's center,
// leaving only the silhouette edge - where the enlarged copy pokes out past the original - visible
// as a thin glowing rim. This makes pickup items readable in near-total darkness without needing a
// light to actually be hitting them.
//
// An earlier version tried to achieve the same thing by overriding the outline material's _Cull
// property to force back-face-only rendering directly. That property read back correctly when
// queried, but the GPU render state didn't actually respect it - the outline rendered as a full
// solid silhouette instead of a rim, confirmed even at an extreme 1.3x test scale. This version
// sidesteps that entirely by working through mesh winding order instead of a shader property
// override, using only the standard (default) cull mode that's confirmed working throughout the
// rest of the project.
[ExecuteAlways]
public class ItemGlowOutline : MonoBehaviour
{
    [Header("Outline Settings")]
    public Color glowColor = new Color(1f, 0.85f, 0.3f);
    [Range(1.0f, 1.15f)] public float outlineScale = 1.05f;
    public float emissionIntensity = 1f;
    [Tooltip("World-space rim width for flat meshes (paper, decals, quads), which can't use the inverted hull.")]
    public float flatRimWidth = 0.02f;

    private readonly List<GameObject> outlineObjects = new List<GameObject>();
    // Parallel to outlineObjects - the source renderer each outline was built from, so its
    // visibility can be mirrored (pickup scripts hide themselves by disabling their own
    // MeshRenderer rather than destroying the object, which would otherwise leave the outline
    // glowing on its own with nothing left to occlude it).
    private readonly List<Renderer> sourceRenderers = new List<Renderer>();
    private static Shader unlitShader;

    void OnEnable()
    {
        BuildOutlines();
    }

    void OnDisable()
    {
        ClearOutlines();
    }

    void Update()
    {
        for (int i = 0; i < outlineObjects.Count; i++)
        {
            if (outlineObjects[i] == null || sourceRenderers[i] == null) continue;
            bool shouldShow = sourceRenderers[i].enabled && sourceRenderers[i].gameObject.activeInHierarchy;
            if (outlineObjects[i].activeSelf != shouldShow)
                outlineObjects[i].SetActive(shouldShow);
        }
    }

    // Lets a caller (e.g. a hover-driven prompt) pulse or boost the glow at runtime without tearing
    // down and rebuilding the outline meshes every frame - just restyles the already-built materials.
    public void SetIntensityMultiplier(float multiplier)
    {
        Color c = glowColor * emissionIntensity * multiplier;
        foreach (var outline in outlineObjects)
        {
            if (outline == null) continue;
            Renderer r = outline.GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null) r.sharedMaterial.SetColor(GlowPipeline.ColorProperty, c);
        }
    }

    public void BuildOutlines()
    {
        ClearOutlines();

        // Picks the unlit shader for whichever pipeline is active (URP here; Built-in/HDRP fall back).
        if (unlitShader == null) unlitShader = GlowPipeline.OutlineShader;
        if (unlitShader == null) return;

        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>();
        Color finalColor = glowColor * emissionIntensity;

        foreach (var mf in filters)
        {
            if (mf.sharedMesh == null || mf.gameObject.name.EndsWith("_Outline")) continue;

            // Imported models can have "Read/Write Enabled" off in their import settings, which
            // blocks the vertex/uv/triangle access BuildReversedWindingMesh needs below - skip
            // gracefully (no outline for this piece) rather than spamming a console error every
            // time this component enables.
            if (!mf.sharedMesh.isReadable) continue;

            Renderer sourceRenderer = mf.GetComponent<Renderer>();
            if (sourceRenderer == null) continue;

            // A zero-thickness mesh has no back to show: its reversed copy faces away from the
            // viewer and is culled, so flat pieces get a front-facing rim set just behind them.
            // A see-through flat piece (a mud-print decal, cut-out marks) has nothing solid to hide a
            // rim placed behind it, so that rim would show through as a filled glowing slab - those get
            // a ring around the edge instead.
            bool flat = IsFlat(mf.sharedMesh, out int flatAxis);
            Material srcMat = sourceRenderer.sharedMaterial;
            bool seeThrough = srcMat != null && srcMat.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            UnityEngine.Mesh invertedMesh = !flat
                ? BuildReversedWindingMesh(mf.sharedMesh)
                : seeThrough
                    ? BuildFlatRingMesh(mf.sharedMesh, flatAxis, mf.transform.lossyScale)
                    : BuildFlatRimMesh(mf.sharedMesh, flatAxis, mf.transform.lossyScale);

            GameObject outline = new GameObject(mf.gameObject.name + "_Outline");
            outline.transform.SetParent(mf.transform, false);
            outline.transform.localScale = flat ? Vector3.one : Vector3.one * outlineScale;
            outline.layer = mf.gameObject.layer;
            outline.hideFlags = HideFlags.DontSave;

            MeshFilter outlineFilter = outline.AddComponent<MeshFilter>();
            outlineFilter.sharedMesh = invertedMesh;

            MeshRenderer outlineRenderer = outline.AddComponent<MeshRenderer>();
            Material mat = new Material(unlitShader);
            mat.SetColor(GlowPipeline.ColorProperty, finalColor);
            outlineRenderer.sharedMaterial = mat;
            outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;

            outlineObjects.Add(outline);
            sourceRenderers.Add(sourceRenderer);
        }

        // Paper notes are SpriteRenderers, with no mesh to copy - and they're drawn translucent, so
        // anything placed behind them shows through. They get a flat frame around the sprite's
        // rectangle instead, which covers nothing of the note itself.
        foreach (var src in GetComponentsInChildren<SpriteRenderer>())
        {
            if (src.sprite == null || src.gameObject.name.EndsWith("_Outline")) continue;

            GameObject outline = new GameObject(src.gameObject.name + "_Outline");
            outline.transform.SetParent(src.transform, false);
            outline.layer = src.gameObject.layer;
            outline.hideFlags = HideFlags.DontSave;

            MeshFilter outlineFilter = outline.AddComponent<MeshFilter>();
            outlineFilter.sharedMesh = BuildFrameMesh(src.sprite.bounds, src.transform.lossyScale);

            MeshRenderer outlineRenderer = outline.AddComponent<MeshRenderer>();
            Material mat = new Material(unlitShader);
            mat.SetColor(GlowPipeline.ColorProperty, finalColor);
            outlineRenderer.sharedMaterial = mat;
            outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;

            outlineObjects.Add(outline);
            sourceRenderers.Add(src);
        }
    }

    // A flatRimWidth-wide rectangular ring around `inner` (in the sprite's local XY plane), with
    // both windings so it shows from either side like the sprite does.
    UnityEngine.Mesh BuildFrameMesh(Bounds inner, Vector3 lossyScale)
    {
        float rx = flatRimWidth / Mathf.Max(Mathf.Abs(lossyScale.x), 0.0001f);
        float ry = flatRimWidth / Mathf.Max(Mathf.Abs(lossyScale.y), 0.0001f);
        Vector3 min = inner.min, max = inner.max;
        float z = inner.center.z;

        var verts = new Vector3[]
        {
            new Vector3(min.x, min.y, z), new Vector3(max.x, min.y, z), new Vector3(max.x, max.y, z), new Vector3(min.x, max.y, z),
            new Vector3(min.x - rx, min.y - ry, z), new Vector3(max.x + rx, min.y - ry, z), new Vector3(max.x + rx, max.y + ry, z), new Vector3(min.x - rx, max.y + ry, z),
        };

        var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < 4; i++)
        {
            int a = i, b = (i + 1) % 4, oa = a + 4, ob = b + 4;
            tris.AddRange(new[] { a, oa, ob, a, ob, b });   // one side
            tris.AddRange(new[] { a, ob, oa, a, b, ob });   // and the other
        }

        UnityEngine.Mesh mesh = new UnityEngine.Mesh();
        mesh.name = "SpriteFrame";
        mesh.vertices = verts;
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static bool IsFlat(UnityEngine.Mesh mesh, out int axis)
    {
        Vector3 size = mesh.bounds.size;
        axis = 0;
        if (size.y < size[axis]) axis = 1;
        if (size.z < size[axis]) axis = 2;
        float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        return largest > 0f && size[axis] <= largest * 0.01f;
    }

    // Same winding as the source, grown by flatRimWidth (in world units) around its centre in the
    // plane and pushed a couple of millimetres behind its face, so the original covers the middle
    // and only the rim shows.
    UnityEngine.Mesh BuildFlatRimMesh(UnityEngine.Mesh source, int flatAxis, Vector3 lossyScale)
    {
        Vector3 center = source.bounds.center;
        Vector3 size = source.bounds.size;

        Vector3 grow = Vector3.one;
        for (int a = 0; a < 3; a++)
        {
            if (a == flatAxis) continue;
            float worldSize = size[a] * Mathf.Abs(lossyScale[a]);
            if (worldSize > 0.0001f) grow[a] = 1f + 2f * flatRimWidth / worldSize;
        }

        // Which way the face points along the flat axis; the rim goes on the other side.
        float facing = 0f;
        Vector3[] normals = source.normals;
        for (int i = 0; i < normals.Length; i++) facing += normals[i][flatAxis];
        float side = facing >= 0f ? -1f : 1f;
        float axisScale = Mathf.Max(Mathf.Abs(lossyScale[flatAxis]), 0.0001f);
        float push = side * 0.002f / axisScale;

        Vector3[] verts = source.vertices;
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 v = verts[i] - center;
            v.Scale(grow);
            v[flatAxis] += push;
            verts[i] = center + v;
        }

        UnityEngine.Mesh copy = new UnityEngine.Mesh();
        copy.name = source.name + "_FlatRim";
        copy.indexFormat = source.indexFormat;
        copy.vertices = verts;
        copy.uv = source.uv;
        copy.subMeshCount = source.subMeshCount;
        for (int sub = 0; sub < source.subMeshCount; sub++) copy.SetTriangles(source.GetTriangles(sub), sub);
        copy.RecalculateNormals();
        copy.RecalculateBounds();
        return copy;
    }

    // A flatRimWidth-wide rectangular ring around a flat mesh's bounds, in its own plane, lifted a
    // millimetre to the side its face points at (so it doesn't z-fight whatever it lies on). Both
    // windings, like BuildFrameMesh. Covers nothing of the mesh itself.
    UnityEngine.Mesh BuildFlatRingMesh(UnityEngine.Mesh source, int flatAxis, Vector3 lossyScale)
    {
        int a = flatAxis == 0 ? 1 : 0;
        int b = flatAxis == 2 ? 1 : 2;
        Bounds bb = source.bounds;
        float ra = flatRimWidth / Mathf.Max(Mathf.Abs(lossyScale[a]), 0.0001f);
        float rb = flatRimWidth / Mathf.Max(Mathf.Abs(lossyScale[b]), 0.0001f);

        float facing = 0f;
        Vector3[] normals = source.normals;
        for (int i = 0; i < normals.Length; i++) facing += normals[i][flatAxis];
        float plane = bb.center[flatAxis] + (facing >= 0f ? 1f : -1f) * 0.001f / Mathf.Max(Mathf.Abs(lossyScale[flatAxis]), 0.0001f);

        float[,] corners =
        {
            { bb.min[a], bb.min[b] }, { bb.max[a], bb.min[b] }, { bb.max[a], bb.max[b] }, { bb.min[a], bb.max[b] },
            { bb.min[a] - ra, bb.min[b] - rb }, { bb.max[a] + ra, bb.min[b] - rb }, { bb.max[a] + ra, bb.max[b] + rb }, { bb.min[a] - ra, bb.max[b] + rb },
        };
        var verts = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            Vector3 v = Vector3.zero;
            v[a] = corners[i, 0]; v[b] = corners[i, 1]; v[flatAxis] = plane;
            verts[i] = v;
        }

        var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < 4; i++)
        {
            int p = i, q = (i + 1) % 4, op = p + 4, oq = q + 4;
            tris.AddRange(new[] { p, op, oq, p, oq, q });
            tris.AddRange(new[] { p, oq, op, p, q, oq });
        }

        UnityEngine.Mesh mesh = new UnityEngine.Mesh();
        mesh.name = source.name + "_FlatRing";
        mesh.vertices = verts;
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static UnityEngine.Mesh BuildReversedWindingMesh(UnityEngine.Mesh source)
    {
        UnityEngine.Mesh copy = new UnityEngine.Mesh();
        copy.name = source.name + "_ReversedWinding";
        copy.vertices = source.vertices;
        copy.uv = source.uv;

        copy.subMeshCount = source.subMeshCount;
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            int[] tris = source.GetTriangles(sub);
            int[] flipped = new int[tris.Length];
            for (int i = 0; i < tris.Length; i += 3)
            {
                flipped[i] = tris[i];
                flipped[i + 1] = tris[i + 2];
                flipped[i + 2] = tris[i + 1];
            }
            copy.SetTriangles(flipped, sub);
        }

        copy.RecalculateNormals();
        copy.RecalculateBounds();
        return copy;
    }

    void ClearOutlines()
    {
        for (int i = outlineObjects.Count - 1; i >= 0; i--)
        {
            if (outlineObjects[i] != null)
            {
                if (Application.isPlaying) Destroy(outlineObjects[i]);
                else DestroyImmediate(outlineObjects[i]);
            }
        }
        outlineObjects.Clear();
        sourceRenderers.Clear();
    }
}
