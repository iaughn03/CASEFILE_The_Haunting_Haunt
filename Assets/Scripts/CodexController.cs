using UnityEngine;
using TMPro;

// Drives the pause-menu Codex/Glossary panel one entry at a time (Homicide/Murder/Parricide)
// instead of stacking all three terms on screen at once. PauseManager.OpenCodex() just
// activates the CodexUI GameObject this is attached to - OnEnable() below resets back to the first
// entry every time the panel opens, so it never reopens mid-browse on a later page.
public class CodexController : MonoBehaviour
{
    [System.Serializable]
    public struct Entry
    {
        public string term;
        [TextArea] public string definition;
        [Tooltip("Left empty, the entry is always readable. Set to a level scene name, it stays locked until LevelProgress marks that level solved (a case page that would give the answer away).")]
        public string unlockedBySolvedScene;
        [Tooltip("Shown instead of the definition while the entry is locked.")]
        [TextArea] public string lockedDefinition;
    }

    public Entry[] entries;

    [Header("UI")]
    public TextMeshProUGUI termText;
    public TextMeshProUGUI definitionText;
    public TextMeshProUGUI pageIndicatorText;

    private int index;

    void OnEnable()
    {
        index = 0;
        Show();
    }

    // Wired to the panel's Next button.
    public void Next()
    {
        if (entries == null || entries.Length == 0) return;
        index = (index + 1) % entries.Length;
        Show();
    }

    // Wired to the panel's Prev button.
    public void Previous()
    {
        if (entries == null || entries.Length == 0) return;
        index = (index - 1 + entries.Length) % entries.Length;
        Show();
    }

    void Show()
    {
        if (entries == null || entries.Length == 0) return;

        Entry e = entries[index];
        bool locked = !string.IsNullOrEmpty(e.unlockedBySolvedScene) && !LevelProgress.IsSolved(e.unlockedBySolvedScene);
        if (termText != null) termText.text = e.term;
        if (definitionText != null)
            definitionText.text = !locked ? e.definition
                : (string.IsNullOrWhiteSpace(e.lockedDefinition) ? "Solve this case to open this page." : e.lockedDefinition);
        if (pageIndicatorText != null) pageIndicatorText.text = $"{index + 1} / {entries.Length}";
    }
}
