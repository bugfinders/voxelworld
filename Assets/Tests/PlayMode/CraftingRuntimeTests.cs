using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

public sealed class CraftingRuntimeTests
{
    [UnityTest]
    public IEnumerator CraftButtonIsEnabledWithFourWood()
    {
        SceneManager.LoadScene("default");
        yield return new WaitForSecondsRealtime(12f);
        GameObject world = GameObject.Find("World");
        Component inventory = world.GetComponent("VoxelInventory");
        MethodInfo add = inventory.GetType().GetMethod("Add");
        for (int i = 0; i < 4; i++)
            add.Invoke(inventory, new object[] { 4 });

        VisualElement gameplayMenuRoot = GameObject.Find("GameplayMenu").GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("game-root");
        Debug.Log($"Gameplay overlay display: {gameplayMenuRoot.resolvedStyle.display}");
        Assert.AreEqual(DisplayStyle.None, gameplayMenuRoot.resolvedStyle.display);
        GameObject inventoryObject = GameObject.Find("InventoryUI");


        Component inventoryUI = inventoryObject.GetComponent("VoxelInventoryUI");
        inventoryUI.GetType().GetMethod("ToggleCrafting").Invoke(inventoryUI, null);
        yield return null;

        UIDocument document = inventoryObject.GetComponent<UIDocument>();
        VisualElement recipeList = document.rootVisualElement.Q<VisualElement>("crafting-recipe-list");
        Button workbenchButton = null;
        for (int i = 0; i < recipeList.childCount; i++)
        {
            VisualElement card = recipeList[i];
            Label output = card.Q<Label>(className: "crafting-recipe-output-text");
            if (output != null && output.text.StartsWith("Workbench"))
            {
                workbenchButton = card.Q<Button>(className: "crafting-recipe-button");
                break;
            }
        }

        int woodCount = (int)inventory.GetType().GetMethod("GetItemCount").Invoke(inventory, new object[] { "Wood" });
        Debug.Log($"Craft UI state: found={workbenchButton != null}, enabled={workbenchButton != null && workbenchButton.enabledSelf}, wood={woodCount}");
        Assert.IsNotNull(workbenchButton);
        Assert.IsTrue(workbenchButton.enabledSelf);
    }
}
