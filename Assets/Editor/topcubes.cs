using UnityEngine;
using System.Collections.Generic;

public class PerlinVoxelTerrain : MonoBehaviour
{
    public Material material;
    public Transform player;
    public Camera cam;

    const int SIZE_X = 500;
    const int SIZE_Z = 500;
    const int MIN_HEIGHT = 20;
    const int MAX_HEIGHT = 30;

    Mesh mesh;
    bool[,,] solid;

    void Start()
    {
        if (material == null || player == null || cam == null)
        {
            Debug.LogError("Material, player, or camera missing.");
            enabled = false;
            return;
        }

        var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);

        solid = new bool[SIZE_X, MAX_HEIGHT + 1, SIZE_Z];

        // Fill solid voxels from Perlin heights
        for (int x = 0; x < SIZE_X; x++)
        for (int z = 0; z < SIZE_Z; z++)
        {
            float n = Mathf.PerlinNoise(x * 0.01f, z * 0.01f);
            int h = Mathf.RoundToInt(Mathf.Lerp(MIN_HEIGHT, MAX_HEIGHT, n));

            for (int y = 0; y <= h; y++)
                solid[x, y, z] = true;
        }

        MovePlayerToCenter();
    }

    void MovePlayerToCenter()
    {
        int cx = SIZE_X / 2;
        int cz = SIZE_Z / 2;

        float n = Mathf.PerlinNoise(cx * 0.01f, cz * 0.01f);
        int h = Mathf.RoundToInt(Mathf.Lerp(MIN_HEIGHT, MAX_HEIGHT, n));

        player.position = new Vector3(cx, h + 2, cz);
    }

    bool IsExposed(int x, int y, int z)
    {
        if (x == 0 || !solid[x - 1, y, z]) return true;
        if (x == SIZE_X - 1 || !solid[x + 1, y, z]) return true;
        if (y == 0 || !solid[x, y - 1, z]) return true;
        if (y == MAX_HEIGHT || !solid[x, y + 1, z]) return true;
        if (z == 0 || !solid[x, y, z - 1]) return true;
        if (z == SIZE_Z - 1 || !solid[x, y, z + 1]) return true;

        return false;
    }

    bool IsInFrontOfCamera(Vector3 worldPos)
    {
        Vector3 toCube = (worldPos - cam.transform.position).normalized;
        float dot = Vector3.Dot(cam.transform.forward, toCube);
        return dot > 0.25f;
    }

    bool IsWithinViewDistance(Vector3 worldPos)
    {
        float dist = Vector3.Distance(cam.transform.position, worldPos);
        return dist < 150f;
    }

    // Cube-volume FOV test: partial cubes at edges are allowed
    bool IsCubeInFOV(Vector3 worldPos)
    {
        Vector3[] corners = new Vector3[]
        {
            worldPos + new Vector3(-0.5f, -0.5f, -0.5f),
            worldPos + new Vector3( 0.5f, -0.5f, -0.5f),
            worldPos + new Vector3(-0.5f,  0.5f, -0.5f),
            worldPos + new Vector3( 0.5f,  0.5f, -0.5f),

            worldPos + new Vector3(-0.5f, -0.5f,  0.5f),
            worldPos + new Vector3( 0.5f, -0.5f,  0.5f),
            worldPos + new Vector3(-0.5f,  0.5f,  0.5f),
            worldPos + new Vector3( 0.5f,  0.5f,  0.5f),
        };

        foreach (var c in corners)
        {
            Vector3 toCorner = c - cam.transform.position;

            if (Vector3.Dot(cam.transform.forward, toCorner.normalized) <= 0f)
                continue;

            float angle = Vector3.Angle(cam.transform.forward, toCorner);

            if (angle < cam.fieldOfView)
                return true;
        }

        return false;
    }

    // Conservative voxel occlusion
    bool IsOccluded(Vector3 worldPos)
    {
        Vector3 camPos = cam.transform.position;
        Vector3 dir = (worldPos - camPos).normalized;

        float dist = Vector3.Distance(camPos, worldPos);
        int steps = Mathf.CeilToInt(dist * 0.5f);

        Vector3 step = dir * 0.5f;
        Vector3 p = camPos;

        for (int i = 0; i < steps; i++)
        {
            p += step;

            int vx = Mathf.FloorToInt(p.x);
            int vy = Mathf.FloorToInt(p.y);
            int vz = Mathf.FloorToInt(p.z);

            if (vx < 0 || vx >= SIZE_X ||
                vy < 0 || vy > MAX_HEIGHT ||
                vz < 0 || vz >= SIZE_Z)
                continue;

            if (solid[vx, vy, vz])
            {
                float edgeAllowance = 1.5f;
                if (Vector3.Distance(p, worldPos) > edgeAllowance)
                    return true;
            }
        }

        return false;
    }

    void Update()
    {
        List<Matrix4x4> visible = new List<Matrix4x4>();

        for (int x = 0; x < SIZE_X; x++)
        for (int z = 0; z < SIZE_Z; z++)
        for (int y = 0; y <= MAX_HEIGHT; y++)
        {
            if (!solid[x, y, z]) continue;
            if (!IsExposed(x, y, z)) continue;

            Vector3 pos = new Vector3(x, y, z);

            if (!IsInFrontOfCamera(pos)) continue;
            if (!IsWithinViewDistance(pos)) continue;
            if (!IsCubeInFOV(pos)) continue;
            if (IsOccluded(pos)) continue;

            visible.Add(Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one));
        }

        Matrix4x4[] matrices = visible.ToArray();

        const int batchSize = 1023;
        int drawn = 0;

        while (drawn < matrices.Length)
        {
            int n = Mathf.Min(batchSize, matrices.Length - drawn);

            Matrix4x4[] batch = new Matrix4x4[n];
            System.Array.Copy(matrices, drawn, batch, 0, n);

            Graphics.DrawMeshInstanced(mesh, 0, material, batch, n);

            drawn += n;
        }
    }
}
