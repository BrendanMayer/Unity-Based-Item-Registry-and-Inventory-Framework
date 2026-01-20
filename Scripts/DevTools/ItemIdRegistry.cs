using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemIdRegistry", menuName = "Objects/Item ID Registry")]
public class ItemIdRegistry : ScriptableObject
{
    public List<int> usedIds = new List<int>();

    // Optional helper if you want sequential IDs starting at 1
    public int GetNextId()
    {
        int n = 1;
        while (usedIds.Contains(n)) n++;
        usedIds.Add(n);
        return n;
    }
}
