using UnityEngine;

// For music AudioSources that MusicManager doesn't drive (e.g. the main menu's BGM): keeps the
// source's volume at its authored level times the Settings > Music Volume slider.
[RequireComponent(typeof(AudioSource))]
public class MusicVolumeFollower : MonoBehaviour
{
    private AudioSource source;
    private float baseVolume;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        baseVolume = source.volume;
        Apply();
    }

    private void Update() => Apply();

    private void Apply() => source.volume = baseVolume * MusicVolume.Multiplier;
}
