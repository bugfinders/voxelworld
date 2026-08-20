using NUnit.Framework;
using UnityEngine;

public class VoxelBodyTests
{
    const float GRAVITY = -9.8f;
    const float JUMP = 1.25f;
    const float DT = 1f / 60f;
    const float SPEED = 6f;

    // A 16x8x16 grid. Ground fills y=0, so its top face is at y=1.
    static bool[,,] Flat()
    {
        bool[,,] g = new bool[16, 8, 16];
        for (int x = 0; x < 16; x++)
        for (int z = 0; z < 16; z++)
            g[x, 0, z] = true;
        return g;
    }

    // Raises the columns at x >= 8 to the given height, forming a step to walk into.
    static void AddStep(bool[,,] g, int height)
    {
        for (int x = 8; x < 16; x++)
        for (int z = 0; z < 16; z++)
        for (int y = 0; y < height; y++)
            g[x, y, z] = true;
    }

    static VoxelBody Body(bool[,,] g, Vector3 feet)
    {
        VoxelBody b = new VoxelBody(
            (x, y, z) =>
            {
                if (x < 0 || x >= g.GetLength(0)) return false;
                if (y < 0 || y >= g.GetLength(1)) return false;
                if (z < 0 || z >= g.GetLength(2)) return false;
                return g[x, y, z];
            },
            2f, 0.3f);

        b.Position = feet;
        b.SnapToGround();
        return b;
    }

    // Open world with a floor whose top face is y=1, for pure falling tests.
    static VoxelBody FloorOnly(Vector3 feet)
    {
        VoxelBody b = new VoxelBody((x, y, z) => y < 1, 2f, 0.3f);
        b.Position = feet;
        return b;
    }

    // Walks along +X for the given time, optionally jumping on the first frame.
    static void Walk(VoxelBody b, float seconds, bool jump = false)
    {
        int frames = Mathf.RoundToInt(seconds / DT);
        for (int i = 0; i < frames; i++)
        {
            if (jump && i == 0) b.TryJump(JUMP, GRAVITY);
            b.Step(new Vector3(SPEED, 0f, 0f), GRAVITY, DT);
        }
    }

    [Test]
    public void SpawnSnapsFeetToSurface()
    {
        VoxelBody b = Body(Flat(), new Vector3(4.5f, 6f, 4.5f));

        Assert.AreEqual(1f, b.Position.y, 1e-4f);
        Assert.IsTrue(b.Grounded);
    }

    [Test]
    public void WalksFreelyOnFlatGround()
    {
        VoxelBody b = Body(Flat(), new Vector3(1.5f, 1f, 4.5f));
        Walk(b, 0.5f);

        Assert.AreEqual(1.5f + SPEED * 0.5f, b.Position.x, 0.05f);
        Assert.AreEqual(1f, b.Position.y, 1e-3f);
    }

    [Test]
    public void OneVoxelStepBlocksAWalk()
    {
        bool[,,] g = Flat();
        AddStep(g, 2); // top face at y=2, one voxel above the y=1 ground

        VoxelBody b = Body(g, new Vector3(6.5f, 1f, 4.5f));
        Walk(b, 1f);

        Assert.Less(b.Position.x, 8f, "should be stopped by the step, not climb it");
        Assert.AreEqual(1f, b.Position.y, 1e-3f);
    }

    [Test]
    public void JumpClearsAOneVoxelStep()
    {
        bool[,,] g = Flat();
        AddStep(g, 2);

        VoxelBody b = Body(g, new Vector3(7.0f, 1f, 4.5f));
        Walk(b, 1.5f, jump: true);

        Assert.Greater(b.Position.x, 8f, "should have landed on top of the step");
        Assert.AreEqual(2f, b.Position.y, 1e-3f);
        Assert.IsTrue(b.Grounded);
    }

    [Test]
    public void JumpCannotClearATwoVoxelStep()
    {
        bool[,,] g = Flat();
        AddStep(g, 3); // top face at y=3, two voxels above the ground

        VoxelBody b = Body(g, new Vector3(7.0f, 1f, 4.5f));
        Walk(b, 1.5f, jump: true);

        Assert.Less(b.Position.x, 8f, "two voxels is too high to leap");
        Assert.AreEqual(1f, b.Position.y, 1e-3f);
    }

