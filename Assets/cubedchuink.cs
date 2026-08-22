using UnityEngine;
using UnityEngine.InputSystem;

public class ChunkedVoxelTerrain : MonoBehaviour
{
    public Material[] materials;
    public float[] materialWeights = { 0f, 45f, 25f, 10f, 7f, 6f, 3f, 2f, 1f, 0.2f, 0.8f };
    public int grassMaterialIndex = 0;
    public Transform player;
    public Camera cam;

    private GameObject hoverCube;
    private LineRenderer[] hoverEdgeLines;
    private bool[,,] solid;
    private int[,,] voxelMaterials;
    private Chunk[,] chunks;
    private VoxelPlayerController control;
    private VoxelInventory inventory;

    public const int SIZE_X = 500;
    public const int SIZE_Z = 500;
    public const int MIN_HEIGHT = 0;
    public const int MAX_HEIGHT = 50;

    private const int CHUNK_SIZE = 25;
    private const string DIRT_MATERIAL_NAME = "Dirt";
    private Vector3 currentVoxel = Vector3.zero;
    private bool hasTarget;

    public VoxelInventory Inventory => inventory;

    private void Awake()
    {
        if (!ValidateMaterials())
            return;

        inventory = GetComponent<VoxelInventory>();
        if (inventory == null)
            inventory = gameObject.AddComponent<VoxelInventory>();
        inventory.Initialize(materials);
    }

    private void Start()
    {
        control = player.gameObject.GetComponent<VoxelPlayerController>();
        if (control == null)
            Debug.LogError("Player has no VoxelPlayerController.");

        if (!inventory || !inventory.IsInitialized)
            return;

        if (control != null)
            control.enabled = false;

        CreateHoverCube();
        GenerateSolidGrid();
        CreateChunks();
        BuildAllChunks();
        MovePlayerToTopSurface();

        if (control != null)
            control.enabled = true;
    }

    private bool ValidateMaterials()
    {
        if (materials == null || materials.Length == 0)
        {
            Debug.LogError("ChunkedVoxelTerrain requires at least one material in the materials array.");
            return false;
        }

        if (materialWeights == null || materialWeights.Length != materials.Length)
        {
            float[] resizedWeights = new float[materials.Length];
            for (int i = 0; i < resizedWeights.Length; i++)
                resizedWeights[i] = i == grassMaterialIndex ? 0f : 1f;
            materialWeights = resizedWeights;
        }

        for (int i = 0; i < materialWeights.Length; i++)
            materialWeights[i] = Mathf.Max(0f, materialWeights[i]);

        materialWeights[grassMaterialIndex] = 0f;
        return HasPositiveSubsurfaceWeight();
    }

    private bool HasPositiveSubsurfaceWeight()
    {
        for (int i = 0; i < materialWeights.Length; i++)
        {
            if (i != grassMaterialIndex && materialWeights[i] > 0f)
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
            float noise = Mathf.PerlinNoise(x * 0.01f, z * 0.01f);
            int surfaceHeight = Mathf.RoundToInt(Mathf.Lerp(MIN_HEIGHT, MAX_HEIGHT, noise));

            for (int y = 0; y <= surfaceHeight; y++)
            {
                solid[x, y, z] = true;
                voxelMaterials[x, y, z] = y == surfaceHeight
                    ? grassMaterialIndex
                    : GetSubsurfaceMaterialIndex(x, y, z);
            }
        }
    }

    private int GetSubsurfaceMaterialIndex(int x, int y, int z)
    {
        if (materials.Length <= 1)
            return grassMaterialIndex;

        float totalWeight = 0f;
        for (int i = 0; i < materialWeights.Length; i++)
        {
            if (i != grassMaterialIndex)
                totalWeight += materialWeights[i];
        }

        if (totalWeight <= 0f)
            return grassMaterialIndex;

        uint hash = (uint)(x * 73856093 ^ y * 19349663 ^ z * 83492791);
        float roll = (hash / (float)uint.MaxValue) * totalWeight;
        float cumulativeWeight = 0f;

        for (int i = 0; i < materialWeights.Length; i++)
        {
            if (i == grassMaterialIndex)
                continue;

            cumulativeWeight += materialWeights[i];
            if (roll < cumulativeWeight)
                return i;
        }

        for (int i = materialWeights.Length - 1; i >= 0; i--)
        {
            if (i != grassMaterialIndex && materialWeights[i] > 0f)
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
    /// Returns whether a voxel coordinate currently contains a block.
    /// </summary>
    public bool IsSolid(int x, int y, int z)
    {
        if (x < 0 || x >= SIZE_X) return false;
        if (y < 0 || y > MAX_HEIGHT) return false;
        if (z < 0 || z >= SIZE_Z) return false;
        return solid[x, y, z];
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
        if (control != null && control.doAction == voxelAction.dig)
        {
            control.doAction = voxelAction.nothing;
            if (hasTarget)
                DeleteVoxel(currentVoxel);
        }

        if (chunks == null)
            return;

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
        solid[voxelX, voxelY, voxelZ] = false;
        inventory.Add(GetDropMaterialIndex(materialIndex));

        int chunkX = voxelX / CHUNK_SIZE;
        int chunkZ = voxelZ / CHUNK_SIZE;
        RebuildChunk(chunkX, chunkZ);

        if (voxelX % CHUNK_SIZE == 0) RebuildChunk(chunkX - 1, chunkZ);
        if (voxelX % CHUNK_SIZE == CHUNK_SIZE - 1) RebuildChunk(chunkX + 1, chunkZ);
        if (voxelZ % CHUNK_SIZE == 0) RebuildChunk(chunkX, chunkZ - 1);
        if (voxelZ % CHUNK_SIZE == CHUNK_SIZE - 1) RebuildChunk(chunkX, chunkZ + 1);
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



    private void RebuildChunk(int chunkX, int chunkZ)
    {
        if (chunkX < 0 || chunkZ < 0 || chunkX >= chunks.GetLength(0) || chunkZ >= chunks.GetLength(1))
            return;
        chunks[chunkX, chunkZ].BuildMesh();
    }

    /// <summary>
    /// Updates the highlighted voxel from a screen-space mouse position.
    /// </summary>
    public void UpdateHighlightedVoxel(Vector2 mousePosition)
    {
        Ray ray = cam.ScreenPointToRay(new Vector3(mousePosition.x, mousePosition.y, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, 10f))
        {
            Vector3 insidePoint = hit.point - hit.normal * 0.001f;
            int voxelX = Mathf.FloorToInt(insidePoint.x);
            int voxelY = Mathf.FloorToInt(insidePoint.y);
            int voxelZ = Mathf.FloorToInt(insidePoint.z);

            if (IsSolid(voxelX, voxelY, voxelZ))
            {
                Vector3 aimedVoxel = new Vector3(voxelX, voxelY, voxelZ);
                if (!hasTarget || aimedVoxel != currentVoxel)
                    control.ResetDigTimer();

                currentVoxel = aimedVoxel;
                hasTarget = true;
                SetHoverCube(currentVoxel);
                return;
            }
        }

        hoverCube.SetActive(false);
        if (hasTarget)
            control.ResetDigTimer();
        currentVoxel = Vector3.zero;
        hasTarget = false;
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
