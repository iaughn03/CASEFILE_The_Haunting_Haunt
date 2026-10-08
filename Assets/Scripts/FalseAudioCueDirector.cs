using System.Collections;
using UnityEngine;

// Plays the occasional footstep/creak/rumble near the player that isn't actually the ghost - the
// paranoia of "was that real?" does more for sustained dread than the real threat alone. Skipped
// while she's actively chasing/attacking (own audio should carry that moment) and for a while after
// a genuine jumpscare, so a false cue never immediately follows and undercuts a real one.
public class FalseAudioCueDirector : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public GhostController ghost;

    [Header("Clips")]
    public AudioClip[] cueClips;

    [Header("Timing")]
    public float minInterval = 30f;
    public float maxInterval = 75f;
    public float quietWindowAfterRealScare = 15f;

    [Header("Placement")]
    public float minDistanceFromPlayer = 4f;
    public float maxDistanceFromPlayer = 10f;
    [Range(0f, 1f)] public float volume = 0.6f;
    public float minPitch = 0.9f;
    public float maxPitch = 1.1f;

    private void Start()
    {
        if (player == null)
        {
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) player = pm.transform;
        }

        if (ghost == null)
        {
            ghost = FindFirstObjectByType<GhostController>(FindObjectsInactive.Include);
        }

        StartCoroutine(CueLoop());
    }

    private IEnumerator CueLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));

            if (!CanPlayCue()) continue;

            PlayCueNearPlayer();
        }
    }

    private bool CanPlayCue()
    {
        if (player == null || cueClips == null || cueClips.Length == 0) return false;
        if (ghost != null && ghost.IsChasing) return false;
        if (Time.time - JumpscareFlash.LastScareTime < quietWindowAfterRealScare) return false;
        return true;
    }

    private void PlayCueNearPlayer()
    {
        AudioClip clip = cueClips[Random.Range(0, cueClips.Length)];
        if (clip == null) return;

        Vector2 dir2D = Random.insideUnitCircle.normalized;
        float distance = Random.Range(minDistanceFromPlayer, maxDistanceFromPlayer);
        Vector3 offset = new Vector3(dir2D.x, 0f, dir2D.y) * distance;
        Vector3 position = player.position + offset;

        GameObject temp = new GameObject("FalseAudioCue");
        temp.transform.position = position;

        AudioSource source = temp.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 1f;
        source.volume = volume * SfxVolume.Multiplier;
        source.pitch = Random.Range(minPitch, maxPitch);
        source.minDistance = 2f;
        source.maxDistance = 20f;
        source.Play();

        Destroy(temp, clip.length + 0.5f);
    }
}
