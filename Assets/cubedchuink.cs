using UnityEngine;
using UnityEngine.InputSystem;

public class ChunkedVoxelTerrain : MonoBehaviour
{
    public Material material;
    public Transform player;
    public Camera cam;
    
    private GameObject hoverCube;
    private LineRenderer[] hoverEdgeLines;

    public const int SIZE_X = 500;
    public const int SIZE_Z = 500;
    public const int MIN_HEIGHT = 00;
    public const int MAX_HEIGHT = 20;

    const int CHUNK_SIZE = 25;

    bool[,,] solid;
    //private int[,,] voxelType;
    Chunk[,] chunks;

    private VoxelPlayerController control;

    void Start()
    {
        control = player.gameObject.GetComponent<VoxelPlayerController>();
        if (control == null) Debug.Log("Player has no VoxelPlayerController");
        control.enabled = false;
        //added
        hoverCube = new GameObject("HoverCube");
        hoverEdgeLines = new LineRenderer[12];
        Material hoverMat = new Material(Shader.Find("Sprites/Default"));

        for (int i = 0; i < hoverEdgeLines.Length; i++)
        {
            GameObject edge = new GameObject($"HoverEdge_{i}");
            edge.transform.parent = hoverCube.transform;

            LineRenderer lr = edge.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.loop = false;
            lr.widthMultiplier = 0.03f;
            lr.material = hoverMat;
            lr.startColor = Color.black;
            lr.endColor = Color.black;

            hoverEdgeLines[i] = lr;
        }

        hoverCube.SetActive(false);
        // end
        GenerateSolidGrid();
        CreateChunks();
        BuildAllChunks();
        MovePlayerToTopSurface();
        control.enabled = true;
    }
    
    void SetHoverCube(Vector3 voxelPos)
    {
        hoverCube.SetActive(true);

        const float edgeOffset = 0.02f;
        int vx = Mathf.FloorToInt(voxelPos.x);
        int vy = Mathf.FloorToInt(voxelPos.y);
        int vz = Mathf.FloorToInt(voxelPos.z);

        bool IsSolidLocal(int x, int y, int z)
        {
            if (x < 0 || x >= SIZE_X) return false;
            if (y < 0 || y > MAX_HEIGHT) return false;
            if (z < 0 || z >= SIZE_Z) return false;
            return solid[x, y, z];
        }

        bool frontExposed = !IsSolidLocal(vx, vy, vz - 1); // z-
        bool backExposed = !IsSolidLocal(vx, vy, vz + 1);  // z+
        bool leftExposed = !IsSolidLocal(vx - 1, vy, vz);  // x-
        bool rightExposed = !IsSolidLocal(vx + 1, vy, vz); // x+
        bool topExposed = !IsSolidLocal(vx, vy + 1, vz);   // y+
        bool bottomExposed = !IsSolidLocal(vx, vy - 1, vz);// y-

        Vector3 cubeCenter = new Vector3(vx + 0.5f, vy + 0.5f, vz + 0.5f);
        Vector3 toCam = (cam.transform.position - cubeCenter).normalized;

        bool frontFacing = Vector3.Dot(toCam, Vector3.back) > 0f;
        bool backFacing = Vector3.Dot(toCam, Vector3.forward) > 0f;
        bool leftFacing = Vector3.Dot(toCam, Vector3.left) > 0f;
        bool rightFacing = Vector3.Dot(toCam, Vector3.right) > 0f;
        bool topFacing = Vector3.Dot(toCam, Vector3.up) > 0f;
        bool bottomFacing = Vector3.Dot(toCam, Vector3.down) > 0f;

        bool frontVisible = frontExposed && frontFacing;
        bool backVisible = backExposed && backFacing;
        bool leftVisible = leftExposed && leftFacing;
        bool rightVisible = rightExposed && rightFacing;
        bool topVisible = topExposed && topFacing;
        bool bottomVisible = bottomExposed && bottomFacing;

        float minX = voxelPos.x - edgeOffset;
        float minY = voxelPos.y - edgeOffset;
        float minZ = voxelPos.z - edgeOffset;
        float maxX = voxelPos.x + 1f + edgeOffset;
        float maxY = voxelPos.y + 1f + edgeOffset;
        float maxZ = voxelPos.z + 1f + edgeOffset;

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
            bottomVisible || frontVisible, // v000-v100
            bottomVisible || rightVisible, // v100-v101
            bottomVisible || backVisible,  // v101-v001
            bottomVisible || leftVisible,  // v001-v000
            topVisible || frontVisible,    // v010-v110
            topVisible || rightVisible,    // v110-v111
            topVisible || backVisible,     // v111-v011
            topVisible || leftVisible,     // v011-v010
            frontVisible || leftVisible,   // v000-v010
            frontVisible || rightVisible,  // v100-v110
            backVisible || rightVisible,   // v101-v111
            backVisible || leftVisible     // v001-v011
        };

