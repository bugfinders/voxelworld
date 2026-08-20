using NUnit.Framework;
using UnityEngine;

// Serialized scene values win over field initializers, so a stale inspector value
// can disable movement entirely while every other test still passes. That is
// exactly what happened with gravity = 0, hence these.
public class ControllerTuningTests
{
    [Test]
    public void ZeroGravityIsReplaced()
    {
        Assert.AreEqual(-9.8f, VoxelPlayerController.SanitiseGravity(0f), 1e-4f);
    }

    [Test]
    public void PositiveGravityIsReplaced()
    {
        Assert.AreEqual(-9.8f, VoxelPlayerController.SanitiseGravity(20f), 1e-4f);
    }

    [Test]
    public void NegativeGravityIsKept()
    {
        Assert.AreEqual(-20f, VoxelPlayerController.SanitiseGravity(-20f), 1e-4f);
    }

    [Test]
    public void ZeroGravityWouldOtherwiseKillTheJump()
    {
        // sqrt(h * -2 * 0) is 0, so the launch velocity vanishes.
        Assert.AreEqual(0f, Mathf.Sqrt(1.25f * -2f * 0f), 1e-6f);
        Assert.Greater(Mathf.Sqrt(1.25f * -2f * VoxelPlayerController.SanitiseGravity(0f)), 0f);
    }

    [TestCase(0f)]
    [TestCase(1f)]
    [TestCase(0.5f)]
    public void JumpTooLowToClearAVoxelIsRaised(float jumpHeight)
    {
        Assert.Greater(VoxelPlayerController.SanitiseJumpHeight(jumpHeight), 1f);
    }

    [TestCase(2f)]
    [TestCase(5f)]
    public void JumpHighEnoughToClearTwoVoxelsIsLowered(float jumpHeight)
    {
        Assert.Less(VoxelPlayerController.SanitiseJumpHeight(jumpHeight), 2f);
    }

    [TestCase(1.25f)]
    [TestCase(1.5f)]
    [TestCase(1.15f)]
    public void UsableJumpHeightIsKept(float jumpHeight)
    {
        Assert.AreEqual(jumpHeight, VoxelPlayerController.SanitiseJumpHeight(jumpHeight), 1e-4f);
    }

    [Test]
    public void SanitisedTuningSatisfiesTheStepRule()
    {
        // Whatever comes out must clear one voxel and fall short of two, since the
        // apex is what decides which ledges are reachable.
        foreach (float raw in new[] { 0f, 1f, 1.25f, 1.5f, 2f, 99f })
        {
            float h = VoxelPlayerController.SanitiseJumpHeight(raw);
            Assert.Greater(h, 1f, $"raw {raw}");
            Assert.Less(h, 2f, $"raw {raw}");
        }
    }
}
