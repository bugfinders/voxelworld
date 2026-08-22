using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    private int chunkX;
    private int chunkZ;
    private int chunkSize;
    private bool[,,] solid;
    private int[,,] voxelMaterials;
    private Material[] materials;
    private int grassMaterialIndex;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private Mesh mesh;

    public Bounds Bounds { get; private set; }

    /// <summary>
    /// Initializes a chunk with the shared voxel data and material array.
    /// </summary>
    public void Init(int chunkX, int chunkZ, int chunkSize, bool[,,] solid, int[,,] voxelMaterials, Material[] materials, int grassMaterialIndex)
    {
        this.chunkX = chunkX;
        this.chunkZ = chunkZ;
        this.chunkSize = chunkSize;
        this.solid = solid;
        this.voxelMaterials = voxelMaterials;
        this.materials = materials;
        this.grassMaterialIndex = grassMaterialIndex;

        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshCollider = gameObject.AddComponent<MeshCollider>();
        meshRenderer.sharedMaterials = materials;

        Bounds = new Bounds(
            new Vector3(chunkX * chunkSize + chunkSize / 2f,
                        (ChunkedVoxelTerrain.MAX_HEIGHT + 1 + ChunkedVoxelTerrain.MIN_HEIGHT) * 0.5f,
                        chunkZ * chunkSize + chunkSize / 2f),
            new Vector3(chunkSize,
                        ChunkedVoxelTerrain.MAX_HEIGHT + 1 - ChunkedVoxelTerrain.MIN_HEIGHT,
                        chunkSize)
        );
    }

    /// <summary>
    /// Rebuilds visible voxel faces and assigns each face to its material-array submesh.
    /// </summary>
    public void BuildMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int>[] trianglesByMaterial = CreateTriangleLists();

        int sizeX = solid.GetLength(0);
        int sizeY = solid.GetLength(1);
        int sizeZ = solid.GetLength(2);
        int startX = chunkX * chunkSize;
        int startZ = chunkZ * chunkSize;

        for (int x = startX; x < startX + chunkSize && x < sizeX; x++)
        for (int z = startZ; z < startZ + chunkSize && z < sizeZ; z++)
        for (int y = 0; y < sizeY; y++)
        {
            if (!solid[x, y, z]) continue;

            int materialIndex = GetMaterialIndex(x, y, z);
            AddCubeFaces(vertices, trianglesByMaterial, uvs, x, y, z, materialIndex);
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
                name = $"ChunkMesh_{chunkX}_{chunkZ}",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
        }
        else
        {
            mesh.Clear();
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = trianglesByMaterial.Length;
        for (int i = 0; i < trianglesByMaterial.Length; i++)
            mesh.SetTriangles(trianglesByMaterial[i], i);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterials = materials;
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
    }

    private List<int>[] CreateTriangleLists()
    {
        int materialCount = materials == null ? 0 : materials.Length;
        List<int>[] trianglesByMaterial = new List<int>[materialCount];
        for (int i = 0; i < materialCount; i++)
            trianglesByMaterial[i] = new List<int>();
        return trianglesByMaterial;
    }

    private int GetMaterialIndex(int x, int y, int z)
    {
        if (materials == null || materials.Length == 0)
            return -1;

        int materialIndex = voxelMaterials[x, y, z];
        return Mathf.Clamp(materialIndex, 0, materials.Length - 1);
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

    private void AddCubeFaces(List<Vector3> vertices, List<int>[] trianglesByMaterial, List<Vector2> uvs,
                              int x, int y, int z, int materialIndex)
    {
        if (materialIndex < 0 || materialIndex >= trianglesByMaterial.Length)
            return;

        Vector3 basePosition = new Vector3(x, y, z);
        bool isGrassMaterial = materialIndex == grassMaterialIndex;
        const float halfPixelU = 1f / 32f;
        const float halfPixelV = 1f / 64f;
        Rect fullTile = new Rect(halfPixelU, halfPixelV, 1f - halfPixelU * 2f, 1f - halfPixelV * 2f);
        Rect grassSideTile = new Rect(halfPixelU, halfPixelV, 1f - halfPixelU * 2f, 0.5f - halfPixelV * 2f);
        Rect grassTopTile = new Rect(halfPixelU, 0.5f + halfPixelV, 1f - halfPixelU * 2f, 0.5f - halfPixelV * 2f);

        if (FaceVisible(x, y, z, 0, 0, -1))
            AddQuad(vertices, trianglesByMaterial[materialIndex], uvs, basePosition,
                isGrassMaterial ? grassSideTile : fullTile,
                new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,0,0));

        if (FaceVisible(x, y, z, 0, 0, 1))
            AddQuad(vertices, trianglesByMaterial[materialIndex], uvs, basePosition,
                isGrassMaterial ? grassSideTile : fullTile,
                new Vector3(0,0,1), new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1));

        if (FaceVisible(x, y, z, -1, 0, 0))
            AddQuad(vertices, trianglesByMaterial[materialIndex], uvs, basePosition,
                isGrassMaterial ? grassSideTile : fullTile,
                new Vector3(0,0,0), new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0));

        if (FaceVisible(x, y, z, 1, 0, 0))
            AddQuad(vertices, trianglesByMaterial[materialIndex], uvs, basePosition,
                isGrassMaterial ? grassSideTile : fullTile,
                new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1));

        if (FaceVisible(x, y, z, 0, 1, 0))
            AddQuad(vertices, trianglesByMaterial[materialIndex], uvs, basePosition,
                isGrassMaterial ? grassTopTile : fullTile,
                new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0));

        if (FaceVisible(x, y, z, 0, -1, 0))
            AddQuad(vertices, trianglesByMaterial[materialIndex], uvs, basePosition,
                isGrassMaterial ? grassSideTile : fullTile,
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

    /// <summary>
    /// Sets whether this chunk renderer is visible.
    /// </summary>
    public void SetVisible(bool visible)
    {
        meshRenderer.enabled = visible;
    }
}
