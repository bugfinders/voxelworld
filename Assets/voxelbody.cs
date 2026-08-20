using UnityEngine;

/// <summary>
/// Collider-free AABB movement against a voxel grid.
/// <see cref="Position"/> is the FEET of the body, centred on X and Z.
/// </summary>
public class VoxelBody
{
    public delegate bool SolidTest(int x, int y, int z);

    // Box edges are nudged inwards by this so a body resting exactly on an
    // integer boundary is not counted as overlapping the voxel it stands on.
    const float EPS = 1e-4f;

    /// <summary>How far above a surface still counts as standing on it.</summary>
    public const float GROUND_TOLERANCE = 0.02f;

    /// <summary>Largest slice the integrator will take, so motion is frame-rate independent.</summary>
    public const float MAX_SUBSTEP = 1f / 60f;

    /// <summary>A frame longer than this is treated as this long, rather than teleporting.</summary>
    public const float MAX_FRAME = 0.25f;

    readonly SolidTest isSolid;

    public float Height { get; }
    public float Radius { get; }

    public Vector3 Position;
    public float VerticalVelocity;
    public bool Grounded { get; private set; }

    public VoxelBody(SolidTest isSolid, float height, float radius)
    {
        this.isSolid = isSolid;
        Height = height;
        Radius = radius;
    }

    /// <summary>True when the body's box, stood at <paramref name="feet"/>, overlaps a solid voxel.</summary>
    public bool Blocked(Vector3 feet)
    {
        int x0 = MinCell(feet.x - Radius), x1 = MaxCell(feet.x + Radius);
        int z0 = MinCell(feet.z - Radius), z1 = MaxCell(feet.z + Radius);
        int y0 = MinCell(feet.y),          y1 = MaxCell(feet.y + Height);

        for (int x = x0; x <= x1; x++)
        for (int z = z0; z <= z1; z++)
        for (int y = y0; y <= y1; y++)
            if (isSolid(x, y, z)) return true;

        return false;
    }

    /// <summary>
    /// Top of the highest solid voxel under the footprint at or below <paramref name="feetY"/>.
    /// Returns 0 — the world floor — when every column below is empty.
    /// </summary>
    public float SurfaceBelow(float x, float z, float feetY)
    {
        int x0 = MinCell(x - Radius), x1 = MaxCell(x + Radius);
        int z0 = MinCell(z - Radius), z1 = MaxCell(z + Radius);
        int startY = Mathf.FloorToInt(feetY + EPS) - 1;

        float best = 0f;

        for (int cx = x0; cx <= x1; cx++)
        for (int cz = z0; cz <= z1; cz++)
        for (int y = startY; y >= 0; y--)
            if (isSolid(cx, y, cz))
            {
                if (y + 1 > best) best = y + 1;
                break;
            }

        return best;
    }

    /// <summary>Drops the body onto whatever is under it. Use after teleporting it.</summary>
    public void SnapToGround()
    {
        Position.y = SurfaceBelow(Position.x, Position.z, Position.y);
        VerticalVelocity = 0f;
        Grounded = true;
    }

    /// <summary>
    /// Launches the body if it is standing on something. The apex is exactly
    /// <paramref name="jumpHeight"/>, which is what caps how high a ledge it can reach.
    /// </summary>
    public bool TryJump(float jumpHeight, float gravity)
    {
        if (!Grounded) return false;

        VerticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        Grounded = false;
        return true;
    }

    /// <summary>
    /// Advances one frame. <paramref name="horizontal"/> is a per-second velocity; its Y is ignored.
    /// </summary>
    public void Step(Vector3 horizontal, float gravity, float dt)
    {
        // Integrating a long frame in one go makes the jump apex depend on frame
        // rate: at ~5 fps a 1.25 apex collapses to 0.7 and no longer clears a
        // voxel. Slicing keeps the reachable step height fixed.
        dt = Mathf.Min(dt, MAX_FRAME);

        while (dt > 0f)
        {
            float slice = Mathf.Min(dt, MAX_SUBSTEP);
            SubStep(horizontal, gravity, slice);
            dt -= slice;
        }
    }

    void SubStep(Vector3 horizontal, float gravity, float dt)
    {
        Vector3 p = Position;

        // X and Z resolve independently, so running at a wall on the diagonal
        // slides along it instead of stopping dead.
        Vector3 tryX = p;
        tryX.x += horizontal.x * dt;
        if (!Blocked(tryX)) p = tryX;

        Vector3 tryZ = p;
        tryZ.z += horizontal.z * dt;
        if (!Blocked(tryZ)) p = tryZ;

        if (!Grounded) VerticalVelocity += gravity * dt;

        float newY = p.y + VerticalVelocity * dt;

        if (VerticalVelocity > 0f)
        {
            Vector3 tryY = p;
            tryY.y = newY;

            if (Blocked(tryY)) VerticalVelocity = 0f; // clipped a ceiling
            else p.y = newY;
        }
        else
        {
            // Clamping against the surface rather than sweeping means a fast fall
            // cannot tunnel through thin ground.
            float support = SurfaceBelow(p.x, p.z, p.y);

            if (newY <= support)
            {
                p.y = support;
                VerticalVelocity = 0f;
            }
            else p.y = newY;
        }

        Position = p;
        Grounded = VerticalVelocity <= 0f &&
                   Position.y - SurfaceBelow(Position.x, Position.z, Position.y) <= GROUND_TOLERANCE;
    }

    // First cell the box overlaps: a box starting exactly at 1.0 starts at cell 1.
    static int MinCell(float v) => Mathf.FloorToInt(v + EPS);

    // Last cell the box overlaps: a box ending exactly at 2.0 ends at cell 1.
    static int MaxCell(float v) => Mathf.CeilToInt(v - EPS) - 1;
}
