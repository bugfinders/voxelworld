using System;
using System.Collections.Generic;
using UnityEngine;

public class VoxelInventory : MonoBehaviour
{
    public const int HotbarSlotCount = 10;
    public const int AdditionalSlotCount = 20;

    [SerializeField] private List<InventorySlotData> hotbarSlots = new List<InventorySlotData>();
    [SerializeField] private List<InventorySlotData> additionalSlots = new List<InventorySlotData>();

    private readonly List<InventorySlotData> materialDefinitions = new List<InventorySlotData>();

    private int materialCount;
    private bool isInitialized;
    private int selectedSlotIndex;

    public event Action InventoryChanged;
    public event Action<int> SelectionChanged;

    public bool IsInitialized => isInitialized;
    public int SelectedSlotIndex => selectedSlotIndex;

    public IReadOnlyList<InventorySlotData> HotbarSlots
    {
        get
        {
            EnsureHotbarSlots();
            return hotbarSlots;
        }
    }

    public IReadOnlyList<InventorySlotData> AdditionalSlots
    {
        get
        {
            EnsureAdditionalSlots();
            return additionalSlots;
        }
    }

    /// <summary>
    /// Initializes the ten-slot hotbar and twenty-slot inventory panel from terrain materials.
    /// </summary>
    public void Initialize(Material[] materials)
    {
        isInitialized = false;
        EnsureHotbarSlots();
        EnsureAdditionalSlots();

        for (int i = 0; i < hotbarSlots.Count; i++)
            hotbarSlots[i] = CreateEmptySlot();
        for (int i = 0; i < additionalSlots.Count; i++)
            additionalSlots[i] = CreateEmptySlot();

        materialDefinitions.Clear();
        materialCount = materials == null ? 0 : materials.Length;
        for (int i = 0; i < materialCount; i++)
            materialDefinitions.Add(CreateEmptySlot());
        for (int i = 0; i < materialCount; i++)
            PopulateMaterialSlot(i, materials[i]);

        selectedSlotIndex = 0;
        isInitialized = true;
    }

    /// <summary>
    /// Adds one dug voxel to the slot matching its material-array index.
    /// </summary>
    public void Add(int materialIndex)
    {
        if (!isInitialized || materialIndex < 0 || materialIndex >= materialCount)
            return;

        InventorySlotData definition = materialDefinitions[materialIndex];
        InventorySlotData existingSlot = FindSlotByItemId(definition.ItemId);
        if (existingSlot != null)
        {
            existingSlot.SetCount(existingSlot.Count + 1);
            Debug.Log($"Inventory: {existingSlot.DisplayName} = {existingSlot.Count}");
            NotifyInventoryChanged();
            return;
        }

        List<InventorySlotData> targetList = hotbarSlots;
        int targetIndex = FindEmptySlotIndex(hotbarSlots);
        if (targetIndex < 0)
        {
            targetList = additionalSlots;
            targetIndex = FindEmptySlotIndex(additionalSlots);
        }

        if (targetIndex < 0)
            return;

        targetList[targetIndex] = new InventorySlotData(definition.ItemId, definition.DisplayName, 1, definition.Icon);
        Debug.Log($"Inventory: {definition.DisplayName} = 1");
        NotifyInventoryChanged();
    }

