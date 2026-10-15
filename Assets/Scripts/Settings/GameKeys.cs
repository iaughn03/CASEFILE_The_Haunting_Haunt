using System.Collections.Generic;
using UnityEngine;

// Every rebindable key in the game, saved in PlayerPrefs as "Key_<Action>". Gameplay scripts ask this
// instead of naming a KeyCode, so a key changed on Settings > Keybinds works everywhere at once.
// Escape (pause/back) and the number keys (hotbar slots) are deliberately not rebindable, so a bad
// binding can never lock the player out of the menu.
public static class GameKeys
{
    public enum Action
    {
        MoveForward, MoveBack, MoveLeft, MoveRight,
        Sprint, Crouch, Jump,
        Interact, Flashlight, EvidenceBag, DropItem,
    }

    private struct Def
    {
        public string label;
        public KeyCode key;
        // Extra keys that also work while the action is still on its default binding - the bag has
        // always opened on Tab, B and I, and that shouldn't change for players who never rebind.
        public KeyCode[] extras;
        public Def(string label, KeyCode key, params KeyCode[] extras) { this.label = label; this.key = key; this.extras = extras; }
    }

    private static readonly Dictionary<Action, Def> defs = new Dictionary<Action, Def>
    {
        { Action.MoveForward, new Def("Move Forward", KeyCode.W) },
        { Action.MoveBack,    new Def("Move Back",    KeyCode.S) },
        { Action.MoveLeft,    new Def("Move Left",    KeyCode.A) },
        { Action.MoveRight,   new Def("Move Right",   KeyCode.D) },
        { Action.Sprint,      new Def("Sprint",       KeyCode.LeftShift) },
        { Action.Crouch,      new Def("Crouch",       KeyCode.LeftControl) },
        { Action.Jump,        new Def("Jump / Vault", KeyCode.Space) },
        { Action.Interact,    new Def("Interact",     KeyCode.E) },
        { Action.Flashlight,  new Def("Flashlight",   KeyCode.F) },
        { Action.EvidenceBag, new Def("Evidence Bag", KeyCode.Tab, KeyCode.B, KeyCode.I) },
        { Action.DropItem,    new Def("Drop Item",    KeyCode.G) },
    };

    private static readonly Dictionary<Action, KeyCode> current = new Dictionary<Action, KeyCode>();
    private static bool loaded;

    public static event System.Action Changed;

