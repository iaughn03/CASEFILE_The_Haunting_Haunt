using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Press and hold to sign: progress climbs slowly while the pointer is held and drains if it is released
// early. Runs on unscaled time because the game is paused during the Final Inspection.
public class HoldToSignButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public float holdSeconds = 2.8f;

    public event Action<float> Progress;
    public event Action Completed;

    public bool Holding { get; private set; }
    public float Value { get; private set; }
    private bool done;

    private void OnEnable()
    {
        Value = 0f;
        Holding = false;
        done = false;
    }

    private void Update()
    {
        if (done) return;

        float before = Value;
        if (Holding) Value += Time.unscaledDeltaTime / Mathf.Max(0.1f, holdSeconds);
        else Value -= Time.unscaledDeltaTime * 0.6f / Mathf.Max(0.1f, holdSeconds) * 2f;
        Value = Mathf.Clamp01(Value);

        if (!Mathf.Approximately(before, Value)) Progress?.Invoke(Value);

        if (Value >= 1f)
        {
            done = true;
            Holding = false;
            Completed?.Invoke();
        }
    }

    public void OnPointerDown(PointerEventData eventData) { if (!done) Holding = true; }
    public void OnPointerUp(PointerEventData eventData) { Holding = false; }
    public void OnPointerExit(PointerEventData eventData) { Holding = false; }
}
