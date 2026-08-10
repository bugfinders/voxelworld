using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    int cx, cz;
    int size;
    bool[,,] solid;
    Material material;

    MeshFilter mf;
    MeshRenderer mr;
    MeshCollider mc;
    Mesh mesh;

    public Bounds Bounds { get; private set; }

    public void Init(int cx, int cz, int size, bool[,,] solid, Material material)
    {
        this.cx = cx;
        this.cz = cz;
        this.size = size;
        this.solid = solid;
        this.material = material;

        mf = gameObject.AddComponent<MeshFilter>();
        mr = gameObject.AddComponent<MeshRenderer>();
        mc = gameObject.AddComponent<MeshCollider>();

        mr.material = material;

        Bounds = new Bounds(
            new Vector3(cx * size + size / 2f,
                        (ChunkedVoxelTerrain.MAX_HEIGHT +1  + ChunkedVoxelTerrain.MIN_HEIGHT) * 0.5f,
                        cz * size + size / 2f),
            new Vector3(size,
                        ChunkedVoxelTerrain.MAX_HEIGHT +1 - ChunkedVoxelTerrain.MIN_HEIGHT,
                        size)
        );
    }

    public void BuildMesh()
    {
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        int sx = solid.GetLength(0);
        int sy = solid.GetLength(1);
        int sz = solid.GetLength(2);

        int startX = cx * size;
        int startZ = cz * size;

        for (int x = startX; x < startX + size && x < sx; x++)
        for (int z = startZ; z < startZ + size && z < sz; z++)
        for (int y = 0; y < sy; y++)
        {
            if (!solid[x, y, z]) continue;

            AddCubeFaces(verts, tris, x, y, z);
        }

        if (verts.Count == 0)
        {
            if (mesh != null) mesh.Clear();
            mf.sharedMesh = null;
            mc.sharedMesh = null;
            return;
        }

        // Reuse one Mesh per chunk. Allocating a new one per rebuild leaks the old
        // one — Unity Objects are not collected when they go out of scope.
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = $"ChunkMesh_{cx}_{cz}";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }
        else
        {
            mesh.Clear();
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.sharedMesh = mesh;

        // Assigning the same reference back is a no-op, so the collider would keep
        // its stale cooked data. Clear it first to force a re-cook.
        mc.sharedMesh = null;
        mc.sharedMesh = mesh;
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    bool FaceVisible(int x, int y, int z, int dx, int dy, int dz)
    {
        int nx = x + dx;
        int ny = y + dy;
        int nz = z + dz;

        int sx = solid.GetLength(0);
        int sy = solid.GetLength(1);
        int sz = solid.GetLength(2);

        if (nx < 0 || nx >= sx ||
            ny < 0 || ny >= sy ||
            nz < 0 || nz >= sz)
            return true;

        return !solid[nx, ny, nz];
    }

    void AddCubeFaces(List<Vector3> verts, List<int> tris, int x, int y, int z)
    {
        Vector3 p = new Vector3(x, y, z);

        // FRONT (z-) — corrected winding
        if (FaceVisible(x, y, z, 0, 0, -1))
            AddQuad(verts, tris, p,
                new Vector3(0,0,0),
                new Vector3(0,1,0),
                new Vector3(1,1,0),
                new Vector3(1,0,0));

        // BACK (z+)
        if (FaceVisible(x, y, z, 0, 0, 1))
            AddQuad(verts, tris, p,
                new Vector3(0,0,1),
                new Vector3(1,0,1),
                new Vector3(1,1,1),
                new Vector3(0,1,1));

// LEFT (x-)
        if (FaceVisible(x, y, z, -1, 0, 0))
            AddQuad(verts, tris, p,
                new Vector3(0,0,0),  // bottom-front
                new Vector3(0,0,1),  // bottom-back
                new Vector3(0,1,1),  // top-back
                new Vector3(0,1,0)); // top-front


// RIGHT (x+)
        if (FaceVisible(x, y, z, 1, 0, 0))
            AddQuad(verts, tris, p,
                new Vector3(1,0,0),  // bottom-front
                new Vector3(1,1,0),  // top-front
                new Vector3(1,1,1),  // top-back
                new Vector3(1,0,1)); // bottom-back


        // TOP (y+) — corrected winding
        if (FaceVisible(x, y, z, 0, 1, 0))
            AddQuad(verts, tris, p,
                new Vector3(0,1,0),
                new Vector3(0,1,1),
                new Vector3(1,1,1),
                new Vector3(1,1,0));

        // BOTTOM (y-)
        if (FaceVisible(x, y, z, 0, -1, 0))
            AddQuad(verts, tris, p,
                new Vector3(0,0,0),
                new Vector3(1,0,0),
                new Vector3(1,0,1),
                new Vector3(0,0,1));
    }

    void AddQuad(List<Vector3> verts, List<int> tris, Vector3 basePos,
                 Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int start = verts.Count;

        verts.Add(basePos + a);
        verts.Add(basePos + b);
        verts.Add(basePos + c);
        verts.Add(basePos + d);

        tris.Add(start + 0);
        tris.Add(start + 1);
        tris.Add(start + 2);
        tris.Add(start + 2);
        tris.Add(start + 3);
        tris.Add(start + 0);
    }

    public void SetVisible(bool visible)
    {
        mr.enabled = visible;
       // mc.enabled = visible;
    }
}
