using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Keeps a percentage readout in sync with a Slider's value - purely cosmetic, doesn't touch whatever
// the slider's own listeners (volume, brightness, etc.) are already doing.
[RequireComponent(typeof(Slider))]
public class SliderValueLabel : MonoBehaviour
{
    public TextMeshProUGUI valueText;

    private Slider slider;

    void Awake()
    {
        slider = GetComponent<Slider>();
        slider.onValueChanged.AddListener(Refresh);
        Refresh(slider.value);
    }

    void Refresh(float value)
    {
        if (valueText != null) valueText.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}
