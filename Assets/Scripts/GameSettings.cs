using UnityEngine;

public static class GameSettings
{
    private const string VolumeKey = "cubeits.settings.volume";
    private const string MouseSensitivityKey = "cubeits.settings.mouseSensitivity";
    private const string FullscreenKey = "cubeits.settings.fullscreen";

    public static float Volume => PlayerPrefs.GetFloat(VolumeKey, 1f);
    public static float MouseSensitivity => PlayerPrefs.GetFloat(MouseSensitivityKey, 2f);
    public static bool Fullscreen => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;

    public static void Apply(float volume, float mouseSensitivity, bool fullscreen)
    {
        AudioListener.volume = Mathf.Clamp01(volume);
        Screen.fullScreen = fullscreen;
        PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
        PlayerPrefs.SetFloat(MouseSensitivityKey, Mathf.Clamp(mouseSensitivity, 0.5f, 6f));
        PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void ApplyToPlayer(VoxelPlayerController player)
    {
        if (player != null)
            player.SetMouseSensitivity(MouseSensitivity);
        AudioListener.volume = Volume;
    }
}
