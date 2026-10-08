using UnityEngine;

public class ClickToZoom : MonoBehaviour
{
    public MenuZoom menuZoomScript;

    void OnMouseDown()
    {
        // Only trigger if the script exists and we are NOT already zoomed in
        if (menuZoomScript != null && !menuZoomScript.isZoomed)
        {
            menuZoomScript.ToggleZoom();
        }
    }
}