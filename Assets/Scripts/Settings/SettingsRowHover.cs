using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One settings row's hover state (added at runtime by SettingsPageBuilder): lightens the row's
// background strip, shows the gold accent bar, and tells the page which option to describe.
public class SettingsRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image background;
    public Image accentBar;
    public Color restColor;
    public Color hoverColor;
    public Action onEnter;
    public Action onExit;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (background != null) background.color = hoverColor;
        if (accentBar != null) accentBar.enabled = true;
        onEnter?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData) => Rest();

    private void OnDisable() => Rest();

    private void Rest()
    {
        if (background != null) background.color = restColor;
        if (accentBar != null) accentBar.enabled = false;
        onExit?.Invoke();
    }
}
