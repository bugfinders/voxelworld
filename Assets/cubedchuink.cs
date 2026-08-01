using UnityEngine;
using System.Collections.Generic;

public class ChunkedVoxelTerrain : MonoBehaviour
{
    public Material material;
    public Transform player;
    public Camera cam;

    public const int SIZE_X = 500;
    public const int SIZE_Z = 500;
    public const int MIN_HEIGHT = 00;
    public const int MAX_HEIGHT = 20;

    const int CHUNK_SIZE = 25;

    bool[,,] solid;
    Chunk[,] chunks;

    private VoxelPlayerController control;

    void Start()
    {
        control = player.gameObject.GetComponent<VoxelPlayerController>();
        if (control == null) Debug.Log("Player has no VoxelPlayerController");
        control.enabled = false;
        GenerateSolidGrid();
        CreateChunks();
        BuildAllChunks();
        MovePlayerToTopSurface();
        control.enabled = true;
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

        return true;
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

    void Update()
    {
        foreach (var chunk in chunks)
            chunk.SetVisible(IsChunkVisible(chunk));
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