    public static IEnumerable<Action> All => defs.Keys;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loaded = false;
        current.Clear();
        Changed = null;
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        foreach (var kv in defs) current[kv.Key] = (KeyCode)PlayerPrefs.GetInt("Key_" + kv.Key, (int)kv.Value.key);
    }

    public static KeyCode Get(Action a) { EnsureLoaded(); return current[a]; }
    public static KeyCode Default(Action a) => defs[a].key;
    public static string Label(Action a) => defs[a].label;

    // Binding a key that another action already uses swaps the two, so no key is ever bound twice.
    // Settings > Keybinds only edits the bindings; Save() (APPLY) keeps them and tells the prompts.
    public static void Set(Action a, KeyCode key)
    {
        EnsureLoaded();
        KeyCode old = current[a];
        if (old == key) return;
        foreach (var other in defs.Keys)
        {
            if (other == a || current[other] != key) continue;
            current[other] = old;
        }
        current[a] = key;
    }

    public static void Save()
    {
        EnsureLoaded();
        foreach (var kv in current) PlayerPrefs.SetInt("Key_" + kv.Key, (int)kv.Value);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    // Every binding at one moment, so the Settings screen can put them back when the player discards
    // changes they never applied (see SettingsApplyBar).
    public static Dictionary<Action, KeyCode> Capture()
    {
        EnsureLoaded();
        return new Dictionary<Action, KeyCode>(current);
    }

    public static bool Matches(Dictionary<Action, KeyCode> saved)
    {
        EnsureLoaded();
        foreach (var kv in current)
            if (!saved.TryGetValue(kv.Key, out var k) || k != kv.Value) return false;
        return true;
    }

    // Puts every binding back at once (one by one through Set would swap keys around on the way).
    public static void Restore(Dictionary<Action, KeyCode> saved)
    {
        EnsureLoaded();
        foreach (var kv in saved) current[kv.Key] = kv.Value;
    }

    // apply false only edits the bindings (the Settings reset button: they take effect on APPLY).
    public static void ResetToDefaults(bool apply = true)
    {
        EnsureLoaded();
        foreach (var kv in defs) current[kv.Key] = kv.Value.key;
        if (apply) Save();
    }

    public static bool Held(Action a)
    {
        if (Input.GetKey(Get(a))) return true;
        foreach (var k in ActiveExtras(a)) if (Input.GetKey(k)) return true;
        return false;
    }

    public static bool Down(Action a)
    {
        if (Input.GetKeyDown(Get(a))) return true;
        foreach (var k in ActiveExtras(a)) if (Input.GetKeyDown(k)) return true;
        return false;
    }

    private static readonly KeyCode[] none = new KeyCode[0];
    private static KeyCode[] ActiveExtras(Action a)
    {
        var def = defs[a];
        if (def.extras == null || def.extras.Length == 0 || Get(a) != def.key) return none;
        return def.extras;
    }

    // Raw movement from the four movement keys: x = strafe (-1..1), y = forward (-1..1).
    public static Vector2 MoveRaw()
    {
        float x = (Held(Action.MoveRight) ? 1f : 0f) - (Held(Action.MoveLeft) ? 1f : 0f);
        float y = (Held(Action.MoveForward) ? 1f : 0f) - (Held(Action.MoveBack) ? 1f : 0f);
        return new Vector2(x, y);
    }

    // Keys a player may bind. Escape and the hotbar number keys stay fixed; the left/right mouse
    // buttons are left out because clicking the bind button itself would grab them.
    public static bool IsBindable(KeyCode k)
    {
        if (k == KeyCode.None || k == KeyCode.Escape) return false;
        if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return false;
        if (k == KeyCode.Mouse0 || k == KeyCode.Mouse1) return false;
        if (k >= KeyCode.JoystickButton0) return false;
        return true;
    }

    public static string KeyName(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.LeftShift: return "L SHIFT";
            case KeyCode.RightShift: return "R SHIFT";
            case KeyCode.LeftControl: return "L CTRL";
            case KeyCode.RightControl: return "R CTRL";
            case KeyCode.LeftAlt: return "L ALT";
            case KeyCode.RightAlt: return "R ALT";
            case KeyCode.Space: return "SPACE";
            case KeyCode.Return: return "ENTER";
            case KeyCode.Backspace: return "BACKSPACE";
            case KeyCode.CapsLock: return "CAPS";
            case KeyCode.Mouse2: return "MOUSE 3";
            case KeyCode.Mouse3: return "MOUSE 4";
            case KeyCode.Mouse4: return "MOUSE 5";
            case KeyCode.Mouse5: return "MOUSE 6";
            case KeyCode.Mouse6: return "MOUSE 7";
            case KeyCode.UpArrow: return "UP";
            case KeyCode.DownArrow: return "DOWN";
            case KeyCode.LeftArrow: return "LEFT";
            case KeyCode.RightArrow: return "RIGHT";
        }
        if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return "NUM " + (k - KeyCode.Keypad0);
        return k.ToString().ToUpperInvariant();
    }

    // Prompts in the scenes are written with "[E]"; this swaps in whatever Interact is bound to.
    public static string FormatPrompt(string text)
    {
        if (string.IsNullOrEmpty(text) || Get(Action.Interact) == KeyCode.E) return text;
        return text.Replace("[E]", "[" + KeyName(Get(Action.Interact)) + "]");
    }
}
