using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DraggableItem : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IPointerClickHandler
{
    [Header("UI Display Settings")]
    public Image image;

    public string itemName;

    [TextArea(2, 4)]
    public string itemDescription;

    public string itemUsage;

    [HideInInspector]
    public Transform parentAfterDrag;

    [HideInInspector]
    public InventorySlot sourceSlot;

    // Set on collection for items that originated from a NoteObject - lets clicking this item in
    // the bag reopen the same full-screen reading overlay used on first pickup, instead of cramming
    // the note's full text into the small generic DescriptionPanel.
    [HideInInspector]
    public bool isReadableNote = false;

    // The original 3D world pickup object (KeyItem/EvidencePickUp/NoteObject), kept hidden rather
    // than destroyed on collection so it can be temporarily borrowed for the 3D inspection view
    // when this item is clicked in the bag, or placed back in the world if the player drops it.
    [HideInInspector]
    public GameObject sourceWorldObject;

    private Transform originalSlotParent;
    private Canvas mainCanvas;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        if (image == null)
        {
            image = GetComponent<Image>();

            if (image == null)
                image = GetComponentInChildren<Image>(true);
        }

        // Icon art comes from different source images with different native aspect ratios (a tall
        // note vs a square item). Stretching them to fill the box made a note a solid block that
        // covered the slot frame, so every icon keeps its own shape inside the box instead.
        if (image != null)
            image.preserveAspect = true;

        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void Start()
    {
        mainCanvas = GetComponentInParent<Canvas>();

        RefreshSourceSlot();
    }

    public void SetupItem(
        string newName,
        string newDescription,
        Sprite newIcon,
        Transform firstTransform = null,
        Transform secondTransform = null)
    {
        itemName = newName;
        itemDescription = newDescription;

        if (image == null)
        {
            image = GetComponent<Image>();

            if (image == null)
                image = GetComponentInChildren<Image>(true);
        }

        if (image != null)
        {
            image.sprite = newIcon;
            image.enabled = newIcon != null;
            // Matches HotbarManager.ICON_TINT - some item art (e.g. the note's dull crumpled-paper
            // photo) reads as duller/smaller against the dark slot background than bright art like
            // the flashlight even at an identical box size, purely from lower contrast.
            image.color = HotbarManager.ICON_TINT;
        }
        else
        {
            Debug.LogError(
                "Inventory Item Prefab has no Image!"
            );
        }

        RefreshSourceSlot();
    }

    public void RefreshSourceSlot()
    {
        InventorySlot slot =
            GetComponentInParent<InventorySlot>();

        if (slot != null)
        {
            sourceSlot = slot;
            parentAfterDrag = slot.transform;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(itemName))
            return;

        InventorySlot currentSlot =
            GetComponentInParent<InventorySlot>();

        if (currentSlot != null)
        {
            sourceSlot = currentSlot;
            originalSlotParent = currentSlot.transform;
        }
        else
        {
            Debug.LogError(
                "Cannot find InventorySlot parent!"
            );
            return;
        }

        parentAfterDrag = originalSlotParent;

        if (mainCanvas == null)
            mainCanvas = GetComponentInParent<Canvas>();

        if (mainCanvas != null)
        {
            transform.SetParent(mainCanvas.transform);
            transform.SetAsLastSibling();

            // The slot we just left compensates its localScale for its own ancestor scale
            // (e.g. the hotbar's is smaller than the bag's, so items sitting in it use a
            // >1 localScale to still read as 1x on screen). That compensation only made
            // sense under the old parent - re-apply it for the canvas now, or the icon
            // renders oversized/undersized for the whole drag until it's dropped again.
            RectTransform dragRect = GetComponent<RectTransform>();
            if (dragRect != null)
            {
                Vector3 canvasLossyScale = UiScale.WithinCanvas(mainCanvas.transform);
                dragRect.localScale = new Vector3(
                    canvasLossyScale.x != 0f ? 1f / canvasLossyScale.x : 1f,
                    canvasLossyScale.y != 0f ? 1f / canvasLossyScale.y : 1f,
                    canvasLossyScale.z != 0f ? 1f / canvasLossyScale.z : 1f
                );
            }
        }

        if (image != null)
            image.raycastTarget = false;

        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (parentAfterDrag == null)
        {
            Debug.LogWarning(
                $"parentAfterDrag NULL for {itemName}, returning to original slot."
            );

            parentAfterDrag = originalSlotParent;
        }

        transform.SetParent(parentAfterDrag);

        RectTransform rect =
            GetComponent<RectTransform>();

        if (rect != null)
        {
            rect.localPosition = Vector3.zero;

            // Slots can sit under differently-scaled ancestors (e.g. the bag vs the hotbar),
            // so compensate here to keep the icon's actual on-screen size consistent everywhere.
            Vector3 parentLossyScale = UiScale.WithinCanvas(parentAfterDrag);
            rect.localScale = new Vector3(
                parentLossyScale.x != 0f ? 1f / parentLossyScale.x : 1f,
                parentLossyScale.y != 0f ? 1f / parentLossyScale.y : 1f,
                parentLossyScale.z != 0f ? 1f / parentLossyScale.z : 1f
            );

            // Bag and hotbar slots have different-sized frames, so take the size the slot it
            // landed in wants - otherwise an icon carried over from a bigger slot covers the border.
            InventorySlot landedSlot = parentAfterDrag.GetComponent<InventorySlot>();
            if (landedSlot != null) rect.sizeDelta = landedSlot.IconSize(isReadableNote);
        }

        if (image != null)
        {
            image.raycastTarget = true;
            image.enabled = image.sprite != null;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        RefreshSourceSlot();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Ignore empty placeholder items
        if (string.IsNullOrEmpty(itemName))
            return;

        if (isReadableNote && NoteSystem.Instance != null)
        {
            // sourceObject is null here (this is a re-read from the bag, not a fresh world pickup)
            // - NoteSystem already treats a null sourceObject as "nothing to re-collect on close",
            // it just re-shows the text. The detail and 3D panels are filled only once the page is
            // closed and the bag is back, so they never sit on top of the page while it's read.
            NoteSystem.Instance.InspectNoteInFace(itemDescription, null, ShowNoteInBag);
            return;
        }

        DescriptionPanel panel =
            FindFirstObjectByType<DescriptionPanel>();

        if (panel != null)
        {
            panel.ShowItem(this);
        }

        if (sourceWorldObject != null && ItemInspection3D.Instance != null)
        {
            ItemInspection3D.Instance.Inspect(sourceWorldObject, itemName);
        }
    }

    // A note back in the bag after reading: case notes get their Observation in the detail panel
    // (a plain note's full text would just be cramped in there), and every note shows its paper in
    // the 3D inspection panel like any other item.
    void ShowNoteInBag()
    {
        if (this == null) return;

        if (CaseFile.Find(itemName) != null)
        {
            DescriptionPanel panel = FindFirstObjectByType<DescriptionPanel>(FindObjectsInactive.Include);
            if (panel != null) panel.ShowItem(this);
        }

        if (sourceWorldObject != null && ItemInspection3D.Instance != null)
            ItemInspection3D.Instance.Inspect(sourceWorldObject, itemName);
    }
}