        for (int i = 0; i < hoverEdgeLines.Length; i++)
        {
            hoverEdgeLines[i].SetPosition(0, edges[i * 2]);
            hoverEdgeLines[i].SetPosition(1, edges[i * 2 + 1]);
            hoverEdgeLines[i].enabled = edgeVisible[i];
        }
    }


    void GenerateSolidGrid()
    {
        solid = new bool[SIZE_X, MAX_HEIGHT + 1, SIZE_Z];

        for (int x = 0; x < SIZE_X; x++)
        for (int z = 0; z < SIZE_Z; z++)
        {
            float n = Mathf.PerlinNoise(x * 0.01f, z * 0.01f);
            int h = Mathf.RoundToInt(Mathf.Lerp(MIN_HEIGHT, MAX_HEIGHT, n));

            for (int y = 0; y <= h; y++)
                solid[x, y, z] = true;
        }

    }

    void CreateChunks()
    {
        int chunksX = SIZE_X / CHUNK_SIZE;
        int chunksZ = SIZE_Z / CHUNK_SIZE;

        chunks = new Chunk[chunksX, chunksZ];

        for (int cx = 0; cx < chunksX; cx++)
        for (int cz = 0; cz < chunksZ; cz++)
        {
            GameObject go = new GameObject($"Chunk_{cx}_{cz}");
            go.transform.parent = transform;

            Chunk chunk = go.AddComponent<Chunk>();
            chunk.Init(cx, cz, CHUNK_SIZE, solid, material);

            chunks[cx, cz] = chunk;
        }
    }

    public bool IsSolid(int x, int y, int z)
    {
        // Bounds check
        if (x < 0 || x >= SIZE_X) return false;
        if (y < 0 || y > MAX_HEIGHT) return false;
        if (z < 0 || z >= SIZE_Z) return false;

        return solid[x, y, z];
    }

    void BuildAllChunks()
    {
        foreach (var chunk in chunks)
            chunk.BuildMesh();
    }

    void MovePlayerToTopSurface()
    {
        int px = SIZE_X / 2;
        int pz = SIZE_Z / 2;

        int sy = solid.GetLength(1);
        float topY = 0;

        for (int y = sy; y > 0; y--)
        {
            if (solid[px, y-1, pz])
            {
                topY = y;
                break;
            }
        }

        topY+= 2f; // half for the cube, 1 for the player.
        player.position = new Vector3(px + 0.5f, topY, pz + 0.5f);
    }

    Vector3 curVoxel = Vector3.zero;
    Vector2 CurPos = Vector3.zero;

    public void DrawVisibleChunks()
    {
        foreach (var chunk in chunks)
            chunk.SetVisible(IsChunkVisible(chunk));

    }


    void FixedUpdate()
    {
        if (control.doAction != voxelAction.dig )
        {
            // delete voxel and update chunk
            DeleteVoxel(curVoxel);
        }
        DrawVisibleChunks();
        UpdateHighlightedVoxel(Mouse.current.position.ReadValue());
    }

    private void DeleteVoxel(Vector3 vector3)
    {
        int vx = Mathf.FloorToInt(vector3.x);
        int vy = Mathf.FloorToInt(vector3.y);
        int vz = Mathf.FloorToInt(vector3.z);

        if (IsSolid(vx, vy, vz))
        {

        }
    }

    public void UpdateHighlightedVoxel(Vector2 mousePos)
    {

        /*Vector2 mousePos = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);*/
        Ray ray = cam.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, 10f))
        {
            // Move slightly inside the hit voxel to avoid boundary precision issues.
            Vector3 insidePoint = hit.point - hit.normal * 0.001f;
            int vx = Mathf.FloorToInt(insidePoint.x);
            int vy = Mathf.FloorToInt(insidePoint.y);
            int vz = Mathf.FloorToInt(insidePoint.z);

            if (IsSolid(vx, vy, vz))
            {
                curVoxel = new Vector3(vx, vy, vz);
                SetHoverCube(curVoxel);
            }
            else
            {
                hoverCube.SetActive(false);
                curVoxel = Vector2.zero;
            }
        }
        else
        {
            hoverCube.SetActive(false);
        }


    }
    
    bool IsChunkVisible(Chunk chunk)
    {
        Vector3 pos = chunk.Bounds.center;
        Vector3 toChunk = pos - cam.transform.position;

        float dist = toChunk.magnitude;
        if (dist > 300f) return false;

        float angle = Vector3.Angle(cam.transform.forward, toChunk);
        if (angle <= cam.fieldOfView * 1.5f)
            return true;

        // --- NEW: check behind using the SAME logic, but flipped ---
        if (dist <= CHUNK_SIZE * 2.5f)
            return true;

        return false;
    }

}
