using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The bag's detail panel. For a case clue it shows only the item's picture, its name and its neutral
// Observation - nothing that interprets the clue.
public class DescriptionPanel : MonoBehaviour
{
    [Tooltip("The visual panel to show/hide - separate from this script's own GameObject.")]
    public GameObject panelRoot;

    public TMP_Text itemNameText;
    public TMP_Text descriptionText;
    public TMP_Text usageText;

    [Header("Case evidence")]
    [Tooltip("Small heading above the description for case clues (\"Observation\").")]
    public TMP_Text observationHeader;
    [Tooltip("Shows the selected item's icon.")]
    public Image itemImage;

    public void ShowItem(DraggableItem item)
    {
        if (item == null) return;

        EvidenceData data = CaseFile.Find(item.itemName);
        bool isCase = data != null;

        if (isCase)
        {
            if (itemNameText != null) itemNameText.text = data.displayName;
            if (descriptionText != null) descriptionText.text = data.observation;
            if (usageText != null) usageText.text = "";
        }
        else
        {
            if (itemNameText != null) itemNameText.text = item.itemName;
            if (descriptionText != null) descriptionText.text = string.IsNullOrEmpty(item.itemDescription) ? "" : "Description: " + item.itemDescription;
            if (usageText != null) usageText.text = string.IsNullOrEmpty(item.itemUsage) ? "" : "Usage: " + item.itemUsage;
        }

        if (observationHeader != null) observationHeader.gameObject.SetActive(isCase);

        if (itemImage != null)
        {
            Sprite icon = item.image != null ? item.image.sprite : null;
            itemImage.sprite = icon;
            itemImage.enabled = icon != null;
            itemImage.gameObject.SetActive(icon != null);
        }

        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }
}
