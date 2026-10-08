using UnityEngine;
using System.Collections.Generic;

// Standalone "how to play" screen shown once, after IntroSequence's narrative beats finish and
// before the player gets control - deliberately kept separate from that typewriter narration
// (rather than appended as extra lines there) so a single impatient skip through the story can't
// also skip past the flashlight/bag/interact controls before the player ever sees them.
public class HowToPlayScreen : MonoBehaviour
{
    public GameObject panel;

    [Tooltip("Key presses are ignored for this long after the screen opens, so a habitual Space/E press from the intro doesn't dismiss it unread. Clicking Continue always works.")]
    public float inputGraceSeconds = 0.4f;

    private readonly List<GameObject> hud = new List<GameObject>();
    private readonly List<bool> hudWasActive = new List<bool>();
    private float shownAt;

    public void Show()
    {
        if (panel != null) panel.SetActive(true);
        shownAt = Time.unscaledTime;
        CollectHud();
        HideHud();
    }

    // Wired to the panel's Continue button; also reachable via E / Space below.
    public void Dismiss()
    {
        if (panel == null || !panel.activeSelf) return;

        RestoreHud();
        panel.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        // Other systems (e.g. the bag/pause toggles) re-enable the HUD when they refresh game state.
        HideHud();

        if (Time.unscaledTime - shownAt < inputGraceSeconds) return;

        if (GameKeys.Down(GameKeys.Action.Interact) || Input.GetKeyDown(KeyCode.Space))
        {
            Dismiss();
        }
    }

    private void OnDisable()
    {
        RestoreHud();
    }

    private void CollectHud()
    {
        if (hud.Count > 0) return;

        var pm = FindFirstObjectByType<PauseManager>();
        if (pm == null) return;

        AddHud(pm.hotbarUI);
        AddHud(pm.batteryUI);
        AddHud(pm.batteryBackgroundUI);
        AddHud(pm.healthBarUI);
        AddHud(pm.energyBarUI);
        AddHud(pm.objectivePanelUI);
        AddHud(pm.crosshairUI);
    }

    private void AddHud(GameObject go)
    {
        if (go == null || hud.Contains(go)) return;
        hud.Add(go);
        hudWasActive.Add(go.activeSelf);
    }

    private void HideHud()
    {
        for (int i = 0; i < hud.Count; i++)
            if (hud[i] != null && hud[i].activeSelf) hud[i].SetActive(false);
    }

    private void RestoreHud()
    {
        for (int i = 0; i < hud.Count; i++)
            if (hud[i] != null) hud[i].SetActive(hudWasActive[i]);

        hud.Clear();
        hudWasActive.Clear();
    }
}
