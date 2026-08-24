using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WorldSaveManager : MonoBehaviour
{
    private const int SaveMagic = 0x43554245;
    private const int SaveVersion = 5;
    private const int MaxHarvestedItemCount = 10000;
    private const string SaveFileName = "cubeits_world.save";

    private static bool loadRequested;

    [SerializeField] private ChunkedVoxelTerrain terrain;
    [SerializeField] private Transform player;
    private Camera playerCamera;

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

        while (terrain == null || !terrain.IsInitialized || terrain.Inventory == null || !terrain.Inventory.IsInitialized)
            yield return null;

        isReady = true;
        GameSettings.ApplyToPlayer(player == null ? null : player.GetComponent<VoxelPlayerController>());

        if (loadRequested)
        {
            loadRequested = false;
            if (!LoadWorld())
                Debug.LogWarning("Cubeits could not load the save file. The generated world will be used.");
        }
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
                if (savedVersion != 3 && savedVersion != 4 && savedVersion != SaveVersion)
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
