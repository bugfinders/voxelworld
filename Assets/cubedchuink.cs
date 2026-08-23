using System;
using System.Collections;

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class ChunkedVoxelTerrain : MonoBehaviour
{
    public Material[] materials;
    [SerializeField] private PlaceableItemAsset[] placeableItems = new PlaceableItemAsset[0];
    [SerializeField] private TerrainGenerationProfile generationProfile;
    public float[] materialWeights = { 0f, 45f, 25f, 10f, 7f, 6f, 3f, 2f, 1f, 0.2f, 0.8f, 0f, 0f, 0f, 0f, 0f, 0f };
    public int grassMaterialIndex = 0;
    public Transform player;
    public Camera cam;

    private int worldSeed;
    private float[] generationMaterialWeights;


    private GameObject hoverCube;
    private LineRenderer[] hoverEdgeLines;
    private bool[,,] solid;
    private int[,,] voxelMaterials;
    private Chunk[,] chunks;
    private VoxelPlayerController control;
    private readonly Dictionary<Vector3Int, ChestInventory> chestInventories = new Dictionary<Vector3Int, ChestInventory>();

    private VoxelInventory inventory;

    public const int SIZE_X = 500;
    public const int SIZE_Z = 500;
    public const int MIN_HEIGHT = 0;
    public const int MAX_HEIGHT = 50;

    private const int CHUNK_SIZE = 25;
    private const string DIRT_MATERIAL_NAME = "Dirt";
    private const string WOOD_MATERIAL_NAME = "Wood";
    
    private const string LEAVES_MATERIAL_NAME = "Leaves";
    private const int TREE_GRID_SPACING = 16;
    private const int TREE_CLUSTER_GRID_SPACING = 72;
    private const int TREE_CLUSTER_RADIUS = 14;
    private const int TREE_CLUSTER_MIN_COUNT = 3;
    private const int TREE_CLUSTER_MAX_COUNT = 6;
    private const int TREE_TRUNK_HEIGHT = 4;
    private const int TREE_CANOPY_RADIUS = 2;
    private const float TREE_SPAWN_CHANCE = 0.27f;
    private const float TREE_CLUSTER_SPAWN_CHANCE = 0.75f;
    private const int TREE_CLEARANCE_RADIUS = 5;
    private const float PLACEMENT_OVERLAP_EPSILON = 0.01f;
    private Vector3 currentVoxel = Vector3.zero;
    private Vector3Int currentPlacementCoordinate = Vector3Int.zero;
    private bool hasTarget;
    private bool hasPlacementTarget;

    public VoxelInventory Inventory => inventory;
    public int WorldSeed => worldSeed;
    public bool IsInitialized => solid != null && voxelMaterials != null && chunks != null;

    private void Awake()
    {
        worldSeed = WorldSaveManager.HasPendingWorldSeed
            ? WorldSaveManager.PendingWorldSeed
            : generationProfile == null ? 0 : generationProfile.Seed;
        if (!ValidateMaterials())
            return;

        inventory = GetComponent<VoxelInventory>();
        if (inventory == null)
            inventory = gameObject.AddComponent<VoxelInventory>();
        inventory.Initialize(materials, placeableItems);
    }

    private IEnumerator Start()
    {
        control = player.gameObject.GetComponent<VoxelPlayerController>();
        if (control == null)
            Debug.LogError("Player has no VoxelPlayerController.");

        if (!inventory || !inventory.IsInitialized)
            yield break;

        if (control != null)
            control.enabled = false;

        CreateHoverCube();
        GenerateSolidGrid();
        CreateChunks();
        BuildAllChunks();
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();
        MovePlayerToTopSurface();
        Physics.SyncTransforms();

        if (control != null)
            control.enabled = true;

        Debug.Log("Voxel terrain initialized.");
    }

    private bool ValidateMaterials()
    {
        if (materials == null || materials.Length == 0)
        {
            Debug.LogError("ChunkedVoxelTerrain requires at least one material in the materials array.");
            return false;
        }

        if (grassMaterialIndex < 0 || grassMaterialIndex >= materials.Length)
            grassMaterialIndex = 0;

        float[] configuredWeights = generationProfile != null && generationProfile.HasMaterialWeights && generationProfile.MaterialWeights.Length == materials.Length
            ? generationProfile.MaterialWeights
            : materialWeights;

        if (configuredWeights == null || configuredWeights.Length != materials.Length)
        {
            configuredWeights = new float[materials.Length];
            for (int i = 0; i < configuredWeights.Length; i++)
                configuredWeights[i] = i == grassMaterialIndex ? 0f : 1f;
        }

        generationMaterialWeights = new float[materials.Length];
        for (int i = 0; i < generationMaterialWeights.Length; i++)
            generationMaterialWeights[i] = Mathf.Max(0f, configuredWeights[i]);

        generationMaterialWeights[grassMaterialIndex] = 0f;
        return HasPositiveSubsurfaceWeight();
    }

    private bool HasPositiveSubsurfaceWeight()
    {
        if (generationMaterialWeights == null)
            return false;

        for (int i = 0; i < generationMaterialWeights.Length; i++)
        {
            if (i != grassMaterialIndex && generationMaterialWeights[i] > 0f)
                return true;
        }

        Debug.LogError("ChunkedVoxelTerrain requires a positive weight for at least one non-grass material.");
        return false;
    }

    private void CreateHoverCube()
    {
        hoverCube = new GameObject("HoverCube");
        hoverEdgeLines = new LineRenderer[12];
        Material hoverMaterial = new Material(Shader.Find("Sprites/Default"));

        for (int i = 0; i < hoverEdgeLines.Length; i++)
        {
            GameObject edge = new GameObject($"HoverEdge_{i}");
            edge.transform.parent = hoverCube.transform;

            LineRenderer lineRenderer = edge.AddComponent<LineRenderer>();
            lineRenderer.positionCount = 2;
            lineRenderer.loop = false;
            lineRenderer.widthMultiplier = 0.03f;
            lineRenderer.material = hoverMaterial;
            lineRenderer.startColor = Color.black;
            lineRenderer.endColor = Color.black;
            hoverEdgeLines[i] = lineRenderer;
        }

        hoverCube.SetActive(false);
    }

    private void SetHoverCube(Vector3 voxelPosition)
    {
        hoverCube.SetActive(true);

        const float edgeOffset = 0.01f;
        int voxelX = Mathf.FloorToInt(voxelPosition.x);
        int voxelY = Mathf.FloorToInt(voxelPosition.y);
        int voxelZ = Mathf.FloorToInt(voxelPosition.z);

        bool IsSolidLocal(int x, int y, int z)
        {
            if (x < 0 || x >= SIZE_X) return false;
            if (y < 0 || y > MAX_HEIGHT) return false;
            if (z < 0 || z >= SIZE_Z) return false;
            return solid[x, y, z];
        }

        bool frontExposed = !IsSolidLocal(voxelX, voxelY, voxelZ - 1);
        bool backExposed = !IsSolidLocal(voxelX, voxelY, voxelZ + 1);
        bool leftExposed = !IsSolidLocal(voxelX - 1, voxelY, voxelZ);
        bool rightExposed = !IsSolidLocal(voxelX + 1, voxelY, voxelZ);
        bool topExposed = !IsSolidLocal(voxelX, voxelY + 1, voxelZ);
        bool bottomExposed = !IsSolidLocal(voxelX, voxelY - 1, voxelZ);

        Vector3 cubeCenter = new Vector3(voxelX + 0.5f, voxelY + 0.5f, voxelZ + 0.5f);
        Vector3 toCamera = (cam.transform.position - cubeCenter).normalized;
        bool frontFacing = Vector3.Dot(toCamera, Vector3.back) > 0f;
        bool backFacing = Vector3.Dot(toCamera, Vector3.forward) > 0f;
        bool leftFacing = Vector3.Dot(toCamera, Vector3.left) > 0f;
        bool rightFacing = Vector3.Dot(toCamera, Vector3.right) > 0f;
        bool topFacing = Vector3.Dot(toCamera, Vector3.up) > 0f;
        bool bottomFacing = Vector3.Dot(toCamera, Vector3.down) > 0f;

        bool frontVisible = frontExposed && frontFacing;
        bool backVisible = backExposed && backFacing;
        bool leftVisible = leftExposed && leftFacing;
        bool rightVisible = rightExposed && rightFacing;
        bool topVisible = topExposed && topFacing;
        bool bottomVisible = bottomExposed && bottomFacing;

        float minX = voxelPosition.x - edgeOffset;
        float minY = voxelPosition.y - edgeOffset;
        float minZ = voxelPosition.z - edgeOffset;
        float maxX = voxelPosition.x + 1f + edgeOffset;
        float maxY = voxelPosition.y + 1f + edgeOffset;
        float maxZ = voxelPosition.z + 1f + edgeOffset;

        Vector3 v000 = new Vector3(minX, minY, minZ);
        Vector3 v100 = new Vector3(maxX, minY, minZ);
        Vector3 v110 = new Vector3(maxX, maxY, minZ);
        Vector3 v010 = new Vector3(minX, maxY, minZ);
        Vector3 v001 = new Vector3(minX, minY, maxZ);
        Vector3 v101 = new Vector3(maxX, minY, maxZ);
        Vector3 v111 = new Vector3(maxX, maxY, maxZ);
        Vector3 v011 = new Vector3(minX, maxY, maxZ);

        Vector3[] edges = new Vector3[]
        {
            v000, v100, v100, v101, v101, v001, v001, v000,
            v010, v110, v110, v111, v111, v011, v011, v010,
            v000, v010, v100, v110, v101, v111, v001, v011
        };

        bool[] edgeVisible = new bool[]
        {
            bottomVisible || frontVisible,
            bottomVisible || rightVisible,
            bottomVisible || backVisible,
            bottomVisible || leftVisible,
            topVisible || frontVisible,
            topVisible || rightVisible,
            topVisible || backVisible,
            topVisible || leftVisible,
            frontVisible || leftVisible,
            frontVisible || rightVisible,
            backVisible || rightVisible,
            backVisible || leftVisible
        };

        for (int i = 0; i < hoverEdgeLines.Length; i++)
        {
            hoverEdgeLines[i].SetPosition(0, edges[i * 2]);
            hoverEdgeLines[i].SetPosition(1, edges[i * 2 + 1]);
            hoverEdgeLines[i].enabled = edgeVisible[i];
        }
    }

    private void GenerateSolidGrid()
    {
        solid = new bool[SIZE_X, MAX_HEIGHT + 1, SIZE_Z];
        voxelMaterials = new int[SIZE_X, MAX_HEIGHT + 1, SIZE_Z];

        for (int x = 0; x < SIZE_X; x++)
        for (int z = 0; z < SIZE_Z; z++)
        {
            float noise = GetHeightNoise(x, z);
            int surfaceHeight = Mathf.RoundToInt(Mathf.Lerp(MIN_HEIGHT, MAX_HEIGHT, noise));

            for (int y = 0; y <= surfaceHeight; y++)
            {
                solid[x, y, z] = true;
                voxelMaterials[x, y, z] = y == surfaceHeight
                    ? grassMaterialIndex
                    : GetSubsurfaceMaterialIndex(x, y, z);
            }
        }

        GenerateTrees();
    }

    private float GetHeightNoise(int x, int z)
    {
        if (generationProfile == null)
            return Mathf.PerlinNoise(x * 0.01f, z * 0.01f);

        float frequency = generationProfile.HeightNoiseScale;
        float amplitude = 1f;
        float value = 0f;
        float amplitudeTotal = 0f;
        float offsetX = GetSeedOffset(worldSeed, 701);
        float offsetZ = GetSeedOffset(worldSeed, 907);

        for (int octave = 0; octave < generationProfile.HeightOctaves; octave++)
        {
            float sampleX = (x + offsetX) * frequency;
            float sampleZ = (z + offsetZ) * frequency;
            value += Mathf.PerlinNoise(sampleX, sampleZ) * amplitude;
            amplitudeTotal += amplitude;
            amplitude *= generationProfile.HeightPersistence;
            frequency *= generationProfile.HeightLacunarity;
        }

        float normalizedNoise = amplitudeTotal <= 0f ? 0.5f : value / amplitudeTotal;
        float heightRange = 1f - generationProfile.HeightFloor;
        return Mathf.Clamp01(generationProfile.HeightFloor + normalizedNoise * generationProfile.HeightAmplitude * heightRange);
    }

    private static float GetSeedOffset(int seed, int salt)
    {
        unchecked
        {
            uint hash = (uint)seed * 374761393u;
            hash ^= (uint)salt * 668265263u;
            hash ^= hash >> 13;
            hash *= 1274126177u;
            return (hash % 100000u) * 0.01f;
        }
    }

    private void GenerateTrees()
    {
        int woodMaterialIndex = FindMaterialIndex(WOOD_MATERIAL_NAME);
        int leavesMaterialIndex = FindMaterialIndex(LEAVES_MATERIAL_NAME);
        if (woodMaterialIndex < 0 || leavesMaterialIndex < 0)
        {
            Debug.LogWarning("Trees could not be generated because Wood or Leaves is not configured.");
            return;
        }

        float treeDensityMultiplier = generationProfile == null ? 1f : generationProfile.TreeDensityMultiplier;
        float treeSpawnChance = Mathf.Clamp01(TREE_SPAWN_CHANCE * treeDensityMultiplier);
        float treeClusterSpawnChance = Mathf.Clamp01(TREE_CLUSTER_SPAWN_CHANCE * treeDensityMultiplier);

        int centerX = SIZE_X / 2;
        int centerZ = SIZE_Z / 2;
        for (int x = TREE_GRID_SPACING / 2; x < SIZE_X; x += TREE_GRID_SPACING)
        for (int z = TREE_GRID_SPACING / 2; z < SIZE_Z; z += TREE_GRID_SPACING)
        {
            if (IsNearPlayerSpawn(x, z, centerX, centerZ))
                continue;

            uint hash = GetTreeHash(x, z, 17);
            float roll = hash / (float)uint.MaxValue;
            if (roll <= treeSpawnChance)
                TryPlaceTreeAt(x, z, woodMaterialIndex, leavesMaterialIndex);
        }

        for (int x = TREE_CLUSTER_GRID_SPACING / 2; x < SIZE_X; x += TREE_CLUSTER_GRID_SPACING)
        for (int z = TREE_CLUSTER_GRID_SPACING / 2; z < SIZE_Z; z += TREE_CLUSTER_GRID_SPACING)
        {
            if (IsNearPlayerSpawn(x, z, centerX, centerZ))
                continue;

            uint clusterHash = GetTreeHash(x, z, 31);
            float clusterRoll = clusterHash / (float)uint.MaxValue;
            if (clusterRoll > treeClusterSpawnChance)
                continue;

            int treeCount = TREE_CLUSTER_MIN_COUNT + (int)(GetTreeHash(x, z, 43) % (uint)(TREE_CLUSTER_MAX_COUNT - TREE_CLUSTER_MIN_COUNT + 1));
            for (int treeIndex = 0; treeIndex < treeCount; treeIndex++)
            {
                uint positionHash = GetTreeHash(x, z, 101 + treeIndex * 2);
                int offsetX = GetHashRange(positionHash, -TREE_CLUSTER_RADIUS, TREE_CLUSTER_RADIUS);
                int offsetZ = GetHashRange(GetTreeHash(x, z, 102 + treeIndex * 2), -TREE_CLUSTER_RADIUS, TREE_CLUSTER_RADIUS);
                TryPlaceTreeAt(x + offsetX, z + offsetZ, woodMaterialIndex, leavesMaterialIndex);
            }
        }
    }

    private bool IsNearPlayerSpawn(int x, int z, int centerX, int centerZ)
    {
        return Mathf.Abs(x - centerX) < TREE_GRID_SPACING || Mathf.Abs(z - centerZ) < TREE_GRID_SPACING;
    }

    private bool TryPlaceTreeAt(int x, int z, int woodMaterialIndex, int leavesMaterialIndex)
    {
        if (x < TREE_CANOPY_RADIUS || x >= SIZE_X - TREE_CANOPY_RADIUS || z < TREE_CANOPY_RADIUS || z >= SIZE_Z - TREE_CANOPY_RADIUS)
            return false;

        int surfaceHeight = FindSurfaceHeight(x, z);
        if (surfaceHeight < MIN_HEIGHT || surfaceHeight + TREE_TRUNK_HEIGHT + TREE_CANOPY_RADIUS >= MAX_HEIGHT)
            return false;
        if (HasNearbyTree(x, z, surfaceHeight, woodMaterialIndex, leavesMaterialIndex))
            return false;

        PlaceTree(x, surfaceHeight + 1, z, woodMaterialIndex, leavesMaterialIndex);
        return true;
    }

    private bool HasNearbyTree(int x, int z, int surfaceHeight, int woodMaterialIndex, int leavesMaterialIndex)
    {
        int minX = Mathf.Max(0, x - TREE_CLEARANCE_RADIUS);
        int maxX = Mathf.Min(SIZE_X - 1, x + TREE_CLEARANCE_RADIUS);
        int minZ = Mathf.Max(0, z - TREE_CLEARANCE_RADIUS);
        int maxZ = Mathf.Min(SIZE_Z - 1, z + TREE_CLEARANCE_RADIUS);
        for (int nearbyX = minX; nearbyX <= maxX; nearbyX++)
        for (int nearbyZ = minZ; nearbyZ <= maxZ; nearbyZ++)
        for (int y = Mathf.Max(MIN_HEIGHT, surfaceHeight - TREE_CANOPY_RADIUS); y <= MAX_HEIGHT; y++)
        {
            if (!solid[nearbyX, y, nearbyZ])
                continue;

            int materialIndex = voxelMaterials[nearbyX, y, nearbyZ];
            if (materialIndex == woodMaterialIndex || materialIndex == leavesMaterialIndex)
                return true;
        }

        return false;
    }

    private uint GetTreeHash(int x, int z, int salt)
    {
        return GetSeededHash(x, 0, z, salt);
    }

    private uint GetSeededHash(int x, int y, int z, int salt)
    {
        int seed = worldSeed;
        unchecked
        {
            uint hash = (uint)x * 92837111u;
            hash ^= (uint)y * 19349663u;
            hash ^= (uint)z * 689287499u;
            hash ^= (uint)seed * 1597334677u;
            hash ^= (uint)salt * 3812015801u;
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            return hash;
        }
    }

    private static int GetHashRange(uint hash, int minimum, int maximum)
    {
        return minimum + (int)(hash % (uint)(maximum - minimum + 1));
    }

    private int FindSurfaceHeight(int x, int z)
    {
        for (int y = MAX_HEIGHT; y >= MIN_HEIGHT; y--)
        {
            if (solid[x, y, z])
                return y;
        }

        return -1;
    }

    private void PlaceTree(int x, int baseY, int z, int woodMaterialIndex, int leavesMaterialIndex)
    {
        for (int y = 0; y < TREE_TRUNK_HEIGHT; y++)
        {
            int trunkY = baseY + y;
            solid[x, trunkY, z] = true;
            voxelMaterials[x, trunkY, z] = woodMaterialIndex;
        }

        int canopyBaseY = baseY + TREE_TRUNK_HEIGHT - 1;
        for (int offsetY = 0; offsetY <= TREE_CANOPY_RADIUS; offsetY++)
        for (int offsetX = -TREE_CANOPY_RADIUS; offsetX <= TREE_CANOPY_RADIUS; offsetX++)
        for (int offsetZ = -TREE_CANOPY_RADIUS; offsetZ <= TREE_CANOPY_RADIUS; offsetZ++)
        {
            int distance = Mathf.Abs(offsetX) + Mathf.Abs(offsetZ);
            if (distance > TREE_CANOPY_RADIUS + 1 || (offsetY == 0 && distance == 0))
                continue;

            int leafX = x + offsetX;
            int leafY = canopyBaseY + offsetY;
            int leafZ = z + offsetZ;
            if (leafX < 0 || leafX >= SIZE_X || leafY < 0 || leafY > MAX_HEIGHT || leafZ < 0 || leafZ >= SIZE_Z)
                continue;

            solid[leafX, leafY, leafZ] = true;
            voxelMaterials[leafX, leafY, leafZ] = leavesMaterialIndex;
        }
    }

    private int FindMaterialIndex(string materialName)
    {
        if (materials == null)
            return -1;

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null && materials[i].name == materialName)
                return i;
        }

        return -1;
    }

    private int GetSubsurfaceMaterialIndex(int x, int y, int z)
    {
        if (materials.Length <= 1)
            return grassMaterialIndex;

        if (generationMaterialWeights == null || generationMaterialWeights.Length != materials.Length)
            return grassMaterialIndex;

        float totalWeight = 0f;
        for (int i = 0; i < generationMaterialWeights.Length; i++)
        {
            if (i != grassMaterialIndex)
                totalWeight += generationMaterialWeights[i];
        }

        if (totalWeight <= 0f)
            return grassMaterialIndex;

        uint hash = GetSeededHash(x, y, z, 211);
        float roll = (hash / (float)uint.MaxValue) * totalWeight;
        float cumulativeWeight = 0f;

        for (int i = 0; i < generationMaterialWeights.Length; i++)
        {
            if (i == grassMaterialIndex)
                continue;

            cumulativeWeight += generationMaterialWeights[i];
            if (roll < cumulativeWeight)
                return i;
        }

        for (int i = generationMaterialWeights.Length - 1; i >= 0; i--)
        {
            if (i != grassMaterialIndex && generationMaterialWeights[i] > 0f)
                return i;
        }

        return grassMaterialIndex;
    }

    private void CreateChunks()
    {
        int chunksX = SIZE_X / CHUNK_SIZE;
        int chunksZ = SIZE_Z / CHUNK_SIZE;
        chunks = new Chunk[chunksX, chunksZ];

        for (int chunkX = 0; chunkX < chunksX; chunkX++)
        for (int chunkZ = 0; chunkZ < chunksZ; chunkZ++)
        {
            GameObject chunkObject = new GameObject($"Chunk_{chunkX}_{chunkZ}");
            chunkObject.transform.parent = transform;

            Chunk chunk = chunkObject.AddComponent<Chunk>();
            chunk.Init(chunkX, chunkZ, CHUNK_SIZE, solid, voxelMaterials, materials, grassMaterialIndex);
            chunks[chunkX, chunkZ] = chunk;
        }
    }

    /// <summary>
    /// Sets the seed associated with the current world for save/load consistency.
    /// </summary>
    public void SetWorldSeed(int seed)
    {
        worldSeed = seed;
    }

    public bool IsSolid(int x, int y, int z)
    {
        if (solid == null)
            return false;
        if (x < 0 || x >= SIZE_X) return false;
        if (y < 0 || y > MAX_HEIGHT) return false;
        if (z < 0 || z >= SIZE_Z) return false;
        return solid[x, y, z];
    }

    /// <summary>
    /// Opens the persistent remote inventory attached to a chest voxel.
    /// </summary>
    public ChestInventory OpenChest(Vector3Int coordinate)
    {
        if (!IsChestCoordinate(coordinate))
            return null;

        if (!chestInventories.TryGetValue(coordinate, out ChestInventory chest))
        {
            chest = new ChestInventory(coordinate);
            chestInventories.Add(coordinate, chest);
        }

        return chest;
    }

    /// <summary>
    /// Returns whether the supplied world coordinate contains a chest voxel.
    /// </summary>
    public bool IsChestCoordinate(Vector3Int coordinate)
    {
        if (!IsSolid(coordinate.x, coordinate.y, coordinate.z))
            return false;

        int materialIndex = voxelMaterials[coordinate.x, coordinate.y, coordinate.z];
        PlaceableItemAsset item = GetPlaceableItemForMaterialIndex(materialIndex);
        return item != null && string.Equals(item.ItemId, "chest", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Removes the stored inventory for a chest that has been mined.
    /// </summary>
    public void RemoveChestInventory(Vector3Int coordinate)
    {
        chestInventories.Remove(coordinate);
    }

    /// <summary>
    /// Writes all chest inventories keyed by their voxel coordinates.
    /// </summary>
    public void WriteChestInventories(BinaryWriter writer)
    {
        writer.Write(chestInventories.Count);
        foreach (KeyValuePair<Vector3Int, ChestInventory> entry in chestInventories)
        {
            writer.Write(entry.Key.x);
            writer.Write(entry.Key.y);
            writer.Write(entry.Key.z);
            writer.Write(ChestInventory.SlotCount);
            for (int i = 0; i < ChestInventory.SlotCount; i++)
            {
                InventorySlotData slot = entry.Value.GetSlot(i);
                writer.Write(slot == null ? string.Empty : slot.ItemId ?? string.Empty);
                writer.Write(slot == null ? string.Empty : slot.DisplayName ?? string.Empty);
                writer.Write(slot == null ? 0 : Mathf.Max(0, slot.Count));
            }
        }
    }

    /// <summary>
    /// Restores chest inventories saved for the current voxel world.
    /// </summary>
    public bool ReadChestInventories(BinaryReader reader)
    {
        int chestCount = reader.ReadInt32();
        if (chestCount < 0 || chestCount > SIZE_X * SIZE_Z)
            throw new InvalidDataException("Save contains an invalid chest count.");

        chestInventories.Clear();
        for (int i = 0; i < chestCount; i++)
        {
            Vector3Int coordinate = new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            int slotCount = reader.ReadInt32();
            if (!IsValidChestCoordinate(coordinate) || slotCount != ChestInventory.SlotCount)
                throw new InvalidDataException("Save contains an invalid chest inventory.");

            List<InventorySlotSaveState> savedSlots = new List<InventorySlotSaveState>(slotCount);
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                string itemId = reader.ReadString();
                string displayName = reader.ReadString();
                int count = reader.ReadInt32();
                if (count < 0)
                    throw new InvalidDataException("Save contains a negative chest inventory count.");
                savedSlots.Add(new InventorySlotSaveState(itemId, displayName, count));
            }

            ChestInventory chest = OpenChest(coordinate);
            if (chest == null || !chest.RestoreState(savedSlots, inventory))
                throw new InvalidDataException("Save contains a chest that is not present in the world.");
        }

        return true;
    }

    private bool IsValidChestCoordinate(Vector3Int coordinate)
    {
        return coordinate.x >= 0 && coordinate.x < SIZE_X && coordinate.y >= MIN_HEIGHT && coordinate.y <= MAX_HEIGHT &&
               coordinate.z >= 0 && coordinate.z < SIZE_Z && IsChestCoordinate(coordinate);
    }


    private void BuildAllChunks()
    {
        foreach (Chunk chunk in chunks)
            chunk.BuildMesh();
    }

    private void MovePlayerToTopSurface()
    {
        int playerX = SIZE_X / 2;
        int playerZ = SIZE_Z / 2;
        float topY = 0f;

        for (int y = solid.GetLength(1); y > 0; y--)
        {
            if (solid[playerX, y - 1, playerZ])
            {
                topY = y;
                break;
            }
        }

        player.position = new Vector3(playerX + 0.5f, topY + 2f, playerZ + 0.5f);
    }

    private void FixedUpdate()
    {
        bool placeRequested = control != null && control.ConsumePlaceRequest();
        bool useRequested = control != null && control.ConsumeUseRequest();
        bool mineRequested = control != null && control.ConsumeMineRequest();

        if (chunks == null)
            return;

        if (placeRequested)
            TryPlaceSelectedBlock();

        if (useRequested)
            TryUseCurrentTarget();

        if (mineRequested && hasTarget)
            DeleteVoxel(currentVoxel);

        DrawVisibleChunks();
        if (Mouse.current != null)
            UpdateHighlightedVoxel(Mouse.current.position.ReadValue());
    }

    private void DeleteVoxel(Vector3 voxelPosition)
    {
        int voxelX = Mathf.FloorToInt(voxelPosition.x);
        int voxelY = Mathf.FloorToInt(voxelPosition.y);
        int voxelZ = Mathf.FloorToInt(voxelPosition.z);

        if (!IsSolid(voxelX, voxelY, voxelZ))
            return;

        int materialIndex = voxelMaterials[voxelX, voxelY, voxelZ];
        Vector3Int coordinate = new Vector3Int(voxelX, voxelY, voxelZ);
        bool wasChest = IsChestCoordinate(coordinate);
        solid[voxelX, voxelY, voxelZ] = false;
        if (wasChest)
            RemoveChestInventory(coordinate);
        inventory.Add(GetDropMaterialIndex(materialIndex));
        RebuildVoxelAndNeighbors(voxelX, voxelZ);
    }

    private bool TryPlaceSelectedBlock()
    {
        if (solid == null || voxelMaterials == null || materials == null || chunks == null || inventory == null || !hasPlacementTarget)
            return false;
        if (IsRestrictedStationTopPlacement())
            return false;

        if (!inventory.TryGetSelectedMaterialIndex(out int materialIndex))
            return false;
        if (materialIndex < 0 || materialIndex >= materials.Length)
            return false;
        if (!IsValidPlacementCoordinate(currentPlacementCoordinate))
            return false;
        if (IsPlacementOverlappingPlayer(currentPlacementCoordinate))
            return false;
        if (!inventory.TryConsumeSelectedMaterial(out materialIndex))
            return false;

        SetVoxel(currentPlacementCoordinate, materialIndex);
        RebuildVoxelAndNeighbors(currentPlacementCoordinate.x, currentPlacementCoordinate.z);
        return true;
    }

    private bool IsPlacementOverlappingPlayer(Vector3Int target)
    {
        if (player == null)
            return false;

        Vector3 center = new Vector3(target.x + 0.5f, target.y + 0.5f, target.z + 0.5f);
        Vector3 halfExtents = Vector3.one * (0.5f - PLACEMENT_OVERLAP_EPSILON);
        Collider[] overlaps = Physics.OverlapBox(center, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider overlap = overlaps[i];
            if (overlap != null && (overlap.transform == player || overlap.transform.IsChildOf(player)))
                return true;
        }

        return false;
    }

    private bool IsValidPlacementCoordinate(Vector3Int target)
    {
        if (solid == null || target.x < 0 || target.x >= SIZE_X || target.y < MIN_HEIGHT || target.y > MAX_HEIGHT || target.z < 0 || target.z >= SIZE_Z)
            return false;

        return !solid[target.x, target.y, target.z];
    }

    private void SetVoxel(Vector3Int coordinate, int materialIndex)
    {
        if (!IsValidPlacementCoordinate(coordinate) || materials == null || materialIndex < 0 || materialIndex >= materials.Length)
            return;

        solid[coordinate.x, coordinate.y, coordinate.z] = true;
        voxelMaterials[coordinate.x, coordinate.y, coordinate.z] = materialIndex;
    }

    private void RebuildVoxelAndNeighbors(int voxelX, int voxelZ)
    {
        int chunkX = voxelX / CHUNK_SIZE;
        int chunkZ = voxelZ / CHUNK_SIZE;
        RebuildChunk(chunkX, chunkZ);

        if (voxelX % CHUNK_SIZE == 0) RebuildChunk(chunkX - 1, chunkZ);
        if (voxelX % CHUNK_SIZE == CHUNK_SIZE - 1) RebuildChunk(chunkX + 1, chunkZ);
        if (voxelZ % CHUNK_SIZE == 0) RebuildChunk(chunkX, chunkZ - 1);
        if (voxelZ % CHUNK_SIZE == CHUNK_SIZE - 1) RebuildChunk(chunkX, chunkZ + 1);
    }

    private bool IsRestrictedStationTopPlacement()
    {
        if (!hasPlacementTarget || solid == null || voxelMaterials == null || materials == null)
            return false;

        Vector3Int stationCoordinate = currentPlacementCoordinate + Vector3Int.down;
        if (!IsSolid(stationCoordinate.x, stationCoordinate.y, stationCoordinate.z))
            return false;

        int materialIndex = voxelMaterials[stationCoordinate.x, stationCoordinate.y, stationCoordinate.z];
        if (materialIndex < 0 || materialIndex >= materials.Length || materials[materialIndex] == null)
            return false;

        PlaceableItemAsset placeableItem = GetPlaceableItemForMaterialIndex(materialIndex);
        return placeableItem != null && placeableItem.BlocksTopPlacement;
    }

    private PlaceableItemAsset GetPlaceableItemForMaterialIndex(int materialIndex)
    {
        if (materials == null || materialIndex < 0 || materialIndex >= materials.Length || placeableItems == null)
            return null;

        Material material = materials[materialIndex];
        for (int i = 0; i < placeableItems.Length; i++)
        {
            PlaceableItemAsset placeableItem = placeableItems[i];
            if (placeableItem != null && placeableItem.IsValid && placeableItem.MatchesMaterial(material))
                return placeableItem;
        }

        return null;
    }

    private bool TryUseCurrentTarget()
    {
        if (!hasTarget || solid == null || voxelMaterials == null || materials == null)
            return false;

        int voxelX = Mathf.FloorToInt(currentVoxel.x);
        int voxelY = Mathf.FloorToInt(currentVoxel.y);
        int voxelZ = Mathf.FloorToInt(currentVoxel.z);
        if (!IsSolid(voxelX, voxelY, voxelZ))
            return false;

        int materialIndex = voxelMaterials[voxelX, voxelY, voxelZ];
        if (materialIndex < 0 || materialIndex >= materials.Length || materials[materialIndex] == null)
            return false;

        Vector3Int targetCoordinate = new Vector3Int(voxelX, voxelY, voxelZ);
        VoxelInventoryUI inventoryUI = UnityEngine.Object.FindFirstObjectByType<VoxelInventoryUI>();
        if (inventoryUI == null)
            return false;

        if (IsChestCoordinate(targetCoordinate))
        {
            ChestInventory chest = OpenChest(targetCoordinate);
            if (chest == null)
                return false;

            inventoryUI.OpenChest(this, chest);
            control?.CancelInteractionHold();
            return true;
        }

        PlaceableItemAsset placeableItem = GetPlaceableItemForMaterialIndex(materialIndex);
        if (placeableItem == null || !placeableItem.OpensCraftingMenu)
            return false;

        inventoryUI.OpenStation(placeableItem);
        control?.CancelInteractionHold();
        return true;
    }
    private int GetDropMaterialIndex(int materialIndex)
    {
        if (materialIndex != grassMaterialIndex)
            return materialIndex;

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null && materials[i].name == DIRT_MATERIAL_NAME)
                return i;
        }

        Debug.LogWarning("Grass was mined, but no Dirt material is configured for its inventory drop.");
        return materialIndex;
    }



    /// <summary>
    /// Writes the current voxel grid in x/y/z order using zero for empty cells and material index plus one for solid cells.
    /// </summary>
    public void WriteVoxelState(BinaryWriter writer)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Voxel terrain is not initialized.");
        if (materials == null || materials.Length > byte.MaxValue)
            throw new InvalidDataException("The material count cannot be represented by the save format.");

        for (int x = 0; x < SIZE_X; x++)
        for (int y = 0; y <= MAX_HEIGHT; y++)
        for (int z = 0; z < SIZE_Z; z++)
        {
            if (!solid[x, y, z])
            {
                writer.Write((byte)0);
                continue;
            }

            int materialIndex = voxelMaterials[x, y, z];
            if (materialIndex < 0 || materialIndex >= materials.Length || materialIndex >= byte.MaxValue)
                throw new InvalidDataException("The voxel grid contains an invalid material index.");
            writer.Write((byte)(materialIndex + 1));
        }
    }

    /// <summary>
    /// Reads and applies a complete voxel grid, then rebuilds every chunk mesh.
    /// </summary>
    public bool ReadVoxelState(BinaryReader reader, int width, int height, int depth)
    {
        if (!IsInitialized || width != SIZE_X || height != MAX_HEIGHT + 1 || depth != SIZE_Z || materials == null || materials.Length > byte.MaxValue)
            return false;

        int voxelCount = width * height * depth;
        byte[] encodedState = reader.ReadBytes(voxelCount);
        if (encodedState.Length != voxelCount)
            return false;

        for (int i = 0; i < encodedState.Length; i++)
        {
            if (encodedState[i] > materials.Length)
                return false;
        }
        chestInventories.Clear();


        int offset = 0;
        for (int x = 0; x < SIZE_X; x++)
        for (int y = 0; y <= MAX_HEIGHT; y++)
        for (int z = 0; z < SIZE_Z; z++)
        {
            byte encodedMaterial = encodedState[offset++];
            solid[x, y, z] = encodedMaterial != 0;
            voxelMaterials[x, y, z] = encodedMaterial == 0 ? 0 : encodedMaterial - 1;
        }

        BuildAllChunks();
        return true;
    }

    private void RebuildChunk(int chunkX, int chunkZ)
    {
        if (chunkX < 0 || chunkZ < 0 || chunkX >= chunks.GetLength(0) || chunkZ >= chunks.GetLength(1))
            return;
        chunks[chunkX, chunkZ].BuildMesh();
    }

    /// <summary>
    /// Updates the highlighted voxel and its face-adjacent placement coordinate from a screen-space mouse position.
    /// </summary>
    public void UpdateHighlightedVoxel(Vector2 mousePosition)
    {
        if (cam != null)
        {
            Ray ray = cam.ScreenPointToRay(new Vector3(mousePosition.x, mousePosition.y, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, 10f) && TryGetPlacementCoordinate(hit, out Vector3Int placementCoordinate))
            {
                Vector3 insidePoint = hit.point - hit.normal * 0.001f;
                Vector3 aimedVoxel = new Vector3(
                    Mathf.FloorToInt(insidePoint.x),
                    Mathf.FloorToInt(insidePoint.y),
                    Mathf.FloorToInt(insidePoint.z));

                if (!hasTarget || aimedVoxel != currentVoxel)
                {
                    if (control != null)
                        control.ResetDigTimer();
                }

                currentVoxel = aimedVoxel;
                currentPlacementCoordinate = placementCoordinate;
                hasTarget = true;
                hasPlacementTarget = true;
                SetHoverCube(currentVoxel);
                return;
            }
        }

        if (hoverCube != null)
            hoverCube.SetActive(false);
        if (hasTarget && control != null)
            control.ResetDigTimer();
        currentVoxel = Vector3.zero;
        currentPlacementCoordinate = Vector3Int.zero;
        hasTarget = false;
        hasPlacementTarget = false;
    }

    private bool TryGetPlacementCoordinate(RaycastHit hit, out Vector3Int target)
    {
        target = Vector3Int.zero;
        Vector3 insidePoint = hit.point - hit.normal * 0.001f;
        Vector3Int solidCoordinate = new Vector3Int(
            Mathf.FloorToInt(insidePoint.x),
            Mathf.FloorToInt(insidePoint.y),
            Mathf.FloorToInt(insidePoint.z));
        if (!IsSolid(solidCoordinate.x, solidCoordinate.y, solidCoordinate.z))
            return false;

        Vector3 normal = hit.normal;
        float absX = Mathf.Abs(normal.x);
        float absY = Mathf.Abs(normal.y);
        float absZ = Mathf.Abs(normal.z);
        Vector3Int faceDirection;
        if (absX >= absY && absX >= absZ)
            faceDirection = new Vector3Int(normal.x >= 0f ? 1 : -1, 0, 0);
        else if (absY >= absX && absY >= absZ)
            faceDirection = new Vector3Int(0, normal.y >= 0f ? 1 : -1, 0);
        else
            faceDirection = new Vector3Int(0, 0, normal.z >= 0f ? 1 : -1);

        target = solidCoordinate + faceDirection;
        return true;
    }

    private void DrawVisibleChunks()
    {
        foreach (Chunk chunk in chunks)
            chunk.SetVisible(IsChunkVisible(chunk));
    }

    private bool IsChunkVisible(Chunk chunk)
    {
        Vector3 toChunk = chunk.Bounds.center - cam.transform.position;
        float distance = toChunk.magnitude;
        if (distance > 300f) return false;

        float angle = Vector3.Angle(cam.transform.forward, toChunk);
        if (angle <= cam.fieldOfView * 1.5f)
            return true;

        return distance <= CHUNK_SIZE * 2.5f;
    }
}
