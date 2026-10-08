using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class HotbarManager : MonoBehaviour
{
    [System.Serializable]
    public class DynamicSlot
    {
        public Image slotFrame;
        public Image iconDisplay;

        [HideInInspector]
        public string itemId = "";
    }


    [Header("Hotbar Slots")]
    public List<DynamicSlot> hotbarSlots = new List<DynamicSlot>();


    [Header("Selection Colors")]
    public Color selectedColor = CrimeSceneTheme.AmberGlow;
    public Color normalColor = CrimeSceneTheme.BoneHandle;


    [Header("Icon Opacity")]
    [Range(0f, 1f)]
    public float iconOpacity = 1f;


    // How much of a slot's own rect the icon fills - 1.0 matches the slot's own declared image
    // size exactly (no padding), which is what a slot's "Image (1)" placeholder is already sized
    // to. Kept in sync with PlayerInteract's identical constant since that's the other icon layer
    // drawn on top of this one for a freshly-picked-up item, and any mismatch between the two
    // shows up as a small icon with a bigger duplicate behind it.
    public const float ICON_FILL_RATIO = 0.95f;

    // Hotbar icons only: share of the slot's on-screen frame the icon may use. At 0.95 an icon
    // covered the frame's carved border and the slot number. 0.62 read as too small, 0.78 as a bit
    // small; 0.85 sits just over the border's inner edge while the outer frame stays visible.
    public const float HOTBAR_ICON_FILL = 0.85f;
    // Notes are a full-bleed sheet of paper, so even at the same box size they read as a big solid
    // block next to cut-out item art - drawn a further step smaller in the hotbar.
    public const float NOTE_ICON_SCALE = 0.9f;

    // Multiplied onto every item icon's sprite color (UI Image tint multiplies the texture, so
    // values above 1 brighten it - some art, like the note's dull crumpled-paper photo, reads as
    // noticeably smaller/duller against the dark slot background than bright art like the
    // flashlight even at an identical box size, purely from lower contrast. Kept in sync with
    // DraggableItem's identical tint for the same reason ICON_FILL_RATIO is shared.
    public static readonly Color ICON_TINT = new Color(1.3f, 1.25f, 1.15f, 1f);

    private int currentSelectedSlot = 0;

    private PlayerInteract playerInteract;



    private void Awake()
    {
        playerInteract = FindFirstObjectByType<PlayerInteract>();
    }



    private void Start()
    {
        HighlightSlot(PlayerInteract.NoSlot);   // nothing in hand at the start
    }



    // Number keys are handled by PlayerInteract.HandleHotbarInput, which calls HighlightSlot. (This
    // used to read them too, and mapped key 1 to the Hand slot, which is now hidden.)





    // showIconDisplay=false is for slots that already have their own DraggableItem rendering the
    // icon (anything collected via PlayerInteract - evidence, notes, keys) - that item's own Image
    // is now the only thing drawn, so this display is turned off instead of drawing a second,
    // separately-sized copy behind it. Hand/Flashlight have no DraggableItem, so they keep using
    // this display normally (default true).
    public void UpdateSlot(int index, Sprite icon, string itemId, bool showIconDisplay = true)
    {
        if (index < 0 || index >= hotbarSlots.Count)
        {
            Debug.LogWarning("Invalid hotbar index: " + index);
            return;
        }


        DynamicSlot slot = hotbarSlots[index];

        slot.itemId = itemId;



        if (slot.iconDisplay != null)
        {
            if (!showIconDisplay)
            {
                slot.iconDisplay.enabled = false;
                // Also clear the sprite, not just disable - HighlightSlot() re-enables this
                // display whenever its sprite is non-null, so a stale sprite left behind here
                // (e.g. Hand's, if this slot previously used the display) would otherwise pop
                // back up the next time the slot is selected.
                slot.iconDisplay.sprite = null;
            }
            else
            {
            slot.iconDisplay.sprite = icon;

            slot.iconDisplay.enabled = icon != null;

            // Fill the box exactly rather than preserving each icon's native aspect ratio - a
            // portrait-shaped image (e.g. the note) otherwise renders visibly smaller than a
            // near-square one (e.g. the flashlight) at the same box size, since preserveAspect
            // shrinks it to fit within the box on its longer axis. Consistent icon size across
            // items matters more here than avoiding the mild stretch on non-square art.
            slot.iconDisplay.preserveAspect = true;
            RectTransform iconRect = slot.iconDisplay.GetComponent<RectTransform>();
            if (iconRect != null)
            {
                // Sized off the actual slot rect (not a fixed pixel constant) so this stays correct
                // regardless of how big any given slot's own frame art is - hotbar slots and bag
                // slots are different sizes, and either can be resized later without this drifting
                // out of sync again. Must match PlayerInteract's CollectEvidence()/CollectKey()
                // fill ratio exactly, since that's the other layer drawn on top of this one.
                RectTransform slotRect = iconRect.parent as RectTransform;
                Vector2 slotSize;
                if (slotRect != null)
                {
                    // The slot's own frame art isn't scale-compensated the way this icon is about
                    // to be, so it renders at rect.size * lossyScale on screen (e.g. a hotbar slot's
                    // 100x100 local rect actually shows as ~167x167 true pixels because of the
                    // hotbar's own scale stack) - not just rect.size. Target that actual on-screen
                    // size, or the icon (compensated back to true pixels below) renders smaller
                    // than the frame it's sitting in.
                    Vector3 slotScale = UiScale.WithinCanvas(slotRect);
                    slotSize = new Vector2(slotRect.rect.size.x * slotScale.x, slotRect.rect.size.y * slotScale.y);
                }
                else
                {
                    slotSize = new Vector2(100f, 100f);
                }
                iconRect.sizeDelta = slotSize * HOTBAR_ICON_FILL;
                iconRect.anchoredPosition = Vector2.zero;

                // Slots can sit under non-uniformly scaled ancestors (the hotbar/equipment bar
                // stack), and unlike PlayerInteract's DraggableItem overlay - which compensates for
                // this on its own rect - this display was left uncompensated, so it rendered ~1.67x
                // too big. Invisible for Hand/Flashlight (nothing else drawn on top to compare
                // against), but a picked-up item shows this oversized layer stacked behind the
                // correctly-sized DraggableItem icon, reading as a small icon with a bigger duplicate
                // behind it. Same fix as CollectEvidence(): cancel the parent's lossy scale.
                Vector3 parentLossyScale = iconRect.parent != null ? UiScale.WithinCanvas(iconRect.parent) : Vector3.one;
                iconRect.localScale = new Vector3(
                    parentLossyScale.x != 0f ? 1f / parentLossyScale.x : 1f,
                    parentLossyScale.y != 0f ? 1f / parentLossyScale.y : 1f,
                    parentLossyScale.z != 0f ? 1f / parentLossyScale.z : 1f
                );
            }

            Color c = ICON_TINT;
            c.a = icon != null ? iconOpacity : 0f;
            slot.iconDisplay.color = c;
            }
        }
        else
        {
            Debug.LogWarning(
                "Missing iconDisplay on Hotbar slot " + index
            );
        }



        if (playerInteract == null)
            playerInteract = FindFirstObjectByType<PlayerInteract>();


        if (playerInteract != null)
        {
            playerInteract.heldItemNames[index] = itemId;
            playerInteract.slotOccupied[index] =
                !string.IsNullOrEmpty(itemId);
        }
    }






    public void ClearSlot(int index)
    {
        if (index < 0 || index >= hotbarSlots.Count)
            return;


        DynamicSlot slot = hotbarSlots[index];


        slot.itemId = "";


        if (slot.iconDisplay != null)
        {
            slot.iconDisplay.sprite = null;
            slot.iconDisplay.enabled = false;
        }


        if (playerInteract == null)
            playerInteract = FindFirstObjectByType<PlayerInteract>();


        if (playerInteract != null)
        {
            playerInteract.heldItemNames[index] = "";
            playerInteract.slotOccupied[index] = false;
        }
    }







    public void HighlightSlot(int index)
    {
        currentSelectedSlot = index;


        if (playerInteract == null)
            playerInteract = FindFirstObjectByType<PlayerInteract>();


        if (playerInteract != null)
            playerInteract.currentlySelectedSlot = index;



        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            DynamicSlot slot = hotbarSlots[i];


            if (slot == null)
                continue;



            if (slot.slotFrame != null)
            {
                slot.slotFrame.color =
                    (i == index)
                    ? selectedColor
                    : normalColor;
            }



            // DO NOT disable icons here
            // This was causing moved items to disappear
            if (slot.iconDisplay != null)
            {
                if (slot.iconDisplay.sprite != null)
                {
                    slot.iconDisplay.enabled = true;

                    Color c = ICON_TINT;
                    c.a = iconOpacity;
                    slot.iconDisplay.color = c;
                }
            }
        }
    }







    private void OnSlotActivated(int index)
    {
        if (index < 0 || index >= hotbarSlots.Count)
            return;
    }

    public bool AddItemToFirstEmptySlot(Sprite icon, string itemId)
    {
        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            if (string.IsNullOrEmpty(hotbarSlots[i].itemId))
            {
                UpdateSlot(i, icon, itemId);
                return true;
            }
        }

        return false;
    }
}