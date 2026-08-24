using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ChestInventory
{
    public const int SlotCount = 27;

    private readonly List<InventorySlotData> slots = new List<InventorySlotData>(SlotCount);

    public ChestInventory(Vector3Int coordinate)
    {
        Coordinate = coordinate;
        EnsureSlots();
    }

    public Vector3Int Coordinate { get; }
    public IReadOnlyList<InventorySlotData> Slots => slots;

    /// <summary>
    /// Returns a slot by index, or null when the index is outside the chest.
    /// </summary>
    public InventorySlotData GetSlot(int index)
    {
        EnsureSlots();
        return index < 0 || index >= SlotCount ? null : slots[index];
    }

    public bool TryMoveItem(int sourceIndex, int targetIndex)
    {
        if (!IsValidIndex(sourceIndex) || !IsValidIndex(targetIndex) || sourceIndex == targetIndex)
            return false;

        InventorySlotData source = slots[sourceIndex];
        InventorySlotData target = slots[targetIndex];
        if (source == null || source.IsEmpty)
            return false;

        if (CanMerge(target, source))
        {
            if ((long)target.Count + source.Count > int.MaxValue)
                return false;

            target.SetCount(target.Count + source.Count);
            source.Clear();
        }
        else
        {
            slots[sourceIndex] = target ?? CreateEmptySlot();
            slots[targetIndex] = source;
        }

        NotifyChanged();
        return true;
    }

    public bool RestoreState(IList<InventorySlotSaveState> savedState, VoxelInventory definitionSource)
    {
        if (savedState == null || savedState.Count != SlotCount || definitionSource == null)
            return false;

        EnsureSlots();
        for (int i = 0; i < SlotCount; i++)
            slots[i] = definitionSource.CreateSlotFromSaveState(savedState[i]);
        return true;
    }

    internal void SetSlot(int index, InventorySlotData slot)
    {
        if (IsValidIndex(index))
            slots[index] = slot ?? CreateEmptySlot();
    }

    internal void NotifyChanged()
    {
        Changed?.Invoke();
    }

    public event Action Changed;

    private void EnsureSlots()
    {
        while (slots.Count < SlotCount)
            slots.Add(CreateEmptySlot());
        if (slots.Count > SlotCount)
            slots.RemoveRange(SlotCount, slots.Count - SlotCount);

        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == null)
                slots[i] = CreateEmptySlot();
    }

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < SlotCount;
    }

    private static bool CanMerge(InventorySlotData target, InventorySlotData source)
    {
        return target != null && !target.IsEmpty && source != null && !source.IsEmpty &&
               string.Equals(target.ItemId, source.ItemId, StringComparison.OrdinalIgnoreCase);
    }

    private static InventorySlotData CreateEmptySlot()
    {
        return new InventorySlotData(string.Empty, string.Empty, 0, null, "Item");
    }
}
