using System;
using UnityEngine;

[System.Serializable]
public class Item
{
    [SerializeField] private ItemData itemData;
    [SerializeField] private int amount;
    [SerializeField] private int maxAmount;
    [SerializeField] private Sprite icon;

    private string itemName;
    private string description;
    private int id;




    public ItemData Data => itemData;


    public ItemData GetItemData()
    {
        return itemData;
    }

    public int GetAmount()
    {
        return amount;
    }

    public void SetAmount(int newAmount)
    {
        if (newAmount < 0)
        {
            amount = 0;
        }
        else if (newAmount > maxAmount)
        {
            amount = maxAmount;
        }
        else
        {
            amount = newAmount;
        }
    }

    public int GetMaxAmount()
    {
        return maxAmount;
    }

    internal Sprite GetIcon()
    {
        return icon;
    }

    public int GetID() => id;
    public string GetName() => itemName;

    public Item(ItemData itemData)
    {
        if (itemData == null)
        {
            Debug.LogError("ItemData not assigned in constructor.");
            return;
        }

        this.itemData = itemData;
        this.maxAmount = itemData.maxAmount;
        this.itemName = itemData.itemName;
        this.description = itemData.description;
        this.id = itemData.id;

        string fullPath = itemData.filePath + "/" + itemName;
        this.icon = Resources.Load<Sprite>(fullPath);

        if (this.icon == null)
        {
            Debug.LogError("Failed to load icon at Resources/" + fullPath);
        }
    }
}
