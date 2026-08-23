using System;
using System.Collections.Generic;
using UnityEngine;

public class CraftingSystem : MonoBehaviour
{
    [SerializeField] private CraftingRecipeAsset[] recipeAssets = new CraftingRecipeAsset[0];
    [SerializeField] private PlaceableItemAsset workbenchItem;

    private readonly List<CraftingRecipe> allRecipes = new List<CraftingRecipe>();
    private readonly List<CraftingRecipe> activeRecipes = new List<CraftingRecipe>();

    private VoxelInventory inventory;
    private CraftingStationType activeStation = CraftingStationType.Inventory;
    private bool stationInUse;
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
    /// Opens the crafting station represented by a placed item definition.
    /// </summary>
    public void BeginStationUse(PlaceableItemAsset stationItem)
    {
        if (stationItem == null || !stationItem.IsValid || !stationItem.OpensCraftingMenu)
            return;

        stationInUse = true;
        RefreshState();
        SetStation(stationItem.CraftingStation);
    }

    /// <summary>
    /// Ends access granted by a placed crafting station and returns to hand crafting.
    /// </summary>
    public void EndStationUse()
    {
        bool stationChanged = activeStation != CraftingStationType.Inventory;
        stationInUse = false;
        if (stationChanged)
        {
            activeStation = CraftingStationType.Inventory;
            RebuildActiveRecipes();
        }

        bool stateChanged = RefreshStateInternal();
        if (stateChanged || stationChanged)
            CraftingChanged?.Invoke();
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
        RefreshStateInternal();
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
        RefreshStateInternal();
        CraftingRecipe recipe = FindActiveRecipe(recipeId);
        if (recipe == null)
        {
            Debug.LogWarning($"Craft failed: recipe '{recipeId}' is not active for station {activeStation}.");
            return false;
        }
        if (!CanCraft(recipeId))
        {
            Debug.LogWarning($"Craft failed: recipe '{recipeId}' is not currently craftable.");
            return false;
        }

        bool crafted = inventory.TryCraft(recipe);
        if (!crafted)
            Debug.LogWarning($"Craft failed: inventory transaction rejected recipe '{recipeId}'.");
        return crafted;
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
        hasWorkbenchAccess = stationInUse || (workbenchItem != null && inventory != null && inventory.IsInitialized && inventory.GetItemCount(workbenchItem.ItemId) > 0);
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
        if (recipeAssets == null)
            return;

        HashSet<string> recipeIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < recipeAssets.Length; i++)
        {
            CraftingRecipeAsset recipeAsset = recipeAssets[i];
            if (recipeAsset == null)
                continue;

            CraftingRecipe recipe = recipeAsset.ToRuntimeRecipe();
            if (recipe == null || !recipe.IsValid() || !recipeIds.Add(recipe.RecipeId))
            {
                Debug.LogWarning($"Crafting recipe asset '{recipeAsset.name}' is invalid or duplicated.");
                continue;
            }

            allRecipes.Add(recipe);
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
            if (string.Equals(activeRecipes[i].RecipeId, normalizedId, StringComparison.OrdinalIgnoreCase))
                return activeRecipes[i];
        }

        return null;
    }
}
