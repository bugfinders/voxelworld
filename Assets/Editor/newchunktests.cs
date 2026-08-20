using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class NewChunkTests
{
    static bool[,,] Grid(int sx, int sy, int sz, bool fill = false)
    {
        bool[,,] g = new bool[sx, sy, sz];
        if (fill)
            for (int x = 0; x < sx; x++)
            for (int y = 0; y < sy; y++)
            for (int z = 0; z < sz; z++)
                g[x, y, z] = true;
        return g;
    }

    static List<Matrix4x4> Collect(bool[,,] g, int cx, int cz, int size, bool cull)
    {
        List<Matrix4x4> m = new List<Matrix4x4>();
        NewChunk.CollectMatrices(g, cx, cz, size, cull, m);
        return m;
    }

    [Test]
    public void EmptyGridProducesNoMatrices()
    {
        Assert.AreEqual(0, Collect(Grid(4, 4, 4), 0, 0, 4, true).Count);
    }

    [Test]
    public void SingleVoxelIsCentredOnItsCell()
    {
        bool[,,] g = Grid(4, 4, 4);
        g[1, 2, 3] = true;

        List<Matrix4x4> m = Collect(g, 0, 0, 4, true);

        Assert.AreEqual(1, m.Count);
        // CubeMesh is centred on the origin, so the voxel occupying [1,2] x [2,3] x [3,4]
        // needs its translation at the cell centre.
        Assert.AreEqual(new Vector3(1.5f, 2.5f, 3.5f), m[0].GetPosition());
    }

    [Test]
    public void RotationIsIdentityAndScaleIsOne()
    {
        bool[,,] g = Grid(2, 2, 2);
        g[0, 0, 0] = true;

        Matrix4x4 m = Collect(g, 0, 0, 2, true)[0];

        Assert.AreEqual(Quaternion.identity, m.rotation);
        Assert.AreEqual(Vector3.one, m.lossyScale);
    }

    [Test]
    public void ChunkOnlyCoversItsOwnColumns()
    {
        // 4x1x4 fully solid, split into 2x2 chunks of size 2.
        bool[,,] g = Grid(4, 1, 4, true);

        List<Matrix4x4> m = Collect(g, 1, 0, 2, true);

        Assert.AreEqual(4, m.Count);
        foreach (Matrix4x4 mat in m)
        {
            Vector3 p = mat.GetColumn(3);
            Assert.GreaterOrEqual(p.x, 2f, "x below chunk start");
            Assert.Less(p.x, 4f, "x past chunk end");
            Assert.Less(p.z, 2f, "z past chunk end");
        }
    }

    [Test]
    public void ChunkClampsToGridBounds()
    {
        // Chunk size 4 over a grid only 3 wide: no out-of-range read, 3 voxels.
        bool[,,] g = Grid(3, 1, 3, true);

        Assert.AreEqual(9, Collect(g, 0, 0, 4, true).Count);
    }

    [Test]
    public void FullyEnclosedVoxelIsCulled()
    {
        // 3x3x3 solid: the centre voxel is the only one with no exposed face.
        bool[,,] g = Grid(3, 3, 3, true);

        Assert.AreEqual(26, Collect(g, 0, 0, 3, true).Count);
        Assert.AreEqual(27, Collect(g, 0, 0, 3, false).Count, "culling disabled should keep all 27");
    }

    [Test]
    public void GridEdgeCountsAsExposed()
    {
        bool[,,] g = Grid(1, 1, 1, true);

        Assert.AreEqual(1, Collect(g, 0, 0, 1, true).Count);
    }

    [Test]
    public void HasExposedFaceDetectsEachDirection()
    {
        bool[,,] g = Grid(3, 3, 3, true);
        Assert.IsFalse(NewChunk.HasExposedFace(g, 1, 1, 1));

        int[,] dirs = { { -1, 0, 0 }, { 1, 0, 0 }, { 0, -1, 0 }, { 0, 1, 0 }, { 0, 0, -1 }, { 0, 0, 1 } };
        for (int d = 0; d < 6; d++)
        {
            bool[,,] h = Grid(3, 3, 3, true);
            h[1 + dirs[d, 0], 1 + dirs[d, 1], 1 + dirs[d, 2]] = false;
            Assert.IsTrue(NewChunk.HasExposedFace(h, 1, 1, 1), $"direction {d} not detected");
        }
    }

    [Test]
    public void InstanceCountsSplitAcrossBatchLimit()
    {
        // 1024 exposed voxels: one voxel more than a single DrawMeshInstanced call.
        bool[,,] g = Grid(32, 1, 32, true);

        int count = Collect(g, 0, 0, 32, true).Count;

        Assert.AreEqual(1024, count);
        int batches = (count + NewChunk.MAX_INSTANCES - 1) / NewChunk.MAX_INSTANCES;
        Assert.AreEqual(2, batches);
        Assert.AreEqual(1, count - NewChunk.MAX_INSTANCES, "second batch should hold the remainder");
    }

    [Test]
    public void CullingRemovesInteriorOfARealisticChunk()
    {
        // 25x21x25 solid column stack, as the world generator produces at max height.
        bool[,,] g = Grid(25, 21, 25, true);

        int culled = Collect(g, 0, 0, 25, true).Count;
        int all = Collect(g, 0, 0, 25, false).Count;

        Assert.AreEqual(25 * 21 * 25, all);
        Assert.Less(culled, all / 4, "hidden-voxel culling should cut this by well over half");
    }
}
