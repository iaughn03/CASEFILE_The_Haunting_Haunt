using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Display, graphics and mouse settings, saved in PlayerPrefs and applied on startup and to every
// scene as it loads. The Settings > Display / Graphics / Controls pages (SettingsPageBuilder) read
// and write these; nothing else needs to know about them.
//
// Graphics options change the active URP asset. In the Editor the asset's original values are put
// back when Play mode ends, so testing a "Low" preset never rewrites the project's pipeline asset.
public static class GameSettings
{
    public enum Preset { Low, Medium, High, Ultra, Custom }
    // Only two choices (user's request). Fullscreen is borderless: changing resolution then never
    // switches the monitor's own mode, so the screen doesn't flash black or lose clicks.
    public enum WindowMode { Fullscreen, Windowed }
    public enum AntiAliasing { Off, FXAA, SMAA, MSAA2x, MSAA4x }
    public enum ShadowQuality { Off, Low, Medium, High, Ultra }
    public enum TextureQuality { Quarter, Half, Full }
    public enum GrassQuality { Off, Low, Medium, High }

    public static readonly float[] RenderScales = { 0.5f, 0.6f, 0.7f, 0.75f, 0.8f, 0.85f, 0.9f, 1f };

    // ---- display
    // V-Sync is always on and there is no frame limit option (removed at the user's request).
    public static int ResolutionWidth, ResolutionHeight;
    public static WindowMode Window = WindowMode.Fullscreen;
    public static float FieldOfView = 60f;
    public static bool ShowFps;

    // ---- graphics
    public static Preset QualityPreset = Preset.High;
    public static float RenderScale = 1f;
    public static AntiAliasing AA = AntiAliasing.SMAA;
    public static ShadowQuality Shadows = ShadowQuality.High;
    public static TextureQuality Textures = TextureQuality.Full;
    public static bool PostProcessing = true;
    // Terrain grass, ferns and fallen leaves (Game2/Game3). Off removes them entirely for weak laptops;
    // trees are separate objects and always stay.
    public static GrassQuality Grass = GrassQuality.High;

    // ---- mouse
    public static float MouseSensitivity = 2f;
    public static bool InvertY;

    public static event System.Action Changed;

    private static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loaded = false;
        Changed = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        var desktop = Screen.currentResolution;
        desktopAtStartup = new Vector2Int(desktop.width, desktop.height);
        Load();
#if UNITY_EDITOR
        CaptureEditorDefaults();
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
        ApplyDisplay(true);
        ApplyGraphics();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyToScene();