    [Test]
    public void JumpApexIsExactlyJumpHeight()
    {
        VoxelBody b = Body(Flat(), new Vector3(4.5f, 1f, 4.5f));
        b.TryJump(JUMP, GRAVITY);

        float apex = b.Position.y;
        for (int i = 0; i < 200; i++)
        {
            b.Step(Vector3.zero, GRAVITY, DT);
            if (b.Position.y > apex) apex = b.Position.y;
        }

        // Over one voxel of clearance and under two: this is what caps the step height.
        Assert.Greater(apex - 1f, 1f);
        Assert.Less(apex - 1f, 2f);
        Assert.AreEqual(1f + JUMP, apex, 0.05f);
    }

    [Test]
    public void CannotJumpWhileAirborne()
    {
        VoxelBody b = Body(Flat(), new Vector3(4.5f, 1f, 4.5f));

        Assert.IsTrue(b.TryJump(JUMP, GRAVITY));
        b.Step(Vector3.zero, GRAVITY, DT);
        Assert.IsFalse(b.TryJump(JUMP, GRAVITY));
    }

    [Test]
    public void WalkingOffALedgeFallsToTheLowerLevel()
    {
        bool[,,] g = Flat();
        AddStep(g, 4); // stand on the high side, then walk off the x=8 edge going -X

        VoxelBody b = Body(g, new Vector3(9f, 4f, 4.5f));

        // Walk off the edge, then stand still long enough to land. Kept short so
        // the body stays inside the test grid rather than running off into the void.
        for (int i = 0; i < 30; i++) b.Step(new Vector3(-SPEED, 0f, 0f), GRAVITY, DT);
        Assert.Less(b.Position.y, 4f, "should already be falling");
        for (int i = 0; i < 120; i++) b.Step(Vector3.zero, GRAVITY, DT);

        Assert.Less(b.Position.x, 8f);
        Assert.AreEqual(1f, b.Position.y, 1e-3f, "should have fallen to the lower surface");
        Assert.IsTrue(b.Grounded);
    }

    [Test]
    public void FallAcceleratesAtGravity()
    {
        VoxelBody b = FloorOnly(new Vector3(4.5f, 6f, 4.5f));

        // v = g*t, so after half a second of free fall the speed is 4.9 m/s down.
        for (int i = 0; i < 30; i++) b.Step(Vector3.zero, GRAVITY, DT);

        Assert.AreEqual(GRAVITY * 0.5f, b.VerticalVelocity, 0.2f);
        Assert.Less(b.Position.y, 6f);
    }

    [Test]
    public void FallStopsExactlyOnTheSurface()
    {
        VoxelBody b = FloorOnly(new Vector3(4.5f, 7f, 4.5f));

        for (int i = 0; i < 300; i++) b.Step(Vector3.zero, GRAVITY, DT);

        Assert.AreEqual(1f, b.Position.y, 1e-4f);
        Assert.AreEqual(0f, b.VerticalVelocity, 1e-4f);
        Assert.IsTrue(b.Grounded);
    }

    [Test]
    public void FastFallDoesNotTunnelThroughGround()
    {
        VoxelBody b = FloorOnly(new Vector3(4.5f, 4f, 4.5f));
        b.VerticalVelocity = -500f;

        b.Step(Vector3.zero, GRAVITY, DT);

        Assert.AreEqual(1f, b.Position.y, 1e-4f);
    }

    [Test]
    public void BlockedAxisStillSlidesAlongTheOther()
    {
        bool[,,] g = Flat();
        AddStep(g, 2);

        VoxelBody b = Body(g, new Vector3(7.5f, 1f, 4.5f));

        // Pushing diagonally into the step: X is refused, Z is not.
        for (int i = 0; i < 30; i++) b.Step(new Vector3(SPEED, 0f, SPEED), GRAVITY, DT);

        Assert.Less(b.Position.x, 8f);
        Assert.Greater(b.Position.z, 4.5f);
    }

    [Test]
    public void HeadHitsCeilingAndStopsRising()
    {
        // Ground at y=0 plus a slab at y=3: standing room, but no room to jump.
        bool[,,] g = Flat();
        for (int x = 0; x < 16; x++)
        for (int z = 0; z < 16; z++)
            g[x, 3, z] = true;

        VoxelBody b = Body(g, new Vector3(4.5f, 1f, 4.5f));
        b.TryJump(JUMP, GRAVITY);

        for (int i = 0; i < 60; i++)
        {
            b.Step(Vector3.zero, GRAVITY, DT);
            Assert.LessOrEqual(b.Position.y + b.Height, 3.0001f, "must never overlap the slab");
        }

        Assert.AreEqual(1f, b.Position.y, 1e-3f, "should be back on the floor");
    }