    /// <summary>
    /// Adds a generic item to the first available panel slot, merging matching identifiers.
    /// </summary>
    public bool TryAddAdditionalItem(string itemId, string displayName, int amount, Texture2D icon)
    {
        if (!isInitialized || string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return false;

        EnsureAdditionalSlots();
        string normalizedId = itemId.Trim();
        for (int i = 0; i < additionalSlots.Count; i++)
        {
            InventorySlotData existingSlot = additionalSlots[i];
            if (existingSlot == null || existingSlot.IsEmpty)
                continue;
            if (!string.Equals(existingSlot.ItemId, normalizedId, StringComparison.Ordinal))
                continue;

            existingSlot.SetCount(existingSlot.Count + amount);
            NotifyInventoryChanged();
            return true;
        }

        int emptyIndex = FindEmptySlotIndex(additionalSlots);
        if (emptyIndex < 0)
            return false;

        additionalSlots[emptyIndex] = new InventorySlotData(normalizedId, displayName, amount, icon);
        NotifyInventoryChanged();
        return true;
    }

    /// <summary>
    /// Selects a valid hotbar slot and notifies listeners when the selection changes.
    /// </summary>
    public void SelectSlot(int index)
    {
        if (!isInitialized || !IsValidHotbarIndex(index) || selectedSlotIndex == index)
            return;

        selectedSlotIndex = index;
        SelectionChanged?.Invoke(selectedSlotIndex);
    }

    /// <summary>
    /// Returns the requested hotbar slot or null for an invalid index.
    /// </summary>
    public InventorySlotData GetHotbarSlot(int index)
    {
        return !IsValidHotbarIndex(index) ? null : hotbarSlots[index];
    }

    /// <summary>
    /// Returns the current count for a material-array index.
    /// </summary>
    public int GetCount(int materialIndex)
    {
        if (!isInitialized || materialIndex < 0 || materialIndex >= materialCount)
            return 0;

        InventorySlotData slot = GetMaterialSlot(materialIndex);
        return slot == null ? 0 : slot.Count;
    }

    /// <summary>
    /// Returns the material name assigned to an inventory slot.
    /// </summary>
    public string GetItemName(int materialIndex)
    {
        if (!isInitialized || materialIndex < 0 || materialIndex >= materialCount)
            return string.Empty;

        return materialDefinitions[materialIndex].DisplayName;
    }

    /// <summary>
    /// Returns a copy of the current material-index inventory counts.
    /// </summary>
    public int[] GetCounts()
    {
        int[] counts = new int[materialCount];
        if (!isInitialized)
            return counts;

        for (int i = 0; i < counts.Length; i++)
            counts[i] = GetCount(i);
        return counts;
    }

    private void EnsureHotbarSlots()
    {
        if (hotbarSlots == null)
            hotbarSlots = new List<InventorySlotData>();

        while (hotbarSlots.Count < HotbarSlotCount)
            hotbarSlots.Add(CreateEmptySlot());
        if (hotbarSlots.Count > HotbarSlotCount)
            hotbarSlots.RemoveRange(HotbarSlotCount, hotbarSlots.Count - HotbarSlotCount);

        for (int i = 0; i < hotbarSlots.Count; i++)
            hotbarSlots[i] = NormalizeSlot(hotbarSlots[i]);
    }

    private void EnsureAdditionalSlots()
    {
        if (additionalSlots == null)
            additionalSlots = new List<InventorySlotData>();

        while (additionalSlots.Count < AdditionalSlotCount)
            additionalSlots.Add(CreateEmptySlot());
        if (additionalSlots.Count > AdditionalSlotCount)
            additionalSlots.RemoveRange(AdditionalSlotCount, additionalSlots.Count - AdditionalSlotCount);

        for (int i = 0; i < additionalSlots.Count; i++)
            additionalSlots[i] = NormalizeSlot(additionalSlots[i]);
    }

    private void PopulateMaterialSlot(int materialIndex, Material material)
    {
        if (materialIndex < 0 || materialIndex >= materialDefinitions.Count)
            return;

        materialDefinitions[materialIndex] = CreateMaterialSlot(materialIndex, material);
    }

    private InventorySlotData CreateMaterialSlot(int materialIndex, Material material)
    {
        string itemId = material == null ? $"material_{materialIndex}" : material.name;
        string displayName = material == null ? $"Material_{materialIndex}" : material.name;
        Texture2D icon = null;
        if (material != null)
        {
            icon = material.GetTexture("_BaseMap") as Texture2D;
            if (icon == null)
                icon = material.GetTexture("_MainTex") as Texture2D;
        }

        return new InventorySlotData(itemId, displayName, 0, icon);
    }

    private InventorySlotData GetMaterialSlot(int materialIndex)
    {
        if (!isInitialized || materialIndex < 0 || materialIndex >= materialDefinitions.Count)
            return null;

        return FindSlotByItemId(materialDefinitions[materialIndex].ItemId);
    }

    private InventorySlotData FindSlotByItemId(string itemId)
    {
        InventorySlotData slot = FindSlotByItemId(hotbarSlots, itemId);
        return slot ?? FindSlotByItemId(additionalSlots, itemId);
    }

    private static InventorySlotData FindSlotByItemId(List<InventorySlotData> slots, string itemId)
    {
        if (slots == null)
            return null;

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlotData slot = slots[i];
            if (slot != null && !slot.IsEmpty && string.Equals(slot.ItemId, itemId, StringComparison.Ordinal))
                return slot;
        }

        return null;
    }

    private static int FindEmptySlotIndex(List<InventorySlotData> slots)
    {
        if (slots == null)
            return -1;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null || slots[i].IsEmpty)
                return i;
        }

        return -1;
    }

    private bool IsValidHotbarIndex(int index)
    {
        return index >= 0 && index < HotbarSlotCount && hotbarSlots != null && index < hotbarSlots.Count;
    }

    private void NotifyInventoryChanged()
    {
        if (isInitialized)
            InventoryChanged?.Invoke();
    }

    private static InventorySlotData CreateEmptySlot()
    {
        return new InventorySlotData(string.Empty, string.Empty, 0, null);
    }

    private static InventorySlotData NormalizeSlot(InventorySlotData slot)
    {
        if (slot == null)
            return CreateEmptySlot();

        slot.Normalize();
        return slot;
    }
}
