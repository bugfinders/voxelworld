using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class VoxelInventoryUI : MonoBehaviour
{
    public const string RootClass = "inventory-ui-root";
    public const string HotbarClass = "inventory-hotbar";
    public const string SlotClass = "inventory-slot";
    public const string OccupiedClass = "inventory-slot--occupied";
    public const string SelectedClass = "inventory-slot--selected";
    public const string EmptyClass = "inventory-slot--empty";
    public const string UnavailableClass = "inventory-slot--unavailable";
    public const string IconClass = "inventory-slot__icon";
    public const string CountClass = "inventory-slot__count";
    public const string ItemNameClass = "inventory-slot__name";
    public const string KeyLabelClass = "inventory-slot__key";
    public const string TooltipClass = "inventory-item-tooltip";
    public const string TooltipNameClass = "inventory-item-tooltip__name";
    public const string TooltipKindClass = "inventory-item-tooltip__kind";
    public const string AdditionalPanelVisibleClass = "inventory-additional-panel--visible";
    public const string CraftingVisibleClass = "crafting-content--visible";
    public const string CraftingStationActiveClass = "crafting-station--active";
    public const string CraftingStationLockedClass = "crafting-station--locked";
    public const string CraftingRecipeUnavailableClass = "crafting-recipe-card--unavailable";
    public const string CraftingIngredientMissingClass = "crafting-recipe-ingredient--missing";

    private const string RootName = "inventory-ui-root";
    private const string HotbarSlotsName = "inventory-hotbar-slots";
    private const string AdditionalPanelName = "inventory-additional-panel";
    private const string PanelTitleName = "inventory-panel-title";
    private const string AdditionalContentName = "inventory-additional-content";
    private const string AdditionalScrollName = "inventory-additional-scroll";
    private const string ChestPanelName = "inventory-chest-panel";
    private const string ChestTitleName = "inventory-chest-title";
    private const string CraftingContentName = "crafting-content";

    private const string ChestContentName = "inventory-chest-content";
    private const string CraftingReturnName = "crafting-inventory-return";
    private const string CraftingInventoryStationName = "crafting-station-inventory";
    private const string CraftingWorkbenchStationName = "crafting-station-workbench";
    private const string CraftingUnavailableToggleName = "crafting-unavailable-toggle";
    private const string CraftingRecipeListName = "crafting-recipe-list";
    private const string TooltipName = "inventory-item-tooltip";
    private const string TooltipItemName = "inventory-tooltip-item-name";
    private const string TooltipKindName = "inventory-tooltip-kind";

    public Key additionalInventoryToggleKey = Key.Tab;
    private enum InventoryContainer
    {
        Hotbar,
        Player,
        Chest
    }


    public Key craftingToggleKey = Key.C;

    private static VoxelInventoryUI activeInstance;
    private UIDocument document;
    private VisualElement root;
    private VisualElement hotbarSlotsHost;
    private VisualElement additionalPanel;
    private Label panelTitle;
    private VisualElement additionalContentHost;
    private VisualElement additionalScroll;
    private VisualElement craftingContentHost;
    private VisualElement chestPanel;
    private Label chestTitle;
    private VisualElement chestContentHost;
    private Button craftingReturnButton;
    private Button craftingInventoryStationButton;
    private Button craftingWorkbenchStationButton;
    private Toggle craftingUnavailableToggle;
    private VisualElement craftingRecipeListHost;
    private ChunkedVoxelTerrain terrain;
    private VoxelInventory inventory;
    private CraftingSystem craftingSystem;
    private ChestInventory activeChest;
    private VisualElement registeredRoot;
    private bool additionalInventoryVisible;
    private bool craftingVisible;
    private bool showUnavailableRecipes;
    private bool cursorStateCaptured;
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;
    private int selectedAdditionalSlotIndex = -1;
    private readonly List<Button> registeredHotbarButtons = new List<Button>();
    private bool dragSourceActive;
    private InventoryContainer dragSourceContainer;
    private int dragSourceIndex;
    private VisualElement dragSourceElement;

    private int dragPointerId;
    private VisualElement itemTooltip;
    private Label tooltipItemName;
    private Label tooltipKind;
    private Button hoveredTooltipSlot;
    private bool hoveredTooltipHotbar;
    private bool hoveredTooltipChest;
    private int hoveredTooltipIndex = -1;
    private Vector2 hoveredTooltipPosition;

    public bool AdditionalInventoryVisible => additionalInventoryVisible;
    public bool CraftingVisible => craftingVisible;
    public static bool IsAnyWindowVisible => activeInstance != null && activeInstance.additionalInventoryVisible;

    private void Awake()
    {
        document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        activeInstance = this;
        if (document == null)
            document = GetComponent<UIDocument>();

        Refresh();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[additionalInventoryToggleKey].wasPressedThisFrame)
            ToggleAdditionalInventory();
        if (keyboard != null && keyboard[craftingToggleKey].wasPressedThisFrame)
            ToggleCrafting();

        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame)
                SelectHotbarSlot(0);
            else if (keyboard.digit2Key.wasPressedThisFrame)
                SelectHotbarSlot(1);
            else if (keyboard.digit3Key.wasPressedThisFrame)
                SelectHotbarSlot(2);
            else if (keyboard.digit4Key.wasPressedThisFrame)
                SelectHotbarSlot(3);
            else if (keyboard.digit5Key.wasPressedThisFrame)
                SelectHotbarSlot(4);
            else if (keyboard.digit6Key.wasPressedThisFrame)
                SelectHotbarSlot(5);
            else if (keyboard.digit7Key.wasPressedThisFrame)
                SelectHotbarSlot(6);
            else if (keyboard.digit8Key.wasPressedThisFrame)
                SelectHotbarSlot(7);
            else if (keyboard.digit9Key.wasPressedThisFrame)
                SelectHotbarSlot(8);
            else if (keyboard.digit0Key.wasPressedThisFrame)
                SelectHotbarSlot(9);
        }

        UpdateTooltipState();
    }

    private void OnDisable()
    {
        UnsubscribeFromInventory();
        UnsubscribeFromCrafting();
        UnsubscribeFromChest();
        CancelDrag(dragPointerId);
        if (activeInstance == this)
            activeInstance = null;
        RestoreCursorState();
    }

    private void OnDestroy()
    {
        UnsubscribeFromInventory();
        UnsubscribeFromCrafting();
        UnsubscribeFromChest();
        CancelDrag(dragPointerId);
        if (activeInstance == this)
            activeInstance = null;
        RestoreCursorState();
    }

    /// <summary>
    /// Opens or closes the additional inventory panel.
    /// </summary>
    public void ToggleAdditionalInventory()
    {
        additionalInventoryVisible = !additionalInventoryVisible;
        if (!additionalInventoryVisible)
        {
            craftingVisible = false;
            craftingSystem?.EndStationUse();
            UnsubscribeFromChest();
            activeChest = null;
        }

        if (additionalInventoryVisible)
            CaptureAndShowCursor();
        else
            RestoreCursorState();

        ApplyPanelVisibility();
    }

    /// <summary>
    /// Opens the crafting view inside the existing inventory panel.
    /// </summary>
    public void ToggleCrafting()
    {
        if (craftingSystem == null)
            ResolveCraftingSystem();
        if (craftingContentHost == null)
        {
            Refresh();
            ResolveCraftingSystem();
        }
        if (craftingSystem == null || craftingContentHost == null)
            return;

        if (craftingVisible)
            craftingSystem.EndStationUse();
        craftingVisible = !craftingVisible;
        additionalInventoryVisible = true;
        if (additionalInventoryVisible)
            CaptureAndShowCursor();

        ApplyPanelVisibility();
        RefreshCraftingView();
    }

    /// <summary>
    /// Opens the crafting view using the placed item definition's station settings.
    /// </summary>
    public void OpenStation(PlaceableItemAsset stationItem)
    {
        if (craftingSystem == null)
            ResolveCraftingSystem();
        if (craftingContentHost == null)
        {
            Refresh();
            ResolveCraftingSystem();
        }
        if (craftingSystem == null || craftingContentHost == null || stationItem == null)
            return;

        craftingSystem.BeginStationUse(stationItem);
        craftingVisible = true;
        additionalInventoryVisible = true;
        CaptureAndShowCursor();
        ApplyPanelVisibility();
        RefreshCraftingView();
    }

    /// <summary>
    /// Opens the remote inventory belonging to a placed chest.
    /// </summary>
    public void OpenChest(ChunkedVoxelTerrain chestTerrain, ChestInventory chest)
    {
        if (chestTerrain == null || chest == null)
            return;

        if (additionalPanel == null || additionalContentHost == null)
            Refresh();
        if (additionalPanel == null || additionalContentHost == null)
            return;

        UnsubscribeFromChest();
        terrain = chestTerrain;
        activeChest = chest;
        activeChest.Changed += Refresh;
        craftingSystem?.EndStationUse();
        craftingVisible = false;
        additionalInventoryVisible = true;
        CaptureAndShowCursor();
        GenerateChestSlots();
        ApplyPanelVisibility();
    }

    /// <summary>
    /// Returns from the crafting view to the additional inventory slots.
    /// </summary>
    public void ShowInventoryPanel()
    {
        craftingSystem?.EndStationUse();
        craftingVisible = false;
        additionalInventoryVisible = true;
        CaptureAndShowCursor();
        ApplyPanelVisibility();
    }

    /// <summary>
    /// Selects an available crafting station and refreshes its recipe cards.
    /// </summary>
    public void SelectCraftingStation(CraftingStationType station)
    {
        if (craftingSystem == null)
            return;

        craftingSystem.SetStation(station);
        craftingVisible = true;
        additionalInventoryVisible = true;
        CaptureAndShowCursor();
        ApplyPanelVisibility();
        RefreshCraftingView();
    }

    /// <summary>
    /// Attempts to craft one recipe through the scene crafting coordinator.
    /// </summary>
    public void CraftRecipe(string recipeId)
    {
        if (craftingSystem == null)
            return;

        if (craftingSystem.TryCraft(recipeId))
            RefreshCraftingView();
    }

    /// <summary>
    /// Toggles whether discovered recipes missing materials remain visible.
    /// </summary>
    public void ToggleUnavailableRecipes()
    {
        showUnavailableRecipes = !showUnavailableRecipes;
        RefreshCraftingView();
    }


    public void SelectHotbarSlot(int index)
    {
        if (inventory == null || !inventory.IsInitialized || index < 0 || index >= VoxelInventory.HotbarSlotCount)
            return;

        inventory.SelectSlot(index);
        RefreshSlotStates();
    }

    /// <summary>
    /// Rebinds the UI to the terrain inventory and refreshes generated controls.
    /// </summary>
    public void Refresh()
    {
        if (document == null)
            return;

        VisualElement documentRoot = document.rootVisualElement;
        if (documentRoot == null)
            return;

        if (root != documentRoot || hotbarSlotsHost == null || additionalPanel == null || additionalContentHost == null || chestPanel == null)
        {
            root = documentRoot.Q<VisualElement>(RootName) ?? documentRoot;
            hotbarSlotsHost = root.Q<VisualElement>(HotbarSlotsName);
            additionalPanel = root.Q<VisualElement>(AdditionalPanelName);
            panelTitle = root.Q<Label>(PanelTitleName);
            additionalContentHost = root.Q<VisualElement>(AdditionalContentName);
            additionalScroll = root.Q<VisualElement>(AdditionalScrollName);
            chestPanel = root.Q<VisualElement>(ChestPanelName);
            chestTitle = root.Q<Label>(ChestTitleName);
            chestContentHost = root.Q<VisualElement>(ChestContentName);

            craftingContentHost = root.Q<VisualElement>(CraftingContentName);
            craftingReturnButton = root.Q<Button>(CraftingReturnName);
            craftingInventoryStationButton = root.Q<Button>(CraftingInventoryStationName);
            craftingWorkbenchStationButton = root.Q<Button>(CraftingWorkbenchStationName);
            craftingUnavailableToggle = root.Q<Toggle>(CraftingUnavailableToggleName);
            craftingRecipeListHost = root.Q<VisualElement>(CraftingRecipeListName);
            registeredRoot = null;
            registeredHotbarButtons.Clear();
        }

        EnsureVisualTreeHosts(documentRoot);
        RegisterCallbacks();
        ResolveInventory();
        ResolveCraftingSystem();
        GenerateHotbarSlots();
        GenerateAdditionalSlots();
        GenerateChestSlots();
        RefreshCraftingView();
        ApplyPanelVisibility();
    }

    private void EnsureVisualTreeHosts(VisualElement documentRoot)
    {
        if (root == documentRoot)
        {
            VisualElement authoredRoot = documentRoot.Q<VisualElement>(RootName);
            if (authoredRoot != null)
                root = authoredRoot;
        }

        if (root == null || root == documentRoot)
        {
            root = new VisualElement { name = RootName };
            root.AddToClassList(RootClass);
            documentRoot.Add(root);
        }

        if (hotbarSlotsHost == null)
        {
            hotbarSlotsHost = root.Q<VisualElement>(HotbarSlotsName);
            if (hotbarSlotsHost == null)
            {
                hotbarSlotsHost = new VisualElement { name = HotbarSlotsName };
                hotbarSlotsHost.AddToClassList(HotbarClass);
                root.Add(hotbarSlotsHost);
            }
        }

        if (additionalPanel == null)
        {
            additionalPanel = root.Q<VisualElement>(AdditionalPanelName);
            if (additionalPanel == null)
            {
                additionalPanel = new VisualElement { name = AdditionalPanelName };
                additionalPanel.AddToClassList("inventory-additional-panel");
                root.Add(additionalPanel);
            }
        }

        if (panelTitle == null)
        {
            panelTitle = root.Q<Label>(PanelTitleName);
            if (panelTitle == null)
            {
                panelTitle = new Label { name = PanelTitleName, text = "Inventory" };
                panelTitle.AddToClassList("inventory-panel-title");
                additionalPanel.Add(panelTitle);
            }
        }

        if (additionalContentHost == null)
        {
            additionalContentHost = root.Q<VisualElement>(AdditionalContentName);
            if (additionalContentHost == null)
            {
                additionalContentHost = new VisualElement { name = AdditionalContentName };
                additionalContentHost.AddToClassList("inventory-additional-content");
                additionalPanel.Add(additionalContentHost);
            }
        }

        if (chestPanel == null)
        {
            chestPanel = root.Q<VisualElement>(ChestPanelName);
            if (chestPanel == null)
            {
                chestPanel = new VisualElement { name = ChestPanelName };
                chestPanel.AddToClassList("inventory-chest-panel");
                additionalPanel.Add(chestPanel);
            }
        }

        if (chestTitle == null)
        {
            chestTitle = chestPanel.Q<Label>(ChestTitleName);
            if (chestTitle == null)
            {
                chestTitle = new Label { name = ChestTitleName, text = "Remote Storage" };
                chestTitle.AddToClassList("inventory-chest-title");
                chestPanel.Add(chestTitle);
            }
        }

        if (chestContentHost == null)
        {
            chestContentHost = chestPanel.Q<VisualElement>(ChestContentName);
            if (chestContentHost == null)
            {
                chestContentHost = new VisualElement { name = ChestContentName };
                chestContentHost.AddToClassList("inventory-chest-content");
                chestPanel.Add(chestContentHost);
            }
        }

        if (craftingContentHost == null)
        {
            craftingContentHost = root.Q<VisualElement>(CraftingContentName);
            if (craftingContentHost == null)
            {
                craftingContentHost = new VisualElement { name = CraftingContentName };
                craftingContentHost.AddToClassList("crafting-content");
                additionalPanel.Add(craftingContentHost);
            }
        }

        if (craftingReturnButton == null)
        {
            craftingReturnButton = root.Q<Button>(CraftingReturnName);
            if (craftingReturnButton == null)
            {
                craftingReturnButton = new Button { name = CraftingReturnName, text = "Inventory" };
                craftingReturnButton.AddToClassList("crafting-return-button");
                craftingContentHost.Add(craftingReturnButton);
            }
        }

        if (craftingInventoryStationButton == null)
        {
            craftingInventoryStationButton = root.Q<Button>(CraftingInventoryStationName);
            if (craftingInventoryStationButton == null)
            {
                craftingInventoryStationButton = new Button { name = CraftingInventoryStationName, text = "Hand Crafting" };
                craftingInventoryStationButton.AddToClassList("crafting-station-button");
                craftingContentHost.Add(craftingInventoryStationButton);
            }
        }

        if (craftingWorkbenchStationButton == null)
        {
            craftingWorkbenchStationButton = root.Q<Button>(CraftingWorkbenchStationName);
            if (craftingWorkbenchStationButton == null)
            {
                craftingWorkbenchStationButton = new Button { name = CraftingWorkbenchStationName, text = "Workbench" };
                craftingWorkbenchStationButton.AddToClassList("crafting-station-button");
                craftingContentHost.Add(craftingWorkbenchStationButton);
            }
        }

        if (craftingUnavailableToggle == null)
        {
            craftingUnavailableToggle = root.Q<Toggle>(CraftingUnavailableToggleName);
            if (craftingUnavailableToggle == null)
            {
                craftingUnavailableToggle = new Toggle("Show Unavailable") { name = CraftingUnavailableToggleName };
                craftingUnavailableToggle.AddToClassList("crafting-unavailable-toggle");
                craftingContentHost.Q<VisualElement>("crafting-toolbar")?.Add(craftingUnavailableToggle);
            }
        }
        if (itemTooltip == null)
        {
            itemTooltip = root.Q<VisualElement>(TooltipName);
            if (itemTooltip == null)
            {
                itemTooltip = new VisualElement { name = TooltipName };
                itemTooltip.AddToClassList(TooltipClass);
                root.Add(itemTooltip);
            }
        }

        if (tooltipItemName == null)
        {
            tooltipItemName = itemTooltip.Q<Label>(TooltipItemName);
            if (tooltipItemName == null)
            {
                tooltipItemName = new Label { name = TooltipItemName };
                itemTooltip.Add(tooltipItemName);
            }
            tooltipItemName.AddToClassList(TooltipNameClass);
        }

        if (tooltipKind == null)
        {
            tooltipKind = itemTooltip.Q<Label>(TooltipKindName);
            if (tooltipKind == null)
            {
                tooltipKind = new Label { name = TooltipKindName };
                itemTooltip.Add(tooltipKind);
            }
            tooltipKind.AddToClassList(TooltipKindClass);
        }

        itemTooltip.pickingMode = PickingMode.Ignore;
    }

    private void RegisterCallbacks()
    {
        if (registeredRoot == root)
            return;

        if (craftingReturnButton != null)
            craftingReturnButton.clicked += ShowInventoryPanel;
        if (craftingInventoryStationButton != null)
            craftingInventoryStationButton.clicked += SelectInventoryStation;
        if (craftingWorkbenchStationButton != null)
            craftingWorkbenchStationButton.clicked += SelectWorkbenchStation;
        if (craftingUnavailableToggle != null)
            craftingUnavailableToggle.RegisterValueChangedCallback(evt =>
            {
                showUnavailableRecipes = evt.newValue;
                RefreshCraftingView();
            });

        registeredRoot = root;
    }

    private void ResolveInventory()
    {
        ChunkedVoxelTerrain nextTerrain = terrain;
        if (nextTerrain == null)
            nextTerrain = Object.FindFirstObjectByType<ChunkedVoxelTerrain>();

        VoxelInventory nextInventory = nextTerrain == null ? null : nextTerrain.Inventory;
        if (nextInventory == inventory)
        {
            terrain = nextTerrain;
            return;
        }

        UnsubscribeFromInventory();
        terrain = nextTerrain;
        inventory = nextInventory;
        if (inventory != null)
        {
            inventory.InventoryChanged += Refresh;
            inventory.SelectionChanged += HandleSelectionChanged;
        }
    }

    private void UnsubscribeFromChest()
    {
        if (activeChest != null)
            activeChest.Changed -= Refresh;
    }

    private void ResolveCraftingSystem()
    {
        CraftingSystem nextCraftingSystem = craftingSystem ?? GetComponent<CraftingSystem>();
        if (nextCraftingSystem == craftingSystem)
            return;

        UnsubscribeFromCrafting();
        craftingSystem = nextCraftingSystem;
        if (craftingSystem != null)
        {
            craftingSystem.CraftingChanged += RefreshCraftingView;
            craftingSystem.RefreshState();
        }
    }

    private void UnsubscribeFromInventory()
    {
        if (inventory == null)
            return;

        inventory.InventoryChanged -= Refresh;
        inventory.SelectionChanged -= HandleSelectionChanged;
        inventory = null;
    }

    private void UnsubscribeFromCrafting()
    {
        if (craftingSystem == null)
            return;

        craftingSystem.CraftingChanged -= RefreshCraftingView;
        craftingSystem = null;
    }

    private void GenerateHotbarSlots()
    {
        if (hotbarSlotsHost == null)
            return;

        while (hotbarSlotsHost.childCount > VoxelInventory.HotbarSlotCount)
            hotbarSlotsHost.RemoveAt(hotbarSlotsHost.childCount - 1);

        for (int index = 0; index < VoxelInventory.HotbarSlotCount; index++)
        {
            Button button = index < hotbarSlotsHost.childCount ? hotbarSlotsHost[index] as Button : null;
            if (button == null)
            {
                button = CreateSlotButton(SlotClass);
                hotbarSlotsHost.Add(button);
            }

            PrepareSlotButton(button, index);
            if (!registeredHotbarButtons.Contains(button))
            {
                int capturedIndex = index;
                button.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0)
                        return;

                    BeginDrag(InventoryContainer.Hotbar, capturedIndex, evt.pointerId, button);
                    SelectHotbarSlot(capturedIndex);
                    evt.StopPropagation();
                });
                button.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (evt.button != 0)
                        return;

                    CompleteDragAtPosition(evt.position, evt.pointerId, evt.shiftKey);
                    evt.StopPropagation();
                });
                RegisterTooltipCallbacks(button, true, capturedIndex, false);
                registeredHotbarButtons.Add(button);
            }

            RefreshHotbarSlot(button, index);
        }
    }

    private void PrepareSlotButton(Button button, int index)
    {
        button.AddToClassList(SlotClass);
        if (button.Q<Label>("inventory-slot-key") == null)
            button.Add(CreateKeyLabel(GetKeyLabel(index)));

        EnsureSlotChildren(button);
    }

    private void EnsureSlotChildren(Button button)
    {
        Image icon = button.Q<Image>("inventory-slot-icon");
        if (icon == null)
        {
            icon = new Image { name = "inventory-slot-icon", scaleMode = ScaleMode.ScaleToFit };
            button.Add(icon);
        }
        icon.AddToClassList(IconClass);

        Label count = button.Q<Label>("inventory-slot-count");
        if (count == null)
        {
            count = new Label { name = "inventory-slot-count" };
            button.Add(count);
        }
        count.AddToClassList(CountClass);

        Label itemName = button.Q<Label>("inventory-slot-name");
        if (itemName == null)
        {
            itemName = new Label { name = "inventory-slot-name" };
            button.Add(itemName);
        }
        itemName.AddToClassList(ItemNameClass);
    }

    private void GenerateAdditionalSlots()
    {
        if (additionalContentHost == null)
            return;

        additionalContentHost.Clear();
        HideItemTooltip();
        if (inventory == null || !inventory.IsInitialized)
            return;

        for (int index = 0; index < VoxelInventory.AdditionalSlotCount; index++)
        {
            InventorySlotData slot = index < inventory.AdditionalSlots.Count ? inventory.AdditionalSlots[index] : null;
            Button button = CreateSlotButton(SlotClass);
            int capturedIndex = index;
            button.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                BeginDrag(InventoryContainer.Player, capturedIndex, evt.pointerId, button);
                evt.StopPropagation();
            });
            button.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                CompleteDragAtPosition(evt.position, evt.pointerId, evt.shiftKey);
                evt.StopPropagation();
            });
            RegisterTooltipCallbacks(button, false, capturedIndex, false);
            additionalContentHost.Add(button);
            RefreshAdditionalSlot(button, slot, index);
        }
    }

    private void GenerateChestSlots()
    {
        if (chestContentHost == null)
            return;

        chestContentHost.Clear();
        if (activeChest == null)
            return;

        for (int index = 0; index < ChestInventory.SlotCount; index++)
        {
            Button button = CreateSlotButton(SlotClass);
            int capturedIndex = index;
            button.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                BeginDrag(InventoryContainer.Chest, capturedIndex, evt.pointerId, button);
                evt.StopPropagation();
            });
            button.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                CompleteDragAtPosition(evt.position, evt.pointerId, evt.shiftKey);
                evt.StopPropagation();
            });
            RegisterTooltipCallbacks(button, false, capturedIndex, true);
            chestContentHost.Add(button);
            RefreshChestSlot(button, index);
        }
    }

    private void RegisterTooltipCallbacks(Button button, bool hotbar, int index, bool chest)
    {
        button.RegisterCallback<PointerEnterEvent>(evt =>
        {
            hoveredTooltipSlot = button;
            hoveredTooltipHotbar = hotbar;
            hoveredTooltipChest = chest;
            hoveredTooltipIndex = index;
            hoveredTooltipPosition = evt.position;
            UpdateItemTooltip();
        });
        button.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (hoveredTooltipSlot != button)
                return;

            hoveredTooltipPosition = evt.position;
            UpdateItemTooltip();
        });
        button.RegisterCallback<PointerLeaveEvent>(evt =>
        {
            if (hoveredTooltipSlot == button)
                HideItemTooltip();
        });
    }

    private void UpdateTooltipState()
    {
        if (!additionalInventoryVisible && !hoveredTooltipHotbar)
        {
            HideItemTooltip();
            return;
        }

        if (additionalInventoryVisible)
            EnsureCursorForOpenInventory();
        UpdateItemTooltip();
    }

    private void EnsureCursorForOpenInventory()
    {
        if (UnityEngine.Cursor.lockState != CursorLockMode.None)
            UnityEngine.Cursor.lockState = CursorLockMode.None;
        if (!UnityEngine.Cursor.visible)
            UnityEngine.Cursor.visible = true;
    }

    private void UpdateItemTooltip()
    {
        if (itemTooltip == null || hoveredTooltipSlot == null || !additionalInventoryVisible && !hoveredTooltipHotbar)
        {
            HideItemTooltip();
            return;
        }

        if (!IsShiftHeld())
        {
            HideItemTooltip(false);
            return;
        }

        InventorySlotData slot = hoveredTooltipChest
            ? activeChest == null ? null : activeChest.GetSlot(hoveredTooltipIndex)
            : hoveredTooltipHotbar
                ? inventory == null ? null : inventory.GetHotbarSlot(hoveredTooltipIndex)
                : inventory == null || hoveredTooltipIndex >= inventory.AdditionalSlots.Count ? null : inventory.AdditionalSlots[hoveredTooltipIndex];
        if (slot == null || slot.IsEmpty)
        {
            HideItemTooltip();
            return;
        }

        tooltipItemName.text = slot.DisplayName;
        tooltipKind.text = $"Kind: {slot.ItemKind}";
        itemTooltip.style.left = hoveredTooltipPosition.x - root.worldBound.x + 14f;
        itemTooltip.style.top = hoveredTooltipPosition.y - root.worldBound.y + 14f;
        itemTooltip.style.display = DisplayStyle.Flex;
    }

    private static bool IsShiftHeld()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
    }

    private void HideItemTooltip(bool clearHover = true)
    {
        if (itemTooltip != null)
            itemTooltip.style.display = DisplayStyle.None;
        if (clearHover)
        {
            hoveredTooltipSlot = null;
            hoveredTooltipIndex = -1;
            hoveredTooltipChest = false;
        }
    }

    private void RefreshCraftingView()
    {
        if (craftingSystem == null || craftingRecipeListHost == null)
            return;

        craftingInventoryStationButton?.EnableInClassList(CraftingStationActiveClass, craftingSystem.ActiveStation == CraftingStationType.Inventory);
        craftingWorkbenchStationButton?.EnableInClassList(CraftingStationActiveClass, craftingSystem.ActiveStation == CraftingStationType.Workbench);
        craftingWorkbenchStationButton?.EnableInClassList(CraftingStationLockedClass, !craftingSystem.HasWorkbenchAccess);
        if (craftingInventoryStationButton != null)
            craftingInventoryStationButton.SetEnabled(true);
        if (craftingWorkbenchStationButton != null)
            craftingWorkbenchStationButton.SetEnabled(craftingSystem.HasWorkbenchAccess);

        if (craftingUnavailableToggle != null)
        {
            craftingUnavailableToggle.SetValueWithoutNotify(showUnavailableRecipes);
        }

        craftingRecipeListHost.Clear();
        List<CraftingRecipe> recipes = new List<CraftingRecipe>(craftingSystem.Recipes);
        for (int i = 0; i < recipes.Count; i++)
        {
            if (!craftingSystem.ShouldShowRecipe(recipes[i].RecipeId, showUnavailableRecipes))
                continue;

            craftingRecipeListHost.Add(CreateRecipeCard(recipes[i]));
        }
    }

    private VisualElement CreateRecipeCard(CraftingRecipe recipe)
    {
        VisualElement card = new VisualElement();
        card.AddToClassList("crafting-recipe-card");
        bool canCraft = craftingSystem != null && craftingSystem.CanCraft(recipe.RecipeId);
        card.EnableInClassList(CraftingRecipeUnavailableClass, !canCraft);

        VisualElement output = new VisualElement();
        output.AddToClassList("crafting-recipe-output");
        Image outputIcon = new Image { scaleMode = ScaleMode.ScaleToFit };
        outputIcon.AddToClassList("crafting-recipe-output-icon");
        outputIcon.image = recipe.OutputIcon;
        outputIcon.style.display = recipe.OutputIcon == null ? DisplayStyle.None : DisplayStyle.Flex;
        output.Add(outputIcon);
        Label outputText = new Label($"{recipe.OutputDisplayName} x{recipe.OutputAmount}");
        outputText.AddToClassList("crafting-recipe-output-text");
        output.Add(outputText);
        card.Add(output);

        VisualElement ingredients = new VisualElement();
        ingredients.AddToClassList("crafting-recipe-ingredients");
        for (int i = 0; i < recipe.Ingredients.Count; i++)
        {
            CraftingIngredient ingredient = recipe.Ingredients[i];
            int currentCount = inventory == null ? 0 : inventory.GetItemCount(ingredient.ItemId);
            Label requirement = new Label($"{ingredient.ItemId}: {currentCount} / {ingredient.Amount}");
            requirement.AddToClassList("crafting-recipe-ingredient");
            requirement.EnableInClassList(CraftingIngredientMissingClass, currentCount < ingredient.Amount);
            ingredients.Add(requirement);
        }
        card.Add(ingredients);

        Button craftButton = new Button { text = "Craft" };
        craftButton.AddToClassList("crafting-recipe-button");
        craftButton.SetEnabled(canCraft);
        string capturedRecipeId = recipe.RecipeId;
        craftButton.clicked += () => CraftRecipe(capturedRecipeId);
        card.Add(craftButton);
        return card;
    }

    private void SelectInventoryStation()
    {
        craftingSystem?.EndStationUse();
        SelectCraftingStation(CraftingStationType.Inventory);
    }

    private void SelectWorkbenchStation()
    {
        SelectCraftingStation(CraftingStationType.Workbench);
    }

    private void BeginDrag(InventoryContainer sourceContainer, int sourceIndex, int pointerId, VisualElement sourceElement)
    {
        if (inventory == null || !inventory.IsInitialized || sourceElement == null)
            return;

        InventorySlotData source = GetSlot(sourceContainer, sourceIndex);
        if (source == null || source.IsEmpty)
            return;

        dragSourceActive = true;
        dragSourceContainer = sourceContainer;
        dragSourceIndex = sourceIndex;
        dragPointerId = pointerId;
        dragSourceElement = sourceElement;
        sourceElement.CapturePointer(pointerId);
    }

    private InventorySlotData GetSlot(InventoryContainer container, int index)
    {
        switch (container)
        {
            case InventoryContainer.Hotbar:
                return inventory.GetHotbarSlot(index);
            case InventoryContainer.Player:
                return index >= 0 && index < inventory.AdditionalSlots.Count ? inventory.AdditionalSlots[index] : null;
            case InventoryContainer.Chest:
                return activeChest == null ? null : activeChest.GetSlot(index);
            default:
                return null;
        }
    }

    private void CompleteDragAtPosition(Vector2 pointerPosition, int pointerId, bool shiftHeld)
    {
        if (!dragSourceActive || dragPointerId != pointerId)
            return;

        InventoryContainer targetContainer;
        int targetIndex;
        if (!TryGetSlotAtPosition(pointerPosition, out targetContainer, out targetIndex))
        {
            CancelDrag(pointerId);
            return;
        }

        bool sameSlot = dragSourceContainer == targetContainer && dragSourceIndex == targetIndex;
        bool moved = false;
        if (!sameSlot)
        {
            if (dragSourceContainer == InventoryContainer.Chest)
            {
                moved = inventory != null && inventory.TryMoveItemFromChest(activeChest, dragSourceIndex,
                    targetContainer == InventoryContainer.Hotbar, targetIndex);
            }
            else if (targetContainer == InventoryContainer.Chest)
            {
                moved = inventory != null && inventory.TryMoveItemToChest(activeChest,
                    dragSourceContainer == InventoryContainer.Hotbar, dragSourceIndex, targetIndex);
            }
            else
            {
                moved = inventory != null && inventory.TryMoveItem(
                    dragSourceContainer == InventoryContainer.Hotbar, dragSourceIndex,
                    targetContainer == InventoryContainer.Hotbar, targetIndex);
            }
        }
        else if (dragSourceContainer == InventoryContainer.Player && shiftHeld)
        {
            moved = inventory != null && inventory.TryTakeAdditionalItemToHotbar(dragSourceIndex, true);
        }

        CancelDrag(pointerId);
        if (moved)
        {
            selectedAdditionalSlotIndex = -1;
            Refresh();
        }
    }

    private void CancelDrag(int pointerId)
    {
        if (dragSourceElement != null && dragSourceElement.HasPointerCapture(pointerId))
            dragSourceElement.ReleasePointer(pointerId);
        dragSourceElement = null;
        dragSourceActive = false;
    }

    private bool TryGetSlotAtPosition(Vector2 pointerPosition, out InventoryContainer container, out int index)
    {
        if (hotbarSlotsHost != null)
        {
            for (int i = 0; i < hotbarSlotsHost.childCount; i++)
            {
                if (hotbarSlotsHost[i].worldBound.Contains(pointerPosition))
                {
                    container = InventoryContainer.Hotbar;
                    index = i;
                    return true;
                }
            }
        }

        if (additionalContentHost != null && additionalContentHost.resolvedStyle.display != DisplayStyle.None)
        {
            for (int i = 0; i < additionalContentHost.childCount; i++)
            {
                if (additionalContentHost[i].worldBound.Contains(pointerPosition))
                {
                    container = InventoryContainer.Player;
                    index = i;
                    return true;
                }
            }
        }

        if (chestContentHost != null && chestContentHost.resolvedStyle.display != DisplayStyle.None)
        {
            for (int i = 0; i < chestContentHost.childCount; i++)
            {
                if (chestContentHost[i].worldBound.Contains(pointerPosition))
                {
                    container = InventoryContainer.Chest;
                    index = i;
                    return true;
                }
            }
        }

        container = InventoryContainer.Player;
        index = -1;
        return false;
    }

    private void RefreshHotbarSlot(Button button, int index)
    {
        InventorySlotData slot = inventory == null || !inventory.IsInitialized ? null : inventory.GetHotbarSlot(index);
        RefreshSlot(button, slot, inventory != null && inventory.IsInitialized, index == (inventory == null ? -1 : inventory.SelectedSlotIndex));
    }

    private void RefreshAdditionalSlot(Button button, InventorySlotData slot, int index)
    {
        RefreshSlot(button, slot, inventory != null && inventory.IsInitialized, index == selectedAdditionalSlotIndex);
    }

    private void RefreshSlot(Button button, InventorySlotData slot, bool available, bool selected)
    {
        bool empty = slot == null || slot.IsEmpty;
        selected = selected && !empty;
        button.EnableInClassList(OccupiedClass, !empty);
        button.EnableInClassList(EmptyClass, empty);
        button.EnableInClassList(UnavailableClass, !available);
        button.EnableInClassList(SelectedClass, selected && available);
        button.SetEnabled(available);

        Image icon = button.Q<Image>("inventory-slot-icon");
        Label count = button.Q<Label>("inventory-slot-count");
        Label itemName = button.Q<Label>("inventory-slot-name");
        Texture2D texture = empty ? null : slot.Icon;
        icon.image = texture;
        icon.style.display = texture == null ? DisplayStyle.None : DisplayStyle.Flex;
        count.text = empty || slot.Count <= 0 ? string.Empty : slot.Count.ToString();
        itemName.text = empty || texture != null ? string.Empty : slot.DisplayName;
        itemName.style.display = empty || texture != null ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void RefreshSlotStates()
    {
        if (hotbarSlotsHost == null)
            return;

        for (int index = 0; index < hotbarSlotsHost.childCount; index++)
        {
            Button button = hotbarSlotsHost[index] as Button;
            if (button != null)
                RefreshHotbarSlot(button, index);
        }
    }

    private void RefreshAdditionalSlotStates()
    {
        if (additionalContentHost == null)
            return;

        for (int index = 0; index < additionalContentHost.childCount; index++)
        {
            Button button = additionalContentHost[index] as Button;
            InventorySlotData slot = inventory != null && index < inventory.AdditionalSlots.Count ? inventory.AdditionalSlots[index] : null;
            if (button != null)
                RefreshAdditionalSlot(button, slot, index);
        }
    }

    private void RefreshChestSlot(Button button, int index)
    {
        InventorySlotData slot = activeChest == null ? null : activeChest.GetSlot(index);
        RefreshSlot(button, slot, activeChest != null, false);
    }

    private void HandleSelectionChanged(int index)
    {
        RefreshSlotStates();
    }

    private void ApplyPanelVisibility()
    {
        if (additionalPanel != null)
            additionalPanel.EnableInClassList(AdditionalPanelVisibleClass, additionalInventoryVisible);
        if (panelTitle != null)
            panelTitle.text = craftingVisible ? "Crafting" : activeChest == null ? "Inventory" : "Chest";
        if (additionalScroll != null)
            additionalScroll.style.display = craftingVisible ? DisplayStyle.None : DisplayStyle.Flex;
        if (additionalContentHost != null)
            additionalContentHost.style.display = craftingVisible ? DisplayStyle.None : DisplayStyle.Flex;
        if (craftingContentHost != null)
        {
            craftingContentHost.EnableInClassList(CraftingVisibleClass, craftingVisible);
            craftingContentHost.style.display = craftingVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }
        if (chestPanel != null)
            chestPanel.style.display = !craftingVisible && activeChest != null ? DisplayStyle.Flex : DisplayStyle.None;

    }

    private void CaptureAndShowCursor()
    {
        if (!cursorStateCaptured)
        {
            previousCursorLockState = UnityEngine.Cursor.lockState;
            previousCursorVisible = UnityEngine.Cursor.visible;
            cursorStateCaptured = true;
        }

        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
    }

    private void RestoreCursorState()
    {
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
        cursorStateCaptured = false;
    }

    private static Button CreateSlotButton(string className)
    {
        Button button = new Button
        {
            name = "inventory-slot-button"
        };
        button.AddToClassList(className);
        Image icon = new Image { name = "inventory-slot-icon", scaleMode = ScaleMode.ScaleToFit };
        icon.AddToClassList(IconClass);
        Label count = new Label { name = "inventory-slot-count" };
        count.AddToClassList(CountClass);
        Label itemName = new Label { name = "inventory-slot-name" };
        itemName.AddToClassList(ItemNameClass);
        button.Add(icon);
        button.Add(count);
        button.Add(itemName);
        return button;
    }

    private static Label CreateKeyLabel(string text)
    {
        Label label = new Label(text)
        {
            name = "inventory-slot-key"
        };
        label.AddToClassList(KeyLabelClass);
        return label;
    }

    private static string GetKeyLabel(int index)
    {
        return index == 9 ? "0" : (index + 1).ToString();
    }
}
