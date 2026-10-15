using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Builds the Display / Graphics / Controls / Keybinds settings pages at runtime by copying one
// existing slider row (rowTemplate, e.g. Row_Brightness). Every scene's pages therefore match that
// scene's own look, and adding an option is one line here instead of hand-built UI in four scenes.
// Values live in GameSettings / GameKeys; this only shows and edits them. Nothing takes effect here:
// SettingsApplyBar applies the edited values when the player presses APPLY.
public class SettingsPageBuilder : MonoBehaviour
{
    public enum Kind { Display, Graphics, Controls, Keybinds }

    public Kind kind;
    [Tooltip("An existing slider row to copy the look from (label font, slider sprites, value text).")]
    public RectTransform rowTemplate;
    [Tooltip("Top of the first generated row, measured down from the top of the page.")]
    public float firstRowY = -40f;
    public float rowSpacing = 82f;

    // True while a keybind button is waiting for a key, and for one frame after - PauseManager checks
    // this so the Esc that cancels a rebind doesn't also close the whole menu.
    public static bool IsCapturingKey => capturingCount > 0 || Time.frameCount - lastCaptureFrame <= 1;
    private static int capturingCount;
    private static int lastCaptureFrame = -10;

    private static readonly List<SettingsPageBuilder> live = new List<SettingsPageBuilder>();
    private static KeyCode[] allKeys;

    private readonly List<Action> refreshers = new List<Action>();
    private bool built;
    private float nextY;
    private Color textColor = new Color(0.85f, 0.84f, 0.82f, 1f);
    private readonly Color accentColor = new Color(0.96f, 0.86f, 0.55f, 1f);

    private GameKeys.Action? capturing;
    private TMP_Text capturingText;

    // Bottom of the page: explains the option under the mouse, otherwise shows the page's tip.
    private TMP_Text description;
    private string pageTip;
    private int rowCount;

    private void Awake() => Build();

    private void OnEnable()
    {
        if (!live.Contains(this)) live.Add(this);
        if (built) RefreshAll();
    }

    private void OnDisable()
    {
        live.Remove(this);
        StopCapture();
    }

    public void RefreshAll() { foreach (var r in refreshers) r(); }

    public static void RefreshEverything() { foreach (var b in live.ToArray()) b.RefreshAll(); }

    // ------------------------------------------------------------------ page contents

