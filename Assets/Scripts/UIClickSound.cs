using UnityEngine;
using UnityEngine.UI;

// Auto-wires a click sound onto every Button already present in the scene at Awake - including
// ones inside currently-inactive panels (Settings/Pause/etc.), which a normal active-only search
// would miss - so no button needs its own manual onClick wiring, and this stays correct if buttons
// are added or rearranged later. Drop one instance of this anywhere in each UI scene.
public class UIClickSound : MonoBehaviour
{
    public AudioClip[] clickClips;
    [Range(0f, 1f)] public float volume = 0.8f;
    public Vector2 pitchRange = new Vector2(0.96f, 1.04f);

    private AudioSource source;

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in buttons)
        {
            b.onClick.AddListener(PlayClick);
        }
    }

    // Public so menus opened by a key rather than a button press (Esc) can play the same click.
    public void PlayClick()
    {
        if (clickClips == null || clickClips.Length == 0) return;
        AudioClip clip = clickClips[Random.Range(0, clickClips.Length)];
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(clip, volume * SfxVolume.Multiplier);
    }
}
