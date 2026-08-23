using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public sealed class WorldSnapshotBackground : MonoBehaviour
{
    [SerializeField] private Texture2D backgroundTexture;
    [SerializeField] private string fallbackColor = "#0A0E16";

    private Texture2D runtimeTexture;

    private void OnEnable()
    {
        UIDocument document = GetComponent<UIDocument>();
        VisualElement root = document.rootVisualElement;
        VisualElement background = new VisualElement { name = "world-snapshot-background" };
        background.style.position = Position.Absolute;
        background.style.left = 0;
        background.style.right = 0;
        background.style.top = 0;
        background.style.bottom = 0;
        background.pickingMode = PickingMode.Ignore;
        background.style.backgroundColor = ParseFallbackColor();

        Texture2D image = backgroundTexture;
        if (image == null)
        {
            string[] candidatePaths = { WorldSnapshotCapture.ProjectSnapshotPath, WorldSnapshotCapture.SnapshotPath };
            for (int i = 0; i < candidatePaths.Length && image == null; i++)
            {
                if (!File.Exists(candidatePaths[i]))
                    continue;

                byte[] imageData = File.ReadAllBytes(candidatePaths[i]);
                runtimeTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (runtimeTexture.LoadImage(imageData))
                    image = runtimeTexture;
                else
                    Destroy(runtimeTexture);
            }
        }

        if (image != null)
        {
            background.style.backgroundImage = Background.FromTexture2D(image);
            background.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
        }

        root.Insert(0, background);
    }

    private void OnDisable()
    {
        if (runtimeTexture != null)
        {
            Destroy(runtimeTexture);
            runtimeTexture = null;
        }
    }

    private Color ParseFallbackColor()
    {
        return ColorUtility.TryParseHtmlString(fallbackColor, out Color color) ? color : new Color(0.04f, 0.055f, 0.086f, 1f);
    }
}
