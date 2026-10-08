using UnityEngine;

// Same pattern as SfxVolume.cs - a shared, PlayerPrefs-backed multiplier that MusicManager's
// crossfade reads every frame, instead of PauseManager's slider poking an AudioSource.volume
// directly (which MusicManager's own per-frame crossfade would immediately overwrite).
public static class MusicVolume
{
    private const string PrefKey = "MusicVolume";
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