    private void Build()
    {
        if (built || rowTemplate == null) return;
        built = true;
        var tl = LabelOf(rowTemplate); if (tl != null) textColor = tl.color;

        switch (kind)
        {
            case Kind.Display:
                // Brightness (already in this page, owned by PauseManager / MainMenu) stays on top.
                nextY = firstRowY;
                if (rowTemplate.parent == transform)
                {
                    rowTemplate.anchoredPosition = new Vector2(rowTemplate.anchoredPosition.x, nextY); nextY -= rowSpacing;
                    Decorate(rowTemplate, "Darkens the whole picture if the game looks too bright on your screen. 70% is the intended look.");
                }
                Selector("Resolution", () => ResolutionLabels(), ResolutionIndex, i =>
                {
                    var list = GameSettings.AvailableResolutions();
                    i = Mathf.Clamp(i, 0, list.Count - 1);
                    GameSettings.ResolutionWidth = list[i].x; GameSettings.ResolutionHeight = list[i].y;
                }, desc: "The size of the picture. Your monitor's own size looks sharpest; a smaller one runs faster.");
                Selector("Window Mode", () => new[] { "FULLSCREEN", "WINDOWED" }, () => (int)GameSettings.Window, i => GameSettings.Window = (GameSettings.WindowMode)i,
                    desc: "Fullscreen fills the whole monitor. Windowed runs the game in a window you can move.");
                Slider("Field of View", 50f, 100f, true, () => GameSettings.FieldOfView, v => GameSettings.FieldOfView = v, v => Mathf.RoundToInt(v).ToString(),
                    "How wide you see while playing. Higher shows more around you. The menus use a fixed camera.");
                Selector("Show FPS", OffOn, () => GameSettings.ShowFps ? 1 : 0, i => GameSettings.ShowFps = i == 1,
                    desc: "Shows the frames per second in the top left corner, handy while tuning the graphics.");
                Hint("Hover over an option to see what it does.");
                break;

            case Kind.Graphics:
                nextY = firstRowY;
                // 7 rows plus the description area: a little tighter than the other pages so it all fits the frame
                rowSpacing = Mathf.Min(rowSpacing, 70f);
                Selector("Quality Preset", () => new[] { "LOW", "MEDIUM", "HIGH", "ULTRA", "CUSTOM" }, () => (int)GameSettings.QualityPreset,
                    i => GameSettings.SetPreset((GameSettings.Preset)i), skipLast: true,
                    desc: "Sets every option below at once. LOW suits older laptops, HIGH most gaming PCs.");
                Selector("Render Resolution", RenderScaleLabels, RenderScaleIndex, i => { GameSettings.RenderScale = GameSettings.RenderScales[i]; GameSettings.RecomputePreset(); },
                    desc: "Draws the 3D view at a lower resolution and sharpens it back up. Lower runs faster but looks softer.");
                Selector("Anti Aliasing", () => new[] { "OFF", "FXAA", "SMAA", "MSAA 2X", "MSAA 4X" }, () => (int)GameSettings.AA, i => { GameSettings.AA = (GameSettings.AntiAliasing)i; GameSettings.RecomputePreset(); },
                    desc: "Smooths jagged edges. FXAA is the cheapest, MSAA looks cleanest but costs the most.");
                Selector("Shadows", () => new[] { "OFF", "LOW", "MEDIUM", "HIGH", "ULTRA" }, () => (int)GameSettings.Shadows, i => { GameSettings.Shadows = (GameSettings.ShadowQuality)i; GameSettings.RecomputePreset(); },
                    desc: "Shadow sharpness and how far away they are drawn. One of the biggest costs on weaker machines.");
                Selector("Texture Quality", () => new[] { "LOW", "MEDIUM", "HIGH" }, () => (int)GameSettings.Textures, i => { GameSettings.Textures = (GameSettings.TextureQuality)i; GameSettings.RecomputePreset(); },
                    desc: "How sharp surfaces look up close. Lower it if your graphics card has little memory.");
                Selector("Post Processing", OffOn, () => GameSettings.PostProcessing ? 1 : 0, i => { GameSettings.PostProcessing = i == 1; GameSettings.RecomputePreset(); },
                    desc: "Screen effects such as glow and colour grading. Turning it off gives a small speed boost.");
                Selector("Grass Detail", OffOn, () => GameSettings.Grass == GameSettings.GrassQuality.Off ? 0 : 1, i => { GameSettings.Grass = i == 1 ? GameSettings.GrassQuality.High : GameSettings.GrassQuality.Off; GameSettings.RecomputePreset(); },
                    desc: "Grass, ferns and fallen leaves outdoors. OFF gives the biggest speed boost on low end laptops.");
                Hint("Running slow? Set Grass Detail to OFF first, then lower Render Resolution and Shadows.");
                break;

            case Kind.Controls:
                nextY = Mathf.Min(firstRowY, BelowExistingContent() - 30f);
                Slider("Mouse Sensitivity", 0.2f, 6f, false, () => GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v, v => v.ToString("0.0"),
                    "How fast the camera turns when you move the mouse.");
                Selector("Invert Mouse Y", OffOn, () => GameSettings.InvertY ? 1 : 0, i => GameSettings.InvertY = i == 1,
                    desc: "When ON, moving the mouse up looks down, like a flight game.");
                Hint("Hover over an option to see what it does.");
                break;

            case Kind.Keybinds:
                nextY = firstRowY;
                var actions = new List<GameKeys.Action>(GameKeys.All);
                int rows = (actions.Count + 1) / 2;
                for (int r = 0; r < rows; r++)
                {
                    var row = EmptyRow("Row_Keys" + r);
                    KeyCell(row, actions[r], 0f);
                    if (r + rows < actions.Count) KeyCell(row, actions[r + rows], 560f);
                }
                Hint("Click a key, then press the new one. Esc cancels. Esc, number keys and mouse buttons can't be used.");
                break;
        }
        RefreshAll();
    }

