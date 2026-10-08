using UnityEngine;

// Small procedural textures and sounds for the Final Inspection, generated once at runtime so the
// screens need no extra asset files: corkboard, pin, vignette, and soft UI tones.
public static class FinalInspectionArt
{
    private static Sprite circle, cork, vignette, silhouette;
    private static AudioClip drop, pin, thud, scratch, chime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        circle = cork = vignette = silhouette = null;
        drop = pin = thud = scratch = chime = null;
    }

    // ---- sprites ----------------------------------------------------------------------------

    public static Sprite Circle()
    {
        if (circle != null) return circle;
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f));
                float a = Mathf.Clamp01(s / 2f - d);
                // simple highlight so the pin reads as a domed head
                float hl = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(s * 0.38f, s * 0.62f)) / (s * 0.35f));
                var c = Color.Lerp(Color.white * 0.78f, Color.white, hl * 0.7f); c.a = a;
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        circle = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return circle;
    }

    public static Sprite Cork()
    {
        if (cork != null) return cork;
        const int w = 512, h = 300;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.045f, y * 0.045f) * 0.5f + Mathf.PerlinNoise(x * 0.25f + 40f, y * 0.25f + 9f) * 0.35f + Random.value * 0.15f;
                float speck = Mathf.PerlinNoise(x * 0.9f + 11f, y * 0.9f) > 0.72f ? 0.75f : 1f;
                var c = Color.Lerp(new Color(0.27f, 0.18f, 0.11f), new Color(0.50f, 0.36f, 0.22f), n) * speck;
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        cork = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return cork;
    }

    public static Sprite Vignette()
    {
        if (vignette != null) return vignette;
        const int s = 256;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x / (float)(s - 1) - 0.5f) * 2f, dy = (y / (float)(s - 1) - 0.5f) * 2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.SmoothStep(0.25f, 1.25f, d);
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, a * 0.85f));
            }
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        vignette = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return vignette;
    }

    // Stand-in mugshot for a suspect with no portrait art: a dark head-and-shoulders shape against a
    // dim backdrop, so the card reads as "unidentified" instead of an empty white box. 4:5 like the
    // portrait frames.
    public static Sprite Silhouette()
    {
        if (silhouette != null) return silhouette;
        const int w = 160, h = 200;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var figure = new Color(0.045f, 0.045f, 0.05f, 1f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                // backdrop: lighter behind the head, falling off to the edges
                float glow = 1f - Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.62f)) / 0.75f);
                var c = Color.Lerp(new Color(0.08f, 0.08f, 0.085f), new Color(0.30f, 0.29f, 0.27f), glow * glow);

                // signed distances in pixels (negative = inside), soft 1.5 px edge
                float head = (new Vector2((x - w * 0.5f) / (w * 0.20f), (y - h * 0.60f) / (h * 0.185f)).magnitude - 1f) * w * 0.20f;
                float neck = Mathf.Max(Mathf.Abs(x - w * 0.5f) - w * 0.085f, Mathf.Abs(y - h * 0.40f) - h * 0.09f);
                float shoulders = (new Vector2((x - w * 0.5f) / (w * 0.50f), (y + h * 0.02f) / (h * 0.36f)).magnitude - 1f) * w * 0.40f;
                float d = Mathf.Min(head, Mathf.Min(neck, shoulders));
                c = Color.Lerp(c, figure, Mathf.Clamp01(0.5f - d / 1.5f));
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        silhouette = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return silhouette;
    }

    // ---- sounds -----------------------------------------------------------------------------

    private const int Rate = 44100;

    private static AudioClip Make(string name, float seconds, System.Func<float, int, float> f)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, i), -1f, 1f);
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Soft "dropped string" - a low, muffled thump with a falling tone.
    public static AudioClip Drop()
    {
        if (drop != null) return drop;
        float lp = 0f;
        drop = Make("fi_drop", 0.32f, (t, i) =>
        {
            float env = Mathf.Exp(-t * 11f);
            float freq = Mathf.Lerp(200f, 80f, Mathf.Clamp01(t / 0.3f));
            lp = Mathf.Lerp(lp, Random.value * 2f - 1f, 0.06f);
            return (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.35f + lp * 0.5f) * env * 0.5f;
        });
        return drop;
    }

    public static AudioClip Pin()
    {
        if (pin != null) return pin;
        pin = Make("fi_pin", 0.12f, (t, i) => Mathf.Sin(2f * Mathf.PI * 1250f * t) * Mathf.Exp(-t * 40f) * 0.35f + (Random.value - 0.5f) * Mathf.Exp(-t * 90f) * 0.2f);
        return pin;
    }

    public static AudioClip Thud()
    {
        if (thud != null) return thud;
        float lp = 0f;
        thud = Make("fi_thud", 0.5f, (t, i) =>
        {
            lp = Mathf.Lerp(lp, Random.value * 2f - 1f, 0.1f);
            return (Mathf.Sin(2f * Mathf.PI * 62f * t) * 0.9f + lp * 0.6f) * Mathf.Exp(-t * 9f) * 0.85f;
        });
        return thud;
    }

    public static AudioClip Scratch()
    {
        if (scratch != null) return scratch;
        float lp = 0f;
        scratch = Make("fi_scratch", 0.6f, (t, i) =>
        {
            float n = Random.value * 2f - 1f;
            lp = Mathf.Lerp(lp, n, 0.5f);
            float mod = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 7f * t);
            return (n - lp) * mod * 0.12f;
        });
        return scratch;
    }

    public static AudioClip Chime()
    {
        if (chime != null) return chime;
        chime = Make("fi_chime", 3.2f, (t, i) =>
        {
            float a = Mathf.Sin(2f * Mathf.PI * 523.25f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 659.25f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * 783.99f * t);
            float env = Mathf.Min(1f, t * 8f) * Mathf.Exp(-t * 1.1f);
            return a * env * 0.16f;
        });
        return chime;
    }
}
