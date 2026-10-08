using UnityEngine;

// Fades a full-screen black CanvasGroup from opaque to transparent over the first moment of a
// scene, so loading into it from elsewhere reads as a deliberate cut rather than an abrupt flash
// (particularly noticeable straight out of the CutScene, which fades to black before loading here).
public class SceneFadeIn : MonoBehaviour
{
    public CanvasGroup fadeGroup;
    public float duration = 0.8f;

    private void Start()
    {
        if (fadeGroup == null) return;
        fadeGroup.alpha = 1f;
        fadeGroup.blocksRaycasts = false;
        StartCoroutine(Fade());
    }

    private System.Collections.IEnumerator Fade()
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(1f, 0f, t / duration);
            yield return null;
        }
        fadeGroup.alpha = 0f;
    }
}
