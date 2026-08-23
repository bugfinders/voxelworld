using System;
using System.Collections.Generic;
using UnityEngine;

public enum CraftingStationType
{
    Inventory,
    Workbench,
    Furnace
}

[Flags]
public enum CraftingRecipeType
{
    None = 0,
    Basic = 1,
    Workstation = 2,
    Furnace = 4
}

[Serializable]
public sealed class CraftingIngredient
{
    [SerializeField] private string itemId;
    [SerializeField] private int amount;

    public CraftingIngredient(string itemId, int amount)
    {
        this.itemId = NormalizeItemId(itemId);
        this.amount = Mathf.Max(0, amount);
    }

    public string ItemId => itemId;
    public int Amount => amount;

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(itemId) && amount > 0;
    }

    public void Normalize()
    {
        itemId = NormalizeItemId(itemId);
        amount = Mathf.Max(0, amount);
    }

    private static string NormalizeItemId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

[Serializable]
public sealed class CraftingRecipe
{
    [SerializeField] private string recipeId;
    [SerializeField] private string outputItemId;
    [SerializeField] private string outputDisplayName;
    [SerializeField] private int outputAmount;
    [SerializeField] private Texture2D outputIcon;
    [SerializeField] private CraftingRecipeType recipeType;
    [SerializeField] private List<CraftingIngredient> ingredients = new List<CraftingIngredient>();

    public CraftingRecipe(string recipeId, string outputItemId, string outputDisplayName, int outputAmount, CraftingRecipeType recipeType, params CraftingIngredient[] ingredients)
    {
        this.recipeId = NormalizeItemId(recipeId);
        this.outputItemId = NormalizeItemId(outputItemId);
        this.outputDisplayName = NormalizeDisplayName(outputDisplayName, this.outputItemId);
        this.outputAmount = Mathf.Max(0, outputAmount);
        this.recipeType = recipeType;
        this.ingredients = ingredients == null ? new List<CraftingIngredient>() : new List<CraftingIngredient>(ingredients);
        Normalize();
    }

    public CraftingRecipe(string recipeId, string outputItemId, string outputDisplayName, int outputAmount, CraftingRecipeType recipeType, Texture2D outputIcon, params CraftingIngredient[] ingredients)
        : this(recipeId, outputItemId, outputDisplayName, outputAmount, recipeType, ingredients)
    {
        this.outputIcon = outputIcon;
    }

    public string RecipeId => recipeId;
    public string OutputItemId => outputItemId;
    public string OutputDisplayName => outputDisplayName;
    public int OutputAmount => outputAmount;
    public Texture2D OutputIcon => outputIcon;
    public CraftingRecipeType RecipeType => recipeType;
    public IReadOnlyList<CraftingIngredient> Ingredients => ingredients;

    public int TotalIngredientCount
    {
        get
        {
            long total = 0;
            if (ingredients == null)
                return 0;

            for (int i = 0; i < ingredients.Count; i++)
            {
                CraftingIngredient ingredient = ingredients[i];
                if (ingredient == null)
                    continue;

                total += ingredient.Amount;
                if (total >= int.MaxValue)
                    return int.MaxValue;
            }

            return (int)total;
        }
    }

    public bool IsValid()
    {
        if (string.IsNullOrEmpty(recipeId) || string.IsNullOrEmpty(outputItemId) || outputAmount <= 0 || (recipeType != CraftingRecipeType.Basic && recipeType != CraftingRecipeType.Workstation && recipeType != CraftingRecipeType.Furnace) || ingredients == null || ingredients.Count == 0)
            return false;

        HashSet<string> ingredientIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < ingredients.Count; i++)
        {
            CraftingIngredient ingredient = ingredients[i];
            if (ingredient == null || !ingredient.IsValid() || !ingredientIds.Add(ingredient.ItemId))
                return false;
        }

        int maximumIngredientCount = recipeType == CraftingRecipeType.Basic ? 4 : 9;
        return TotalIngredientCount <= maximumIngredientCount;
    }

    public void Normalize()
    {
        recipeId = NormalizeItemId(recipeId);
        outputItemId = NormalizeItemId(outputItemId);
        outputDisplayName = NormalizeDisplayName(outputDisplayName, outputItemId);
        outputAmount = Mathf.Max(0, outputAmount);
        if (ingredients == null)
            ingredients = new List<CraftingIngredient>();

        for (int i = 0; i < ingredients.Count; i++)
        {
            if (ingredients[i] != null)
                ingredients[i].Normalize();
        }
    }

    private static string NormalizeItemId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeDisplayName(string value, string normalizedItemId)
    {
        return string.IsNullOrWhiteSpace(value) ? normalizedItemId : value.Trim();
    }
}
