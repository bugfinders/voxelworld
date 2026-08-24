using System;

[Serializable]
public sealed class InventorySlotSaveState
{
    public string itemId;
    public string displayName;
    public int count;

    public InventorySlotSaveState()
    {
        itemId = string.Empty;
        displayName = string.Empty;
        count = 0;
    }

    public InventorySlotSaveState(string itemId, string displayName, int count)
    {
        this.itemId = itemId ?? string.Empty;
        this.displayName = displayName ?? string.Empty;
        this.count = count < 0 ? 0 : count;
    }
}
