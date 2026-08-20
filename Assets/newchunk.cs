using System.Collections.Generic;
using UnityEngine;

public class NewChunk : MonoBehaviour
{
    // Graphics.DrawMeshInstanced draws at most 1023 instances per call, so a
    // chunk's transforms are split across however many batches that needs.
    public const int MAX_INSTANCES = 1023;

    int cx, cz;
    int size;
    bool[,,] solid;
    Material material;
    Mesh mesh;
    bool cullHiddenVoxels;

    Matrix4x4[][] batches = new Matrix4x4[0][];
    int[] batchCounts = new int[0];
    int instanceCount;
    bool visible;

    // Shared staging buffer: only one chunk builds at a time and this keeps the
    // per-rebuild allocation out of the profiler.
    static readonly List<Matrix4x4> scratch = new List<Matrix4x4>();

    public Bounds Bounds { get; private set; }
    public int InstanceCount => instanceCount;
    public int BatchCount => batches.Length;

    public void Init(int cx, int cz, int size, bool[,,] solid, Mesh mesh, Material material,
                     bool cullHiddenVoxels = true)
    {
        this.cx = cx;
        this.cz = cz;
        this.size = size;
        this.solid = solid;
        this.mesh = mesh;
        this.material = material;
        this.cullHiddenVoxels = cullHiddenVoxels;

        Bounds = new Bounds(
            new Vector3(cx * size + size / 2f,
                        (NewWorld.MAX_HEIGHT + 1 + NewWorld.MIN_HEIGHT) * 0.5f,
                        cz * size + size / 2f),
            new Vector3(size,
                        NewWorld.MAX_HEIGHT + 1 - NewWorld.MIN_HEIGHT,
                        size)
        );

        Rebuild();
    }

    /// <summary>Rebuilds the instance transforms from the current solid grid.</summary>
    public void Rebuild()
    {
        scratch.Clear();
        CollectMatrices(solid, cx, cz, size, cullHiddenVoxels, scratch);
        instanceCount = scratch.Count;

        int needed = (instanceCount + MAX_INSTANCES - 1) / MAX_INSTANCES;
        if (batches.Length != needed)
        {
            Matrix4x4[][] grown = new Matrix4x4[needed][];
            for (int b = 0; b < needed && b < batches.Length; b++) grown[b] = batches[b];
            batches = grown;
            batchCounts = new int[needed];
        }

        for (int b = 0; b < needed; b++)
        {
            // Every batch is allocated full size so the arrays survive a rebuild
            // that changes the voxel count; DrawMeshInstanced takes the count.
            if (batches[b] == null) batches[b] = new Matrix4x4[MAX_INSTANCES];

            int count = Mathf.Min(MAX_INSTANCES, instanceCount - b * MAX_INSTANCES);
            scratch.CopyTo(b * MAX_INSTANCES, batches[b], 0, count);
            batchCounts[b] = count;
        }
    }

    /// <summary>
    /// Fills <paramref name="into"/> with one TRS matrix per drawn voxel in the chunk.
    /// Rotation is identity and scale is one; only the translation varies.
    /// </summary>
    public static void CollectMatrices(bool[,,] solid, int cx, int cz, int size,
                                       bool cullHiddenVoxels, List<Matrix4x4> into)
    {
        int sx = solid.GetLength(0);
        int sy = solid.GetLength(1);
        int sz = solid.GetLength(2);

        int startX = cx * size;
        int startZ = cz * size;
        int endX = Mathf.Min(startX + size, sx);
        int endZ = Mathf.Min(startZ + size, sz);

        for (int x = startX; x < endX; x++)
        for (int z = startZ; z < endZ; z++)
        for (int y = 0; y < sy; y++)
        {
            if (!solid[x, y, z]) continue;
            if (cullHiddenVoxels && !HasExposedFace(solid, x, y, z)) continue;

            // CubeMesh is a unit cube centred on the origin, so the voxel that
            // occupies [x,x+1] has its centre half a unit along each axis.
            into.Add(Matrix4x4.TRS(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f),
                                   Quaternion.identity, Vector3.one));
        }
    }

    /// <summary>True when at least one of the six neighbours is air or out of bounds.</summary>
    public static bool HasExposedFace(bool[,,] solid, int x, int y, int z)
    {
        return !IsSolid(solid, x - 1, y, z) || !IsSolid(solid, x + 1, y, z)
            || !IsSolid(solid, x, y - 1, z) || !IsSolid(solid, x, y + 1, z)
            || !IsSolid(solid, x, y, z - 1) || !IsSolid(solid, x, y, z + 1);
    }

    static bool IsSolid(bool[,,] solid, int x, int y, int z)
    {
        if (x < 0 || x >= solid.GetLength(0)) return false;
        if (y < 0 || y >= solid.GetLength(1)) return false;
        if (z < 0 || z >= solid.GetLength(2)) return false;
        return solid[x, y, z];
    }

    /// <summary>Submits the chunk's instances for this frame. Must be called from Update.</summary>
    public void Draw()
    {
        if (!visible || instanceCount == 0 || mesh == null || material == null) return;

        for (int b = 0; b < batches.Length; b++)
            Graphics.DrawMeshInstanced(mesh, 0, material, batches[b], batchCounts[b]);
    }

    public void SetVisible(bool visible)
    {
        this.visible = visible;
    }
}
