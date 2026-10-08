using UnityEngine;
using UnityEngine.Rendering;

public enum RenderPipelineKind { BuiltIn, URP, HDRP }

// Picks the right unlit shader / color property for whichever render pipeline the project is using,
// so glow outlines and dust particles work in Built-in, URP and HDRP. This project runs URP; the
// Built-in and HDRP branches are best-effort.
public static class GlowPipeline
{
    private static bool detected;
    private static RenderPipelineKind kind;

    public static RenderPipelineKind Kind
    {
        get
        {
            if (!detected)
            {
                detected = true;
                var rp = GraphicsSettings.currentRenderPipeline;
                if (rp == null) kind = RenderPipelineKind.BuiltIn;
                else
                {
                    string t = rp.GetType().Name;
                    kind = t.Contains("HD") ? RenderPipelineKind.HDRP : RenderPipelineKind.URP;
                }
            }
            return kind;
        }
    }

    public static string ColorProperty
    {
        get
        {
            switch (Kind)
            {
                case RenderPipelineKind.HDRP: return "_UnlitColor";
                case RenderPipelineKind.BuiltIn: return "_Color";
                default: return "_BaseColor";
            }
        }
    }

    // Opaque unlit shader used by the inverted-hull outline.
    public static Shader OutlineShader
    {
        get
        {
            Shader s = null;
            switch (Kind)
            {
                case RenderPipelineKind.HDRP: s = Shader.Find("HDRP/Unlit"); break;
                case RenderPipelineKind.BuiltIn: s = Shader.Find("Unlit/Color"); break;
                default: s = Shader.Find("Universal Render Pipeline/Unlit"); break;
            }
            return s != null ? s : Shader.Find("Sprites/Default");
        }
    }

    // Soft additive-looking particle material for the dust motes.
    public static Material CreateParticleMaterial(Texture2D texture)
    {
        Shader s = null;
        switch (Kind)
        {
            case RenderPipelineKind.HDRP: s = Shader.Find("HDRP/Unlit"); break;
            default: s = Shader.Find("Sprites/Default"); break; // unlit + alpha blend, works in Built-in and URP
        }
        if (s == null) s = Shader.Find("Sprites/Default");

        var m = new Material(s);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", texture);
        if (m.HasProperty("_UnlitColorMap")) m.SetTexture("_UnlitColorMap", texture);
        if (Kind == RenderPipelineKind.HDRP)
        {
            if (m.HasProperty("_SurfaceType")) m.SetFloat("_SurfaceType", 1f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
        return m;
    }
}