    [Test]
    public void RadiusStopsCornerClipping()
    {
        bool[,,] g = Flat();
        g[6, 1, 4] = true; // single voxel at knee height beside the walk line

        VoxelBody b = Body(g, new Vector3(4.5f, 1f, 4.5f));

        // Radius 0.3 spans z 4.2..4.8, which overlaps cell z=4 only.
        Assert.IsTrue(b.Blocked(new Vector3(6.5f, 1f, 4.5f)));
        Assert.IsFalse(b.Blocked(new Vector3(6.5f, 1f, 5.5f)));
    }

    [Test]
    public void RestingOnASurfaceIsNotAnOverlap()
    {
        VoxelBody b = Body(Flat(), new Vector3(4.5f, 1f, 4.5f));

        Assert.IsFalse(b.Blocked(new Vector3(4.5f, 1f, 4.5f)), "feet exactly on the top face");
        Assert.IsTrue(b.Blocked(new Vector3(4.5f, 0.99f, 4.5f)), "sunk into the ground");
    }

    // Simulates the same wall-clock duration at a given frame time.
    static float ApexOverTime(float frameDt, float seconds)
    {
        VoxelBody b = Body(Flat(), new Vector3(4.5f, 1f, 4.5f));
        b.TryJump(JUMP, GRAVITY);

        float apex = b.Position.y;
        int frames = Mathf.RoundToInt(seconds / frameDt);
        for (int i = 0; i < frames; i++)
        {
            b.Step(Vector3.zero, GRAVITY, frameDt);
            if (b.Position.y > apex) apex = b.Position.y;
        }
        return apex - 1f;
    }

    [Test]
    public void JumpApexDoesNotDependOnFrameRate()
    {
        // Integrating a whole slow frame at once collapsed the apex to ~0.7,
        // which silently made a one-voxel step unclearable.
        float at60 = ApexOverTime(1f / 60f, 2f);

        foreach (float dt in new[] { 1f / 30f, 1f / 20f, 1f / 10f, 1f / 5f })
        {
            // The apex is only sampled once per frame, so a coarse frame can miss
            // the true peak by the distance gravity covers in one frame.
            float samplingError = 0.5f * Mathf.Abs(GRAVITY) * dt * dt;

            Assert.AreEqual(at60, ApexOverTime(dt, 2f), samplingError + 0.001f, $"dt {dt}");
        }
    }

    [TestCase(1f / 60f)]
    [TestCase(1f / 30f)]
    [TestCase(1f / 10f)]
    [TestCase(1f / 5f)]
    public void JumpAlwaysClearsOneVoxelAndNeverTwo(float frameDt)
    {
        float rise = ApexOverTime(frameDt, 2f);

        Assert.Greater(rise, 1f, $"cannot clear a one-voxel step at dt {frameDt}");
        Assert.Less(rise, 2f, $"could clear a two-voxel step at dt {frameDt}");
    }

    [Test]
    public void OneVoxelStepIsClimbableAtALowFrameRate()
    {
        bool[,,] g = Flat();
        AddStep(g, 2);

        VoxelBody b = Body(g, new Vector3(7.0f, 1f, 4.5f));
        b.TryJump(JUMP, GRAVITY);
        for (int i = 0; i < 15; i++) b.Step(new Vector3(SPEED, 0f, 0f), GRAVITY, 1f / 10f);

        Assert.Greater(b.Position.x, 8f);
        Assert.AreEqual(2f, b.Position.y, 1e-3f);
    }

    [Test]
    public void ALongFrameDoesNotTeleportTheBody()
    {
        VoxelBody b = Body(Flat(), new Vector3(4.5f, 1f, 4.5f));

        b.Step(new Vector3(SPEED, 0f, 0f), GRAVITY, 10f);

        // Clamped to MAX_FRAME, so at most 0.25s of travel.
        Assert.LessOrEqual(b.Position.x - 4.5f, SPEED * VoxelBody.MAX_FRAME + 1e-3f);
    }
}
