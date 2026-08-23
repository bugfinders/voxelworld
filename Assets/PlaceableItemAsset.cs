using UnityEngine;

[CreateAssetMenu(fileName = "PlaceableItem", menuName = "Cubeits/Placeable Item")]
public class PlaceableItemAsset : ScriptableObject
{
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private Texture2D icon;
    [SerializeField] private Material voxelMaterial;
    [SerializeField] private bool blocksTopPlacement;
    [SerializeField] private bool opensCraftingMenu;
    [SerializeField] private CraftingStationType craftingStation = CraftingStationType.Inventory;

    public string ItemId => itemId == null ? string.Empty : itemId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ItemId : displayName.Trim();
    public Texture2D Icon => icon;
    public Material VoxelMaterial => voxelMaterial;
    public bool BlocksTopPlacement => blocksTopPlacement;
    public bool OpensCraftingMenu => opensCraftingMenu;
    public CraftingStationType CraftingStation => craftingStation;

    public bool IsValid => !string.IsNullOrWhiteSpace(ItemId) && voxelMaterial != null;

    /// <summary>
    /// Returns whether this item definition represents the supplied terrain material.
    /// </summary>
    public bool MatchesMaterial(Material material)
    {
        return material != null && voxelMaterial == material;
    }
}
