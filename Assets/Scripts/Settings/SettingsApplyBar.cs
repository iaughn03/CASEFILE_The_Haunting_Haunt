using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Sits on the SettingMenu root (SettingsResetHook adds it). Changing an option in Settings only edits
// it: nothing takes effect until APPLY is pressed and confirmed. The Audio page's volume sliders are
// the exception (user's request): they apply as they move and never count as a pending change. The APPLY button only shows while
// something differs from the settings in use. Leaving with unapplied changes (Back or Esc) asks to
// apply or discard them; discarding puts every option back to the settings in use.
public class SettingsApplyBar : MonoBehaviour
{
    // True while a confirmation dialog is up, and on the frame Esc closed one. PauseManager checks
    // this so that Esc doesn't also close Settings or the pause menu behind the dialog.
    public static bool OwnsEscape => dialogOwner != null || Time.frameCount == escapeFrame;
    private static SettingsApplyBar dialogOwner;
    private static int escapeFrame = -10;

    private readonly Color accentColor = new Color(0.96f, 0.86f, 0.55f, 1f);
    private Color textColor = new Color(0.85f, 0.84f, 0.82f, 1f);

    // the settings in use (last applied)
    private GameSettings.State savedSettings;
    private Dictionary<GameKeys.Action, KeyCode> savedKeys;
    private bool hasSnapshot;

    // owner of the brightness slider in this scene (one or the other)
    private PauseManager pause;
    private MainMenu menu;

    private RectTransform buttonTemplate;
    private Button applyButton;
    private TMP_Text appliedNote;
    private float appliedNoteUntil;
    private GameObject dialog;
    private RectTransform dialogPanel;
    private TMP_Text dialogTitle, dialogBody;
    private readonly List<GameObject> dialogButtons = new List<GameObject>();
    private bool quitting;

    // Called by PauseManager / MainMenu CloseSettings. Returns true when it showed the "unapplied
    // changes" dialog instead; onClosed runs again once the player has applied or discarded.
    public static bool InterceptClose(GameObject settingsMenu, Action onClosed)
    {
        if (settingsMenu == null) return false;
        var bar = settingsMenu.GetComponent<SettingsApplyBar>();
        if (bar == null || !bar.isActiveAndEnabled || !bar.HasChanges()) return false;
        bar.AskBeforeLeaving(onClosed);
        return true;
    }

    // ---- helpers for the scene owned brightness slider (PauseManager / MainMenu)

    public static bool SliderDiffers(Slider s, float applied) => s != null && Mathf.Abs(s.value - applied) > 0.0001f;

    public static void ShowPercent(TMP_Text t, float v)
    {
        if (t != null) t.text = Mathf.RoundToInt(v * 100) + "%";
    }

    public static void ShowSlider(Slider s, TMP_Text t, float v)
    {
        if (s != null) s.SetValueWithoutNotify(v);
        ShowPercent(t, v);
    }

    private void Awake()
    {
        pause = FindAnyObjectByType<PauseManager>(FindObjectsInactive.Include);
        if (pause == null) menu = FindAnyObjectByType<MainMenu>(FindObjectsInactive.Include);

        buttonTemplate = transform.Find("ResetButton") as RectTransform;
        if (buttonTemplate == null) return;
        var label = buttonTemplate.GetComponentInChildren<TMP_Text>(true);
        if (label != null) textColor = label.color;
        BuildApplyButton();
        BuildDialog();
    }

    private void OnEnable()
    {
        TakeSnapshot();
        HideDialog();
        if (appliedNote != null) appliedNote.gameObject.SetActive(false);
        Refresh();
    }

    private void OnDisable()
    {
        HideDialog();
        // Settings closed some other way (scene change etc.) with changes never applied: drop them.
        if (!quitting && HasChanges()) Discard();
    }

    private void OnApplicationQuit() => quitting = true;

    private void Update()
    {
        if (dialogOwner == this && Input.GetKeyDown(KeyCode.Escape) && !SettingsPageBuilder.IsCapturingKey)
        {
            escapeFrame = Time.frameCount;
            PlayClick();
            HideDialog();
        }
        Refresh();
    }

    private void Refresh()
    {
        if (applyButton != null)
        {
            bool changed = HasChanges();
            if (applyButton.gameObject.activeSelf != changed) applyButton.gameObject.SetActive(changed);
        }
        if (appliedNote != null && appliedNote.gameObject.activeSelf)
        {
            float left = appliedNoteUntil - Time.unscaledTime;
            if (left <= 0f || applyButton.gameObject.activeSelf) appliedNote.gameObject.SetActive(false);
            else { var c = appliedNote.color; c.a = Mathf.Clamp01(left / 0.6f); appliedNote.color = c; }
        }
    }

