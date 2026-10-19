using UnityEngine;

// If the player goes too long without finding anything, pulse the nearest missing required item and
// change the HUD hint to point at its area. The delay comes from DifficultyDirector (so it stretches
// as the level escalates); "time" only advances while the game is actually running, so tutorials,
// menus, the bag and note reading don't count as being stuck.
public class EvidenceHintDirector : MonoBehaviour
{
    [Tooltip("Used only if no DifficultyDirector exists in the scene.")]
    public float fallbackDelaySeconds = 90f;
    [Tooltip("How long the boosted glow lasts once triggered.")]
    public float hintPulseSeconds = 30f;

    [Header("Late-unlock announcement - one clue that only becomes findable partway through, called\n     out once so the player knows to go back for it. Leave the name empty to disable.")]
    [Tooltip("EvidenceData.displayName of the clue to announce. Level 1: \"Physician's Preliminary Notes\". Level 2: the autopsy summary.")]
    public string announceItemDisplayName = "Physician's Preliminary Notes";
    [Tooltip("Announce once the player is holding this many required clues.")]
    public int announceAfterCollected = 3;
    [TextArea(1, 3)] public string announceMessage = "The on call physician's preliminary notes should be ready in the office.";

    private EvidenceManager evidence;
    private ObjectiveTracker tracker;
    private DifficultyDirector director;
    private PlayerInteract player;

    private float stuckTime;
    private int lastCollectedCount = -1;
    private EvidenceItem hinted;
    private float hintEndsAt;
    private bool autopsyAnnounced;

    private void Start()
    {
        evidence = FindFirstObjectByType<EvidenceManager>();
        tracker = FindFirstObjectByType<ObjectiveTracker>(FindObjectsInactive.Include);
        director = FindFirstObjectByType<DifficultyDirector>();
        player = FindFirstObjectByType<PlayerInteract>();
    }

    private void Update()
    {
        if (evidence == null) return;

        int collected = evidence.collectedEvidenceNames.Count;
        if (collected != lastCollectedCount)
        {
            if (lastCollectedCount >= 0) OnCollectedChanged();
            lastCollectedCount = collected;
        }

        if (hinted != null && (hinted.IsCollected || Time.time > hintEndsAt))
        {
            hinted.HintBoost = 0f;
            hinted = null;
        }

        if (Time.timeScale <= 0f || PauseManager.isPaused || PauseManager.isBagOpen) return;
        if (evidence.currentEvidenceCount >= evidence.maxEvidenceNeeded) return;

        stuckTime += Time.deltaTime;

        float delay = director != null ? director.EvidenceHintDelay : fallbackDelaySeconds;
        if (stuckTime >= delay)
        {
            stuckTime = 0f;
            TriggerHint();
        }
    }

    private void OnCollectedChanged()
    {
        stuckTime = 0f;

        if (hinted != null) { hinted.HintBoost = 0f; hinted = null; }
        if (tracker != null) tracker.ClearHintOverride();

        if (!autopsyAnnounced && !string.IsNullOrEmpty(announceItemDisplayName) &&
            evidence.currentEvidenceCount >= announceAfterCollected)
        {
            var late = CaseFile.FindItem(announceItemDisplayName);
            if (late != null && !late.IsCollected)
            {
                autopsyAnnounced = true;
                if (player != null) player.DisplayNotification(announceMessage);
            }
        }
    }

    private void TriggerHint()
    {
        if (player == null) return;

        EvidenceItem nearest = null;
        float best = float.MaxValue;

        foreach (var item in CaseFile.Items)
        {
            // Flashlight-only details are never pointed out - the player has to find them by sweeping the beam.
            if (item == null || !item.IsRequired || item.IsCollected || item.VisibleOnlyWhenLit) continue;
            // Only point at something the player can actually take right now - a locked clue would
            // just answer "this doesn't add up yet".
            if (!evidence.CanCollectNow(item)) continue;
            float d = (item.transform.position - player.transform.position).sqrMagnitude;
            if (d < best) { best = d; nearest = item; }
        }

        if (nearest == null) return;

        hinted = nearest;
        hinted.HintBoost = 1.2f;
        hintEndsAt = Time.time + hintPulseSeconds;

        string hint = "Something doesn't add up " + EvidenceZoneText.HintPhrase(nearest.CurrentZone) + ".";
        if (tracker != null) tracker.SetHintOverride(hint);

        // The task panel only shows inside the bag now, so say it on screen too.
        player.DisplayNotification(hint);
    }
}
