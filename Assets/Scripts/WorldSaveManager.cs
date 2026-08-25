using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public sealed class WorldSaveManager : MonoBehaviour
{
    private const int SaveMagic = 0x43554245;
    private const int SaveVersion = 6;
    private const int MaxHarvestedItemCount = 10000;
    private const string SaveFileName = "cubeits_world.save";
    private const float DefaultTimeOfDay = 0.25f;
    private const float MinimumDayDurationSeconds = 10f;
    private const float DebugMidnightTime = 0f;
    private const float DayReflectionIntensity = 1f;
    private const float NightReflectionIntensity = 0.08f;
    private const float NightSkyExposure = 0.02f;
    private const float DaySkyExposure = 1f;
    private const float NightAtmosphereThickness = 1.5f;
    private const float DayAtmosphereThickness = 1f;
    private const float NightSkySunSize = 0.005f;
    private const float DaySkySunSize = 0.04f;

    private static bool loadRequested;

    [SerializeField] private ChunkedVoxelTerrain terrain;
    [SerializeField] private Transform player;
    [SerializeField] private Light sun;
    [SerializeField] private Light moon;
    [SerializeField] private float dayDurationSeconds = 600f;
    [SerializeField, Range(0f, 1f)] private float timeOfDay = DefaultTimeOfDay;
    [SerializeField] private float daySunIntensity = 1f;
    [SerializeField] private float nightMoonIntensity = 0.3f;
    [SerializeField] private float dayAmbientIntensity = 1f;
    [SerializeField] private float nightAmbientIntensity = 0.2f;
    [SerializeField, Range(0f, 1f)] private float nightGrassBrightness = 0.4f;
    [SerializeField, Range(0f, 1f)] private float nightGroundBrightness = 0.25f;
    [SerializeField, Range(0f, 1f)] private float nightTreeBrightness = 0.5f;
    [SerializeField] private float sunYaw = -30f;
    private Camera playerCamera;
    private Material runtimeSkybox;
    private GameObject runtimeMoonObject;

    private static readonly Color DaySunColor = new Color(1f, 0.956f, 0.839f);
    private static readonly Color NightSunColor = new Color(0.22f, 0.3f, 0.5f);
    private static readonly Color DaySkyAmbientColor = new Color(0.55f, 0.68f, 0.9f);
    private static readonly Color DayEquatorAmbientColor = new Color(0.35f, 0.45f, 0.62f);
    private static readonly Color DayGroundAmbientColor = new Color(0.2f, 0.24f, 0.3f);
    private static readonly Color NightSkyAmbientColor = new Color(0.015f, 0.025f, 0.06f);
    private static readonly Color NightEquatorAmbientColor = new Color(0.008f, 0.012f, 0.025f);
    private static readonly Color NightGroundAmbientColor = new Color(0.004f, 0.006f, 0.012f);
    private static readonly Color DaySkyboxTint = new Color(0.45f, 0.65f, 1f);
    private static readonly Color NightSkyboxTint = new Color(0.01f, 0.015f, 0.04f);
    private static readonly Color DaySkyboxGroundColor = new Color(0.35f, 0.42f, 0.55f);
    private static readonly Color NightSkyboxGroundColor = new Color(0.005f, 0.008f, 0.015f);

    private bool isReady;

    public static bool HasPendingWorldSeed { get; private set; }
    public static int PendingWorldSeed { get; private set; }

    public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public bool IsReady => isReady;
    public static bool HasSaveFile()
    {
        return File.Exists(SavePath) && new FileInfo(SavePath).Length > 0;
    }

    public static void RequestNewGame()
    {
        loadRequested = false;
        PendingWorldSeed = Guid.NewGuid().GetHashCode();
        HasPendingWorldSeed = true;
        // Debug.Log($"Cubeits new world seed: {PendingWorldSeed}");
    }

    public static void RequestLoadGame()
    {
        loadRequested = HasSaveFile();
        HasPendingWorldSeed = false;
    }

    private IEnumerator Start()
    {
        if (terrain == null)
            terrain = FindFirstObjectByType<ChunkedVoxelTerrain>();
        if (player == null && terrain != null)
            player = terrain.player;
        if (playerCamera == null && player != null)
            playerCamera = player.GetComponentInChildren<Camera>(true);
        if (sun == null)
            sun = GameObject.Find("Directional Light")?.GetComponent<Light>();
        if (moon == null)
        {
            GameObject moonObject = GameObject.Find("Moon Light");
            moon = moonObject == null ? CreateRuntimeMoon() : moonObject.GetComponent<Light>();
        }
        InitializeSkybox();

        while (terrain == null || !terrain.IsInitialized || terrain.Inventory == null || !terrain.Inventory.IsInitialized)
            yield return null;

        isReady = true;
        ApplyLighting();
        GameSettings.ApplyToPlayer(player == null ? null : player.GetComponent<VoxelPlayerController>());

        if (loadRequested)
        {
            loadRequested = false;
            if (!LoadWorld())
                Debug.LogWarning("Cubeits could not load the save file. The generated world will be used.");
        }
    }

    private void Update()
    {
        if (!isReady)
            return;

        // TEMPORARY DEBUG SHORTCUT: remove after day/night tuning is complete.
        if (Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
            SetTimeOfDay(DebugMidnightTime);

        float duration = Mathf.Max(MinimumDayDurationSeconds, dayDurationSeconds);
        timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime / duration, 1f);
        ApplyLighting();
    }

    /// <summary>
    /// Gets the current normalized time of day, where 0.25 is 6:00 AM and 0.75 is 6:00 PM.
    /// </summary>
    public float TimeOfDay => timeOfDay;

    /// <summary>
    /// Sets the normalized time of day and immediately applies its lighting state.
    /// </summary>
    public void SetTimeOfDay(float normalizedTime)
    {
        timeOfDay = Mathf.Repeat(normalizedTime, 1f);
        ApplyLighting();
    }

    private Light CreateRuntimeMoon()
    {
        runtimeMoonObject = new GameObject("Moon Light (Runtime)");
        Light runtimeMoon = runtimeMoonObject.AddComponent<Light>();
        runtimeMoon.type = LightType.Directional;
        runtimeMoon.color = NightSunColor;
        runtimeMoon.intensity = 0f;
        runtimeMoon.shadows = LightShadows.None;
        return runtimeMoon;
    }

    private void InitializeSkybox()
    {
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;

        Shader proceduralSkyShader = Shader.Find("Skybox/Procedural");
        if (proceduralSkyShader == null)
            return;

        runtimeSkybox = new Material(proceduralSkyShader)
        {
            name = "Cubeits Day Night Skybox (Runtime)"
        };
        RenderSettings.skybox = runtimeSkybox;
    }

    private void ApplyLighting()
    {
        float daylight = Mathf.Clamp01(Mathf.Sin((timeOfDay - 0.25f) * Mathf.PI * 2f));
        float transition = daylight * daylight * (3f - 2f * daylight);
        float moonlight = Mathf.Clamp01(-Mathf.Sin((timeOfDay - 0.25f) * Mathf.PI * 2f));
        float moonTransition = moonlight * moonlight * (3f - 2f * moonlight);
        float sunAngle = timeOfDay * 360f - 90f;

        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(sunAngle, sunYaw, 0f);
            sun.intensity = Mathf.Lerp(0f, daySunIntensity, transition);
            sun.color = DaySunColor;
        }

        if (moon != null)
        {
            moon.transform.rotation = Quaternion.Euler(sunAngle + 180f, sunYaw, 0f);
            moon.intensity = Mathf.Lerp(0f, nightMoonIntensity, moonTransition);
            moon.color = NightSunColor;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientIntensity = Mathf.Lerp(nightAmbientIntensity, dayAmbientIntensity, transition);
        RenderSettings.ambientSkyColor = Color.Lerp(NightSkyAmbientColor, DaySkyAmbientColor, transition);
        RenderSettings.ambientEquatorColor = Color.Lerp(NightEquatorAmbientColor, DayEquatorAmbientColor, transition);
        RenderSettings.ambientGroundColor = Color.Lerp(NightGroundAmbientColor, DayGroundAmbientColor, transition);
        RenderSettings.reflectionIntensity = Mathf.Lerp(NightReflectionIntensity, DayReflectionIntensity, transition);
        if (terrain != null)
        {
            float grassBrightness = Mathf.Lerp(nightGrassBrightness, 1f, transition);
            float groundBrightness = Mathf.Lerp(nightGroundBrightness, 1f, transition);
            float treeBrightness = Mathf.Lerp(nightTreeBrightness, 1f, transition);
            terrain.SetNightMaterialBrightness(grassBrightness, groundBrightness, treeBrightness);
        }
        ApplySkybox(transition);
    }

    private void ApplySkybox(float transition)
    {
        if (runtimeSkybox == null)
            return;

        runtimeSkybox.SetColor("_SkyTint", Color.Lerp(NightSkyboxTint, DaySkyboxTint, transition));
        runtimeSkybox.SetColor("_GroundColor", Color.Lerp(NightSkyboxGroundColor, DaySkyboxGroundColor, transition));
        runtimeSkybox.SetFloat("_Exposure", Mathf.Lerp(NightSkyExposure, DaySkyExposure, transition));
        runtimeSkybox.SetFloat("_AtmosphereThickness", Mathf.Lerp(NightAtmosphereThickness, DayAtmosphereThickness, transition));
        runtimeSkybox.SetFloat("_SunSize", Mathf.Lerp(NightSkySunSize, DaySkySunSize, transition));
    }

    private void OnDestroy()
    {
        if (runtimeSkybox != null)
            Destroy(runtimeSkybox);
    }

    public bool SaveWorld()
    {
        if (!isReady || terrain == null || player == null || terrain.Inventory == null)
            return false;

        string temporaryPath = SavePath + ".tmp";
        try
        {
            using (FileStream stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(SaveMagic);
                writer.Write(SaveVersion);
                writer.Write(ChunkedVoxelTerrain.SIZE_X);
                writer.Write(ChunkedVoxelTerrain.MAX_HEIGHT + 1);
                writer.Write(ChunkedVoxelTerrain.SIZE_Z);
                writer.Write(terrain.WorldSeed);
                terrain.WriteVoxelState(writer);
                WriteVector3(writer, player.position);
                WriteVector3(writer, player.eulerAngles);
                if (playerCamera != null)
                {
                    writer.Write(true);
                    WriteVector3(writer, playerCamera.transform.localPosition);
                    WriteVector3(writer, playerCamera.transform.localEulerAngles);
                }
                else
                {
                    writer.Write(false);
                }
                writer.Write(timeOfDay);
                WriteInventory(writer, terrain.Inventory);
                terrain.WriteChestInventories(writer);
                writer.Flush();
            }

            if (File.Exists(SavePath))
                File.Delete(SavePath);
            File.Move(temporaryPath, SavePath);
            // Debug.Log("Cubeits world saved.");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Cubeits save failed: {exception.Message}");
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
            return false;
        }
    }

    public bool LoadWorld()
    {
        if (!isReady || terrain == null || player == null || terrain.Inventory == null || !HasSaveFile())
            return false;

        try
        {
            using (System.IO.FileStream stream = new System.IO.FileStream(SavePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != SaveMagic)
                    throw new InvalidDataException("Invalid save file.");

                int savedVersion = reader.ReadInt32();
                if (savedVersion != 3 && savedVersion != 4 && savedVersion != 5 && savedVersion != SaveVersion)
                    throw new InvalidDataException("Unsupported save version.");

                int width = reader.ReadInt32();
                int height = reader.ReadInt32();
                int depth = reader.ReadInt32();
                if (width != ChunkedVoxelTerrain.SIZE_X || height != ChunkedVoxelTerrain.MAX_HEIGHT + 1 || depth != ChunkedVoxelTerrain.SIZE_Z)
                    throw new InvalidDataException("Save dimensions do not match this world.");

                int savedSeed = reader.ReadInt32();
                terrain.SetWorldSeed(savedSeed);
                if (!terrain.ReadVoxelState(reader, width, height, depth))
                    throw new InvalidDataException("Voxel data is invalid.");

                player.position = ReadVector3(reader);
                player.eulerAngles = ReadVector3(reader);
                if (reader.ReadBoolean())
                {
                    if (playerCamera == null)
                        throw new InvalidDataException("Save contains a camera pose but the player camera is missing.");
                    playerCamera.transform.localPosition = ReadVector3(reader);
                    playerCamera.transform.localEulerAngles = ReadVector3(reader);
                }
                if (savedVersion >= 6)
                    SetTimeOfDay(reader.ReadSingle());
                ReadInventory(reader, terrain.Inventory, savedVersion >= 4);
                if (savedVersion >= SaveVersion)
                    terrain.ReadChestInventories(reader);
                VoxelPlayerController playerController = player.GetComponent<VoxelPlayerController>();
                if (playerController != null)
                    playerController.SyncLookToTransforms();
            }

            // Debug.Log("Cubeits world loaded.");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Cubeits load failed: {exception.Message}");
            return false;
        }
    }

    private static void WriteInventory(BinaryWriter writer, VoxelInventory inventory)
    {
        writer.Write(VoxelInventory.HotbarSlotCount);
        for (int i = 0; i < VoxelInventory.HotbarSlotCount; i++)
            WriteSlot(writer, inventory.GetHotbarSlot(i));

        writer.Write(VoxelInventory.AdditionalSlotCount);
        IReadOnlyList<InventorySlotData> additionalSlots = inventory.AdditionalSlots;
        for (int i = 0; i < VoxelInventory.AdditionalSlotCount; i++)
            WriteSlot(writer, additionalSlots[i]);

        writer.Write(inventory.SelectedSlotIndex);
        IReadOnlyCollection<string> harvestedItemIds = inventory.HarvestedItemIds;
        writer.Write(harvestedItemIds.Count);
        foreach (string itemId in harvestedItemIds)
            writer.Write(itemId ?? string.Empty);
    }

    private static void ReadInventory(BinaryReader reader, VoxelInventory inventory, bool includesHarvestHistory)
    {
        int hotbarCount = reader.ReadInt32();
        if (hotbarCount != VoxelInventory.HotbarSlotCount)
            throw new InvalidDataException("Save hotbar size does not match this game.");

        List<InventorySlotSaveState> hotbar = new List<InventorySlotSaveState>(hotbarCount);
        for (int i = 0; i < hotbarCount; i++)
            hotbar.Add(ReadSlot(reader));

        int additionalCount = reader.ReadInt32();
        if (additionalCount != VoxelInventory.AdditionalSlotCount)
            throw new InvalidDataException("Save inventory size does not match this game.");

        List<InventorySlotSaveState> additional = new List<InventorySlotSaveState>(additionalCount);
        for (int i = 0; i < additionalCount; i++)
            additional.Add(ReadSlot(reader));

        inventory.RestoreState(hotbar, additional, reader.ReadInt32());
        if (!includesHarvestHistory)
            return;

        int harvestedCount = reader.ReadInt32();
        if (harvestedCount < 0 || harvestedCount > MaxHarvestedItemCount)
            throw new InvalidDataException("Save contains an invalid harvested-item count.");

        List<string> harvestedItemIds = new List<string>(harvestedCount);
        for (int i = 0; i < harvestedCount; i++)
            harvestedItemIds.Add(reader.ReadString());
        inventory.RestoreHarvestedItemIds(harvestedItemIds);
    }

    private static void WriteSlot(BinaryWriter writer, InventorySlotData slot)
    {
        writer.Write(slot == null ? string.Empty : slot.ItemId ?? string.Empty);
        writer.Write(slot == null ? string.Empty : slot.DisplayName ?? string.Empty);
        writer.Write(slot == null ? 0 : Mathf.Max(0, slot.Count));
    }

    private static InventorySlotSaveState ReadSlot(BinaryReader reader)
    {
        string itemId = reader.ReadString();
        string displayName = reader.ReadString();
        int count = reader.ReadInt32();
        if (count < 0)
            throw new InvalidDataException("Save contains a negative inventory count.");
        return new InventorySlotSaveState(itemId, displayName, count);
    }

    private static void WriteVector3(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.x);
        writer.Write(value.y);
        writer.Write(value.z);
    }

    private static Vector3 ReadVector3(BinaryReader reader)
    {
        return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }

    public void ReturnToStartMenu()
    {
        RequestNewGame();
        Time.timeScale = 1f;
        SceneManager.LoadScene("StartScene");
    }
}