    // ------------------------------------------------------------------ pending changes

    private void TakeSnapshot()
    {
        savedSettings = GameSettings.Capture();
        savedKeys = GameKeys.Capture();
        hasSnapshot = true;
    }

    private bool BrightnessChanged() => pause != null ? pause.BrightnessPending : menu != null && menu.BrightnessPending;
    private bool DisplayChanged() => BrightnessChanged() || !savedSettings.DisplayEquals(GameSettings.Capture());
    private bool GraphicsChanged() => !savedSettings.GraphicsEquals(GameSettings.Capture());
    private bool ControlsChanged() => !savedSettings.ControlsEquals(GameSettings.Capture());
    private bool KeysChanged() => !GameKeys.Matches(savedKeys);

    public bool HasChanges() =>
        hasSnapshot && (DisplayChanged() || GraphicsChanged() || ControlsChanged() || KeysChanged());

    // "Display, Graphics and Keybinds": the pages that have something unapplied.
    private string ChangedPages()
    {
        var pages = new List<string>();
        if (DisplayChanged()) pages.Add("Display");
        if (GraphicsChanged()) pages.Add("Graphics");
        if (ControlsChanged()) pages.Add("Controls");
        if (KeysChanged()) pages.Add("Keybinds");
        if (pages.Count == 0) return "";
        if (pages.Count == 1) return pages[0];
        return string.Join(", ", pages.GetRange(0, pages.Count - 1)) + " and " + pages[pages.Count - 1];
    }

    // Makes every edited option take effect and saves it.
    private void Apply()
    {
        GameSettings.Commit();
        GameKeys.Save();
        if (pause != null) pause.ApplyPendingBrightness();
        else if (menu != null) menu.ApplyPendingBrightness();
        TakeSnapshot();
        SettingsPageBuilder.RefreshEverything();
        Refresh();
        if (appliedNote != null)
        {
            appliedNote.gameObject.SetActive(true);
            appliedNoteUntil = Time.unscaledTime + 2f;
            var c = appliedNote.color; c.a = 1f; appliedNote.color = c;
        }
    }

    // Puts every edited option back to the settings in use. Nothing was applied, so nothing to undo.
    private void Discard()
    {
        if (!hasSnapshot) return;
        GameSettings.Restore(savedSettings, false);
        GameKeys.Restore(savedKeys);
        if (pause != null) pause.RevertPendingBrightness();
        else if (menu != null) menu.RevertPendingBrightness();
        SettingsPageBuilder.RefreshEverything();
    }

    // ------------------------------------------------------------------ dialogs

    private void AskToApply()
    {
        if (!HasChanges()) return;
        ShowDialog("APPLY CHANGES?",
            "Apply your new " + ChangedPages() + " settings? They take effect right away.",
            ("APPLY", true, () => { HideDialog(); Apply(); }),
            ("CANCEL", false, HideDialog));
    }

    private void AskBeforeLeaving(Action onClosed)
    {
        ShowDialog("UNAPPLIED CHANGES",
            "You changed your " + ChangedPages() + " settings but did not apply them. Apply them now, or discard them and keep your current settings?",
            ("APPLY", true, () => { HideDialog(); Apply(); onClosed?.Invoke(); }),
            ("DISCARD", false, () => { HideDialog(); Discard(); onClosed?.Invoke(); }),
            ("CANCEL", false, HideDialog));
    }

    private void ShowDialog(string title, string body, params (string label, bool primary, Action onClick)[] buttons)
    {
        if (dialog == null) return;
        foreach (var b in dialogButtons) Destroy(b);
        dialogButtons.Clear();

        dialogTitle.text = title;
        dialogBody.text = body;
        const float width = 270f, gap = 30f;
        float total = buttons.Length * width + (buttons.Length - 1) * gap;
        for (int i = 0; i < buttons.Length; i++)
        {
            var (label, primary, onClick) = buttons[i];
            float x = -total * 0.5f + width * 0.5f + i * (width + gap);
            var btn = CloneButton(dialogPanel, "Button_" + label, label, new Vector2(x, -128f), new Vector2(width, 76f), primary ? accentColor : textColor);
            btn.onClick.AddListener(() => onClick());
            dialogButtons.Add(btn.gameObject);
        }

        dialog.SetActive(true);
        dialog.transform.SetAsLastSibling();
        dialogOwner = this;
    }

    private void HideDialog()
    {
        if (dialog != null) dialog.SetActive(false);
        if (dialogOwner == this) dialogOwner = null;
    }

