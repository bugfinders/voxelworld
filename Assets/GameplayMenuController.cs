using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public sealed class GameplayMenuController : MonoBehaviour
{
    [SerializeField] private WorldSaveManager saveManager;
    [SerializeField] private VoxelPlayerController playerController;

    private UIDocument document;
    private VisualElement menuRoot;
    private VisualElement menuPanel;
    private VisualElement settingsPanel;
    private Button resumeButton;
    private Button saveButton;
    private Button newButton;
    private Button settingsButton;
    private Button closeSettingsButton;
    private Label statusLabel;
    private Slider volumeSlider;
    private Slider sensitivitySlider;
    private Toggle fullscreenToggle;
    private bool isOpen;

    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        VisualElement root = document.rootVisualElement;
        menuRoot = root.Q<VisualElement>("game-root");
        menuPanel = root.Q<VisualElement>("game-menu-panel");
        settingsPanel = root.Q<VisualElement>("game-settings-panel");
        resumeButton = root.Q<Button>("game-resume");
        saveButton = root.Q<Button>("game-save");
        newButton = root.Q<Button>("game-new");
        settingsButton = root.Q<Button>("game-settings");
        closeSettingsButton = root.Q<Button>("game-settings-close");
        statusLabel = root.Q<Label>("game-status");
        volumeSlider = root.Q<Slider>("game-settings-volume");
        sensitivitySlider = root.Q<Slider>("game-settings-sensitivity");
        fullscreenToggle = root.Q<Toggle>("game-settings-fullscreen");

        resumeButton.clicked += CloseMenu;
        saveButton.clicked += SaveGame;
        newButton.clicked += NewGame;
        settingsButton.clicked += OpenSettings;
        closeSettingsButton.clicked += CloseSettings;
        volumeSlider.RegisterValueChangedCallback(OnSettingsChanged);
        sensitivitySlider.RegisterValueChangedCallback(OnSettingsChanged);
        fullscreenToggle.RegisterValueChangedCallback(OnSettingsChanged);
        LoadSettingsIntoControls();
        SetMenuVisible(false);
    }

    private void OnDisable()
    {
        if (resumeButton != null) resumeButton.clicked -= CloseMenu;
        if (saveButton != null) saveButton.clicked -= SaveGame;
        if (newButton != null) newButton.clicked -= NewGame;
        if (settingsButton != null) settingsButton.clicked -= OpenSettings;
        if (closeSettingsButton != null) closeSettingsButton.clicked -= CloseSettings;
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isOpen)
                CloseMenu();
            else
                OpenMenu();
        }
    }

    private void OpenMenu()
    {
        SetMenuVisible(true);
        UnityEngine.Cursor.visible = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        if (playerController != null)
            playerController.enabled = false;
    }

    private void CloseMenu()
    {
        settingsPanel.style.display = DisplayStyle.None;
        SetMenuVisible(false);
        UnityEngine.Cursor.visible = false;
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        if (playerController != null)
            playerController.enabled = true;
    }

    private void SetMenuVisible(bool visible)
    {
        isOpen = visible;
        if (menuRoot != null)
            menuRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        menuPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (!visible)
            settingsPanel.style.display = DisplayStyle.None;
    }

    private void SaveGame()
    {
        if (saveManager != null && saveManager.SaveWorld())
            statusLabel.text = "World saved.";
        else
            statusLabel.text = "The world is still initializing.";
    }

    private void NewGame()
    {
        if (saveManager != null)
            saveManager.ReturnToStartMenu();
    }

    private void OpenSettings()
    {
        settingsPanel.style.display = DisplayStyle.Flex;
    }

    private void CloseSettings()
    {
        GameSettings.Apply(volumeSlider.value, sensitivitySlider.value, fullscreenToggle.value);
        GameSettings.ApplyToPlayer(playerController);
        settingsPanel.style.display = DisplayStyle.None;
    }

    private void OnSettingsChanged(ChangeEvent<float> changeEvent)
    {
        AudioListener.volume = Mathf.Clamp01(volumeSlider.value);
    }

    private void OnSettingsChanged(ChangeEvent<bool> changeEvent)
    {
        Screen.fullScreen = fullscreenToggle.value;
    }

    private void LoadSettingsIntoControls()
    {
        volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
        sensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
        fullscreenToggle.SetValueWithoutNotify(GameSettings.Fullscreen);
        settingsPanel.style.display = DisplayStyle.None;
    }
}
