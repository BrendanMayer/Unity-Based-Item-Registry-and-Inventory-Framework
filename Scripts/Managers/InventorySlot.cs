using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("Index & Side")]
    public int slotIndex;
    [SerializeField] private bool isContainerSlot = false;

    [Header("UI Refs")]
    [SerializeField] private Image icon;            // child image that shows the item
    [SerializeField] private TMP_Text quantityText; // child text
    [SerializeField] private Image hitArea;         // parent Slot image (the click surface)
    [SerializeField] private GameObject Highlight;
    [SerializeField] private bool isSelected;

    [Header("Hover FX")]
    [SerializeField] private float hoverScale = 1.08f;              // 8% pop
    [SerializeField] private float hoverLift = 6f;                  // px up
    [SerializeField] private float hoverDuration = 0.08f;           // seconds
    [SerializeField] private AnimationCurve hoverEase = null;

    private RectTransform iconRect;
    private Vector3 baseScale;
    private Vector2 baseAnchoredPos;
    private Coroutine hoverRoutine;

    private Item shownItem;
    private int quantity;
    private int pendingDragAmount = 0;

    public void SetIsContainerSlot(bool v) => isContainerSlot = v;

    // --- Accessors used by UIManager animation ---
    public RectTransform GetIconRect() => icon ? icon.rectTransform : null;
    public Item GetShownItem() => shownItem;

    private void Awake()
    {
        if (hoverEase == null) hoverEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // Auto-wire
        if (!hitArea) hitArea = GetComponent<Image>();
        if (!hitArea) { hitArea = gameObject.AddComponent<Image>(); hitArea.color = new Color(0, 0, 0, 0); }
        if (!icon) icon = transform.Find("Icon")?.GetComponent<Image>() ?? GetComponentInChildren<Image>(true);
        if (!quantityText) quantityText = GetComponentInChildren<TMP_Text>(true);

        // Raycast policy: parent ON, children OFF
        foreach (var g in GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;
        hitArea.raycastTarget = true;

        // Hover FX bases
        iconRect = icon ? icon.rectTransform : null;
        if (iconRect != null)
        {
            baseScale = iconRect.localScale;
            baseAnchoredPos = iconRect.anchoredPosition;
        }
    }

    private void OnDisable()
    {
        if (iconRect != null)
        {
            if (hoverRoutine != null) StopCoroutine(hoverRoutine);
            iconRect.localScale = baseScale;
            iconRect.anchoredPosition = baseAnchoredPos;
        }
    }

    public void UpdateSlot(Item item, int itemQuantity)
    {
        shownItem = item;
        quantity = itemQuantity;

        if (!icon)
        {
            Debug.LogError("[InventorySlot] No Icon Image wired on " + name);
            return;
        }

        if (item != null)
        {
            icon.enabled = true;
            icon.sprite = item.GetIcon();
            icon.color = Color.white;
            quantity = item.GetAmount();
        }
        else
        {
            icon.sprite = null;
            icon.enabled = true;
            icon.color = new Color(1, 1, 1, 0);
            quantity = 0;

            if (iconRect != null)
            {
                if (hoverRoutine != null) StopCoroutine(hoverRoutine);
                iconRect.localScale = baseScale;
                iconRect.anchoredPosition = baseAnchoredPos;
            }
        }

        if (quantityText) quantityText.text = (quantity > 1) ? "x " + quantity : "";
    }

    public void SelectSlot(bool toggle)
    {
        if (Highlight)
        {
            Highlight.SetActive(toggle);
            isSelected = toggle;
        }
    }

    // ---------- Pointer / Drag ----------

    public void OnPointerEnter(PointerEventData e)
    {
        UIManager.instance?.SetHoveredSlot(this);
        if (shownItem != null) StartHoverFX(true);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (UIManager.instance?.CurrentHoveredSlot() == this)
            UIManager.instance.SetHoveredSlot(null);
        if (shownItem != null) StartHoverFX(false);
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (shownItem == null) return;

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        // Shift + LMB: quick move whole stack (animate fly)
        if (shift && e.button == PointerEventData.InputButton.Left)
        {
            QuickMove(shownItem.GetAmount(), animate: true);
            return;
        }

        // MIDDLE: split half. Shift+MIDDLE: quick move half (animate)
        if (e.button == PointerEventData.InputButton.Middle)
        {
            int half = Mathf.Max(1, shownItem.GetAmount() / 2);
            if (shift)
            {
                QuickMove(half, animate: true);
            }
            else
            {
                pendingDragAmount = half; // prepare to drag; we do NOT remove yet
            }
            return;
        }

        // RMB: prepare drag single item (Shift+RMB quick move single with animation)
        if (e.button == PointerEventData.InputButton.Right)
        {
            if (shift)
            {
                QuickMove(1, animate: true);
            }
            else
            {
                pendingDragAmount = 1;
                return;
            }
        }

        // LMB: prepare drag whole stack
        if (e.button == PointerEventData.InputButton.Left)
        {
            pendingDragAmount = shownItem.GetAmount();
        }
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (shownItem == null || pendingDragAmount <= 0) return;

        var ghost = new Item(shownItem.Data);
        ghost.SetAmount(pendingDragAmount);

        int maxAtOrigin = shownItem.GetAmount();
        UIManager.instance.BeginDrag(ghost, pendingDragAmount, maxAtOrigin, isContainerSlot, slotIndex);
    }

    public void OnDrag(PointerEventData e) { }

    public void OnEndDrag(PointerEventData e)
    {
        pendingDragAmount = 0;
        UIManager.instance.EndDrag(true);
    }

    public void OnDrop(PointerEventData e)
    {
        var p = UIManager.instance.CurrentPayload();
        if (p == null || p.item == null || p.amount <= 0) return;

        var inv = UIManager.instance.PlayerInventory;
        var con = UIManager.instance.ActiveContainer;

        bool fromContainer = p.fromContainer;
        bool toContainer = isContainerSlot;

        if (fromContainer == toContainer && p.originIndex == slotIndex)
        {
            UIManager.instance.EndDrag(true);
            return;
        }

        var attempt = new Item(p.item.Data);
        attempt.SetAmount(p.amount);

        int leftover = toContainer
            ? con.TryAddIntoSlot(slotIndex, attempt)
            : inv.TryAddIntoSlot(slotIndex, attempt);

        int moved = p.amount - leftover;
        if (moved > 0)
        {
            if (fromContainer) con.RemoveFromIndex(p.originIndex, moved);
            else inv.RemoveFromIndex(p.originIndex, moved);
        }

        inv.UpdateInventorySlots();
        con.UpdateInventorySlots();

        UIManager.instance.EndDrag();
        pendingDragAmount = 0;
    }

    // ---------- Helpers ----------

    void QuickMove(int amount, bool animate = false)
    {
        if (shownItem == null)
        {
            Debug.LogWarning($"[InventorySlot:{name}] QuickMove called with no item.");
            return;
        }
        if (shownItem.Data == null)
        {
            Debug.LogError("[InventorySlot] shownItem.Data is null.");
            return;
        }

        amount = Mathf.Clamp(amount, 1, shownItem.GetAmount());

        var ui = UIManager.instance;
        if (ui == null) { Debug.LogError("[InventorySlot] UIManager.instance is null."); return; }

        var inv = ui.PlayerInventory;
        var con = ui.ActiveContainer;
        if (inv == null) { Debug.LogError("[InventorySlot] PlayerInventory is null on UIManager."); return; }
        if (isContainerSlot && con == null) { Debug.LogWarning("[InventorySlot] No ActiveContainer open (container - inventory)."); return; }
        if (!isContainerSlot && con == null) { Debug.LogWarning("[InventorySlot] No ActiveContainer open (inventory - container)."); return; }

        ItemData dataForAnim = shownItem.Data;
        RectTransform fromIcon = GetIconRect();
        Sprite iconSprite = icon ? icon.sprite : null;

        // Snapshot destination side BEFORE the move
        bool toContainer = !isContainerSlot;
        List<UIManager.SlotSnapshot> beforeSnap = null;
        if (animate && iconSprite && fromIcon)
            beforeSnap = ui.SnapshotSlots(toContainer);

        var part = new Item(dataForAnim);
        part.SetAmount(amount);

        if (isContainerSlot)
        {
            // Move FROM container TO inventory
            int before = part.GetAmount();
            int leftover = inv.AddItem(part);
            int moved = before - leftover;
            if (moved > 0) con.RemoveFromIndex(slotIndex, moved);

            inv.UpdateInventorySlots();
            con.UpdateInventorySlots();
        }
        else
        {
            // Move FROM inventory TO container
            int before = part.GetAmount();
            int leftover = con.AddItem(part);
            int moved = before - leftover;
            if (moved > 0) inv.RemoveFromIndex(slotIndex, moved);

            inv.UpdateInventorySlots();
            con.UpdateInventorySlots();
        }

        // Snapshot destination side AFTER the move and find exact target slot
        InventorySlot targetSlot = null;
        if (animate && iconSprite && fromIcon)
        {
            var afterSnap = ui.SnapshotSlots(toContainer);
            targetSlot = ui.FindDestinationSlotByDiff(toContainer, dataForAnim, beforeSnap, afterSnap);

            if (targetSlot != null && targetSlot.GetIconRect() != null)
            {
                ui.AnimateQuickMove(
                    iconSprite,
                    fromIcon,
                    targetSlot.GetIconRect(),
                    shrinkTo: 0.5f,
                    flyTime: 0.25f,
                    popScale: 1.12f,
                    popTime: 0.07f
                );
            }
            // else: nothing obvious changed visually (could have merged outside visible grid, etc.)
        }
    }


    public bool GetIsSelected() => isSelected;

    // ---------- Hover FX internals ----------

    private void StartHoverFX(bool on)
    {
        if (iconRect == null) return;
        if (on) iconRect.localScale = baseScale * (hoverScale + 0.06f);
        if (hoverRoutine != null) StopCoroutine(hoverRoutine);
        hoverRoutine = StartCoroutine(HoverTween(on));
    }

    private System.Collections.IEnumerator HoverTween(bool on)
    {
        float t = 0f;
        Vector3 fromScale = iconRect.localScale;
        Vector3 toScale = on ? baseScale * hoverScale : baseScale;

        Vector2 fromPos = iconRect.anchoredPosition;
        Vector2 toPos = on ? baseAnchoredPos + new Vector2(0f, hoverLift) : baseAnchoredPos;

        float dur = Mathf.Max(0.0001f, hoverDuration);

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float k = hoverEase.Evaluate(Mathf.Clamp01(t));
            iconRect.localScale = Vector3.LerpUnclamped(fromScale, toScale, k);
            iconRect.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, k);
            yield return null;
        }

        iconRect.localScale = toScale;
        iconRect.anchoredPosition = toPos;
    }
}
