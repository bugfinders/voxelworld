using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CraftingRecipe", menuName = "Cubeits/Crafting Recipe")]
public sealed class CraftingRecipeAsset : ScriptableObject
{
    [SerializeField] private string recipeId;
    [SerializeField] private PlaceableItemAsset outputItem;
    [SerializeField] private string outputItemId;
    [SerializeField] private string outputDisplayName;
    [SerializeField] private int outputAmount = 1;
    [SerializeField] private Texture2D outputIcon;
    [SerializeField] private CraftingRecipeType recipeType = CraftingRecipeType.Basic;
    [SerializeField] private string[] ingredientItemIds = new string[0];
    [SerializeField] private int[] ingredientAmounts = new int[0];

    /// <summary>
    /// Converts the editable asset data into the runtime recipe representation used by the crafting system.
    /// </summary>
    public CraftingRecipe ToRuntimeRecipe()
    {
        List<CraftingIngredient> runtimeIngredients = new List<CraftingIngredient>();
        int itemCount = ingredientItemIds == null ? 0 : ingredientItemIds.Length;
        int amountCount = ingredientAmounts == null ? 0 : ingredientAmounts.Length;
        int ingredientCount = Mathf.Min(itemCount, amountCount);
        for (int i = 0; i < ingredientCount; i++)
            runtimeIngredients.Add(new CraftingIngredient(ingredientItemIds[i], ingredientAmounts[i]));

        string resolvedOutputItemId = outputItem != null && !string.IsNullOrWhiteSpace(outputItem.ItemId) ? outputItem.ItemId : outputItemId;
        string resolvedDisplayName = outputItem != null ? outputItem.DisplayName : outputDisplayName;
        Texture2D resolvedIcon = outputItem != null && outputItem.Icon != null ? outputItem.Icon : outputIcon;
        return new CraftingRecipe(recipeId, resolvedOutputItemId, resolvedDisplayName, outputAmount, recipeType, resolvedIcon, runtimeIngredients.ToArray());
    }
}
