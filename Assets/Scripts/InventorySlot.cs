using UnityEngine;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour, IDropHandler
{
    public int slotIndex;

    // How big an item icon sitting in this slot should be (its sizeDelta). A bag slot's own "Image"
    // child marks the area inside the frame art, so an icon sized to it never covers the border.
    // isNote shrinks a note a step further in the hotbar (see HotbarManager.NOTE_ICON_SCALE); bag
    // slots size every icon to their inner image area as before.
    public Vector2 IconSize(bool isNote = false)
    {
        Transform parent = transform.parent;
        UnityEngine.UI.GridLayoutGroup parentGrid = parent != null ? parent.GetComponent<UnityEngine.UI.GridLayoutGroup>() : null;
        if (parentGrid != null)
        {
            foreach (Transform child in transform)
            {
                if (child.GetComponent<DraggableItem>() != null || child.GetComponent<UnityEngine.UI.Image>() == null) continue;
                Vector2 inner = ((RectTransform)child).rect.size;
                if (inner.x > 1f && inner.y > 1f) return inner;
            }
            return parentGrid.cellSize * HotbarManager.ICON_FILL_RATIO;
        }

        RectTransform slotRect = GetComponent<RectTransform>();
        float fill = HotbarManager.HOTBAR_ICON_FILL * (isNote ? HotbarManager.NOTE_ICON_SCALE : 1f);
        if (slotRect == null) return new Vector2(100f, 100f) * fill;

        // The hotbar's slots inherit a non-uniform scale from EquipmentBar/HotBar (their own
        // frame art isn't scale-compensated), so a slot's rect.size (100x100 locally) actually
        // renders at rect.size * lossyScale on screen (~167x167 true pixels) - not 100x100. The
        // icon is scale-compensated back to true pixels, so target the frame's actual on-screen
        // size.
        Vector3 slotScale = UiScale.WithinCanvas(slotRect);
        Vector2 trueOnScreenSize = new Vector2(
            slotRect.rect.size.x * slotScale.x,
            slotRect.rect.size.y * slotScale.y
        );
        return trueOnScreenSize * fill;
    }

    public void OnDrop(PointerEventData eventData)
    {
        GameObject droppedObject = eventData.pointerDrag;

        if (droppedObject == null)
            return;

        DraggableItem draggedItem =
            droppedObject.GetComponent<DraggableItem>();

        if (draggedItem == null)
            return;

        InventorySlot sourceSlot =
            draggedItem.sourceSlot;

        if (sourceSlot == null)
        {
            Debug.LogWarning("Source slot missing!");
            return;
        }

        // Same slot
        if (sourceSlot == this)
        {
            draggedItem.parentAfterDrag = transform;
            draggedItem.sourceSlot = this;
            return;
        }

        PlayerInteract player =
            FindFirstObjectByType<PlayerInteract>();

        HotbarManager hotbar =
            FindFirstObjectByType<HotbarManager>();

        DraggableItem targetItem =
            GetDirectDraggableItem(this);

        // ==================================================
        // MOVE TO EMPTY SLOT
        // ==================================================

        if (targetItem == null)
        {
            // Data only - DraggableItem.OnEndDrag() (which always runs right after this, per
            // Unity's drag event order) is the single place that actually performs the
            // SetParent/position/scale work, reading parentAfterDrag/sourceSlot set here.
            draggedItem.parentAfterDrag =
                transform;

            draggedItem.sourceSlot = this;

            if (player != null)
            {
                player.heldItemNames[slotIndex] =
                    draggedItem.itemName;

                player.slotOccupied[slotIndex] =
                    true;

                player.heldItemNames[
                    sourceSlot.slotIndex
                ] = "";

                player.slotOccupied[
                    sourceSlot.slotIndex
                ] = false;
            }

            if (hotbar != null)
            {
                if (slotIndex < 4)
                {
                    // false - draggedItem's own Image already renders this icon, so the
                    // permanent display isn't needed (and would otherwise draw a second,
                    // separately-sized copy behind it).
                    hotbar.UpdateSlot(
                        slotIndex,
                        GetSprite(draggedItem),
                        draggedItem.itemName,
                        false
                    );
                }

                if (sourceSlot.slotIndex < 4)
                {
                    hotbar.UpdateSlot(
                        sourceSlot.slotIndex,
                        null,
                        "",
                        false
                    );
                }
            }

            return;
        }

        // ==================================================
        // SWAP ITEMS
        // ==================================================

        string tempName =
            targetItem.itemName;

        string tempDescription =
            targetItem.itemDescription;

        string tempUsage =
            targetItem.itemUsage;

        Sprite tempSprite =
            GetSprite(targetItem);

        bool tempIsReadableNote =
            targetItem.isReadableNote;

        GameObject tempSourceWorldObject =
            targetItem.sourceWorldObject;

        // Target gets dragged item

        targetItem.itemName =
            draggedItem.itemName;

        targetItem.itemDescription =
            draggedItem.itemDescription;

        targetItem.itemUsage =
            draggedItem.itemUsage;

        SetSprite(
            targetItem,
            GetSprite(draggedItem)
        );

        targetItem.isReadableNote =
            draggedItem.isReadableNote;

        targetItem.sourceWorldObject =
            draggedItem.sourceWorldObject;

        targetItem.sourceSlot = this;

        // Dragged gets old target item

        draggedItem.itemName =
            tempName;

        draggedItem.itemDescription =
            tempDescription;

        draggedItem.itemUsage =
            tempUsage;

        SetSprite(
            draggedItem,
            tempSprite
        );

        draggedItem.isReadableNote =
            tempIsReadableNote;

        draggedItem.sourceWorldObject =
            tempSourceWorldObject;

        draggedItem.parentAfterDrag =
            sourceSlot.transform;

        draggedItem.sourceSlot =
            sourceSlot;

        if (player != null)
        {
            player.heldItemNames[slotIndex] =
                targetItem.itemName;

            player.slotOccupied[slotIndex] =
                !string.IsNullOrEmpty(
                    targetItem.itemName
                );

            player.heldItemNames[
                sourceSlot.slotIndex
            ] =
                draggedItem.itemName;

            player.slotOccupied[
                sourceSlot.slotIndex
            ] =
                !string.IsNullOrEmpty(
                    draggedItem.itemName
                );
        }

        if (hotbar != null)
        {
            // false in both calls below - targetItem/draggedItem's own Image already renders
            // each icon, so the permanent display isn't needed here either.
            if (slotIndex < 4)
            {
                hotbar.UpdateSlot(
                    slotIndex,
                    GetSprite(targetItem),
                    targetItem.itemName,
                    false
                );
            }

            if (sourceSlot.slotIndex < 4)
            {
                hotbar.UpdateSlot(
                    sourceSlot.slotIndex,
                    GetSprite(draggedItem),
                    draggedItem.itemName,
                    false
                );
            }
        }
    }

    private DraggableItem GetDirectDraggableItem(
        InventorySlot slot)
    {
        foreach (Transform child in slot.transform)
        {
            DraggableItem item =
                child.GetComponent<DraggableItem>();

            if (item == null)
                continue;

            // Ignore empty placeholder items
            if (string.IsNullOrEmpty(item.itemName))
                continue;

            return item;
        }

        return null;
    }

    private Sprite GetSprite(
        DraggableItem item)
    {
        if (item != null &&
            item.image != null)
        {
            return item.image.sprite;
        }

        return null;
    }

    private void SetSprite(
        DraggableItem item,
        Sprite sprite)
    {
        if (item != null &&
            item.image != null)
        {
            item.image.sprite = sprite;
            item.image.enabled =
                sprite != null;
        }
    }
}