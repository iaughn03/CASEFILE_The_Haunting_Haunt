using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// Attached alongside DoorController on each level's exit (Game1 Main Door, Level2/Level3 exit points).
// PlayerInteract.HandleInteraction() checks for this component first: until every clue is found it
// shows ProgressText, and once HasAllEvidence is true the interaction opens the Final Inspection.
// The older three card choice below (OpenChoice/ChooseMurder/...) only runs in a scene without a
// FinalInspection, which no level has any more; its texts still follow the Philippine Revised
// Penal Code like the rest of the game:
//   Parricide (Art. 246) - the killer is the victim's father, mother, child, other direct ascendant or
//                          descendant, or spouse
//   Murder    (Art. 248) - not parricide, but with a qualifying circumstance (evident premeditation,
//                          treachery, poison, fire, a reward, cruelty)
//   Homicide  (Art. 249) - neither of the above
public class DeductionGate : MonoBehaviour
{
    [Header("Evidence Requirement")]
    public EvidenceManager evidenceManager;

    [Header("UI Panels")]
    public GameObject choicePanel;
    public GameObject victoryPanel;
    public GameObject gameOverPanel;

    [Header("Case Recap")]
    [Tooltip("Lists the evidence the player actually collected when the choice panel opens, so the final decision reads as a conclusion drawn from the case file rather than a cold guess between three buttons.")]
    public TextMeshProUGUI evidenceRecapText;

    [Header("Evidence Review")]
    [Tooltip("The player's existing bag/evidence UI, reused here so they can double-check what they found before committing to a final answer. Reusing it (rather than a separate viewer) means it already knows how to display every evidence item.")]
    public GameObject evidenceViewPanel;
    [Tooltip("Shown only while evidenceViewPanel is open from this screen, so the player has an obvious way back to the choice cards instead of needing to remember the Tab/I shortcut.")]
    public GameObject evidenceViewCloseButton;

    [Header("Wrong-Answer Explanations")]
    [Tooltip("Shown on the game-over panel so a wrong guess still teaches the player what they missed, instead of just reusing the same generic 'YOU DIED' text for every wrong answer.")]
    public TextMeshProUGUI gameOverSubtitleText;
    [TextArea]
    public string homicideWrongReason = HomicideWrongDefault;
    [TextArea]
    public string parricideWrongReason = ParricideWrongDefault;

    public const string HomicideWrongDefault = "Homicide (Revised Penal Code, Art. 249) is only a killing that is neither parricide nor murder. Check the evidence again for a family tie to the victim, or for a qualifying circumstance such as deciding ahead of time to kill and carrying it out, or attacking so the victim cannot defend themselves.";
    public const string ParricideWrongDefault = "Parricide (Revised Penal Code, Art. 246) needs the killer to be the victim's father, mother, child, other direct ascendant or descendant, or spouse. Check again how the person you suspect is related to the victim.";
    public const string CaseClosedDefault = "Case closed. The ruling follows the Revised Penal Code: Parricide (Art. 246) when the killer is the victim's father, mother, child, other direct ascendant or descendant, or spouse; otherwise Murder (Art. 248) when there is a qualifying circumstance such as evident premeditation or treachery; otherwise Homicide (Art. 249).";

    [Header("Case Closed Debrief")]
    [Tooltip("Optional direct reference - leave empty and it's found by name under victoryPanel instead. Kept as an explicit override slot rather than the only way to wire it because victoryPanel/DeductionGate live in different scenes (Game1 vs Game3): Unity doesn't reliably persist a direct cross-scene reference assigned here, so ChooseMurder() falls back to victoryPanel.transform.Find(\"CaseClosedDebrief\") at runtime instead.")]
    public TextMeshProUGUI caseClosedDebriefText;
    [TextArea]
    public string caseClosedDebrief = CaseClosedDefault;

    [Header("Choice Reveal Animation")]
    [Tooltip("The panel's own backdrop/scrim CanvasGroup - fades in first, together with the title punching into place.")]
    public CanvasGroup panelFadeGroup;
    [Tooltip("The title question ('What really happened to Dr. Vance?') - punches in from a slight overscale rather than just appearing.")]
    public CanvasGroup titleGroup;
    public CanvasGroup evidenceRecapGroup;
    [Tooltip("Homicide, Parricide, Murder cards in the order they should reveal - top to bottom, like evidence being laid out on a board one at a time rather than all three slamming into view together.")]
    public CanvasGroup[] choiceCardGroups;
    public CanvasGroup viewEvidenceGroup;
    public float revealFadeInDuration = 0.35f;
    public float cardRevealDuration = 0.35f;
    [Tooltip("Delay before each successive card starts its own reveal.")]
    public float cardStagger = 0.18f;
    [Tooltip("How far below its resting position each card starts, sliding up into place as it fades in.")]
    public float cardSlideDistance = 40f;

