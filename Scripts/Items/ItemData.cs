using UnityEngine;

public enum ItemKind { None, Placeable, Weapon, Consumable }

[CreateAssetMenu(fileName = "New Item", menuName = "Objects/Item")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public int maxAmount;
    public string description;
    public int id;
    public string filePath;

    public ItemKind kind = ItemKind.None;
    public GameObject placeablePrefab;

}
