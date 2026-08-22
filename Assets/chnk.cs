using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    private int cx;
    private int cz;
    private int size;
    private bool[,,] solid;
    private Material material;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private Mesh mesh;

    public Bounds Bounds { get; private set; }

    public void Init(int cx, int cz, int size, bool[,,] solid, Material material)
    {
        this.cx = cx;
        this.cz = cz;
        this.size = size;
        this.solid = solid;
        this.material = material;

        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshCollider = gameObject.AddComponent<MeshCollider>();
        meshRenderer.sharedMaterial = material;

        Bounds = new Bounds(
            new Vector3(cx * size + size / 2f,
                        (ChunkedVoxelTerrain.MAX_HEIGHT + 1 + ChunkedVoxelTerrain.MIN_HEIGHT) * 0.5f,
                        cz * size + size / 2f),
            new Vector3(size,
                        ChunkedVoxelTerrain.MAX_HEIGHT + 1 - ChunkedVoxelTerrain.MIN_HEIGHT,
                        size)
        );
    }

    public void BuildMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        int sizeX = solid.GetLength(0);
        int sizeY = solid.GetLength(1);
        int sizeZ = solid.GetLength(2);
        int startX = cx * size;
        int startZ = cz * size;

        for (int x = startX; x < startX + size && x < sizeX; x++)
        for (int z = startZ; z < startZ + size && z < sizeZ; z++)
        for (int y = 0; y < sizeY; y++)
        {
            if (!solid[x, y, z]) continue;
            AddCubeFaces(vertices, triangles, uvs, x, y, z);
        }

        if (vertices.Count == 0)
        {
            if (mesh != null) mesh.Clear();
            meshFilter.sharedMesh = null;
            meshCollider.sharedMesh = null;
            return;
        }

        if (mesh == null)
        {
            mesh = new Mesh
            {
                name = $"ChunkMesh_{cx}_{cz}",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
        }
        else
        {
            mesh.Clear();
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = material;
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    private bool FaceVisible(int x, int y, int z, int dx, int dy, int dz)
    {
        int nextX = x + dx;
        int nextY = y + dy;
        int nextZ = z + dz;
        int sizeX = solid.GetLength(0);
        int sizeY = solid.GetLength(1);
        int sizeZ = solid.GetLength(2);

        if (nextX < 0 || nextX >= sizeX ||
            nextY < 0 || nextY >= sizeY ||
            nextZ < 0 || nextZ >= sizeZ)
            return true;

        return !solid[nextX, nextY, nextZ];
    }

    private void AddCubeFaces(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, int x, int y, int z)
    {
        Vector3 basePosition = new Vector3(x, y, z);
        const float halfPixelU = 1f / 32f;
        const float halfPixelV = 1f / 64f;
        Rect sideTile = new Rect(halfPixelU, halfPixelV, 1f - halfPixelU * 2f, 0.5f - halfPixelV * 2f);
        Rect topTile = new Rect(halfPixelU, 0.5f + halfPixelV, 1f - halfPixelU * 2f, 0.5f - halfPixelV * 2f);

        if (FaceVisible(x, y, z, 0, 0, -1))
            AddQuad(vertices, triangles, uvs, basePosition, sideTile,
                new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,0,0));

        if (FaceVisible(x, y, z, 0, 0, 1))
            AddQuad(vertices, triangles, uvs, basePosition, sideTile,
                new Vector3(0,0,1), new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1));

        if (FaceVisible(x, y, z, -1, 0, 0))
            AddQuad(vertices, triangles, uvs, basePosition, sideTile,
                new Vector3(0,0,0), new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0));

        if (FaceVisible(x, y, z, 1, 0, 0))
            AddQuad(vertices, triangles, uvs, basePosition, sideTile,
                new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1));

        if (FaceVisible(x, y, z, 0, 1, 0))
            AddQuad(vertices, triangles, uvs, basePosition, topTile,
                new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0));

        if (FaceVisible(x, y, z, 0, -1, 0))
            AddQuad(vertices, triangles, uvs, basePosition, sideTile,
                new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1), new Vector3(0,0,1));
    }

    private void AddQuad(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, Vector3 basePosition,
                         Rect tile, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int startIndex = vertices.Count;
        vertices.Add(basePosition + a);
        vertices.Add(basePosition + b);
        vertices.Add(basePosition + c);
        vertices.Add(basePosition + d);

        triangles.Add(startIndex + 0);
        triangles.Add(startIndex + 1);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 3);
        triangles.Add(startIndex + 0);

        uvs.Add(new Vector2(tile.xMin, tile.yMin));
        uvs.Add(new Vector2(tile.xMin, tile.yMax));
        uvs.Add(new Vector2(tile.xMax, tile.yMax));
        uvs.Add(new Vector2(tile.xMax, tile.yMin));
    }

    public void SetVisible(bool visible)
    {
        meshRenderer.enabled = visible;
    }
}