    // ------------------------------------------------------------------ load / save

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        ResolutionWidth = PlayerPrefs.GetInt("Gfx_ResW", 0);
        ResolutionHeight = PlayerPrefs.GetInt("Gfx_ResH", 0);
        // old saves used 0 Fullscreen / 1 Borderless / 2 Windowed under "Gfx_Window"
        if (!PlayerPrefs.HasKey("Gfx_DisplayMode") && PlayerPrefs.HasKey("Gfx_Window"))
            PlayerPrefs.SetInt("Gfx_DisplayMode", PlayerPrefs.GetInt("Gfx_Window") == 2 ? (int)WindowMode.Windowed : (int)WindowMode.Fullscreen);
        PlayerPrefs.DeleteKey("Gfx_Window");
        Window = PlayerPrefs.GetInt("Gfx_DisplayMode", (int)WindowMode.Fullscreen) == (int)WindowMode.Windowed ? WindowMode.Windowed : WindowMode.Fullscreen;
        PlayerPrefs.DeleteKey("Gfx_VSync");
        PlayerPrefs.DeleteKey("Gfx_FrameLimit");
        FieldOfView = PlayerPrefs.GetFloat("Gfx_Fov", 60f);
        ShowFps = PlayerPrefs.GetInt("Gfx_ShowFps", 0) == 1;
        QualityPreset = (Preset)PlayerPrefs.GetInt("Gfx_Preset", (int)Preset.High);
        RenderScale = PlayerPrefs.GetFloat("Gfx_RenderScale", 1f);
        AA = (AntiAliasing)PlayerPrefs.GetInt("Gfx_AA", (int)AntiAliasing.SMAA);
        Shadows = (ShadowQuality)PlayerPrefs.GetInt("Gfx_Shadows", (int)ShadowQuality.High);
        Textures = (TextureQuality)PlayerPrefs.GetInt("Gfx_Textures", (int)TextureQuality.Full);
        PostProcessing = PlayerPrefs.GetInt("Gfx_Post", 1) == 1;
        Grass = (GrassQuality)PlayerPrefs.GetInt("Gfx_Grass", (int)GrassQuality.High);
        // Grass is just On or Off now; an old saved Low or Medium counts as On.
        if (Grass != GrassQuality.Off) Grass = GrassQuality.High;
        MouseSensitivity = PlayerPrefs.GetFloat("Input_MouseSensitivity", 2f);
        InvertY = PlayerPrefs.GetInt("Input_InvertY", 0) == 1;
    }

    public static void Save()
    {
        PlayerPrefs.SetInt("Gfx_ResW", ResolutionWidth);
        PlayerPrefs.SetInt("Gfx_ResH", ResolutionHeight);
        PlayerPrefs.SetInt("Gfx_DisplayMode", (int)Window);
        PlayerPrefs.SetFloat("Gfx_Fov", FieldOfView);
        PlayerPrefs.SetInt("Gfx_ShowFps", ShowFps ? 1 : 0);
        PlayerPrefs.SetInt("Gfx_Preset", (int)QualityPreset);
        PlayerPrefs.SetFloat("Gfx_RenderScale", RenderScale);
        PlayerPrefs.SetInt("Gfx_AA", (int)AA);
        PlayerPrefs.SetInt("Gfx_Shadows", (int)Shadows);
        PlayerPrefs.SetInt("Gfx_Textures", (int)Textures);
        PlayerPrefs.SetInt("Gfx_Post", PostProcessing ? 1 : 0);
        PlayerPrefs.SetInt("Gfx_Grass", (int)Grass);
        PlayerPrefs.SetFloat("Input_MouseSensitivity", MouseSensitivity);
        PlayerPrefs.SetInt("Input_InvertY", InvertY ? 1 : 0);
        PlayerPrefs.Save();
    }

    // The settings pages only edit the fields above; this (APPLY, via SettingsApplyBar) saves them
    // and makes them take effect.
    public static void Commit()
    {
        Save();
        ApplyDisplay();
        ApplyGraphics();
        ApplyToScene();
        Changed?.Invoke();
    }

    // Every option at one moment, so the Settings screen can put things back when the player
    // discards changes they never applied (see SettingsApplyBar).
    public struct State
    {
        public int resW, resH;
        public WindowMode window;
        public float fov;
        public bool showFps;
        public Preset preset;
        public float renderScale;
        public AntiAliasing aa;
        public ShadowQuality shadows;
        public TextureQuality textures;
        public bool post;
        public GrassQuality grass;
        public float mouse;
        public bool invertY;

        public bool DisplayEquals(State o) => resW == o.resW && resH == o.resH && window == o.window && fov == o.fov && showFps == o.showFps;
        public bool GraphicsEquals(State o) => preset == o.preset && renderScale == o.renderScale && aa == o.aa && shadows == o.shadows && textures == o.textures && post == o.post && grass == o.grass;
        public bool ControlsEquals(State o) => mouse == o.mouse && invertY == o.invertY;
    }

    public static State Capture()
    {
        Load();
        return new State
        {
            resW = ResolutionWidth, resH = ResolutionHeight, window = Window, fov = FieldOfView, showFps = ShowFps,
            preset = QualityPreset, renderScale = RenderScale, aa = AA, shadows = Shadows, textures = Textures, post = PostProcessing, grass = Grass,
            mouse = MouseSensitivity, invertY = InvertY,
        };
    }

    public static void Restore(State s, bool apply = true)
    {
        ResolutionWidth = s.resW; ResolutionHeight = s.resH; Window = s.window; FieldOfView = s.fov; ShowFps = s.showFps;
        QualityPreset = s.preset; RenderScale = s.renderScale; AA = s.aa; Shadows = s.shadows; Textures = s.textures; PostProcessing = s.post; Grass = s.grass;
        MouseSensitivity = s.mouse; InvertY = s.invertY;
        if (apply) Commit();
    }

    // apply false only edits the values (the Settings reset button: they take effect on APPLY).
    public static void ResetToDefaults(bool apply = true)
    {
        ResolutionWidth = 0; ResolutionHeight = 0;
        Window = WindowMode.Fullscreen; FieldOfView = 60f; ShowFps = false;
        MouseSensitivity = 2f; InvertY = false;
        SetPreset(Preset.High);
        if (apply) Commit();
    }

    // ------------------------------------------------------------------ presets

    public static void SetPreset(Preset p)
    {
        QualityPreset = p;
        switch (p)
        {
            case Preset.Low:    RenderScale = 0.7f;  AA = AntiAliasing.Off;    Shadows = ShadowQuality.Off;  Textures = TextureQuality.Half; PostProcessing = false; Grass = GrassQuality.Off; break;
            case Preset.Medium: RenderScale = 0.85f; AA = AntiAliasing.FXAA;   Shadows = ShadowQuality.Low;  Textures = TextureQuality.Full; PostProcessing = true;  Grass = GrassQuality.High; break;
            case Preset.High:   RenderScale = 1f;    AA = AntiAliasing.SMAA;   Shadows = ShadowQuality.High; Textures = TextureQuality.Full; PostProcessing = true;  Grass = GrassQuality.High; break;
            case Preset.Ultra:  RenderScale = 1f;    AA = AntiAliasing.MSAA4x; Shadows = ShadowQuality.Ultra; Textures = TextureQuality.Full; PostProcessing = true; Grass = GrassQuality.High; break;
        }
    }

    // After an individual graphics option changes: name the preset it now matches, else Custom.
    public static void RecomputePreset()
    {
        foreach (Preset p in new[] { Preset.Low, Preset.Medium, Preset.High, Preset.Ultra })
        {
            float rs = RenderScale; var aa = AA; var sh = Shadows; var tx = Textures; bool pp = PostProcessing; var gr = Grass;
            SetPreset(p);
            bool same = Mathf.Approximately(rs, RenderScale) && aa == AA && sh == Shadows && tx == Textures && pp == PostProcessing && gr == Grass;
            RenderScale = rs; AA = aa; Shadows = sh; Textures = tx; PostProcessing = pp; Grass = gr;
            if (same) { QualityPreset = p; return; }
        }
        QualityPreset = Preset.Custom;
    }

    // ------------------------------------------------------------------ apply

    // Only sizes with the monitor's own shape (e.g. all 16:9 on a 16:9 screen). A 4:3 or 5:4 size
    // gets stretched or barred on a widescreen monitor and pushes the UI out of place.
    public static List<Vector2Int> AvailableResolutions()
    {
        var native = NativeResolution();
        float nativeAspect = (float)native.x / native.y;
        var list = new List<Vector2Int>();
        foreach (var r in Screen.resolutions)
        {
            var v = new Vector2Int(r.width, r.height);
            if (v.x < 800 || v.y <= 0 || list.Contains(v)) continue;
            if (v.x > native.x || v.y > native.y) continue;
            if (Mathf.Abs((float)v.x / v.y - nativeAspect) > 0.02f) continue;
            list.Add(v);
        }
        if (!list.Contains(native)) list.Add(native);
        list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return list;
    }

    // The monitor's real size. Not Screen.currentResolution: in Fullscreen that reports the mode the
    // game itself switched to, so after lowering the resolution the list (and Reset) shrank with it.
    public static Vector2Int NativeResolution()
    {
        // (not "largest entry in Screen.resolutions" either - Windows lists upscaled modes such as
        // 3840x2160 on a 1080p screen)
#if !UNITY_EDITOR
        var d = Display.main;
        if (d != null && d.systemWidth >= 800 && d.systemHeight >= 600) return new Vector2Int(d.systemWidth, d.systemHeight);
#endif
        if (desktopAtStartup.x >= 800 && desktopAtStartup.y >= 600) return desktopAtStartup;
        var cur = Screen.currentResolution;
        return new Vector2Int(Mathf.Max(1, cur.width), Mathf.Max(1, cur.height));
    }

    private static Vector2Int desktopAtStartup;

    public static Vector2Int CurrentResolution()
    {
        var saved = new Vector2Int(ResolutionWidth, ResolutionHeight);
        if (saved.x > 0 && saved.y > 0 && AvailableResolutions().Contains(saved)) return saved;
        return NativeResolution();
    }

    public static FullScreenMode ToFullScreenMode(WindowMode m)
    {
        return m == WindowMode.Windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
    }

    public static void ApplyDisplay() => ApplyDisplay(false);

    // A resolution / window-mode change is not instant - the screen takes a moment to switch, and
    // Screen.width keeps reporting the old size until it has. Clicking the arrows quickly used to
    // start a new switch on every click while the last was still going, which left the menu
    // unclickable. Now each click only updates the request; DisplayApplier switches once, after the
    // clicks have stopped for a moment.
    private static void ApplyDisplay(bool immediate)
    {
#if !UNITY_EDITOR
        DisplayApplier.Request(CurrentResolution(), ToFullScreenMode(Window), immediate);
#endif
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
    }

    public static void ApplyGraphics()
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            urp.renderScale = Mathf.Clamp(RenderScale, 0.5f, 1f);
            urp.upscalingFilter = RenderScale < 0.999f ? UpscalingFilterSelection.FSR : UpscalingFilterSelection.Auto;
            urp.msaaSampleCount = AA == AntiAliasing.MSAA4x ? 4 : AA == AntiAliasing.MSAA2x ? 2 : 1;
            switch (Shadows)
            {
                case ShadowQuality.Off:    urp.shadowDistance = 0f;  urp.shadowCascadeCount = 1; urp.mainLightShadowmapResolution = 256;  urp.additionalLightsShadowmapResolution = 256; break;
                case ShadowQuality.Low:    urp.shadowDistance = 20f; urp.shadowCascadeCount = 1; urp.mainLightShadowmapResolution = 512;  urp.additionalLightsShadowmapResolution = 512; break;
                case ShadowQuality.Medium: urp.shadowDistance = 35f; urp.shadowCascadeCount = 1; urp.mainLightShadowmapResolution = 1024; urp.additionalLightsShadowmapResolution = 1024; break;
                case ShadowQuality.High:   urp.shadowDistance = 50f; urp.shadowCascadeCount = 1; urp.mainLightShadowmapResolution = 2048; urp.additionalLightsShadowmapResolution = 2048; break;
                case ShadowQuality.Ultra:  urp.shadowDistance = 70f; urp.shadowCascadeCount = 2; urp.mainLightShadowmapResolution = 4096; urp.additionalLightsShadowmapResolution = 4096; break;
            }
        }
        QualitySettings.globalTextureMipmapLimit = Textures == TextureQuality.Full ? 0 : Textures == TextureQuality.Half ? 1 : 2;
        QualitySettings.anisotropicFiltering = Textures == TextureQuality.Full ? AnisotropicFiltering.ForceEnable
                                             : Textures == TextureQuality.Half ? AnisotropicFiltering.Enable : AnisotropicFiltering.Disable;
    }

    // Per-camera options (FXAA/SMAA, post-processing), the player's field of view, and the FPS readout.
    public static void ApplyToScene()
    {
        var mode = AA == AntiAliasing.FXAA ? AntialiasingMode.FastApproximateAntialiasing
                 : AA == AntiAliasing.SMAA ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                 : AntialiasingMode.None;
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null || data.renderType == CameraRenderType.Overlay) continue;
            data.antialiasing = mode;
            data.renderPostProcessing = PostProcessing;
        }
        foreach (var pm in Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
            if (pm.playerCamera != null) pm.playerCamera.fieldOfView = FieldOfView;

        // Grass/fern/leaf detail on the terrains: how far out it is drawn and how thick. Off skips it
        // entirely, the biggest saving on a weak laptop.
        float grassDistance = Grass == GrassQuality.High ? 70f : Grass == GrassQuality.Medium ? 50f : Grass == GrassQuality.Low ? 35f : 0f;
        float grassDensity = Grass == GrassQuality.High ? 1f : Grass == GrassQuality.Medium ? 0.7f : Grass == GrassQuality.Low ? 0.4f : 0f;
        foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            terrain.detailObjectDistance = grassDistance;
            terrain.detailObjectDensity = grassDensity;
        }

        FpsCounter.SetVisible(ShowFps);
    }

