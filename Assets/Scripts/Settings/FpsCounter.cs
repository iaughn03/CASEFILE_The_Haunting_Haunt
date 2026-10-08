using UnityEngine;
using UnityEngine.UI;

// Small frames-per-second readout in the top-left corner, toggled by Settings > Display > Show FPS.
// Built at runtime and kept across scene loads, so no scene needs to carry it.
public class FpsCounter : MonoBehaviour
{
    private static FpsCounter instance;
    private Text label;
    private float smoothed;
    private float refreshTimer;

    public static void SetVisible(bool visible)
    {
        if (!visible)
        {
            if (instance != null) instance.gameObject.SetActive(false);
            return;
        }
        if (instance == null) Create();
        instance.gameObject.SetActive(true);
    }

    private static void Create()
    {
        var go = new GameObject("FpsCounter");
        DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var rt = (RectTransform)textGo.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(12f, -10f);
        rt.sizeDelta = new Vector2(240f, 40f);

        instance = go.AddComponent<FpsCounter>();
        instance.label = textGo.AddComponent<Text>();
        instance.label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        instance.label.fontSize = 22;
        instance.label.color = new Color(0.85f, 0.85f, 0.8f, 0.9f);
        instance.label.raycastTarget = false;
        textGo.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
    }

    private void Update()
    {
        // unscaled: the readout keeps working while the game is paused in a menu
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        smoothed = smoothed <= 0f ? 1f / dt : Mathf.Lerp(smoothed, 1f / dt, 0.1f);
        refreshTimer -= dt;
        if (refreshTimer > 0f) return;
        refreshTimer = 0.25f;
        label.text = Mathf.RoundToInt(smoothed) + " FPS";
    }
}
