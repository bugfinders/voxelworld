using System.Collections;
using UnityEngine;

using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public sealed class StartMenuController : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "default";

    private UIDocument document;
    private Button newButton;
    private Button loadButton;
    private Button settingsButton;
    private Button saveButton;
    private Button closeSettingsButton;
    private VisualElement settingsPanel;
    private Label statusLabel;
    private Slider volumeSlider;
    private Slider sensitivitySlider;
    private Toggle fullscreenToggle;

    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        VisualElement root = document.rootVisualElement;
        newButton = root.Q<Button>("start-new");
        loadButton = root.Q<Button>("start-load");
        settingsButton = root.Q<Button>("start-settings");
        saveButton = root.Q<Button>("start-save");
        closeSettingsButton = root.Q<Button>("settings-close");
        settingsPanel = root.Q<VisualElement>("settings-panel");
        statusLabel = root.Q<Label>("start-status");
        volumeSlider = root.Q<Slider>("settings-volume");
        sensitivitySlider = root.Q<Slider>("settings-sensitivity");
        fullscreenToggle = root.Q<Toggle>("settings-fullscreen");

        newButton.clicked += StartNewGame;
        loadButton.clicked += LoadGame;
        settingsButton.clicked += OpenSettings;
        saveButton.clicked += ShowSaveUnavailableMessage;
        closeSettingsButton.clicked += CloseSettings;
        volumeSlider.RegisterValueChangedCallback(OnSettingsChanged);
        sensitivitySlider.RegisterValueChangedCallback(OnSettingsChanged);
        fullscreenToggle.RegisterValueChangedCallback(OnSettingsChanged);
        LoadSettingsIntoControls();
        RefreshSaveButtons();
    }

    private IEnumerator Start()
    {
        yield return null;
        if (!WorldSnapshotCapture.HasSnapshot && !WorldSnapshotCapture.CaptureInProgress)
            WorldSnapshotCapture.RequestStartMenuCapture(gameplaySceneName);
    }

    private void OnDisable()
    {
        if (loadButton != null) loadButton.clicked -= LoadGame;
        if (settingsButton != null) settingsButton.clicked -= OpenSettings;
        if (saveButton != null) saveButton.clicked -= ShowSaveUnavailableMessage;
        if (closeSettingsButton != null) closeSettingsButton.clicked -= CloseSettings;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && settingsPanel != null && settingsPanel.style.display == DisplayStyle.Flex)
            CloseSettings();
    }

    private void StartNewGame()
    {
        GameSettings.Apply(volumeSlider.value, sensitivitySlider.value, fullscreenToggle.value);
        WorldSaveManager.RequestNewGame();
        SceneManager.LoadScene(gameplaySceneName);
    }

    private void LoadGame()
    {
        if (!WorldSaveManager.HasSaveFile())
        {
            RefreshSaveButtons();
            return;
        }

        GameSettings.Apply(volumeSlider.value, sensitivitySlider.value, fullscreenToggle.value);
        WorldSaveManager.RequestLoadGame();
        SceneManager.LoadScene(gameplaySceneName);
    }

    private void OpenSettings()
    {
        settingsPanel.style.display = DisplayStyle.Flex;
    }

    private void CloseSettings()
    {
        GameSettings.Apply(volumeSlider.value, sensitivitySlider.value, fullscreenToggle.value);
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

    private void RefreshSaveButtons()
    {
        bool hasSave = WorldSaveManager.HasSaveFile();
        loadButton.SetEnabled(hasSave);
        saveButton.SetEnabled(hasSave);
        statusLabel.text = hasSave ? "A saved world is ready to load." : "No save file found. Load is disabled until you save a world.";
    }

    private void ShowSaveUnavailableMessage()
    {
        statusLabel.text = WorldSaveManager.HasSaveFile()
            ? "The latest saved world is already stored. Load it to continue playing."
            : "Save is available from the in-game menu after starting a world.";
    }
}