    public bool HasAllEvidence =>
        evidenceManager != null &&
        evidenceManager.currentEvidenceCount >= evidenceManager.maxEvidenceNeeded;

    public string ProgressText =>
        evidenceManager != null
            ? $"Find all the evidence before you leave. ({evidenceManager.currentEvidenceCount}/{evidenceManager.maxEvidenceNeeded})"
            : "Find all the evidence before you leave.";

    public void OpenChoice()
    {
        if (choicePanel == null) return;

        if (evidenceRecapText != null && evidenceManager != null)
        {
            evidenceRecapText.text = BuildCaseFileText();
        }

        choicePanel.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        StartCoroutine(PlayChoiceReveal());
    }

    // Groups the case-file recap under Means/Motive/Opportunity headers instead of one flat list,
    // so the final choice reads as a conclusion drawn from categorized evidence. Items left
    // Uncategorized (see InspectItem.EvidenceCategory) don't fit any of the three legal buckets and
    // are omitted here rather than forced under a header they don't belong in.
    string BuildCaseFileText()
    {
        var means = new List<string>();
        var motive = new List<string>();
        var opportunity = new List<string>();

        for (int i = 0; i < evidenceManager.collectedEvidenceNames.Count; i++)
        {
            InspectItem.EvidenceCategory category = i < evidenceManager.collectedEvidenceCategories.Count
                ? evidenceManager.collectedEvidenceCategories[i]
                : InspectItem.EvidenceCategory.Uncategorized;

            string name = evidenceManager.collectedEvidenceNames[i];

            switch (category)
            {
                case InspectItem.EvidenceCategory.Means: means.Add(name); break;
                case InspectItem.EvidenceCategory.Motive: motive.Add(name); break;
                case InspectItem.EvidenceCategory.Opportunity: opportunity.Add(name); break;
            }
        }

        var lines = new List<string>();
        if (means.Count > 0) lines.Add("Means: " + string.Join("  •  ", means));
        if (motive.Count > 0) lines.Add("Motive: " + string.Join("  •  ", motive));
        if (opportunity.Count > 0) lines.Add("Opportunity: " + string.Join("  •  ", opportunity));

        return "Case file:\n" + string.Join("\n", lines);
    }

