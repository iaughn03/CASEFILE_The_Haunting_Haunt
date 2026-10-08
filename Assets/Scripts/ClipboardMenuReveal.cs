using UnityEngine;
using System.Collections;

// The clipboard's menu Canvas does not animate itself at all - it simply switches on at its fixed
// size once the camera's zoom-in (MenuZoom) has arrived. The only "going in" animation is the
// camera's own travel toward the clipboard; the menu is just there waiting when it lands.
public class ClipboardMenuReveal : MonoBehaviour
{
    [Header("References")]
    public MenuZoom menuZoom;
    public Transform menuCanvasTransform;
    public Vector3 revealedScale = new Vector3(0.0016f, 0.0016f, 0.0016f);

    [Tooltip("Roughly how long MenuZoom's camera lerp takes to arrive, so the menu switches on right as the camera lands rather than before it.")]
    public float revealDelay = 0.4f;

    private bool hasRevealed = false;
    private Coroutine revealRoutine;

    void Start()
    {
        if (menuZoom == null) menuZoom = FindFirstObjectByType<MenuZoom>();
        if (menuCanvasTransform != null) menuCanvasTransform.localScale = Vector3.zero;
    }

    // Unity broadcasts OnMouseDown to every component on the GameObject that implements it, so this
    // coexists cleanly alongside ClickToZoom's own OnMouseDown - no need to touch that script.
    void OnMouseDown() => TriggerReveal();

    void Update()
    {
        // Zooming back out (right-click, per MenuZoom) hides the menu again so a future click
        // re-triggers the same reveal.
        if (hasRevealed && menuZoom != null && !menuZoom.isZoomed)
        {
            hasRevealed = false;
            if (revealRoutine != null) StopCoroutine(revealRoutine);
            if (menuCanvasTransform != null) menuCanvasTransform.localScale = Vector3.zero;
        }
    }

    public void TriggerReveal()
    {
        if (hasRevealed) return;
        hasRevealed = true;
        revealRoutine = StartCoroutine(RevealAfterZoom());
    }

    IEnumerator RevealAfterZoom()
    {
        yield return new WaitForSecondsRealtime(revealDelay);
        if (menuCanvasTransform != null) menuCanvasTransform.localScale = revealedScale;
    }
}
