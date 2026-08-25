using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CubeitsCoreRegressionTests
{
    private readonly List<GameObject> objectsToDestroy = new List<GameObject>();
    private readonly List<UnityEngine.Object> assetsToDestroy = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objectsToDestroy.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(objectsToDestroy[i]);
        objectsToDestroy.Clear();

        for (int i = assetsToDestroy.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(assetsToDestroy[i]);
        assetsToDestroy.Clear();
    }

    [Test]
    public void InventorySlotData_NormalizesIdentifiersDisplayNamesAndCounts()
    {
        InventorySlotData slot = new InventorySlotData("  Wood  ", "  ", -5, null, "  Resource ");

        Assert.AreEqual("Wood", slot.ItemId);
        Assert.AreEqual("Wood", slot.DisplayName);
        Assert.AreEqual(0, slot.Count);
        Assert.AreEqual("Resource", slot.ItemKind);
        Assert.IsTrue(slot.IsEmpty);
    }

    [Test]
    public void CraftingRecipe_RejectsDuplicateIngredientsAndBasicRecipesOverFourItems()
    {
        CraftingRecipe duplicateRecipe = new CraftingRecipe(
            "duplicate",
            "Output",
            "Output",
            1,
            CraftingRecipeType.Basic,
            new CraftingIngredient("Wood", 1),
            new CraftingIngredient(" wood ", 1));
        CraftingRecipe oversizedBasicRecipe = new CraftingRecipe(
            "oversized-basic",
            "Output",
            "Output",
            1,
            CraftingRecipeType.Basic,
            new CraftingIngredient("Wood", 5));

        Assert.IsFalse(duplicateRecipe.IsValid());
        Assert.IsFalse(oversizedBasicRecipe.IsValid());
    }

    [Test]
    public void CraftingRecipe_AllowsNineIngredientsForStationRecipes()
    {
        CraftingIngredient[] ingredients = new CraftingIngredient[9];
        for (int i = 0; i < ingredients.Length; i++)
            ingredients[i] = new CraftingIngredient($"Item_{i}", 1);

        CraftingRecipe recipe = new CraftingRecipe(
            "station-recipe",
            "Output",
            "Output",
            1,
            CraftingRecipeType.Workstation,
            ingredients);

        Assert.IsTrue(recipe.IsValid());
        Assert.AreEqual(9, recipe.TotalIngredientCount);
    }

    [Test]
    public void PlaceableItemAsset_MatchesRuntimeClonedMaterialByName()
    {
        Shader shader = Shader.Find("Hidden/InternalErrorShader");
        Assert.IsNotNull(shader, "The test shader is required to create a runtime material clone.");

        Material sourceMaterial = new Material(shader);
        sourceMaterial.name = "Workbench";
        Material runtimeMaterialClone = new Material(sourceMaterial);
        runtimeMaterialClone.name = sourceMaterial.name;
        PlaceableItemAsset workbench = ScriptableObject.CreateInstance<PlaceableItemAsset>();
        FieldInfo voxelMaterialField = typeof(PlaceableItemAsset).GetField("voxelMaterial", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(voxelMaterialField);
        voxelMaterialField.SetValue(workbench, sourceMaterial);
        assetsToDestroy.Add(workbench);
        assetsToDestroy.Add(runtimeMaterialClone);
        assetsToDestroy.Add(sourceMaterial);

        Assert.IsTrue(workbench.MatchesMaterial(sourceMaterial));
        Assert.IsTrue(workbench.MatchesMaterial(runtimeMaterialClone));
    }

    [Test]
    public void VoxelInventory_InitializesFixedSlotCountsAndMergesItemIdsCaseInsensitively()
    {
        VoxelInventory inventory = CreateInventory();

        Assert.IsTrue(inventory.IsInitialized);
        Assert.AreEqual(VoxelInventory.HotbarSlotCount, inventory.HotbarSlots.Count);
        Assert.AreEqual(VoxelInventory.AdditionalSlotCount, inventory.AdditionalSlots.Count);
        Assert.IsTrue(inventory.TryAddItem(" Wood ", "Wood", 2, null));
        Assert.IsTrue(inventory.TryAddItem("wood", "Wood", 3, null));

        Assert.AreEqual(5, inventory.GetItemCount("WOOD"));
        Assert.AreEqual("Wood", inventory.GetHotbarSlot(0).ItemId);
        Assert.AreEqual(5, inventory.GetHotbarSlot(0).Count);
    }

    [Test]
    public void VoxelInventory_TryMoveItemSwapsWithEmptySlotAndRejectsInvalidTransfers()
    {
        VoxelInventory inventory = CreateInventory();
        inventory.TryAddItem("Wood", "Wood", 2, null);

        Assert.IsFalse(inventory.TryMoveItem(true, 0, true, 0));
        Assert.IsFalse(inventory.TryMoveItem(true, -1, false, 0));
        Assert.IsTrue(inventory.TryMoveItem(true, 0, false, 0));

        Assert.IsTrue(inventory.GetHotbarSlot(0).IsEmpty);
        Assert.AreEqual("Wood", inventory.AdditionalSlots[0].ItemId);
        Assert.AreEqual(2, inventory.AdditionalSlots[0].Count);
    }

    [Test]
    public void VoxelInventory_TryCraftConsumesIngredientsAndAddsOutputAtomically()
    {
        VoxelInventory inventory = CreateInventory();
        inventory.TryAddItem("Wood", "Wood", 4, null);
        CraftingRecipe recipe = new CraftingRecipe(
            "wood-to-output",
            "Workbench",
            "Workbench",
            1,
            CraftingRecipeType.Basic,
            new CraftingIngredient(" wood ", 4));

        Assert.IsTrue(inventory.TryCraft(recipe));
        Assert.AreEqual(0, inventory.GetItemCount("Wood"));
        Assert.AreEqual(1, inventory.GetItemCount("workbench"));

        Assert.IsFalse(inventory.TryCraft(recipe));
        Assert.AreEqual(1, inventory.GetItemCount("Workbench"));
    }

    [Test]
    public void VoxelInventory_SelectedMaterialCanBeResolvedAndConsumed()
    {
        VoxelInventory inventory = CreateInventory(new Material[] { null });
        inventory.Add(0);
        inventory.SelectSlot(0);

        int materialIndex;
        Assert.IsTrue(inventory.TryGetSelectedMaterialIndex(out materialIndex));
        Assert.AreEqual(0, materialIndex);
        Assert.IsTrue(inventory.TryConsumeSelectedMaterial(out materialIndex));
        Assert.AreEqual(0, materialIndex);
        Assert.AreEqual(-1, inventory.SelectedSlotIndex);
        Assert.AreEqual(0, inventory.GetCount(0));
    }

    [Test]
    public void ChestInventory_MergesPlayerItemsAndRejectsInvalidState()
    {
        VoxelInventory inventory = CreateInventory();
        ChestInventory chest = new ChestInventory(Vector3Int.zero);
        List<InventorySlotSaveState> savedState = CreateEmptyChestState();
        savedState[0] = new InventorySlotSaveState("wood", "Wood", 3);

        Assert.IsFalse(chest.RestoreState(savedState.GetRange(0, ChestInventory.SlotCount - 1), inventory));
        Assert.IsTrue(chest.RestoreState(savedState, inventory));
        inventory.TryAddItem("Wood", "Wood", 2, null);

        Assert.IsTrue(inventory.TryMoveItemToChest(chest, true, 0, 0));
        Assert.AreEqual(5, chest.GetSlot(0).Count);
        Assert.IsTrue(inventory.GetHotbarSlot(0).IsEmpty);
        Assert.IsFalse(chest.TryMoveItem(-1, 0));
    }

    [Test]
    public void VoxelInventory_RestoreStateOnlySelectsAnOccupiedHotbarSlot()
    {
        VoxelInventory inventory = CreateInventory();
        List<InventorySlotSaveState> hotbarState = new List<InventorySlotSaveState>();
        for (int i = 0; i < VoxelInventory.HotbarSlotCount; i++)
            hotbarState.Add(new InventorySlotSaveState());
        hotbarState[2] = new InventorySlotSaveState("Wood", "Wood", 1);
        List<InventorySlotSaveState> additionalState = new List<InventorySlotSaveState>();
        for (int i = 0; i < VoxelInventory.AdditionalSlotCount; i++)
            additionalState.Add(new InventorySlotSaveState());

        Assert.IsTrue(inventory.RestoreState(hotbarState, additionalState, 2));
        Assert.AreEqual(2, inventory.SelectedSlotIndex);
        Assert.IsTrue(inventory.RestoreState(hotbarState, additionalState, 4));
        Assert.AreEqual(-1, inventory.SelectedSlotIndex);
    }

    private VoxelInventory CreateInventory(Material[] materials = null)
    {
        GameObject inventoryObject = new GameObject("TestInventory");
        objectsToDestroy.Add(inventoryObject);
        VoxelInventory inventory = inventoryObject.AddComponent<VoxelInventory>();
        inventory.Initialize(materials);
        return inventory;
    }

    private static List<InventorySlotSaveState> CreateEmptyChestState()
    {
        List<InventorySlotSaveState> state = new List<InventorySlotSaveState>();
        for (int i = 0; i < ChestInventory.SlotCount; i++)
            state.Add(new InventorySlotSaveState());
        return state;
    }
}
