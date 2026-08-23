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
    private int selectedSlotIndex = -1;

    public event Action InventoryChanged;
    public event Action<int> SelectionChanged;

    public bool IsInitialized => isInitialized;
    public int SelectedSlotIndex => selectedSlotIndex;
    public bool HasSelectedSlot => isInitialized && IsValidHotbarIndex(selectedSlotIndex) && hotbarSlots[selectedSlotIndex] != null && !hotbarSlots[selectedSlotIndex].IsEmpty;

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
    public void Initialize(Material[] materials, PlaceableItemAsset[] placeableItems = null)
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
            PopulateMaterialSlot(i, materials[i], FindPlaceableItem(placeableItems, materials[i]));

        selectedSlotIndex = -1;
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
        string itemId = definition.ItemId;
        InventorySlotData existingSlot = FindSlotByItemId(itemId);
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

        targetList[targetIndex] = new InventorySlotData(itemId, definition.DisplayName, 1, definition.Icon);
        Debug.Log($"Inventory: {definition.DisplayName} = 1");
        NotifyInventoryChanged();
    }

    /// <summary>
    /// Adds a generic item to the first available panel slot, merging matching identifiers.
    /// </summary>
    public bool TryAddAdditionalItem(string itemId, string displayName, int amount, Texture2D icon)
    {
        if (!CanAcceptGenericItem(itemId, amount))
            return false;

        EnsureAdditionalSlots();
        string normalizedId = itemId.Trim();
        InventorySlotData existingSlot = FindSlotByItemId(additionalSlots, normalizedId);
        if (existingSlot != null)
        {
            if (!CanIncreaseCount(existingSlot.Count, amount))
                return false;

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
    /// Returns the total count of an item across the hotbar and additional inventory.
    /// </summary>
    public int GetItemCount(string itemId)
    {
        if (!isInitialized || string.IsNullOrWhiteSpace(itemId))
            return 0;

        EnsureHotbarSlots();
        EnsureAdditionalSlots();
        string normalizedId = itemId.Trim();
        long total = (long)GetItemCount(hotbarSlots, normalizedId) + GetItemCount(additionalSlots, normalizedId);
        return total >= int.MaxValue ? int.MaxValue : (int)total;
    }

    /// <summary>
    /// Returns whether an item can be stored without consuming any existing items.
    /// </summary>
    public bool CanStoreItem(string itemId, int amount)
    {
        if (!CanAcceptGenericItem(itemId, amount))
            return false;

        EnsureHotbarSlots();
        EnsureAdditionalSlots();
        string normalizedId = itemId.Trim();
        InventorySlotData existingSlot = FindSlotByItemId(normalizedId);
        if (existingSlot != null)
            return CanIncreaseCount(existingSlot.Count, amount);

        return FindEmptySlotIndex(hotbarSlots) >= 0 || FindEmptySlotIndex(additionalSlots) >= 0;
    }

    /// <summary>
    /// Returns whether an item can fit after the supplied recipe consumes its ingredients.
    /// </summary>
    public bool CanStoreItem(string itemId, int amount, CraftingRecipe pendingRecipe)
    {
        if (pendingRecipe == null || !pendingRecipe.IsValid())
            return CanStoreItem(itemId, amount);

        List<string> ingredientIds = new List<string>();
        List<int> ingredientAmounts = new List<int>();
        for (int i = 0; i < pendingRecipe.Ingredients.Count; i++)
        {
            CraftingIngredient ingredient = pendingRecipe.Ingredients[i];
            if (ingredient == null || !ingredient.IsValid())
                return false;

            int existingIndex = ingredientIds.IndexOf(ingredient.ItemId);
            if (existingIndex < 0)
            {
                ingredientIds.Add(ingredient.ItemId);
                ingredientAmounts.Add(ingredient.Amount);
            }
            else
            {
                long aggregate = (long)ingredientAmounts[existingIndex] + ingredient.Amount;
                if (aggregate > int.MaxValue)
                    return false;
                ingredientAmounts[existingIndex] = (int)aggregate;
            }
        }

        for (int i = 0; i < ingredientIds.Count; i++)
        {
            if (GetItemCount(ingredientIds[i]) < ingredientAmounts[i])
                return false;
        }

        return CanStoreItemAfterConsumption(itemId, amount, ingredientIds, ingredientAmounts);
    }

    /// <summary>
    /// Merges or inserts a generic item, preferring hotbar slots for new items.
    /// </summary>
    public bool TryAddItem(string itemId, string displayName, int amount, Texture2D icon)
    {
        if (!CanAcceptGenericItem(itemId, amount))
            return false;

        EnsureHotbarSlots();
        EnsureAdditionalSlots();
        string normalizedId = itemId.Trim();
        InventorySlotData existingSlot = FindSlotByItemId(normalizedId);
        if (existingSlot != null)
        {
            if (!CanIncreaseCount(existingSlot.Count, amount))
                return false;

            existingSlot.SetCount(existingSlot.Count + amount);
            NotifyInventoryChanged();
            return true;
        }

        List<InventorySlotData> targetList = hotbarSlots;
        int emptyIndex = FindEmptySlotIndex(hotbarSlots);
        if (emptyIndex < 0)
        {
            targetList = additionalSlots;
            emptyIndex = FindEmptySlotIndex(additionalSlots);
        }

        if (emptyIndex < 0)
            return false;

        targetList[emptyIndex] = new InventorySlotData(normalizedId, displayName, amount, icon);
        NotifyInventoryChanged();
        return true;
    }

    /// <summary>
    /// Consumes all recipe ingredients and inserts the output as one atomic inventory change.
    /// </summary>
    public bool TryCraft(CraftingRecipe recipe)
    {
        if (!isInitialized || recipe == null || !recipe.IsValid())
            return false;

        EnsureHotbarSlots();
        EnsureAdditionalSlots();
        List<string> ingredientIds = new List<string>();
        List<int> ingredientAmounts = new List<int>();
        for (int i = 0; i < recipe.Ingredients.Count; i++)
        {
            CraftingIngredient ingredient = recipe.Ingredients[i];
            if (ingredient == null || string.IsNullOrWhiteSpace(ingredient.ItemId) || ingredient.Amount <= 0)
                return false;

            string normalizedId = ingredient.ItemId.Trim();
            int existingIndex = ingredientIds.IndexOf(normalizedId);
            if (existingIndex < 0)
            {
                ingredientIds.Add(normalizedId);
                ingredientAmounts.Add(ingredient.Amount);
            }
            else
            {
                long aggregate = (long)ingredientAmounts[existingIndex] + ingredient.Amount;
                if (aggregate > int.MaxValue)
                    return false;
                ingredientAmounts[existingIndex] = (int)aggregate;
            }
        }

        for (int i = 0; i < ingredientIds.Count; i++)
        {
            if (GetItemCount(ingredientIds[i]) < ingredientAmounts[i])
                return false;
        }

        if (!CanStoreItemAfterConsumption(recipe.OutputItemId, recipe.OutputAmount, ingredientIds, ingredientAmounts))
            return false;

        for (int i = 0; i < ingredientIds.Count; i++)
            ConsumeItem(ingredientIds[i], ingredientAmounts[i]);

        if (!TryAddItemWithoutNotify(recipe.OutputItemId, recipe.OutputDisplayName, recipe.OutputAmount, recipe.OutputIcon))
            return false;

        NotifyInventoryChanged();
        return true;
    }

    /// <summary>
    /// Selects an occupied hotbar slot, or toggles the current slot off when selected again.
    /// </summary>
    public void SelectSlot(int index)
    {
        if (!isInitialized || !IsValidHotbarIndex(index))
            return;

        InventorySlotData slot = hotbarSlots[index];
        if (slot == null || slot.IsEmpty)
            return;

        if (selectedSlotIndex == index)
        {
            selectedSlotIndex = -1;
            SelectionChanged?.Invoke(selectedSlotIndex);
            return;
        }

        selectedSlotIndex = index;
        SelectionChanged?.Invoke(selectedSlotIndex);
    }

    /// <summary>
    /// Resolves the selected occupied hotbar item to its configured material-array index.
    /// </summary>
    public bool TryGetSelectedMaterialIndex(out int materialIndex)
    {
        materialIndex = -1;
        if (!HasSelectedSlot || materialDefinitions == null || materialDefinitions.Count != materialCount)
            return false;

        InventorySlotData selectedSlot = hotbarSlots[selectedSlotIndex];
        string selectedItemId = selectedSlot.ItemId == null ? string.Empty : selectedSlot.ItemId.Trim();
        if (string.IsNullOrEmpty(selectedItemId))
            return false;

        for (int i = 0; i < materialDefinitions.Count; i++)
        {
            InventorySlotData definition = materialDefinitions[i];
            if (definition == null || string.IsNullOrEmpty(definition.ItemId))
                continue;

            if (string.Equals(definition.ItemId.Trim(), selectedItemId, StringComparison.OrdinalIgnoreCase))
            {
                materialIndex = i;
                return materialIndex >= 0 && materialIndex < materialCount;
            }
        }

        return false;
    }

    
    /// <summary>
    /// Consumes exactly one item from the selected material slot after resolving its material index.
    /// </summary>
    public bool TryConsumeSelectedMaterial(out int materialIndex)
    {
        materialIndex = -1;
        if (!TryGetSelectedMaterialIndex(out materialIndex))
            return false;

        InventorySlotData selectedSlot = hotbarSlots[selectedSlotIndex];
        if (selectedSlot == null || selectedSlot.IsEmpty || selectedSlot.Count <= 0)
        {
            materialIndex = -1;
            return false;
        }

        selectedSlot.SetCount(selectedSlot.Count - 1);
        bool selectionCleared = selectedSlot.Count == 0;
        if (selectionCleared)
        {
            selectedSlot.Clear();
            selectedSlotIndex = -1;
        }

        NotifyInventoryChanged();
        if (selectionCleared)
            SelectionChanged?.Invoke(selectedSlotIndex);
        return true;
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

    /// <summary>
    /// Restores the ordered hotbar and inventory slots, including the selected slot, from a save file.
    /// </summary>
    public bool RestoreState(IList<InventorySlotSaveState> hotbarState, IList<InventorySlotSaveState> additionalState, int savedSelectedSlotIndex)
    {
        if (!isInitialized || hotbarState == null || additionalState == null || hotbarState.Count != HotbarSlotCount || additionalState.Count != AdditionalSlotCount)
            return false;

        EnsureHotbarSlots();
        EnsureAdditionalSlots();
        for (int i = 0; i < HotbarSlotCount; i++)
            hotbarSlots[i] = CreateSlotFromSave(hotbarState[i]);
        for (int i = 0; i < AdditionalSlotCount; i++)
            additionalSlots[i] = CreateSlotFromSave(additionalState[i]);

        selectedSlotIndex = IsValidHotbarIndex(savedSelectedSlotIndex) && !hotbarSlots[savedSelectedSlotIndex].IsEmpty
            ? savedSelectedSlotIndex
            : -1;
        NotifyInventoryChanged();
        SelectionChanged?.Invoke(selectedSlotIndex);
        return true;
    }

    private InventorySlotData CreateSlotFromSave(InventorySlotSaveState savedSlot)
    {
        if (savedSlot == null || string.IsNullOrWhiteSpace(savedSlot.itemId) || savedSlot.count <= 0)
            return CreateEmptySlot();

        InventorySlotData definition = FindDefinitionByItemId(savedSlot.itemId);
        string displayName = string.IsNullOrWhiteSpace(savedSlot.displayName)
            ? definition == null ? savedSlot.itemId : definition.DisplayName
            : savedSlot.displayName;
        Texture2D icon = definition == null ? null : definition.Icon;
        return new InventorySlotData(savedSlot.itemId, displayName, savedSlot.count, icon);
    }

    private InventorySlotData FindDefinitionByItemId(string itemId)
    {
        if (materialDefinitions == null || string.IsNullOrWhiteSpace(itemId))
            return null;

        for (int i = 0; i < materialDefinitions.Count; i++)
        {
            InventorySlotData definition = materialDefinitions[i];
            if (definition != null && string.Equals(definition.ItemId, itemId.Trim(), StringComparison.OrdinalIgnoreCase))
                return definition;
        }

        return null;
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

    private void PopulateMaterialSlot(int materialIndex, Material material, PlaceableItemAsset placeableItem)
    {
        if (materialIndex < 0 || materialIndex >= materialDefinitions.Count)
            return;

        materialDefinitions[materialIndex] = placeableItem != null && placeableItem.IsValid
            ? new InventorySlotData(placeableItem.ItemId, placeableItem.DisplayName, 0, placeableItem.Icon)
            : CreateMaterialSlot(materialIndex, material);
    }

    
    private static PlaceableItemAsset FindPlaceableItem(PlaceableItemAsset[] placeableItems, Material material)
    {
        if (placeableItems == null || material == null)
            return null;

        for (int i = 0; i < placeableItems.Length; i++)
        {
            PlaceableItemAsset placeableItem = placeableItems[i];
            if (placeableItem != null && placeableItem.IsValid && placeableItem.MatchesMaterial(material))
                return placeableItem;
        }

        return null;
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
            if (slot != null && !slot.IsEmpty && string.Equals(slot.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
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

    private bool CanAcceptGenericItem(string itemId, int amount)
    {
        return isInitialized && !string.IsNullOrWhiteSpace(itemId) && amount > 0;
    }

    private static bool CanIncreaseCount(int currentCount, int amount)
    {
        return currentCount >= 0 && amount > 0 && (long)currentCount + amount <= int.MaxValue;
    }

    private static int GetItemCount(List<InventorySlotData> slots, string itemId)
    {
        long total = 0;
        if (slots == null)
            return 0;

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlotData slot = slots[i];
            if (slot == null || slot.IsEmpty || !string.Equals(slot.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                continue;

            total += slot.Count;
            if (total >= int.MaxValue)
                return int.MaxValue;
        }

        return (int)total;
    }

    private bool CanStoreItemAfterConsumption(string itemId, int amount, List<string> ingredientIds, List<int> ingredientAmounts)
    {
        if (!CanAcceptGenericItem(itemId, amount))
            return false;

        List<InventorySlotData> allSlots = new List<InventorySlotData>(HotbarSlotCount + AdditionalSlotCount);
        List<int> remainingCounts = new List<int>(HotbarSlotCount + AdditionalSlotCount);
        AddSlotsForSimulation(hotbarSlots, allSlots, remainingCounts);
        AddSlotsForSimulation(additionalSlots, allSlots, remainingCounts);

        for (int ingredientIndex = 0; ingredientIndex < ingredientIds.Count; ingredientIndex++)
        {
            int remainingToConsume = ingredientAmounts[ingredientIndex];
            for (int slotIndex = 0; slotIndex < allSlots.Count && remainingToConsume > 0; slotIndex++)
            {
                InventorySlotData slot = allSlots[slotIndex];
                if (slot == null || !string.Equals(slot.ItemId, ingredientIds[ingredientIndex], StringComparison.OrdinalIgnoreCase))
                    continue;

                int consumed = Mathf.Min(remainingCounts[slotIndex], remainingToConsume);
                remainingCounts[slotIndex] -= consumed;
                remainingToConsume -= consumed;
            }
        }

        string normalizedId = itemId.Trim();
        for (int i = 0; i < allSlots.Count; i++)
        {
            if (remainingCounts[i] > 0 && string.Equals(allSlots[i].ItemId, normalizedId, StringComparison.OrdinalIgnoreCase))
                return CanIncreaseCount(remainingCounts[i], amount);
        }

        for (int i = 0; i < remainingCounts.Count; i++)
        {
            if (remainingCounts[i] <= 0)
                return true;
        }

        return false;
    }

    private static void AddSlotsForSimulation(List<InventorySlotData> slots, List<InventorySlotData> allSlots, List<int> remainingCounts)
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlotData slot = slots[i];
            allSlots.Add(slot);
            remainingCounts.Add(slot == null || slot.IsEmpty ? 0 : slot.Count);
        }
    }

    private void ConsumeItem(string itemId, int amount)
    {
        int consumed = ConsumeItemFromSlots(hotbarSlots, itemId, amount);
        ConsumeItemFromSlots(additionalSlots, itemId, amount - consumed);
    }

    private static int ConsumeItemFromSlots(List<InventorySlotData> slots, string itemId, int amount)
    {
        if (slots == null || amount <= 0)
            return 0;

        int consumedTotal = 0;
        for (int i = 0; i < slots.Count && consumedTotal < amount; i++)
        {
            InventorySlotData slot = slots[i];
            if (slot == null || slot.IsEmpty || !string.Equals(slot.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                continue;

            int consumed = Mathf.Min(slot.Count, amount - consumedTotal);
            slot.SetCount(slot.Count - consumed);
            consumedTotal += consumed;
            if (slot.Count == 0)
                slot.Clear();
        }

        return consumedTotal;
    }

    private bool TryAddItemWithoutNotify(string itemId, string displayName, int amount, Texture2D icon)
    {
        if (!CanAcceptGenericItem(itemId, amount))
            return false;

        string normalizedId = itemId.Trim();
        InventorySlotData existingSlot = FindSlotByItemId(normalizedId);
        if (existingSlot != null)
        {
            if (!CanIncreaseCount(existingSlot.Count, amount))
                return false;

            existingSlot.SetCount(existingSlot.Count + amount);
            return true;
        }

        List<InventorySlotData> targetList = hotbarSlots;
        int emptyIndex = FindEmptySlotIndex(hotbarSlots);
        if (emptyIndex < 0)
        {
            targetList = additionalSlots;
            emptyIndex = FindEmptySlotIndex(additionalSlots);
        }

        if (emptyIndex < 0)
            return false;

        targetList[emptyIndex] = new InventorySlotData(normalizedId, displayName, amount, icon);
        return true;
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
