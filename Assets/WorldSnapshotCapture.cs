using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public sealed class WorldSnapshotCapture : MonoBehaviour
{
    public const string SnapshotFileName = "cubeits_world_snapshot.jpg";
    public const string ProjectSnapshotRelativePath = "Assets/UI/StartWorldBackground.jpg";

    [SerializeField] private ChunkedVoxelTerrain terrain;
    [SerializeField] private int delayFrames = 12;

    private static bool returnToStartMenuAfterCapture;
    private static bool captureInProgress;

    public static string SnapshotPath => Path.Combine(Application.persistentDataPath, SnapshotFileName);
    public static string ProjectSnapshotPath => Path.Combine(Application.dataPath, "UI", "StartWorldBackground.jpg");
    public static bool HasSnapshot => File.Exists(ProjectSnapshotPath) && new FileInfo(ProjectSnapshotPath).Length > 0;
    public static bool CaptureInProgress => captureInProgress;

    public static void RequestStartMenuCapture(string gameplaySceneName)
    {
        if (captureInProgress)
            return;

        returnToStartMenuAfterCapture = true;
        WorldSaveManager.RequestNewGame();
        SceneManager.LoadScene(gameplaySceneName);
    }

    private IEnumerator Start()
    {
        if (HasSnapshot)
            yield break;

        captureInProgress = true;
        if (terrain == null)
            terrain = FindFirstObjectByType<ChunkedVoxelTerrain>();

        while (terrain == null || !terrain.IsInitialized)
            yield return null;

        for (int i = 0; i < Mathf.Max(1, delayFrames); i++)
            yield return null;

        UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        List<UIDocument> enabledDocuments = new List<UIDocument>();
        for (int i = 0; i < documents.Length; i++)
        {
            if (documents[i] != null && documents[i].enabled)
            {
                documents[i].enabled = false;
                enabledDocuments.Add(documents[i]);
            }
        }

        yield return new WaitForEndOfFrame();
        Directory.CreateDirectory(Path.GetDirectoryName(ProjectSnapshotPath));
        ScreenCapture.CaptureScreenshot(ProjectSnapshotPath, 1);
        yield return new WaitUntil(() => File.Exists(ProjectSnapshotPath) && new FileInfo(ProjectSnapshotPath).Length > 0);
        File.Copy(ProjectSnapshotPath, SnapshotPath, true);

        for (int i = 0; i < enabledDocuments.Count; i++)
            enabledDocuments[i].enabled = true;

        Debug.Log("Cubeits world snapshot saved to Assets/UI/StartWorldBackground.jpg.");
        captureInProgress = false;

        if (returnToStartMenuAfterCapture)
        {
            returnToStartMenuAfterCapture = false;
            SceneManager.LoadScene("StartScene");
        }
    }
}