    // Lays the choice out as a small reveal rather than one flat instant cut: the backdrop and title
    // punch in together, the case-file recap fades in under it, then the three verdict cards stagger
    // in one at a time - like evidence actually being placed on a board - before View Evidence
    // appears last. Every card is non-interactable until its own reveal finishes so a stray click
    // can't land on a card that's still fading in.
    IEnumerator PlayChoiceReveal()
    {
        if (panelFadeGroup != null) panelFadeGroup.alpha = 0f;

        if (titleGroup != null)
        {
            titleGroup.alpha = 0f;
            titleGroup.transform.localScale = Vector3.one * 1.15f;
        }

        if (evidenceRecapGroup != null) evidenceRecapGroup.alpha = 0f;

        Vector2[] cardRestPositions = null;
        if (choiceCardGroups != null)
        {
            cardRestPositions = new Vector2[choiceCardGroups.Length];
            for (int i = 0; i < choiceCardGroups.Length; i++)
            {
                if (choiceCardGroups[i] == null) continue;
                choiceCardGroups[i].alpha = 0f;
                choiceCardGroups[i].interactable = false;
                choiceCardGroups[i].blocksRaycasts = false;
                var rt = choiceCardGroups[i].GetComponent<RectTransform>();
                cardRestPositions[i] = rt.anchoredPosition;
                rt.anchoredPosition = cardRestPositions[i] - new Vector2(0f, cardSlideDistance);
            }
        }

        if (viewEvidenceGroup != null) viewEvidenceGroup.alpha = 0f;

        float t = 0f;
        while (t < revealFadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / revealFadeInDuration), 2f);
            if (panelFadeGroup != null) panelFadeGroup.alpha = eased;
            if (titleGroup != null)
            {
                titleGroup.alpha = eased;
                titleGroup.transform.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, eased);
            }
            yield return null;
        }

        if (panelFadeGroup != null) panelFadeGroup.alpha = 1f;
        if (titleGroup != null)
        {
            titleGroup.alpha = 1f;
            titleGroup.transform.localScale = Vector3.one;
        }

        if (evidenceRecapGroup != null) yield return StartCoroutine(FadeGroup(evidenceRecapGroup, 0.25f));

        if (choiceCardGroups != null)
        {
            for (int i = 0; i < choiceCardGroups.Length; i++)
            {
                if (choiceCardGroups[i] != null)
                    StartCoroutine(RevealCard(choiceCardGroups[i], cardRestPositions[i]));
                yield return new WaitForSecondsRealtime(cardStagger);
            }
        }

        if (viewEvidenceGroup != null) yield return StartCoroutine(FadeGroup(viewEvidenceGroup, 0.25f));
    }

    IEnumerator RevealCard(CanvasGroup group, Vector2 restPos)
    {
        var rt = group.GetComponent<RectTransform>();
        Vector2 startPos = rt.anchoredPosition;

        float t = 0f;
        while (t < cardRevealDuration)
        {
            t += Time.unscaledDeltaTime;
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / cardRevealDuration), 3f);
            group.alpha = eased;
            rt.anchoredPosition = Vector2.Lerp(startPos, restPos, eased);
            yield return null;
        }

        group.alpha = 1f;
        rt.anchoredPosition = restPos;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    IEnumerator FadeGroup(CanvasGroup group, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }

        group.alpha = 1f;
    }

    // Wired to the choice panel's "View Evidence" button. Hides the choice cards (rather than
    // layering the bag on top) so we don't have to fight the two panels' Canvas sibling order -
    // Time.timeScale and the unlocked cursor from OpenChoice() are left untouched since we're
    // still in the same paused, mouse-driven state the whole time.
    public void OpenEvidenceView()
    {
        if (evidenceViewPanel == null) return;

        if (choicePanel != null) choicePanel.SetActive(false);
        evidenceViewPanel.SetActive(true);
        if (evidenceViewCloseButton != null) evidenceViewCloseButton.SetActive(true);
    }

    // Wired to the "Back to Case File" button shown only while reviewing evidence from the choice
    // screen - mirrors InventoryToggle's own close cleanup (description panel, borrowed 3D item)
    // without touching its Time.timeScale/cursor handling, which would incorrectly resume the game
    // while the choice screen is still up.
    public void CloseEvidenceView()
    {
        if (evidenceViewPanel != null) evidenceViewPanel.SetActive(false);
        if (evidenceViewCloseButton != null) evidenceViewCloseButton.SetActive(false);

        DescriptionPanel descPanel = FindFirstObjectByType<DescriptionPanel>();
        if (descPanel != null) descPanel.Hide();

        if (ItemInspection3D.Instance != null) ItemInspection3D.Instance.StopInspecting();

        if (choicePanel != null) choicePanel.SetActive(true);
    }

    // Wired to the choice panel's "Homicide" card - a wrong answer.
    public void ChooseHomicide()
    {
        ShowGameOver(homicideWrongReason);
    }

    // Wired to the choice panel's "Parricide" card - a wrong answer.
    public void ChooseParricide()
    {
        ShowGameOver(parricideWrongReason);
    }

    void ShowGameOver(string reason)
    {
        if (choicePanel != null) choicePanel.SetActive(false);
        if (gameOverSubtitleText != null) gameOverSubtitleText.text = reason;
        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    // Wired to the choice panel's "Murder" card - the correct answer.
    public void ChooseMurder()
    {
        if (choicePanel != null) choicePanel.SetActive(false);
        ResolveCaseClosedDebriefText();
        if (caseClosedDebriefText != null) caseClosedDebriefText.text = caseClosedDebrief;
        if (victoryPanel != null) victoryPanel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    // caseClosedDebriefText can't be wired directly in the Inspector - DeductionGate lives on the
    // Map 1 door (Game1) while victoryPanel lives in Game3, and Unity doesn't reliably persist a
    // direct cross-scene reference assigned that way. victoryPanel itself is fine (resolved once at
    // scene-load time before either scene is saved standalone), so find the debrief text by name
    // under it instead of serializing a second cross-scene link.
    void ResolveCaseClosedDebriefText()
    {
        if (caseClosedDebriefText != null || victoryPanel == null) return;

        Transform found = victoryPanel.transform.Find("CaseClosedDebrief");
        if (found != null) caseClosedDebriefText = found.GetComponent<TextMeshProUGUI>();
    }
}