#if UNITY_EDITOR
    // ------------------------------------------------------------------ editor safety net

    private static bool captured;
    private static float edRenderScale, edShadowDistance;
    private static int edMsaa, edCascades, edMainRes, edAddRes, edVSync, edFrameRate, edMipLimit;
    private static UpscalingFilterSelection edUpscale;
    private static AnisotropicFiltering edAniso;

    private static void CaptureEditorDefaults()
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;
        captured = true;
        edRenderScale = urp.renderScale; edUpscale = urp.upscalingFilter; edMsaa = urp.msaaSampleCount;
        edShadowDistance = urp.shadowDistance; edCascades = urp.shadowCascadeCount;
        edMainRes = urp.mainLightShadowmapResolution; edAddRes = urp.additionalLightsShadowmapResolution;
        edVSync = QualitySettings.vSyncCount; edFrameRate = Application.targetFrameRate;
        edMipLimit = QualitySettings.globalTextureMipmapLimit; edAniso = QualitySettings.anisotropicFiltering;
    }

    private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state != UnityEditor.PlayModeStateChange.ExitingPlayMode || !captured) return;
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            urp.renderScale = edRenderScale; urp.upscalingFilter = edUpscale; urp.msaaSampleCount = edMsaa;
            urp.shadowDistance = edShadowDistance; urp.shadowCascadeCount = edCascades;
            urp.mainLightShadowmapResolution = edMainRes; urp.additionalLightsShadowmapResolution = edAddRes;
        }
        QualitySettings.vSyncCount = edVSync; Application.targetFrameRate = edFrameRate;
        QualitySettings.globalTextureMipmapLimit = edMipLimit; QualitySettings.anisotropicFiltering = edAniso;
        captured = false;
    }
#endif
}
