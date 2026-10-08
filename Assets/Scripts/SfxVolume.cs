using UnityEngine;

// PauseManager's SFX slider previously only updated its percentage label - nothing in the game
// actually read the value. The project's only AudioMixer has a single Master group (no Music/SFX
// children), and creating mixer groups isn't reliably scriptable from outside the Editor's internal
// audio APIs, so this gives every SFX call site a real, shared multiplier to respect instead.
// Works identically in the Editor and in a standalone build.
public static class SfxVolume
{
    private const string PrefKey = "SFXVolume";
    private static float cached = -1f;

    public static float Multiplier
    {
        get
        {
            if (cached < 0f) cached = PlayerPrefs.GetFloat(PrefKey, 1f);
            return cached;
        }
        set
        {
            cached = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefKey, cached);
            PlayerPrefs.Save();
        }
    }
}
