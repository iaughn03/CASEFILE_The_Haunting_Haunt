using UnityEngine;

// Applies the resolution / window mode chosen in Settings > Display. Changes are collected and
// applied once, a short moment after the last click, instead of one screen switch per click
// (see GameSettings.ApplyDisplay). Built at runtime and kept across scene loads.
public class DisplayApplier : MonoBehaviour
{
    private const float Delay = 0.5f;

    private static DisplayApplier instance;
    private static Vector2Int lastApplied;
    private static FullScreenMode lastMode;
    private static bool hasApplied;

    private Vector2Int pendingRes;
    private FullScreenMode pendingMode;
    private float applyAt = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        hasApplied = false;
    }

    public static void Request(Vector2Int res, FullScreenMode mode, bool immediate)
    {
        if (immediate)
        {
            if (instance != null) instance.applyAt = -1f;
            Apply(res, mode);
            return;
        }
        if (instance == null)
        {
            var go = new GameObject("DisplayApplier");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DisplayApplier>();
        }
        instance.pendingRes = res;
        instance.pendingMode = mode;
        instance.applyAt = Time.unscaledTime + Delay;
    }

    private void Update()
    {
        if (applyAt < 0f || Time.unscaledTime < applyAt) return;
        applyAt = -1f;
        Apply(pendingRes, pendingMode);
    }

    // Compares against what was last asked for, not Screen.width (which lags behind a switch), so a
    // repeated Commit (e.g. dragging the FOV slider) never re-sets the same mode and flashes black.
    private static void Apply(Vector2Int res, FullScreenMode mode)
    {
        if (hasApplied && res == lastApplied && mode == lastMode) return;
        if (!hasApplied && Screen.width == res.x && Screen.height == res.y && Screen.fullScreenMode == mode)
        {
            hasApplied = true; lastApplied = res; lastMode = mode;
            return;
        }
        hasApplied = true; lastApplied = res; lastMode = mode;
        Screen.SetResolution(res.x, res.y, mode);
    }
}
