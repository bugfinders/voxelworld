using System;
using UnityEngine;

[Serializable]
public class InventorySlotData
{
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private int count;
    [SerializeField] private Texture2D icon;
    [SerializeField] private string itemKind;

    /// <summary>
    /// Creates a normalized inventory slot entry.
    /// </summary>
    public InventorySlotData(string itemId, string displayName, int count, Texture2D icon, string itemKind = "Item")
    {
        this.itemId = NormalizeItemId(itemId);
        this.displayName = NormalizeDisplayName(displayName, this.itemId);
        this.count = Mathf.Max(0, count);
        this.icon = icon;
        this.itemKind = NormalizeItemKind(itemKind);
    }

    /// <summary>
    /// Gets the stable identifier used to merge matching items.
    /// </summary>
    public string ItemId => itemId;

    /// <summary>
    /// Gets the name displayed when the slot has no icon.
    /// </summary>
    public string DisplayName => displayName;

    /// <summary>
    /// Gets the broad category shown by the inventory tooltip.
    /// </summary>
    public string ItemKind => string.IsNullOrWhiteSpace(itemKind) ? "Item" : itemKind;

    /// <summary>
    /// Gets the non-negative item count.
    /// </summary>
    public int Count => count;

    public Texture2D Icon => icon;

    /// <summary>
    /// Gets whether the slot has no usable item identifier or count.
    /// </summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(itemId) || count <= 0;

    internal void Clear()
    {
        itemId = string.Empty;
        displayName = string.Empty;
        count = 0;
        itemKind = "Item";
    }

    internal void SetCount(int value)
    {
        count = Mathf.Max(0, value);
    }

    internal void Normalize()
    {
        itemId = NormalizeItemId(itemId);
        displayName = NormalizeDisplayName(displayName, itemId);
        itemKind = NormalizeItemKind(itemKind);
        if (string.IsNullOrEmpty(itemId) || count == 0)
            Clear();
    }

    private static string NormalizeItemId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeDisplayName(string value, string normalizedItemId)
    {
        return string.IsNullOrWhiteSpace(value) ? normalizedItemId : value.Trim();
    }

    private static string NormalizeItemKind(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Item" : value.Trim();
    }
}
