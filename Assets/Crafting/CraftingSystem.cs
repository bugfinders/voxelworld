using System;
using System.Collections.Generic;
using UnityEngine;

public class CraftingSystem : MonoBehaviour
{
    private readonly List<CraftingRecipe> allRecipes = new List<CraftingRecipe>();
    private readonly List<CraftingRecipe> activeRecipes = new List<CraftingRecipe>();

    private VoxelInventory inventory;
    private CraftingStationType activeStation = CraftingStationType.Inventory;
    private bool hasWorkbenchAccess;
    private bool catalogInitialized;
    private bool dependencyWarningLogged;

    public IReadOnlyList<CraftingRecipe> Recipes => activeRecipes;
    public CraftingStationType ActiveStation => activeStation;
    public bool HasWorkbenchAccess => hasWorkbenchAccess;
    public event Action CraftingChanged;

    private void Awake()
    {
        InitializeCatalog();
    }

    private void OnEnable()
    {
        InitializeCatalog();
        RefreshState();
    }

    private void Update()
    {
        if (inventory == null || !inventory.IsInitialized)
            RefreshState();
    }

    private void OnDisable()
    {
        UnsubscribeFromInventory();
    }

    /// <summary>
    /// Changes the selected crafting station when the station is available.
    /// </summary>
    public void SetStation(CraftingStationType station)
    {
        if (station != CraftingStationType.Inventory && station != CraftingStationType.Workbench)
            return;
        if (station == CraftingStationType.Workbench && !hasWorkbenchAccess)
            return;
        if (activeStation == station)
            return;

        activeStation = station;
        RebuildActiveRecipes();
        CraftingChanged?.Invoke();
    }

    /// <summary>
    /// Returns whether the requested active-station recipe can currently be crafted.
    /// </summary>
    public bool CanCraft(string recipeId)
    {
        CraftingRecipe recipe = FindActiveRecipe(recipeId);
        if (recipe == null || inventory == null || !inventory.IsInitialized || !recipe.IsValid())
            return false;
        if (recipe.RequiredStation != activeStation)
            return false;
        if (recipe.RequiredStation == CraftingStationType.Workbench && !hasWorkbenchAccess)
            return false;

        for (int i = 0; i < recipe.Ingredients.Count; i++)
        {
            CraftingIngredient ingredient = recipe.Ingredients[i];
            if (ingredient == null || inventory.GetItemCount(ingredient.ItemId) < ingredient.Amount)
                return false;
        }

        return inventory.CanStoreItem(recipe.OutputItemId, recipe.OutputAmount, recipe);
    }

    /// <summary>
    /// Attempts one atomic craft using the active crafting station.
    /// </summary>
    public bool TryCraft(string recipeId)
    {
        CraftingRecipe recipe = FindActiveRecipe(recipeId);
        if (recipe == null || !CanCraft(recipeId))
            return false;

        return inventory.TryCraft(recipe);
    }

    /// <summary>
    /// Resolves the terrain-owned inventory and refreshes station access and active recipes.
    /// </summary>
    public void RefreshState()
    {
        bool stateChanged = RefreshStateInternal();
        if (stateChanged)
            CraftingChanged?.Invoke();
    }

    private bool RefreshStateInternal()
    {
        InitializeCatalog();
        ResolveInventory();

        bool previousAccess = hasWorkbenchAccess;
        CraftingStationType previousStation = activeStation;
        int previousRecipeCount = activeRecipes.Count;
        hasWorkbenchAccess = inventory != null && inventory.IsInitialized && inventory.GetItemCount("workbench") > 0;
        if (!hasWorkbenchAccess && activeStation == CraftingStationType.Workbench)
            activeStation = CraftingStationType.Inventory;

        RebuildActiveRecipes();
        return previousAccess != hasWorkbenchAccess || previousStation != activeStation || previousRecipeCount != activeRecipes.Count;
    }

    private void InitializeCatalog()
    {
        if (catalogInitialized)
            return;

        catalogInitialized = true;
        allRecipes.Clear();
        allRecipes.Add(new CraftingRecipe(
            "stool",
            "stool",
            "Stool",
            1,
            CraftingStationType.Inventory,
            new CraftingIngredient("Wood", 2)));
        allRecipes.Add(new CraftingRecipe(
            "workbench",
            "workbench",
            "Workbench",
            1,
            CraftingStationType.Inventory,
            new CraftingIngredient("Wood", 4)));
        allRecipes.Add(new CraftingRecipe(
            "chest",
            "chest",
            "Chest",
            1,
            CraftingStationType.Workbench,
            new CraftingIngredient("Wood", 6)));
        allRecipes.Add(new CraftingRecipe(
            "furnace",
            "furnace",
            "Furnace",
            1,
            CraftingStationType.Workbench,
            new CraftingIngredient("Stone", 8)));

        HashSet<string> recipeIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < allRecipes.Count; i++)
        {
            CraftingRecipe recipe = allRecipes[i];
            if (recipe == null || !recipe.IsValid() || !recipeIds.Add(recipe.RecipeId))
                Debug.LogWarning("Crafting catalog contains an invalid or duplicate recipe.");
        }
    }

    private void ResolveInventory()
    {
        ChunkedVoxelTerrain terrain = UnityEngine.Object.FindFirstObjectByType<ChunkedVoxelTerrain>();
        VoxelInventory nextInventory = terrain == null ? null : terrain.Inventory;
        if (nextInventory == inventory)
            return;

        UnsubscribeFromInventory();
        inventory = nextInventory;
        if (inventory != null)
        {
            inventory.InventoryChanged += HandleInventoryChanged;
            return;
        }

        if (!dependencyWarningLogged)
        {
            Debug.LogWarning("CraftingSystem could not resolve the terrain-owned VoxelInventory.");
            dependencyWarningLogged = true;
        }
    }

    private void UnsubscribeFromInventory()
    {
        if (inventory == null)
            return;

        inventory.InventoryChanged -= HandleInventoryChanged;
        inventory = null;
    }

    private void HandleInventoryChanged()
    {
        bool stateChanged = RefreshStateInternal();
        if (!stateChanged)
            CraftingChanged?.Invoke();
    }

    private void RebuildActiveRecipes()
    {
        activeRecipes.Clear();
        for (int i = 0; i < allRecipes.Count; i++)
        {
            CraftingRecipe recipe = allRecipes[i];
            if (recipe != null && recipe.IsValid() && recipe.RequiredStation == activeStation)
                activeRecipes.Add(recipe);
        }
    }

    private CraftingRecipe FindActiveRecipe(string recipeId)
    {
        if (string.IsNullOrWhiteSpace(recipeId))
            return null;

        string normalizedId = recipeId.Trim();
        for (int i = 0; i < activeRecipes.Count; i++)
        {
            if (string.Equals(activeRecipes[i].RecipeId, normalizedId, StringComparison.Ordinal))
                return activeRecipes[i];
        }

        return null;
    }
}
