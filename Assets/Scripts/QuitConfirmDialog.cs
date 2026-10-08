using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The quit dialog opens with No already chosen, so a stray Enter or gamepad A can never drop the case.
// The highlight follows whatever is actually selected, including keyboard and gamepad navigation.
// Esc / gamepad B are handled by PauseManager so backing out can't race with the pause toggle.
public class QuitConfirmDialog : MonoBehaviour
{
    public Button yesButton;
    public Button noButton;

    [Header("Selection highlight")]
    public Outline yesHighlight;
    public Outline noHighlight;
    public Color highlightColor = new Color(0.96f, 0.86f, 0.55f, 1f);
    [Tooltip("The unchosen button sits back a little so the selected one reads at a glance.")]
    public Color idlePlate = new Color(0.62f, 0.62f, 0.62f, 1f);
    public Color idleLabel = new Color(0.62f, 0.61f, 0.58f, 1f);
    public Color selectedPlate = Color.white;
    public Color selectedLabel = new Color(0.96f, 0.90f, 0.72f, 1f);

    [Tooltip("Soft darkening at the edges so the text reads against the photo.")]
    public Image vignette;

    private void Awake()
    {
        if (vignette != null && vignette.sprite == null) vignette.sprite = FinalInspectionArt.Vignette();
    }

    private void OnEnable()
    {
        SelectNo();
        Refresh();
    }

    // Runs on every frame the dialog is up; the game is paused, so nothing else moves the selection.
    private void Update()
    {
        Refresh();
    }

    private void SelectNo()
    {
        var es = EventSystem.current;
        if (es == null || noButton == null) return;
        es.SetSelectedGameObject(null);
        es.SetSelectedGameObject(noButton.gameObject);
    }

    private void Refresh()
    {
        var es = EventSystem.current;
        GameObject selected = es != null ? es.currentSelectedGameObject : null;

        bool onYes = yesButton != null && selected == yesButton.gameObject;
        bool onNo = noButton != null && selected == noButton.gameObject;

        // Clicking empty space clears the selection, which would leave nothing highlighted and let a
        // later Enter press go nowhere - fall back to No, the safe answer.
        if (!onYes && !onNo)
        {
            SelectNo();
            onNo = true;
        }

        Apply(yesButton, yesHighlight, onYes);
        Apply(noButton, noHighlight, onNo);
    }

    private void Apply(Button button, Outline outline, bool selected)
    {
        if (outline != null)
        {
            outline.effectColor = highlightColor;
            outline.enabled = selected;
        }

        if (button == null) return;
        // Go through ButtonHoverPunch so it lerps from these resting colours instead of fighting them.
        var punch = button.GetComponent<ButtonHoverPunch>();
        if (punch != null) punch.SetBaseColors(selected ? selectedPlate : idlePlate, selected ? selectedLabel : idleLabel);
    }
}