    private static string[] OffOn() => new[] { "OFF", "ON" };

    private static string[] ResolutionLabels()
    {
        var list = GameSettings.AvailableResolutions();
        var s = new string[list.Count];
        // named by height like video sizes (1080P, 720P) instead of "1920 X 1080" (user's request)
        for (int i = 0; i < list.Count; i++) s[i] = list[i].y + "P";
        return s;
    }

    private static int ResolutionIndex()
    {
        var list = GameSettings.AvailableResolutions(); var cur = GameSettings.CurrentResolution();
        int best = list.Count - 1; long bestD = long.MaxValue;
        for (int i = 0; i < list.Count; i++) { long d = Math.Abs((long)list[i].x * list[i].y - (long)cur.x * cur.y); if (d < bestD) { bestD = d; best = i; } }
        return best;
    }

    private static string[] RenderScaleLabels()
    {
        var s = new string[GameSettings.RenderScales.Length];
        for (int i = 0; i < s.Length; i++) s[i] = Mathf.RoundToInt(GameSettings.RenderScales[i] * 100f) + "%";
        return s;
    }

    private static int RenderScaleIndex()
    {
        int best = 0; float bestD = float.MaxValue;
        for (int i = 0; i < GameSettings.RenderScales.Length; i++) { float d = Mathf.Abs(GameSettings.RenderScales[i] - GameSettings.RenderScale); if (d < bestD) { bestD = d; best = i; } }
        return best;
    }

    // ------------------------------------------------------------------ row types

    private RectTransform CloneRow(string name, string desc = null)
    {
        var row = Instantiate(rowTemplate, transform, false);
        row.name = name;
        row.gameObject.SetActive(true);
        row.anchoredPosition = new Vector2(rowTemplate.anchoredPosition.x, nextY);
        nextY -= rowSpacing;
        // the template may already carry a decoration from the Display page; start clean
        foreach (Transform c in row) if (c.name == "RowBackground") Destroy(c.gameObject);
        var oldHover = row.GetComponent<SettingsRowHover>(); if (oldHover != null) Destroy(oldHover);
        Decorate(row, desc);
        return row;
    }

