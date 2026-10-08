using TMPro;
using UnityEngine;

// Text written with {ActionName} placeholders (e.g. "Hide in a locker with {Interact}") that always
// shows the keys currently set on Settings > Keybinds. Used by the main menu's How to Play pages.
public class KeyTokenText : MonoBehaviour
{
    public TMP_Text label;
    [TextArea(4, 20)] public string template;
    [Tooltip("Rich text wrapped around each key name, with {0} where the name goes.")]
    public string keyFormat = "<color=#F2C46B>{0}</color>";

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        if (string.IsNullOrEmpty(template) && label != null) template = label.text;
    }

    private void OnEnable()
    {
        GameKeys.Changed += Refresh;
        Refresh();
    }

    private void OnDisable() => GameKeys.Changed -= Refresh;

    public void Refresh()
    {
        if (label == null || string.IsNullOrEmpty(template)) return;
        string text = template;
        foreach (GameKeys.Action a in GameKeys.All)
            text = text.Replace("{" + a + "}", string.Format(keyFormat, GameKeys.KeyName(GameKeys.Get(a))));
        label.text = text;
    }
}
