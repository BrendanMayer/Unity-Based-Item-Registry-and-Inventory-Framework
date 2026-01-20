using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager instance { get; private set; }

    [Header("Scene Refs")]
    [SerializeField] private PlayerController playerController;    // optional
    [SerializeField] private bool autoFindPlayerController = true;

    [Header("Inventory/Container UI")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private RectTransform containerUI;
    [SerializeField] private GameObject consoleWindow;
    [SerializeField] private GameObject[] inventorySlots;          // assign in Inspector
    [SerializeField] private List<GameObject> containerSlots = new();
    [SerializeField] private GameObject inventoryTooltips;

    [Header("Player/Container")]
    public Inventory PlayerInventory;                              // assign in Inspector
    public ContainerInventory ActiveContainer { get; private set; }

    [Header("Drag Ghost (assign these)")]
    [SerializeField] private Canvas dragCanvas;                    // same canvas as your UI
    [SerializeField] private RectTransform dragGhost;              // root object (start inactive)
    [SerializeField] private Image dragGhostImage;                 // icon image (Raycast Target OFF)
    [SerializeField] private TMP_Text dragGhostAmount;             // optional stack text

    [Header("Drag Amount Controls")]
    [SerializeField] private int scrollStep = 1;                   // amount per wheel notch
    [SerializeField] private int fastStepMultiplier = 5;           // hold Shift to multiply

    // ---------- Drag Payload ----------
    [Serializable]
    public class DragPayload
    {
        public Item item;
        public int amount;
        public int maxAmount;
        public bool fromContainer;
        public int originIndex;
        public TMP_Text ghostAmountText;

        public void SetAmount(int newAmount)
        {
            maxAmount = Mathf.Max(1, maxAmount);
            amount = Mathf.Clamp(newAmount, 1, maxAmount);

            if (ghostAmountText)
                ghostAmountText.text = (amount > 1) ? $"x {amount}" : "";
        }
    }

    private DragPayload payload;
    private InventorySlot hoveredSlot;

    // ---------- Singleton ----------
    private void Awake()
    {
        if (instance == null) instance = this;
    }

    private PlayerController GetPlayerController()
    {
        if (playerController == null && autoFindPlayerController)
        {
#if UNITY_2023_1_OR_NEWER
            playerController = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
#else
            playerController = FindObjectOfType<PlayerController>();
#endif
        }
        return playerController;
    }

    private void Start()
    {
        // Index & mark INVENTORY slots (not container)
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            var slot = inventorySlots[i].GetComponentInChildren<InventorySlot>(true);
            if (!slot) continue;
            slot.slotIndex = i;
            slot.SelectSlot(false);
            slot.SetIsContainerSlot(false);
        }

        // ensure ghost hidden
        if (dragGhost) dragGhost.gameObject.SetActive(false);
        if (dragGhostImage) dragGhostImage.raycastTarget = false;
    }

    private void Update()
    {
        // move drag ghost with mouse when dragging
        if (payload != null && dragGhost && dragCanvas)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragCanvas.transform as RectTransform,
                Input.mousePosition,
                dragCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : dragCanvas.worldCamera,
                out var pos);

            float speed = 15f;
            dragGhost.anchoredPosition = Vector2.Lerp(
                dragGhost.anchoredPosition,
                pos,
                Time.deltaTime * speed
            );
        }

        // Adjust dragging amount via mouse wheel
        if (payload != null && payload.item != null)
        {
            float wheel = Input.mouseScrollDelta.y; // +1 up, -1 down
            if (Mathf.Abs(wheel) > 0.01f)
            {
                int step = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    ? scrollStep * fastStepMultiplier
                    : scrollStep;

                int delta = (int)wheel * step;
                payload.SetAmount(payload.amount + delta);
            }
        }
    }

    // ---------- Public UI helpers ----------
    public GameObject[] GetInventorySlots() => inventorySlots;
    public List<GameObject> GetContainerSlots() => containerSlots;

    public void ToggleMouseAndCamera(bool toggle)
    {
        Cursor.lockState = toggle ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = toggle;

        var pc = GetPlayerController();
        if (pc != null)
        {
            pc.lockCursor = toggle;
        }
    }

    public void ToggleConsoleWindow(bool toggle)
    {
        if (consoleWindow) consoleWindow.SetActive(toggle);
    }

    // ---------- Drag API ----------
    public void BeginDrag(Item item, int amount, bool fromContainer, int originIndex)
    {
        BeginDrag(item, amount, amount, fromContainer, originIndex);
    }

    public void BeginDrag(Item item, int initialAmount, int maxAmount, bool fromContainer, int originIndex)
    {
        payload = new DragPayload
        {
            item = item,
            amount = Mathf.Clamp(initialAmount, 1, Mathf.Max(1, maxAmount)),
            maxAmount = Mathf.Max(1, maxAmount),
            fromContainer = fromContainer,
            originIndex = originIndex,
            ghostAmountText = dragGhostAmount
        };

        if (dragGhost)
        {
            if (dragGhostImage) dragGhostImage.sprite = item?.GetIcon();
            if (dragGhostAmount) dragGhostAmount.text = (payload.amount > 1) ? $"x {payload.amount}" : "";
            dragGhost.gameObject.SetActive(true);
            if (payload != null && dragGhost && dragCanvas)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragCanvas.transform as RectTransform,
                    Input.mousePosition,
                    dragCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : dragCanvas.worldCamera,
                    out var pos);
                dragGhost.anchoredPosition = pos;
            }
        }
    }

    public DragPayload CurrentPayload() => payload;

    public void EndDrag(bool cancel = false)
    {
        if (dragGhost) dragGhost.gameObject.SetActive(false);
        payload = null;
        hoveredSlot = null;
    }

    public void SetHoveredSlot(InventorySlot s) => hoveredSlot = s;
    public InventorySlot CurrentHoveredSlot() => hoveredSlot;

    // ---------- Container open/build ----------
    public void OpenContainer(ContainerInventory container)
    {
        ActiveContainer = container;
        GenerateSlots(container.gridSize, container.fillHorizontally, container.xPadding, container.yPadding);

        for (int i = 0; i < containerSlots.Count; i++)
        {
            var slot = containerSlots[i].GetComponentInChildren<InventorySlot>(true);
            if (!slot) continue;
            slot.slotIndex = i;
            slot.SelectSlot(false);
            slot.SetIsContainerSlot(true);
        }

        container.UpdateInventorySlots();
        ShowContainerUI(true);
        ToggleMouseAndCamera(true);
    }

    internal void ShowContainerUI(bool toggle)
    {
        if (containerUI) containerUI.gameObject.SetActive(toggle);
        if (!toggle) ActiveContainer = null;
    }

    // ---------- Slot grid generation ----------
    public void GenerateSlots(Vector2Int gridSize, bool fillHorizontally, int xPadding, int yPadding)
    {
        if (!slotPrefab || !containerUI)
        {
            Debug.LogWarning($"[{name}] Missing slotPrefab or containerUI.");
            return;
        }

        for (int i = containerUI.childCount - 1; i >= 0; i--)
        {
            containerSlots.Remove(containerUI.GetChild(i).gameObject);
            Destroy(containerUI.GetChild(i).gameObject);
        }

        var grid = containerUI.GetComponent<GridLayoutGroup>();
        if (!grid) grid = containerUI.gameObject.AddComponent<GridLayoutGroup>();

        if (fillHorizontally)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, gridSize.x);
        }
        else
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = Mathf.Max(1, gridSize.y);
        }

        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.spacing = new Vector2(xPadding, yPadding);

        int total = Mathf.Max(0, gridSize.x * gridSize.y);
        for (int i = 0; i < total; i++)
        {
            var go = Instantiate(slotPrefab, containerUI);
            go.name = $"Slot {i}";
            containerSlots.Add(go);

            var slot = go.GetComponentInChildren<InventorySlot>(true);
            if (slot)
            {
                slot.slotIndex = i;
                slot.SelectSlot(false);
                slot.SetIsContainerSlot(true);
            }
        }
    }

    // =====================================================================
    //                            FLY ICON FX
    // =====================================================================

    /// <summary>
    /// Try to find a reasonable target slot for a quick-moved item.
    /// It picks the first slot on the destination side that holds the same ItemData.
    /// </summary>
    public InventorySlot FindLikelyTargetSlot(bool toContainer, ItemData data)
    {
        if (data == null) return null;

        if (toContainer)
        {
            for (int i = 0; i < containerSlots.Count; i++)
            {
                var s = containerSlots[i].GetComponentInChildren<InventorySlot>(true);
                var it = s ? s.GetShownItem() : null;
                if (it != null && it.Data == data)
                    return s;
            }
        }
        else
        {
            var arr = inventorySlots;
            for (int i = 0; i < arr.Length; i++)
            {
                var s = arr[i].GetComponentInChildren<InventorySlot>(true);
                var it = s ? s.GetShownItem() : null;
                if (it != null && it.Data == data)
                    return s;
            }
        }

        // fallback: first empty slot on destination side
        if (toContainer)
        {
            for (int i = 0; i < containerSlots.Count; i++)
            {
                var s = containerSlots[i].GetComponentInChildren<InventorySlot>(true);
                if (s != null && s.GetShownItem() == null)
                    return s;
            }
        }
        else
        {
            var arr = inventorySlots;
            for (int i = 0; i < arr.Length; i++)
            {
                var s = arr[i].GetComponentInChildren<InventorySlot>(true);
                if (s != null && s.GetShownItem() == null)
                    return s;
            }
        }

        return null;
    }

    /// <summary>
    /// Spawns a temp Image on dragCanvas and flies it from 'fromIcon' to 'toIcon'.
    /// Shrinks to 'shrinkTo' during flight, then pops 'toIcon' to 'popScale'.
    /// </summary>
    public void AnimateQuickMove(
        Sprite sprite,
        RectTransform fromIcon,
        RectTransform toIcon,
        float shrinkTo = 0.5f,
        float flyTime = 0.25f,
        float popScale = 1.12f,
        float popTime = 0.07f)
    {
        if (!dragCanvas || !sprite || !fromIcon || !toIcon) return;
        StartCoroutine(FlyIconRoutine(sprite, fromIcon, toIcon, shrinkTo, flyTime, popScale, popTime));
    }

    private System.Collections.IEnumerator FlyIconRoutine(
        Sprite sprite,
        RectTransform fromIcon,
        RectTransform toIcon,
        float shrinkTo,
        float flyTime,
        float popScale,
        float popTime)
    {
        // temp visual under the drag canvas (overlay layer)
        var go = new GameObject("FlyIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = true;

        rt.SetParent(dragCanvas.transform, worldPositionStays: false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        // start/end positions in canvas local space
        Vector2 start = CanvasLocalPointFromWorld(fromIcon);
        Vector2 end = CanvasLocalPointFromWorld(toIcon);

        // size roughly matches source icon rect
        var size = fromIcon.rect.size;
        rt.sizeDelta = size;

        // init at source
        rt.anchoredPosition = start;
        rt.localScale = Vector3.one;

        // fly
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, flyTime);
            float k = Mathf.SmoothStep(0f, 1f, t);
            rt.anchoredPosition = Vector2.LerpUnclamped(start, end, k);

            // scale down to shrinkTo during flight
            float s = Mathf.Lerp(1f, shrinkTo, k);
            rt.localScale = Vector3.one * s;

            yield return null;
        }

        // destroy temp
        Destroy(go);

        // pop the destination icon if it still exists
        if (toIcon)
        {
            // quick pop coroutine
            Vector3 baseScale = toIcon.localScale;
            Vector3 big = baseScale * popScale;

            float tt = 0f;
            while (tt < 1f)
            {
                tt += Time.unscaledDeltaTime / Mathf.Max(0.0001f, popTime);
                float k = Mathf.SmoothStep(0f, 1f, tt);
                toIcon.localScale = Vector3.LerpUnclamped(baseScale, big, k);
                yield return null;
            }

            // return back
            tt = 0f;
            while (tt < 1f)
            {
                tt += Time.unscaledDeltaTime / Mathf.Max(0.0001f, popTime);
                float k = Mathf.SmoothStep(0f, 1f, tt);
                toIcon.localScale = Vector3.LerpUnclamped(big, baseScale, k);
                yield return null;
            }

            toIcon.localScale = baseScale;
        }
    }

    private Vector2 CanvasLocalPointFromWorld(RectTransform worldRt)
    {
        var canvasRt = dragCanvas.transform as RectTransform;
        Camera cam = (dragCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : dragCanvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldRt.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, cam, out var local);
        return local;
    }

    public struct SlotSnapshot
    {
        public InventorySlot slot;
        public ItemData data;
        public int amount;
    }

    public List<SlotSnapshot> SnapshotSlots(bool ofContainer)
    {
        var list = new List<SlotSnapshot>();

        if (ofContainer)
        {
            for (int i = 0; i < containerSlots.Count; i++)
            {
                var s = containerSlots[i].GetComponentInChildren<InventorySlot>(true);
                if (s == null) continue;
                var it = s.GetShownItem();
                list.Add(new SlotSnapshot
                {
                    slot = s,
                    data = it != null ? it.Data : null,
                    amount = it != null ? it.GetAmount() : 0
                });
            }
        }
        else
        {
            for (int i = 0; i < inventorySlots.Length; i++)
            {
                var s = inventorySlots[i].GetComponentInChildren<InventorySlot>(true);
                if (s == null) continue;
                var it = s.GetShownItem();
                list.Add(new SlotSnapshot
                {
                    slot = s,
                    data = it != null ? it.Data : null,
                    amount = it != null ? it.GetAmount() : 0
                });
            }
        }

        return list;
    }

    /// <summary>
    /// Find the slot on the destination side where the specified item data increased.
    /// Priority:
    /// 1) Same item where amount increased
    /// 2) Empty slot that became this item
    /// 3) Fallback: first slot with this item
    /// </summary>
    public InventorySlot FindDestinationSlotByDiff(
        bool toContainer, ItemData data,
        List<SlotSnapshot> before, List<SlotSnapshot> after)
    {
        if (data == null || before == null || after == null || before.Count != after.Count)
            return null;

        // 1) Same item where amount increased
        for (int i = 0; i < after.Count; i++)
        {
            if (after[i].data == data && before[i].data == data && after[i].amount > before[i].amount)
                return after[i].slot;
        }

        // 2) Empty slot that became this item
        for (int i = 0; i < after.Count; i++)
        {
            if (before[i].data == null && after[i].data == data && after[i].amount > 0)
                return after[i].slot;
        }

        // 3) Fallback: any slot now holding this item
        for (int i = 0; i < after.Count; i++)
        {
            if (after[i].data == data)
                return after[i].slot;
        }

        return null;
    }

    public void ToggleInventoryTooltips(bool toggle)
    {
        if (inventoryTooltips) inventoryTooltips.SetActive(toggle);
    }

}
