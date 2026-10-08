using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// Ramps up tension as the mystery gets closer to being solved. EvidenceManager.AddEvidence() calls
// into this at two fixed thresholds: half the evidence collected (the ghost becomes noticeably
// harder to escape) and only 3 pieces left (the asylum gets visibly darker). Both thresholds also
// punctuate the shift with a jumpscare so the change actually registers with the player instead of
// creeping in unnoticed.
public class DifficultyDirector : MonoBehaviour
{
    [Header("Ghost Escalation (fires once, at half the evidence collected)")]
    public GhostController ghost;
    public float chaseSpeedMultiplier = 1.3f;
    public float roamSpeedMultiplier = 1.25f;
    public float loseSightGraceMultiplier = 0.5f;
    public float searchDurationBonus = 3f;
    public float detectionRangeBonus = 3f;
    [Range(0f, 1f)] public float flashlightSlowMultiplierBoost = 0.2f; // less effective flashlight weakness

    [Header("Darkness Escalation (fires once, at 3 evidence remaining)")]
    public Image brightnessOverlay;
    [Range(0f, 1f)] public float extraDarknessAlpha = 0.28f;
    public float darknessRampDuration = 4f;

    [Header("Evidence Visibility (read by EvidenceGlow / EvidenceHintDirector)")]
    [Tooltip("Base multiplier on how strongly uncollected evidence glows and emits dust.")]
    public float baseGlowStrength = 1f;
    [Range(0f, 1f)] public float glowScaleAfterHalf = 0.85f;
    [Range(0f, 1f)] public float glowScaleThreeLeft = 0.8f;
    [Tooltip("Seconds without finding anything before the stuck hint fires.")]
    public float baseHintDelaySeconds = 90f;
    public float hintDelayScaleAfterHalf = 1.15f;
    public float hintDelayScaleThreeLeft = 1.15f;

    public float EvidenceGlowMultiplier =>
        baseGlowStrength * (halfTriggered ? glowScaleAfterHalf : 1f) * (threeLeftTriggered ? glowScaleThreeLeft : 1f);

    public float EvidenceHintDelay =>
        baseHintDelaySeconds * (halfTriggered ? hintDelayScaleAfterHalf : 1f) * (threeLeftTriggered ? hintDelayScaleThreeLeft : 1f);

    [Header("Escalation Jumpscares (reuses the shared scare actor/system)")]
    public JumpScareTrigger halfEvidenceScare;
    public JumpScareTrigger threeLeftScare;

    private bool halfTriggered = false;
    private bool threeLeftTriggered = false;

    public void OnHalfEvidenceCollected()
    {
        if (halfTriggered) return;
        halfTriggered = true;

        if (ghost != null)
        {
            ghost.chaseSpeed *= chaseSpeedMultiplier;
            ghost.roamSpeed *= roamSpeedMultiplier;
            ghost.loseSightGracePeriod *= loseSightGraceMultiplier;
            ghost.searchDuration += searchDurationBonus;
            ghost.detectionRange += detectionRangeBonus;
            ghost.flashlightSlowMultiplier = Mathf.Clamp01(ghost.flashlightSlowMultiplier + flashlightSlowMultiplierBoost);
        }

        // Milestone beat - has to land the moment the threshold is crossed, so it bypasses the shared
        // scare spacing rather than being skipped for arriving too soon after an ambient trigger.
        if (halfEvidenceScare != null) halfEvidenceScare.TriggerScare(true);
    }

    public void OnThreeEvidenceRemaining()
    {
        if (threeLeftTriggered) return;
        threeLeftTriggered = true;

        if (brightnessOverlay != null) StartCoroutine(RampDarkness());
        if (threeLeftScare != null) threeLeftScare.TriggerScare(true);
    }

    private IEnumerator RampDarkness()
    {
        Color start = brightnessOverlay.color;
        Color end = start;
        end.a = Mathf.Clamp01(start.a + extraDarknessAlpha);

        float t = 0f;
        while (t < darknessRampDuration)
        {
            t += Time.deltaTime;
            brightnessOverlay.color = Color.Lerp(start, end, t / darknessRampDuration);
            yield return null;
        }

        brightnessOverlay.color = end;
    }
}
