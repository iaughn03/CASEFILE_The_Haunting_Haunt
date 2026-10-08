using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Same auto-wiring approach as UIClickSound - adds a hover sound to every Button already present in
// the scene at Awake (including inactive panels), so no button needs manual per-instance setup. Uses
// a runtime EventTrigger since Button itself only exposes onClick, not pointer-enter. Drop one
// instance of this anywhere in each UI scene, alongside UIClickSound.
public class UIHoverSound : MonoBehaviour
{
    public AudioClip[] hoverClips;
    [Range(0f, 1f)] public float volume = 0.5f;
    public Vector2 pitchRange = new Vector2(0.96f, 1.04f);

    private AudioSource source;

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in buttons)
        {
            EventTrigger trigger = b.GetComponent<EventTrigger>();
            if (trigger == null) trigger = b.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => PlayHover());
            trigger.triggers.Add(entry);
        }
    }

    void PlayHover()
    {
        if (hoverClips == null || hoverClips.Length == 0) return;
        AudioClip clip = hoverClips[Random.Range(0, hoverClips.Length)];
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(clip, volume * SfxVolume.Multiplier);
    }
}
