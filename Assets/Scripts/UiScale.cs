using UnityEngine;

// Scale of a UI element measured inside its canvas, i.e. with the canvas' own scale taken out.
// The canvas scales with the screen size (CanvasScaler), so anything that compensates for a slot's
// ancestor scale has to leave that part alone - otherwise an item icon stays the same pixel size
// while its slot shrinks on a smaller screen.
public static class UiScale
{
    public static Vector3 WithinCanvas(Transform t)
    {
        if (t == null) return Vector3.one;
        Vector3 s = t.lossyScale;
        Canvas canvas = t.GetComponentInParent<Canvas>(true);
        if (canvas == null) return s;
        Vector3 c = canvas.rootCanvas.transform.lossyScale;
        return new Vector3(
            c.x != 0f ? s.x / c.x : s.x,
            c.y != 0f ? s.y / c.y : s.y,
            c.z != 0f ? s.z / c.z : s.z);
    }
}
