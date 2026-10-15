using UnityEditor;
using UnityEngine;

// Editor-only shortcut for checking a level's ending without playing through it: enters Play mode
// (if needed), skips the intro, collects every case clue through the game's own pickup code, then
// opens the Final Inspection. Lives in an Editor folder, so it never ships in a build, and only
// touches the running Play session - nothing is saved to the scene.
[InitializeOnLoad]
static class FinalInspectionShortcut
{
    const string PendingKey = "FinalInspectionShortcut.Pending";
    static int waitFrames;

    static FinalInspectionShortcut()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            SessionState.SetBool(PendingKey, false);
            // Let every Start() run first (inventory, evidence manager, intro) before collecting.
            waitFrames = 10;
            EditorApplication.update += WaitThenRun;
        };
    }

    [MenuItem("Tools/Haunting Haunt/Jump To Final Inspection")]
    static void Jump()
    {
        if (EditorApplication.isPlaying) { Run(); return; }
        SessionState.SetBool(PendingKey, true);
        EditorApplication.EnterPlaymode();
    }

    static void WaitThenRun()
    {
        if (--waitFrames > 0) return;
        EditorApplication.update -= WaitThenRun;
        Run();
    }

    static void Run()
    {
        var final = Object.FindFirstObjectByType<FinalInspection>(FindObjectsInactive.Include);
        if (final == null) { Debug.LogWarning("Jump To Final Inspection: this scene has no Final Inspection."); return; }

        var intro = Object.FindFirstObjectByType<IntroSequence>();
        if (intro != null && intro.introPanel != null && intro.introPanel.activeSelf)
        {
            intro.StopAllCoroutines();
            intro.SendMessage("EndIntro");
        }
        // the case briefing follows the intro; skip it too
        if (CaseFileUI.IsOpen) CaseFileUI.Instance.Close();

        // EvidenceManager calls the director's milestone scares directly as the count climbs; hidden
        // from FindFirstObjectByType while every clue lands at once, so no screamer fires mid-jump.
        var director = Object.FindFirstObjectByType<DifficultyDirector>();
        if (director != null) director.gameObject.SetActive(false);

        var player = Object.FindFirstObjectByType<PlayerInteract>();
        int collected = 0;
        for (int pass = 0; pass < 3; pass++)
        {
            foreach (var item in Object.FindObjectsByType<EvidenceItem>(FindObjectsSortMode.None))
            {
                if (item.data == null || item.IsCollected) continue;
                item.HasBeenCollected = true; // flashlight-only clues (the tape) count as already seen

                var pickup = item.GetComponent<EvidencePickUp>();
                var note = item.GetComponent<NoteObject>();
                var inspect = item.GetComponent<InspectItem>();
                if (pickup != null) pickup.Interact();
                else if (note != null && inspect != null && player != null) { note.Hide(); player.CollectEvidence(inspect); }
                if (item.IsCollected) collected++;
            }
        }

        if (director != null) director.gameObject.SetActive(true);

        Debug.Log("Jump To Final Inspection: collected " + collected + " clues, opening the Final Inspection.");
        final.Open();
    }
}
