using UnityEngine;
using System.Collections;

// Mirrors MainMenu's cinematic push-out: starts fully black with the letterbox bars at full height
// (matching exactly how the previous scene ended), then fades to transparent while the bars retract,
// like curtains opening onto the new scene rather than an abrupt cut.
public class CinematicRevealIn : MonoBehaviour
{
    public CanvasGroup fadeGroup;
    public RectTransform letterboxTop;
    public RectTransform letterboxBottom;
    public float letterboxHeight = 140f;
    public float duration = 0.6f;

    private void Start()
    {
        if (fadeGroup == null) return;
        fadeGroup.alpha = 1f;
        fadeGroup.blocksRaycasts = false;
        if (letterboxTop != null) letterboxTop.sizeDelta = new Vector2(letterboxTop.sizeDelta.x, letterboxHeight);
        if (letterboxBottom != null) letterboxBottom.sizeDelta = new Vector2(letterboxBottom.sizeDelta.x, letterboxHeight);
        StartCoroutine(Reveal());
    }

    private IEnumerator Reveal()
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float eased = 1f - (1f - p) * (1f - p); // ease-out - settles rather than snapping open
            fadeGroup.alpha = Mathf.Lerp(1f, 0f, eased);
            float barHeight = Mathf.Lerp(letterboxHeight, 0f, eased);
            if (letterboxTop != null) letterboxTop.sizeDelta = new Vector2(letterboxTop.sizeDelta.x, barHeight);
            if (letterboxBottom != null) letterboxBottom.sizeDelta = new Vector2(letterboxBottom.sizeDelta.x, barHeight);
            yield return null;
        }
        fadeGroup.alpha = 0f;
        if (letterboxTop != null) letterboxTop.sizeDelta = new Vector2(letterboxTop.sizeDelta.x, 0f);
        if (letterboxBottom != null) letterboxBottom.sizeDelta = new Vector2(letterboxBottom.sizeDelta.x, 0f);
    }
}