    // A faint strip behind the row (every other row slightly lighter, like a table), which lights up
    // with a gold bar on its left edge while the mouse is over the row and describes the option below.
    private void Decorate(RectTransform row, string desc)
    {
        int index = rowCount++;
        var bgGo = new GameObject("RowBackground", typeof(RectTransform));
        var bgRt = (RectTransform)bgGo.transform;
        bgRt.SetParent(row, false);
        bgRt.SetAsFirstSibling();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = new Vector2(-18f, -5f); bgRt.offsetMax = new Vector2(18f, 5f);
        var bg = bgGo.AddComponent<UnityEngine.UI.Image>();
        // UI blends in linear colour here, so even a tiny alpha shows up clearly: keep these very low.
        // (An Image with alpha 0 still catches the mouse, so the odd rows can be fully clear.)
        var rest = index % 2 == 0 ? new Color(1f, 0.95f, 0.85f, 0.004f) : new Color(1f, 0.95f, 0.85f, 0f);
        bg.color = rest;

        var barGo = new GameObject("AccentBar", typeof(RectTransform));
        var barRt = (RectTransform)barGo.transform;
        barRt.SetParent(bgRt, false);
        barRt.anchorMin = new Vector2(0f, 0.12f); barRt.anchorMax = new Vector2(0f, 0.88f);
        barRt.pivot = new Vector2(0f, 0.5f); barRt.sizeDelta = new Vector2(5f, 0f); barRt.anchoredPosition = Vector2.zero;
        var bar = barGo.AddComponent<UnityEngine.UI.Image>();
        bar.color = accentColor; bar.raycastTarget = false; bar.enabled = false;

        var hover = row.gameObject.AddComponent<SettingsRowHover>();
        hover.background = bg; hover.accentBar = bar;
        hover.restColor = rest; hover.hoverColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.014f);
        hover.onEnter = () => ShowDescription(desc);
        hover.onExit = () => ShowDescription(null);
    }

    private RectTransform EmptyRow(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var row = (RectTransform)go.transform;
        row.SetParent(transform, false);
        row.anchorMin = rowTemplate.anchorMin; row.anchorMax = rowTemplate.anchorMax; row.pivot = rowTemplate.pivot;
        row.sizeDelta = rowTemplate.sizeDelta;
        row.anchoredPosition = new Vector2(rowTemplate.anchoredPosition.x, nextY);
        nextY -= rowSpacing;
        Decorate(row, null);
        return row;
    }

    // The row's label is the direct-child text anchored on the left; its value text is the one on the right.
    private static TMP_Text LabelOf(Transform row)
    {
        foreach (Transform c in row) { var t = c.GetComponent<TMP_Text>(); if (t != null && ((RectTransform)c).anchorMin.x < 0.5f) return t; }
        return null;
    }

    private static TMP_Text ValueOf(Transform row)
    {
        foreach (Transform c in row) { var t = c.GetComponent<TMP_Text>(); if (t != null && ((RectTransform)c).anchorMin.x > 0.5f) return t; }
        return null;
    }

    // Long names ("Render Resolution") shrink to fit the label slot instead of being cut off.
    private static void SetLabel(RectTransform row, string label)
    {
        var lbl = LabelOf(row);
        if (lbl == null) return;
        lbl.text = label;
        lbl.enableAutoSizing = true;
        lbl.fontSizeMax = lbl.fontSize;
        lbl.fontSizeMin = Mathf.Min(24f, lbl.fontSize);
        lbl.overflowMode = TextOverflowModes.Overflow;
        lbl.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void Slider(string label, float min, float max, bool whole, Func<float> get, Action<float> set, Func<float, string> format, string desc = null)
    {
        var row = CloneRow("Row_" + label.Replace(" ", ""), desc);
        SetLabel(row, label);
        var slider = row.GetComponentInChildren<Slider>(true);
        var value = ValueOf(row);
        slider.onValueChanged.RemoveAllListeners();
        slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = whole;
        // stray text some template sliders carry inside them
        foreach (var t in slider.GetComponentsInChildren<TMP_Text>(true)) t.text = "";
        slider.onValueChanged.AddListener(v =>
        {
            set(v);
            if (value != null) value.text = format(v);
        });
        refreshers.Add(() =>
        {
            float v = Mathf.Clamp(get(), min, max);
            slider.SetValueWithoutNotify(v);
            if (value != null) value.text = format(v);
        });
    }

    private void Selector(string label, Func<string[]> options, Func<int> get, Action<int> set, bool skipLast = false, string desc = null)
    {
        var row = CloneRow("Row_" + label.Replace(" ", "").Replace("-", ""), desc);
        SetLabel(row, label);

        // the slider's slot becomes  <  VALUE  >
        var slider = row.GetComponentInChildren<Slider>(true);
        var area = (RectTransform)slider.transform;
        Vector2 pos = area.anchoredPosition; float width = area.rect.width;
        slider.gameObject.SetActive(false);
        Destroy(slider.gameObject);

        var value = ValueOf(row);
        var vrt = (RectTransform)value.transform;
        vrt.anchorMin = vrt.anchorMax = new Vector2(0f, 0.5f); vrt.pivot = new Vector2(0.5f, 0.5f);
        vrt.anchoredPosition = new Vector2(pos.x + width * 0.5f, 0f);
        vrt.sizeDelta = new Vector2(width - 130f, 60f);
        value.alignment = TextAlignmentOptions.Center;
        value.enableAutoSizing = true; value.fontSizeMax = value.fontSize; value.fontSizeMin = 18f;

        Action<int> step = dir =>
        {
            var opts = options();
            int count = skipLast ? opts.Length - 1 : opts.Length;
            int cur = Mathf.Clamp(get(), 0, opts.Length - 1);
            if (skipLast && cur >= count) cur = dir > 0 ? -1 : count;   // from "Custom", step into the real presets
            int next = ((cur + dir) % count + count) % count;
            set(next);
            RefreshEverything();
        };
        Arrow(row, "<", pos.x + 30f, value, () => step(-1));
        Arrow(row, ">", pos.x + width - 30f, value, () => step(1));
        var valueButton = value.gameObject.AddComponent<Button>();
        valueButton.transition = Selectable.Transition.None;
        valueButton.onClick.AddListener(() => step(1));
        value.raycastTarget = true;

        refreshers.Add(() =>
        {
            var opts = options();
            value.text = opts.Length == 0 ? "-" : opts[Mathf.Clamp(get(), 0, opts.Length - 1)];
        });
    }

    private void Arrow(RectTransform row, string glyph, float x, TMP_Text style, Action onClick)
    {
        var go = new GameObject("Arrow" + (glyph == "<" ? "Left" : "Right"), typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(row, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f); rt.sizeDelta = new Vector2(60f, 60f);
        var hit = go.AddComponent<UnityEngine.UI.Image>(); hit.color = new Color(0f, 0f, 0f, 0.001f);
        var txt = Instantiate(style, rt, false);
        Destroy(txt.GetComponent<Button>());
        var trt = (RectTransform)txt.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        txt.text = glyph; txt.enableAutoSizing = false; txt.fontSize = style.fontSizeMax > 0 ? style.fontSizeMax : 34f;
        txt.alignment = TextAlignmentOptions.Center; txt.raycastTarget = false;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = txt;
        var colors = btn.colors; colors.normalColor = Color.white; colors.highlightedColor = accentColor; colors.pressedColor = accentColor * 0.8f; colors.selectedColor = Color.white; btn.colors = colors;
        btn.onClick.AddListener(() => onClick());
    }

    private void KeyCell(RectTransform row, GameKeys.Action action, float x)
    {
        var templateLabel = LabelOf(rowTemplate);
        var lbl = Instantiate(templateLabel, row, false);
        var lrt = (RectTransform)lbl.transform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 0.5f); lrt.pivot = new Vector2(0f, 0.5f);
        lrt.anchoredPosition = new Vector2(x + 20f, 0f); lrt.sizeDelta = new Vector2(300f, 60f);
        lbl.text = GameKeys.Label(action);
        lbl.enableAutoSizing = true; lbl.fontSizeMax = Mathf.Min(lbl.fontSize, 32f); lbl.fontSizeMin = 18f;

        var go = new GameObject("Key_" + action, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(row, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x + 330f, 0f); rt.sizeDelta = new Vector2(200f, 54f);
        var bg = go.AddComponent<UnityEngine.UI.Image>(); bg.color = new Color(0.10f, 0.08f, 0.07f, 0.92f);
        var outline = go.AddComponent<Outline>(); outline.effectColor = new Color(0.62f, 0.52f, 0.34f, 0.8f); outline.effectDistance = new Vector2(2f, -2f);

        var keyText = Instantiate(templateLabel, rt, false);
        var krt = (RectTransform)keyText.transform;
        krt.anchorMin = Vector2.zero; krt.anchorMax = Vector2.one; krt.offsetMin = new Vector2(6f, 0f); krt.offsetMax = new Vector2(-6f, 0f);
        keyText.alignment = TextAlignmentOptions.Center; keyText.raycastTarget = false;
        keyText.enableAutoSizing = true; keyText.fontSizeMax = 30f; keyText.fontSizeMin = 14f;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        var colors = btn.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.6f, 1.4f, 1.1f, 1f); colors.selectedColor = Color.white; btn.colors = colors;
        btn.onClick.AddListener(() => StartCapture(action, keyText));

        refreshers.Add(() =>
        {
            if (capturing == action) return;
            keyText.text = GameKeys.KeyName(GameKeys.Get(action));
            keyText.color = textColor;
        });
    }

    // The page's tip, shown in the description area at the bottom of the frame whenever no option is
    // under the mouse.
    private void Hint(string text)
    {
        pageTip = text;
        EnsureDescription();
        ShowDescription(null);
    }

    private void EnsureDescription()
    {
        if (description != null) return;
        // thin gold rule separating the options from the description
        var ruleGo = new GameObject("DescriptionRule", typeof(RectTransform));
        var ruleRt = (RectTransform)ruleGo.transform;
        ruleRt.SetParent(transform, false);
        ruleRt.anchorMin = ruleRt.anchorMax = new Vector2(0.5f, 0f); ruleRt.pivot = new Vector2(0.5f, 0.5f);
        ruleRt.anchoredPosition = new Vector2(0f, 92f); ruleRt.sizeDelta = new Vector2(1080f, 2f);
        var rule = ruleGo.AddComponent<UnityEngine.UI.Image>();
        rule.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.28f); rule.raycastTarget = false;

        description = Instantiate(LabelOf(rowTemplate), transform, false);
        description.name = "Description";
        var rt = (RectTransform)description.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 16f); rt.sizeDelta = new Vector2(1060f, 66f);
        description.alignment = TextAlignmentOptions.Center;
        description.richText = true;
        description.enableAutoSizing = true; description.fontSizeMax = 25f; description.fontSizeMin = 16f;
        description.textWrappingMode = TextWrappingModes.Normal;
        description.overflowMode = TextOverflowModes.Overflow;
        description.raycastTarget = false;
    }

    private void ShowDescription(string desc)
    {
        if (description == null) return;
        if (!string.IsNullOrEmpty(desc))
        {
            description.text = desc;
            description.color = textColor;
        }
        else
        {
            description.text = string.IsNullOrEmpty(pageTip) ? "" : "<color=#F2C46B>TIP</color>   " + pageTip;
            description.color = new Color(textColor.r * 0.78f, textColor.g * 0.76f, textColor.b * 0.72f, 1f);
        }
    }

    // Lowest point of whatever the page already contains (top-anchored children), so new rows go below it.
    private float BelowExistingContent()
    {
        float lowest = 0f;
        foreach (Transform c in transform)
        {
            var rt = c as RectTransform;
            if (rt == null || !c.gameObject.activeSelf || rt.anchorMin.y < 0.99f) continue;
            float bottom = rt.anchoredPosition.y - rt.rect.height * rt.pivot.y;
            if (bottom < lowest) lowest = bottom;
        }
        return lowest;
    }

    // ------------------------------------------------------------------ key capture

    private void StartCapture(GameKeys.Action action, TMP_Text label)
    {
        StopCapture();
        capturing = action;
        capturingText = label;
        capturingCount++;
        label.text = "PRESS A KEY";
        label.color = accentColor;
    }

    private void StopCapture()
    {
        if (capturing == null) return;
        capturing = null;
        capturingText = null;
        capturingCount = Mathf.Max(0, capturingCount - 1);
        lastCaptureFrame = Time.frameCount;
        RefreshAll();
    }

    private void Update()
    {
        if (capturing == null) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { StopCapture(); return; }
        if (allKeys == null) allKeys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
        foreach (var k in allKeys)
        {
            if (!Input.GetKeyDown(k) || !GameKeys.IsBindable(k)) continue;
            var action = capturing.Value;
            StopCapture();
            GameKeys.Set(action, k);
            RefreshEverything();
            return;
        }
    }
}
