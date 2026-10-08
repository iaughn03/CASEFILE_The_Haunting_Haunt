using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Full-screen punch shown alongside a jump scare's shake + sound - without it the scare has no
// screen-space payoff, just an actor popping into view for a quarter second. Owns a dedicated
// overlay Image rather than reusing the damage-flash overlay so it can't fight PlayerHealth's own
// coroutine over the same CanvasGroup alpha when a hit and a scare land close together.
public class JumpscareFlash : MonoBehaviour
{
    public static JumpscareFlash Instance;

    // Lets other atmosphere systems (false ambient cues, the ghost's glimpse behavior) check how
    // recently a real scare landed and hold off for a beat afterward, so a quiet stinger doesn't
    // immediately follow and undercut it.
    public static float LastScareTime { get; private set; } = -999f;

    private Image overlay;
    private Coroutine activeRoutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        overlay = GetComponent<Image>();
        if (overlay != null)
        {
            Color c = overlay.color;
            c.a = 0f;
            overlay.color = c;
            overlay.raycastTarget = false;
        }
    }

    public void Flash(float peakAlpha = 0.85f, float duration = 0.25f)
    {
        LastScareTime = Time.time;

        // Piggyback the 2D screamer picture+sting onto every existing scare call site (actor
        // pop-in, picture swing, ghost chase) instead of threading it through each one individually.
        if (Jumpscare2D.Instance != null) Jumpscare2D.Instance.Show();

        if (overlay == null) return;
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(FlashRoutine(peakAlpha, duration));
    }

    private IEnumerator FlashRoutine(float peakAlpha, float duration)
    {
        Color c = overlay.color;
        c.a = peakAlpha;
        overlay.color = c;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(peakAlpha, 0f, t / duration);
            overlay.color = c;
            yield return null;
        }

        c.a = 0f;
        overlay.color = c;
    }
}
