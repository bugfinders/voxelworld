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
    public const string AdditionalPanelVisibleClass = "inventory-additional-panel--visible";

    private const string RootName = "inventory-ui-root";
    private const string HotbarSlotsName = "inventory-hotbar-slots";
    private const string AdditionalPanelName = "inventory-additional-panel";
    private const string PanelToggleName = "inventory-panel-toggle";
    private const string AdditionalContentName = "inventory-additional-content";

    public Key additionalInventoryToggleKey = Key.Tab;

    private UIDocument document;
    private VisualElement root;
    private VisualElement hotbarSlotsHost;
    private VisualElement additionalPanel;
    private Button panelToggle;
    private VisualElement additionalContentHost;
    private ChunkedVoxelTerrain terrain;
    private VoxelInventory inventory;
    private VisualElement registeredRoot;
    private bool additionalInventoryVisible;
    private bool cursorStateCaptured;
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;
    private int selectedAdditionalSlotIndex = -1;
    private readonly List<Button> registeredHotbarButtons = new List<Button>();

    public bool AdditionalInventoryVisible => additionalInventoryVisible;

    private void Awake()
    {
        document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        if (document == null)
            document = GetComponent<UIDocument>();

        Refresh();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[additionalInventoryToggleKey].wasPressedThisFrame)
            ToggleAdditionalInventory();

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

        if (Time.frameCount % 10 == 0 && (inventory == null || !inventory.IsInitialized || hotbarSlotsHost == null || additionalPanel == null || panelToggle == null || additionalContentHost == null))
            Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeFromInventory();
        RestoreCursorState();
    }

    private void OnDestroy()
    {
        UnsubscribeFromInventory();
        RestoreCursorState();
    }

    /// <summary>
    /// Opens or closes the additional twenty-slot inventory panel.
    /// </summary>
    public void ToggleAdditionalInventory()
    {
        additionalInventoryVisible = !additionalInventoryVisible;
        if (additionalInventoryVisible)
            CaptureAndShowCursor();
        else
            RestoreCursorState();

        ApplyPanelVisibility();
    }

    /// <summary>
    /// Selects a visible hotbar slot through the inventory model.
    /// </summary>
    public void SelectHotbarSlot(int index)
    {
        if (inventory == null || !inventory.IsInitialized || index < 0 || index >= VoxelInventory.HotbarSlotCount)
            return;

        inventory.SelectSlot(index);
        RefreshSlotStates();
    }

    /// <summary>
    /// Rebinds the UI to the terrain inventory and refreshes generated slots.
    /// </summary>
    public void Refresh()
    {
        if (document == null)
            return;

        VisualElement documentRoot = document.rootVisualElement;
        if (documentRoot == null)
            return;

        if (root != documentRoot || hotbarSlotsHost == null || additionalPanel == null || panelToggle == null || additionalContentHost == null)
        {
            root = documentRoot.Q<VisualElement>(RootName) ?? documentRoot;
            hotbarSlotsHost = root.Q<VisualElement>(HotbarSlotsName);
            additionalPanel = root.Q<VisualElement>(AdditionalPanelName);
            panelToggle = root.Q<Button>(PanelToggleName);
            additionalContentHost = root.Q<VisualElement>(AdditionalContentName);
            registeredRoot = null;
            registeredHotbarButtons.Clear();
        }

        EnsureVisualTreeHosts(documentRoot);
        RegisterCallbacks();
        ResolveInventory();
        GenerateHotbarSlots();
        GenerateAdditionalSlots();
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

        if (panelToggle == null)
        {
            panelToggle = root.Q<Button>(PanelToggleName);
            if (panelToggle == null)
            {
                panelToggle = new Button { name = PanelToggleName, text = "Open (Tab)" };
                panelToggle.AddToClassList("inventory-panel-toggle");
                additionalPanel.Add(panelToggle);
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
    }

    private void RegisterCallbacks()
    {
        if (registeredRoot == root)
            return;

        if (panelToggle != null)
            panelToggle.clicked += ToggleAdditionalInventory;

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

    private void UnsubscribeFromInventory()
    {
        if (inventory == null)
            return;

        inventory.InventoryChanged -= Refresh;
        inventory.SelectionChanged -= HandleSelectionChanged;
        inventory = null;
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

                    SelectHotbarSlot(capturedIndex);
                    evt.StopPropagation();
                });
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
        selectedAdditionalSlotIndex = -1;
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

                selectedAdditionalSlotIndex = capturedIndex;
                RefreshAdditionalSlotStates();
                evt.StopPropagation();
            });
            additionalContentHost.Add(button);
            RefreshAdditionalSlot(button, slot, index);
        }
    }

    private Button CreateSlotButton(string className)
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

    private Label CreateKeyLabel(string text)
    {
        Label label = new Label(text)
        {
            name = "inventory-slot-key"
        };
        label.AddToClassList(KeyLabelClass);
        return label;
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

    private void HandleSelectionChanged(int index)
    {
        RefreshSlotStates();
    }

    private void ApplyPanelVisibility()
    {
        if (additionalPanel != null)
            additionalPanel.EnableInClassList(AdditionalPanelVisibleClass, additionalInventoryVisible);
        if (panelToggle != null)
            panelToggle.text = additionalInventoryVisible ? "Close" : "Open (Tab)";
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
        if (!cursorStateCaptured)
            return;

        UnityEngine.Cursor.lockState = previousCursorLockState;
        UnityEngine.Cursor.visible = previousCursorVisible;
        cursorStateCaptured = false;
    }

    private static string GetKeyLabel(int index)
    {
        return index == 9 ? "0" : (index + 1).ToString();
    }
}
