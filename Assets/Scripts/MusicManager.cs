using UnityEngine;

// Crossfades between an ambient exploration track and a more intense chase track based on
// whether the ghost is actively hunting the player, so the shift reads as a music change rather
// than an abrupt cut. Both clips loop continuously on their own AudioSource; only their volumes
// move.
public class MusicManager : MonoBehaviour
{
    [Header("Tracks")]
    public AudioClip backgroundMusic;
    public AudioClip chaseMusic;

    [Header("Sources")]
    public AudioSource backgroundSource;
    public AudioSource chaseSource;

    [Header("Settings")]
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    public float fadeSpeed = 1.5f;

    private GhostController ghost;

    void Start()
    {
        ghost = FindFirstObjectByType<GhostController>();

        // The soundtrack keeps playing while a menu has the game paused - pausing the AudioListener
        // is there to freeze the world's own sounds, not to cut the music.
        if (backgroundSource != null)
        {
            backgroundSource.clip = backgroundMusic;
            backgroundSource.loop = true;
            backgroundSource.ignoreListenerPause = true;
            backgroundSource.volume = musicVolume * MusicVolume.Multiplier;
            backgroundSource.Play();
        }

        if (chaseSource != null)
        {
            chaseSource.clip = chaseMusic;
            chaseSource.loop = true;
            chaseSource.ignoreListenerPause = true;
            chaseSource.volume = 0f;
            chaseSource.Play();
        }
    }

    // Runs on unscaled time so the crossfade keeps working correctly even while the game is
    // paused (notes, bag, menus) - AudioSource playback itself already ignores Time.timeScale.
    void Update()
    {
        if (backgroundSource == null) return;

        float effectiveVolume = musicVolume * MusicVolume.Multiplier;

        // Scenes without a GhostController (e.g. Game3) have nothing to crossfade to - the
        // background track should still track the settings slider directly instead of freezing
        // at whatever volume Start() happened to set it to.
        if (ghost == null || chaseSource == null)
        {
            backgroundSource.volume = effectiveVolume;
            return;
        }

        bool chasing = ghost.IsChasing;
        float targetBackground = chasing ? 0f : effectiveVolume;
        float targetChase = chasing ? effectiveVolume : 0f;
        float step = fadeSpeed * Time.unscaledDeltaTime;

        backgroundSource.volume = Mathf.MoveTowards(backgroundSource.volume, targetBackground, step);
        chaseSource.volume = Mathf.MoveTowards(chaseSource.volume, targetChase, step);
    }
}
