using UnityEngine;
using UnityEngine.UI;

// A dark card button (used for the suspect dossiers) with a neutral "this is the one I picked" state.
public class SelectableCard : MonoBehaviour
{
    public Image plate;
    public Outline outline;
    public Color offColor = new Color(0.10f, 0.10f, 0.12f, 0.96f);
    public Color onColor = new Color(0.20f, 0.17f, 0.13f, 0.98f);
    public Color outlineOff = new Color(0.55f, 0.50f, 0.42f, 0.35f);
    public Color outlineOn = new Color(0.96f, 0.86f, 0.55f, 1f);

    private void Awake()
    {
        SetSelected(false);
    }

    public void SetSelected(bool on)
    {
        if (plate != null) plate.color = on ? onColor : offColor;
        if (outline != null) outline.effectColor = on ? outlineOn : outlineOff;
    }
}
