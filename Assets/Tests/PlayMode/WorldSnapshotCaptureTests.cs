using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class WorldSnapshotCaptureTests
{
    [UnityTest]
    public IEnumerator GameplaySceneCreatesWorldSnapshot()
    {
        SceneManager.LoadScene("default");
        yield return new WaitForSecondsRealtime(12f);
        string snapshotPath = Path.Combine(Application.persistentDataPath, "cubeits_world_snapshot.jpg");
        Assert.IsTrue(File.Exists(snapshotPath));
        Assert.Greater(new FileInfo(snapshotPath).Length, 0);

        SceneManager.LoadScene("StartScene");
        yield return new WaitForSecondsRealtime(2f);
    }
}
