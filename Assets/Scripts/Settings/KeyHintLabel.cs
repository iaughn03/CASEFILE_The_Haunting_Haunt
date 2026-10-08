using TMPro;
using UnityEngine;

// A keycap on the How to Play / Controls screen that follows Settings > Keybinds. While the action is
// on its default key the authored text stays as it is ("Tab", "F"); once rebound it shows the new key.
public class KeyHintLabel : MonoBehaviour
{
    public GameKeys.Action action;
    public TMP_Text label;
    [Tooltip("Shown only while the action is on its default key - e.g. the \"/ B\" after Tab on the bag row, since B stops opening the bag once the bag is rebound.")]
    public GameObject[] onlyWhileDefault;
    [Tooltip("Optional sentence instead of a bare keycap, with {key} where the key name goes - e.g. \"Press {key} or Space\".")]
    public string template;

    private string authoredText;
    private float authoredSize;

    private void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (label != null) { authoredText = label.text; authoredSize = label.fontSize; }
    }

    private void OnEnable()
    {
        GameKeys.Changed += Refresh;
        Refresh();
    }

    private void OnDisable() => GameKeys.Changed -= Refresh;

    private void Refresh()
    {
        bool isDefault = GameKeys.Get(action) == GameKeys.Default(action);
        if (label != null && !string.IsNullOrEmpty(template))
        {
            label.text = template.Replace("{key}", GameKeys.KeyName(GameKeys.Get(action)));
        }
        else if (label != null)
        {
            label.text = isDefault ? authoredText : GameKeys.KeyName(GameKeys.Get(action));
            // a long name ("L-SHIFT") shrinks to fit the keycap, never grows past the authored size
            label.enableAutoSizing = !isDefault;
            label.fontSizeMax = authoredSize;
            label.fontSizeMin = Mathf.Min(12f, authoredSize);
            if (isDefault) label.fontSize = authoredSize;
        }
        if (onlyWhileDefault != null)
            foreach (var go in onlyWhileDefault) if (go != null) go.SetActive(isDefault);
    }
}
