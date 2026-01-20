using System.Collections.Generic;
using UnityEngine;

public class ContainerInventory : MonoBehaviour
{
    [SerializeField] private int capacity = 18;
    [SerializeField] private List<Item> items = new List<Item>();

    [Header("UI Grid")]
    public Vector2Int gridSize = new Vector2Int(6, 3);  // cols x rows
    public bool fillHorizontally = true;
    public int xPadding = 8;
    public int yPadding = 8;

    [Header("Test (optional)")]
    public ItemData testData;
    public int testAmount;
    public int testAmountToAdd;

    private void Awake()
    {
        EnsureSize();
    }

    private void Start()
    {
        if (testData && testAmount > 0) { var it = new Item(testData); it.SetAmount(testAmount); AddItem(it); }
        if (testData && testAmountToAdd > 0) { var it = new Item(testData); it.SetAmount(testAmountToAdd); AddItem(it); }
    }

    private void EnsureSize()
    {
        if (items == null) items = new List<Item>(capacity);
        while (items.Count < capacity) items.Add(null);
        if (items.Count > capacity) items.RemoveRange(capacity, items.Count - capacity);
    }

    // ---------- Add / Stack into fixed slots ----------
    public int AddItem(Item incoming)
    {
        if (incoming == null || incoming.Data == null) return 0;
        EnsureSize();

        int toAdd = incoming.GetAmount();
        int maxPerStack = incoming.GetMaxAmount();
        var data = incoming.Data;

        // 1) Fill partial stacks
        for (int i = 0; i < items.Count && toAdd > 0; i++)
        {
            var stack = items[i];
            if (stack == null || stack.Data != data) continue;

            int canTake = stack.GetMaxAmount() - stack.GetAmount();
            if (canTake <= 0) continue;

            int addNow = Mathf.Min(canTake, toAdd);
            stack.SetAmount(stack.GetAmount() + addNow);
            toAdd -= addNow;
        }

        // 2) Create new stacks in first empty slots
        for (int i = 0; i < items.Count && toAdd > 0; i++)
        {
            if (items[i] != null) continue;
            int addNow = Mathf.Min(maxPerStack, toAdd);
            var newStack = new Item(data);
            newStack.SetAmount(addNow);
            items[i] = newStack;
            toAdd -= addNow;
        }

        UpdateInventorySlots();
        return toAdd; // leftover
    }

    // ---------- Drag/Drop helpers (no compaction) ----------
    public void RemoveFromIndex(int index, int amount)
    {
        EnsureSize();
        if (index < 0 || index >= items.Count) return;
        var it = items[index];
        if (it == null) return;

        it.SetAmount(it.GetAmount() - amount);
        if (it.GetAmount() <= 0) items[index] = null; // DO NOT RemoveAt()
    }

    public int TryAddIntoSlot(int index, Item incoming)
    {
        EnsureSize();
        if (incoming == null) return 0;
        if (index < 0 || index >= items.Count) return incoming.GetAmount();

        var slotItem = items[index];
        if (slotItem == null)
        {
            int put = Mathf.Min(incoming.GetAmount(), incoming.GetMaxAmount());
            var place = new Item(incoming.Data); place.SetAmount(put);
            items[index] = place;
            incoming.SetAmount(incoming.GetAmount() - put);
        }
        else if (slotItem.Data == incoming.Data)
        {
            int canTake = slotItem.GetMaxAmount() - slotItem.GetAmount();
            int add = Mathf.Min(canTake, incoming.GetAmount());
            slotItem.SetAmount(slotItem.GetAmount() + add);
            incoming.SetAmount(incoming.GetAmount() - add);
        }
        else
        {
            // swap
            var tmp = slotItem;
            items[index] = new Item(incoming.Data); items[index].SetAmount(incoming.GetAmount());
            incoming.SetAmount(tmp.GetAmount());
            incoming = tmp;
        }

        UpdateInventorySlots();
        return incoming.GetAmount();
    }

    // ---------- UI ----------
    public void GenerateSlots() =>
        UIManager.instance.GenerateSlots(gridSize, fillHorizontally, xPadding, yPadding);

    public void UpdateInventorySlots()
    {
        EnsureSize();
        var slots = UIManager.instance.GetContainerSlots();
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i].GetComponentInChildren<InventorySlot>(true);
            if (!slot) continue;

            var it = (i < items.Count) ? items[i] : null;
            if (it != null) slot.UpdateSlot(it, it.GetAmount());
            else slot.UpdateSlot(null, 0);
        }
    }
}
