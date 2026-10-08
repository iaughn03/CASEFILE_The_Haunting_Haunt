using UnityEngine;
using UnityEngine.SceneManagement;

// Scare triggers are authored per-location and none of them knows the others exist, so a cluster of
// them in one stretch of corridor fires back-to-back and the scares stop landing - the player is
// still recovering from the last one when the next hits, and both read as noise. Every ambient scare
// asks here before it fires; one that comes too soon after the previous scare is refused.
//
// A refused trigger deliberately stays armed rather than counting itself as spent, so walking back
// through later still gets the scare instead of it being silently burned on a moment where it
// wouldn't have registered anyway.
public static class JumpscareDirector
{
    // Milestone scares (DifficultyDirector's escalation beats) bypass the wait but still stamp the
    // clock, so ambient scares stay spaced out from them too.
    private static float lastScareTime = float.NegativeInfinity;

    public static float TimeSinceLastScare => Time.unscaledTime - lastScareTime;

    public static bool TryClaim(float minGapSeconds)
    {
        if (TimeSinceLastScare < minGapSeconds) return false;
        Claim();
        return true;
    }

    public static void Claim()
    {
        lastScareTime = Time.unscaledTime;
    }

    // Time.unscaledTime keeps counting across a scene load, so without this a Retry would start the
    // level still holding the previous run's cooldown and swallow the first scare.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        lastScareTime = float.NegativeInfinity;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        lastScareTime = float.NegativeInfinity;
    }
}
