using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Educational assessment mechanic: before DeductionGate reveals the "who did it" choice, this asks
// the player to classify WHAT happened - Murder, Manslaughter, or Accidental Death - using only the
// evidence they've collected. Deliberately separate from DeductionGate's whodunit choice (that's
// about identifying a suspect; this is a legal-classification exercise), so it fires first and
// DeductionGate.OpenChoice() only runs once the player has answered here.
public class CaseClassificationQuiz : MonoBehaviour
{
    public enum Classification { Murder, Manslaughter, Accidental }

    [Header("Correct Answer")]
    [Tooltip("The classification this case's evidence actually supports. The choking predates the staged hanging, the note was coerced, and a threat letter and taped confession show deliberate concealment - premeditation and cover-up are what make this Murder rather than Manslaughter (no premeditation) or an Accident (no intent at all).")]
    public Classification correctClassification = Classification.Murder;

    [Header("Flow")]
    [Tooltip("Opened once the player answers (right or wrong) and presses Continue - this is DeductionGate's existing whodunit choice, untouched by this quiz.")]
    public DeductionGate deductionGate;

    [Header("UI")]
    [Tooltip("The whole screen - background plus everything else. Toggled on/off by Open()/Proceed().")]
    public GameObject rootPanel;
    [Tooltip("Title + the three answer cards - hidden once answered so feedback isn't stacked on top of them.")]
    public GameObject questionGroup;
    public GameObject feedbackPanel;
    public TextMeshProUGUI feedbackText;

    [Header("Feedback Text")]
    // Source: standard criminal-law distinction between murder and manslaughter (premeditation plus
    // concealment vs. a sudden, unplanned act) - general legal principle, not a specific statistic.
    [TextArea]
    public string correctFeedback = "Correct. The choking injuries predate the rope marks, the note was typed under duress, and the scene was staged afterward. That planning and concealment are the hallmarks of murder, not a sudden, unplanned act. Legally, that premeditation plus concealment is usually exactly what separates a murder charge from manslaughter.";
    [TextArea]
    public string manslaughterWrongFeedback = "Not quite. Manslaughter covers a killing without premeditation: a sudden fight, a reckless act with no plan to conceal it. Here, the choking happened first and the hanging was staged afterward to cover it up. That planning points to something more deliberate.";
    [TextArea]
    public string accidentalWrongFeedback = "Not quite. An accidental death wouldn't come with a staged crime scene, a coerced note, and a threat letter warning the victim to stay silent. Someone went to considerable lengths to make this look like something it wasn't.";

    private bool hasAnswered;

    // Wired to the deduction door's interaction in place of calling DeductionGate.OpenChoice()
    // directly - see the matching change in PlayerInteract.HandleInteraction().
    public void Open()
    {
        hasAnswered = false;

        if (rootPanel == null)
        {
            // No quiz UI wired up - don't block the existing ending over an optional feature.
            if (deductionGate != null) deductionGate.OpenChoice();
            return;
        }

        rootPanel.SetActive(true);
        if (questionGroup != null) questionGroup.SetActive(true);
        if (feedbackPanel != null) feedbackPanel.SetActive(false);

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Wired to the quiz panel's three answer buttons.
    public void AnswerMurder() => Answer(Classification.Murder);
    public void AnswerManslaughter() => Answer(Classification.Manslaughter);
    public void AnswerAccidental() => Answer(Classification.Accidental);

    void Answer(Classification chosen)
    {
        if (hasAnswered) return;
        hasAnswered = true;

        bool correct = chosen == correctClassification;
        AssessmentResults.RecordClassification(correct);

        if (feedbackText != null)
        {
            feedbackText.text = correct
                ? correctFeedback
                : (chosen == Classification.Manslaughter ? manslaughterWrongFeedback : accidentalWrongFeedback);
        }

        if (questionGroup != null) questionGroup.SetActive(false);
        if (feedbackPanel != null) feedbackPanel.SetActive(true);
    }

    // Wired to the feedback panel's Continue button.
    public void Proceed()
    {
        if (rootPanel != null) rootPanel.SetActive(false);
        if (deductionGate != null) deductionGate.OpenChoice();
    }
}
