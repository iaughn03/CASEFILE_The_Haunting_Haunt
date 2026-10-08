using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MapMainMenu : MonoBehaviour
{
    [Header("Main Map Panel")]
    public GameObject mapSelectionPanel;

    [Header("Map Difficulty Panels")]
    public GameObject firstMapDiffPanel;  // Your 1st map panel ("0" / Asylum)
    public GameObject secondMapDiffPanel; // Your 2ndMapDiffPanel

    [Header("Hover Settings")]
    [Tooltip("The scale the button will expand to when hovered (e.g., 1.1f = 110% size)")]
    public float hoverScaleMultiplier = 1.1f;

    // Coming back to this screen after having already hovered/clicked a button here once before
    // (Main Menu -> Map Selection -> Back -> Map Selection again) can leave a button's Animator
    // paused mid-transition (e.g. still on "Pressed"/"Highlighted") instead of "Normal", and a
    // leftover EventSystem selection pointing at nothing useful - both make hover/press stop
    // visibly animating even though the buttons still work. Reset both on every load.
    private void Start()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        foreach (var animator in GetComponentsInChildren<Animator>(true))
        {
            animator.Rebind();
            animator.Update(0f);
        }
    }

    // 1. Call this for your 1st Map Button (Asylum)
    public void OpenFirstMapPanel()
    {
        if (firstMapDiffPanel != null)
        {
            if (mapSelectionPanel != null) mapSelectionPanel.SetActive(false);
            firstMapDiffPanel.SetActive(true);
        }
    }

    // 2. Call this for your 2nd Map Button
    public void OpenSecondMapPanel()
    {
        if (secondMapDiffPanel != null)
        {
            if (mapSelectionPanel != null) mapSelectionPanel.SetActive(false);
            secondMapDiffPanel.SetActive(true);
        }
    }

    // 3. BACK BUTTON: Closes BOTH panels to make sure nothing stays visible
    public void BackToMapSelection()
    {
        // Force hide both difficulty panels safely
        if (firstMapDiffPanel != null) firstMapDiffPanel.SetActive(false);
        if (secondMapDiffPanel != null) secondMapDiffPanel.SetActive(false);

        // Show the map selection screen again
        if (mapSelectionPanel != null) mapSelectionPanel.SetActive(true);
    }

    // 4. BACK BUTTON 2: Goes back to Main Menu Scene
    public void BackToMainMenuScene(string mainMenuSceneName)
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // 5. Play Button Function
    public void PlayGame(string gameplaySceneName)
    {
        SceneManager.LoadScene(gameplaySceneName);
    }

    // =================================================================
    // NEW: Hover functions for expanding/shrinking UI elements
    // =================================================================

    /// <summary>
    /// Call this via an Event Trigger (Pointer Enter) to expand the map image/button.
    /// </summary>
    public void OnHoverEnter(GameObject targetMapButton)
    {
        if (targetMapButton != null)
        {
            // Expands the button uniformly based on your multiplier
            targetMapButton.transform.localScale = Vector3.one * hoverScaleMultiplier;
        }
    }

    /// <summary>
    /// Call this via an Event Trigger (Pointer Exit) to reset the map image/button size.
    /// </summary>
    public void OnHoverExit(GameObject targetMapButton)
    {
        if (targetMapButton != null)
        {
            // Resets the button back to its original 100% size
            targetMapButton.transform.localScale = Vector3.one;
        }
    }
}