    // ------------------------------------------------------------------ building the UI

    // APPLY sits to the right of RESET ALL TO DEFAULT, inside the page frame's width.
    private void BuildApplyButton()
    {
        var reset = buttonTemplate;
        float right = reset.anchoredPosition.x + reset.sizeDelta.x * 0.5f;
        var pos = new Vector2(right + 20f + 150f, reset.anchoredPosition.y);
        applyButton = CloneButton((RectTransform)transform, "ApplyButton", "APPLY", pos, new Vector2(300f, reset.sizeDelta.y), accentColor);
        applyButton.onClick.AddListener(AskToApply);
        applyButton.gameObject.SetActive(false);

        appliedNote = NewText((RectTransform)transform, "AppliedNote", "SETTINGS APPLIED", 28f, accentColor);
        var rt = (RectTransform)appliedNote.transform;
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(300f, reset.sizeDelta.y);
        appliedNote.gameObject.SetActive(false);
    }

    private void BuildDialog()
    {
        // full screen shade: dims Settings and catches every click outside the dialog
        dialog = new GameObject("ApplyConfirmDialog", typeof(RectTransform));
        var drt = (RectTransform)dialog.transform;
        drt.SetParent(transform, false);
        drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
        dialog.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.78f);

        var panelGo = new GameObject("Panel", typeof(RectTransform));
        dialogPanel = (RectTransform)panelGo.transform;
        dialogPanel.SetParent(drt, false);
        dialogPanel.anchorMin = dialogPanel.anchorMax = dialogPanel.pivot = new Vector2(0.5f, 0.5f);
        dialogPanel.anchoredPosition = Vector2.zero; dialogPanel.sizeDelta = new Vector2(1000f, 420f);
        panelGo.AddComponent<UnityEngine.UI.Image>().color = new Color(0.075f, 0.06f, 0.05f, 0.98f);
        var outline = panelGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.52f, 0.34f, 0.8f); outline.effectDistance = new Vector2(2f, -2f);

        dialogTitle = NewText(dialogPanel, "Title", "", 46f, accentColor);
        var trt = (RectTransform)dialogTitle.transform;
        trt.anchoredPosition = new Vector2(0f, 140f); trt.sizeDelta = new Vector2(920f, 70f);

        var ruleGo = new GameObject("Rule", typeof(RectTransform));
        var rrt = (RectTransform)ruleGo.transform;
        rrt.SetParent(dialogPanel, false);
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
        rrt.anchoredPosition = new Vector2(0f, 100f); rrt.sizeDelta = new Vector2(820f, 2f);
        var rule = ruleGo.AddComponent<UnityEngine.UI.Image>();
        rule.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.28f); rule.raycastTarget = false;

        dialogBody = NewText(dialogPanel, "Body", "", 28f, textColor);
        var brt = (RectTransform)dialogBody.transform;
        brt.anchoredPosition = new Vector2(0f, 20f); brt.sizeDelta = new Vector2(900f, 130f);
        dialogBody.textWrappingMode = TextWrappingModes.Normal;
        dialogBody.enableAutoSizing = true; dialogBody.fontSizeMax = 28f; dialogBody.fontSizeMin = 18f;

        dialog.SetActive(false);
    }

    // Copies RESET ALL TO DEFAULT so every new button has this scene's own plate, font and hover.
    // The copy is made under an inactive holder so its hover script only wakes up after the label
    // colour is set (it remembers that colour as the one to return to).
    private Button CloneButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size, Color labelColor)
    {
        var holder = new GameObject("CloneHolder");
        holder.SetActive(false);
        var rt = Instantiate(buttonTemplate, holder.transform, false);
        rt.name = name;
        var btn = rt.GetComponent<Button>();
        btn.onClick = new Button.ButtonClickedEvent();   // drop the copied ResetSettings listener
        var text = rt.GetComponentInChildren<TMP_Text>(true);
        if (text != null) { text.text = label; text.color = labelColor; }
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        Destroy(holder);
        btn.onClick.AddListener(PlayClick);
        return btn;
    }

    private TMP_Text NewText(RectTransform parent, string name, string text, float size, Color color)
    {
        var t = Instantiate(buttonTemplate.GetComponentInChildren<TMP_Text>(true), parent, false);
        t.name = name;
        var rt = (RectTransform)t.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        t.text = text; t.color = color;
        t.enableAutoSizing = false; t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    private static UIClickSound clickSound;
    private static void PlayClick()
    {
        if (clickSound == null) clickSound = FindFirstObjectByType<UIClickSound>(FindObjectsInactive.Include);
        if (clickSound != null) clickSound.PlayClick();
    }
}
