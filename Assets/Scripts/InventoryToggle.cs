using UnityEngine;

public class InventoryToggle : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject inventoryPanel; // Drag your BagPanel here
    public GameObject descriptionPanel; // Drag your DescriptionPanel here

    [Header("HUD Elements")]
    public GameObject batteryBackgroundUI; // Flashlight battery meter - stays visible while the bag is open

    private PlayerHideState hideState;

    void Start()
    {
        // Force closed on start
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (descriptionPanel != null) descriptionPanel.SetActive(false);
        
        // Ensure time flows normally on start
        Time.timeScale = 1f;
    }

    void Update()
    {
        // Skip while the pause menu is up - PauseManager's own I/Tab handling already guards on
        // this (if (!isPaused) ToggleBag()), but this component had no matching guard, so pressing
        // I/Tab while paused opened the bag panel on top of the pause menu without PauseManager's
        // isBagOpen ever learning about it. Resuming afterward then left the bag panel stuck open
        // over normal gameplay since nothing told it to close.
        if (PauseManager.isPaused || PauseManager.isCaseUiOpen) return;

        // When PauseManager drives this same panel it owns the bag key. Both used to flip the panel
        // on the same press, and in scenes where PauseManager ran first this flipped it straight back,
        // leaving isBagOpen true with the bag hidden (which then blocked hiding in lockers).
        if (OwningPauseManager() != null) return;

        // PlayerHideState is only added the first time the player gets near a hiding spot (see
        // LockerHidingSpot.CachePlayerRefs), so it won't exist yet at scene start - keep retrying
        // the lookup lazily until it does, then stop.
        if (hideState == null) hideState = FindFirstObjectByType<PlayerHideState>();
        if (hideState != null && hideState.IsHiding) return;

        // The Evidence Bag key (Settings > Keybinds; Tab / B / I by default)
        if (GameKeys.Down(GameKeys.Action.EvidenceBag))
        {
            ToggleInventory();
        }
    }

    public void ToggleInventory()
    {
        if (inventoryPanel == null) return;

        // NoteSystem closes and reopens the bag through here; let PauseManager do it so its
        // isBagOpen and the HUD stay in step with the panel.
        PauseManager owner = OwningPauseManager();
        if (owner != null)
        {
            owner.ToggleBag();
            return;
        }

        // Base the new state on the ACTUAL current active status of the panel
        // This stops the boolean state from getting broken or out of sync!
        bool targetState = !inventoryPanel.activeSelf;

        inventoryPanel.SetActive(targetState);

        // Only ever force it CLOSED here - it should stay hidden until the player actually clicks
        // an item (DescriptionPanel.ShowItem opens it), not pop up empty the instant the bag opens.
        if (!targetState)
        {
            if (descriptionPanel != null) descriptionPanel.SetActive(false);

            // Return any borrowed 3D item back to its normal hidden-in-the-world state - otherwise
            // closing the bag mid-inspection would leave it stuck on the inspection stage.
            if (ItemInspection3D.Instance != null) ItemInspection3D.Instance.StopInspecting();
        }

        if (targetState)
        {
            // Pause game state
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // Unpause game state
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private PauseManager pauseManager;

    private PauseManager OwningPauseManager()
    {
        if (pauseManager == null) pauseManager = FindFirstObjectByType<PauseManager>();
        return pauseManager != null && pauseManager.bagMenuUI == inventoryPanel ? pauseManager : null;
    }

    // Safely reads directly from the UI state itself
    public bool IsInventoryOpen() => inventoryPanel != null && inventoryPanel.activeSelf;
}