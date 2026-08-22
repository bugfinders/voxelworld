using System;
using UnityEngine;

public class VoxelInventory : MonoBehaviour
{
    [SerializeField] private int[] itemCounts = Array.Empty<int>();
    [SerializeField] private string[] itemNames = Array.Empty<string>();

    /// <summary>
    /// Creates one inventory slot for each configured voxel material.
    /// </summary>
    public void Initialize(Material[] materials)
    {
        int materialCount = materials == null ? 0 : materials.Length;
        itemCounts = new int[materialCount];
        itemNames = new string[materialCount];

        for (int i = 0; i < materialCount; i++)
            itemNames[i] = materials[i] == null ? $"Material_{i}" : materials[i].name;
    }

    /// <summary>
    /// Adds one dug voxel to the slot matching its material-array index.
    /// </summary>
    public void Add(int materialIndex)
    {
        if (materialIndex < 0 || materialIndex >= itemCounts.Length)
            return;

        itemCounts[materialIndex]++;
        Debug.Log($"Inventory: {itemNames[materialIndex]} = {itemCounts[materialIndex]}");
    }

    /// <summary>
    /// Returns the current count for a material-array index.
    /// </summary>
    public int GetCount(int materialIndex)
    {
        if (materialIndex < 0 || materialIndex >= itemCounts.Length)
            return 0;

        return itemCounts[materialIndex];
    }

    /// <summary>
    /// Returns the material name assigned to an inventory slot.
    /// </summary>
    public string GetItemName(int materialIndex)
    {
        if (materialIndex < 0 || materialIndex >= itemNames.Length)
            return string.Empty;

        return itemNames[materialIndex];
    }

    /// <summary>
    /// Returns a copy of the current inventory counts.
    /// </summary>
    public int[] GetCounts()
    {
        return (int[])itemCounts.Clone();
    }
}
