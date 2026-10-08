using UnityEngine;

// Shared palette so UI scripts stop hardcoding Color.yellow/Color.white/Color.grey - keeps every
// panel, slider and highlight pulling from the same crime-scene language (rust, oxblood, bone).
public static class CrimeSceneTheme
{
    public static readonly Color OxbloodRed = new Color(0.55f, 0.09f, 0.09f, 1f);
    public static readonly Color RustTrack = new Color(0.22f, 0.16f, 0.13f, 1f);
    public static readonly Color BoneHandle = new Color(0.72f, 0.62f, 0.42f, 1f);
    public static readonly Color AmberGlow = new Color(0.82f, 0.55f, 0.18f, 1f);
    public static readonly Color Parchment = new Color(0.85f, 0.78f, 0.64f, 1f);
    public static readonly Color EvidenceTapeYellow = new Color(0.83f, 0.69f, 0.02f, 1f);
    public static readonly Color MutedTeal = new Color(0.30f, 0.48f, 0.52f, 1f);
}
