using System;
using UnityEditor;
using UnityEngine;

public static class VoxelTextureAssetGenerator
{
    private const int TextureSize = 16;
    private const string RootFolder = "Assets/VoxelTextures";
    private const string TextureFolder = RootFolder + "/Textures";
    private const string MaterialFolder = RootFolder + "/Materials";
    private const string GrassBlockAtlasPath = TextureFolder + "/GrassBlock_16x32.asset";
    private const string WorkbenchAtlasPath = TextureFolder + "/Workbench_16x32.asset";
    [InitializeOnLoadMethod]
    private static void QueueCreation()
    {
        EditorApplication.delayCall += CreateIfMissing;
    }

    private static void CreateIfMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Grass.mat") == null ||
            AssetDatabase.LoadAssetAtPath<Texture2D>(GrassBlockAtlasPath) == null ||
            AssetDatabase.LoadAssetAtPath<Texture2D>(WorkbenchAtlasPath) == null)
            CreateBlockTextures();
        else
            RefreshWorkbenchAtlas();
    }


    private enum BlockKind
    {
        Grass,
        Dirt,
        Stone,
        Sand,
        Wood,
        Leaves,
        CoalOre,
        IronOre,
        GoldOre,
        DiamondOre,
        RedstoneOre
    }

    [MenuItem("Tools/Voxel/Create Stylized 16x16 Block Textures")]
    public static void CreateBlockTextures()
    {
        EnsureFolder("Assets", "VoxelTextures");
        EnsureFolder(RootFolder, "Textures");
        EnsureFolder(RootFolder, "Materials");

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            throw new InvalidOperationException("No compatible Unity material shader was found.");

        foreach (BlockKind kind in Enum.GetValues(typeof(BlockKind)))
        {
            string label = GetLabel(kind);
            string texturePath = $"{TextureFolder}/{label}_16x16.asset";
            string materialPath = $"{MaterialFolder}/{label}.mat";

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                texture = BuildTexture(kind, label);
                AssetDatabase.CreateAsset(texture, texturePath);
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = label,
                    enableInstancing = true
                };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.05f);
            EditorUtility.SetDirty(material);
        }

        Texture2D grassBlockAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassBlockAtlasPath);
        if (grassBlockAtlas == null)
        {
            grassBlockAtlas = BuildGrassBlockAtlas();
            AssetDatabase.CreateAsset(grassBlockAtlas, GrassBlockAtlasPath);
        }

        Material grassMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Grass.mat");
        grassMaterial.shader = shader;
        grassMaterial.SetTexture("_BaseMap", grassBlockAtlas);
        grassMaterial.SetColor("_BaseColor", Color.white);
        grassMaterial.SetFloat("_Smoothness", 0.05f);
        EditorUtility.SetDirty(grassMaterial);

        Material workbenchMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Workbench.mat");
        if (workbenchMaterial != null)
        {
            Texture2D workbenchAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(WorkbenchAtlasPath);
            if (workbenchAtlas == null)
            {
                workbenchAtlas = BuildWorkbenchAtlas();
                AssetDatabase.CreateAsset(workbenchAtlas, WorkbenchAtlasPath);
            }

            workbenchMaterial.shader = shader;
            workbenchMaterial.SetTexture("_BaseMap", workbenchAtlas);
            workbenchMaterial.SetColor("_BaseColor", Color.white);
            workbenchMaterial.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(workbenchMaterial);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static Texture2D BuildGrassBlockAtlas()
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize * 2, TextureFormat.RGBA32, false, false)
        {
            name = "GrassBlock_16x32",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0
        };

        Color[] pixels = new Color[TextureSize * TextureSize * 2];
        for (int y = 0; y < TextureSize * 2; y++)
        for (int x = 0; x < TextureSize; x++)
        {
            BlockKind sourceKind = y < TextureSize ? BlockKind.Dirt : BlockKind.Grass;
            pixels[y * TextureSize + x] = Sample(sourceKind, x, y % TextureSize);
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static void RefreshWorkbenchAtlas()
    {
        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(WorkbenchAtlasPath);
        if (atlas == null)
            return;

        Texture2D generated = BuildWorkbenchAtlas();
        atlas.SetPixels(generated.GetPixels());
        atlas.Apply(false, false);
        EditorUtility.SetDirty(atlas);
        UnityEngine.Object.DestroyImmediate(generated);
        AssetDatabase.SaveAssets();
    }

    private static Texture2D BuildWorkbenchAtlas()
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize * 2, TextureFormat.RGBA32, false, false)
        {
            name = "Workbench_16x32",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0
        };

        Color[] pixels = new Color[TextureSize * TextureSize * 2];
        for (int y = 0; y < TextureSize * 2; y++)
        for (int x = 0; x < TextureSize; x++)
        {
            Color color = y < TextureSize ? SampleWorkbenchSide(x, y) : SampleWorkbenchTop(x, y - TextureSize);
            pixels[y * TextureSize + x] = color;
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static Color SampleWorkbenchSide(int x, int y)
    {
        Color color = Sample(BlockKind.Wood, x, y);
        bool edge = x == 1 || x == TextureSize - 2 || y == TextureSize - 2;
        if (edge)
            color = new Color(0.10f, 0.045f, 0.018f, 1f);
        return color;
    }

    private static Color SampleWorkbenchTop(int x, int y)
    {
        int value = Hash(x, y, 913);
        Color color = Palette(value, new Color(0.48f, 0.25f, 0.10f), new Color(0.68f, 0.38f, 0.14f), new Color(0.31f, 0.14f, 0.05f));
        bool plankLine = y == 3 || y == 8 || y == 13;
        bool grainLine = (x + y * 2) % 7 == 0;
        if (plankLine)
            color *= 0.58f;
        else if (grainLine)
            color *= 1.10f;

        Color edgeColor = new Color(0.10f, 0.045f, 0.018f, 1f);
        bool tabletopEdge = x == 1 || x == TextureSize - 2 || y == 1 || y == TextureSize - 2;
        if (tabletopEdge)
            color = edgeColor;

        return new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), 1f);
    }

    private static Texture2D BuildTexture(BlockKind kind, string label)
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, false)
        {
            name = label + "_16x16",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            anisoLevel = 0
        };

        Color[] pixels = new Color[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                pixels[y * TextureSize + x] = Sample(kind, x, y);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static Color Sample(BlockKind kind, int x, int y)
    {
        int value = Hash(x, y, (int)kind * 37 + 11);
        Color baseColor;

        switch (kind)
        {
            case BlockKind.Grass:
                baseColor = Palette(value, new Color(0.25f, 0.68f, 0.22f), new Color(0.40f, 0.82f, 0.27f), new Color(0.16f, 0.48f, 0.16f));
                if ((x * 7 + y * 11 + value) % 29 == 0)
                    baseColor = new Color(0.72f, 0.88f, 0.30f);
                break;
            case BlockKind.Dirt:
                baseColor = Palette(value, new Color(0.45f, 0.25f, 0.12f), new Color(0.62f, 0.36f, 0.17f), new Color(0.32f, 0.16f, 0.08f));
                if ((x * 5 + y * 3 + value) % 23 == 0)
                    baseColor = new Color(0.74f, 0.45f, 0.22f);
                break;
            case BlockKind.Stone:
                baseColor = Palette(value, new Color(0.42f, 0.46f, 0.50f), new Color(0.62f, 0.66f, 0.69f), new Color(0.28f, 0.31f, 0.35f));
                break;
            case BlockKind.Sand:
                baseColor = Palette(value, new Color(0.82f, 0.68f, 0.36f), new Color(0.98f, 0.86f, 0.52f), new Color(0.68f, 0.52f, 0.25f));
                if ((x * 13 + y * 7 + value) % 31 == 0)
                    baseColor = new Color(1.0f, 0.91f, 0.62f);
                break;
            case BlockKind.Wood:
                int seam = (x + (y % 3)) % 7;
                baseColor = Palette(value, new Color(0.48f, 0.25f, 0.10f), new Color(0.72f, 0.42f, 0.16f), new Color(0.30f, 0.13f, 0.05f));
                if (seam == 0 || seam == 1)
                    baseColor *= 0.65f;
                else if (seam == 5)
                    baseColor *= 1.12f;
                break;
            case BlockKind.Leaves:
                baseColor = Palette(value, new Color(0.16f, 0.56f, 0.22f), new Color(0.36f, 0.78f, 0.28f), new Color(0.08f, 0.34f, 0.14f));
                if ((x * 3 + y * 5 + value) % 17 == 0)
                    baseColor = new Color(0.58f, 0.86f, 0.30f);
                break;
            default:
                baseColor = SampleOre(kind, x, y, value);
                break;
        }

        return new Color(Mathf.Clamp01(baseColor.r), Mathf.Clamp01(baseColor.g), Mathf.Clamp01(baseColor.b), 1f);
    }

    private static Color SampleOre(BlockKind kind, int x, int y, int value)
    {
        Color stone = Palette(value, new Color(0.34f, 0.38f, 0.42f), new Color(0.55f, 0.59f, 0.62f), new Color(0.23f, 0.26f, 0.30f));
        int oreHash = Hash(x / 2, y / 2, (int)kind * 101 + 3);
        bool orePixel = oreHash % 7 == 0 || ((x + y * 3 + oreHash) % 19 == 0);
        if (!orePixel)
            return stone;

        switch (kind)
        {
            case BlockKind.CoalOre:
                return Palette(value, new Color(0.06f, 0.08f, 0.10f), new Color(0.16f, 0.19f, 0.22f), new Color(0.02f, 0.03f, 0.04f));
            case BlockKind.IronOre:
                return Palette(value, new Color(0.78f, 0.49f, 0.31f), new Color(0.95f, 0.68f, 0.43f), new Color(0.54f, 0.29f, 0.20f));
            case BlockKind.GoldOre:
                return Palette(value, new Color(1.0f, 0.68f, 0.05f), new Color(1.0f, 0.90f, 0.20f), new Color(0.82f, 0.40f, 0.02f));
            case BlockKind.DiamondOre:
                return Palette(value, new Color(0.10f, 0.78f, 0.88f), new Color(0.42f, 1.0f, 0.94f), new Color(0.04f, 0.45f, 0.68f));
            case BlockKind.RedstoneOre:
                return Palette(value, new Color(0.88f, 0.08f, 0.05f), new Color(1.0f, 0.26f, 0.08f), new Color(0.52f, 0.02f, 0.02f));
            default:
                return stone;
        }
    }

    private static Color Palette(int value, Color mid, Color light, Color dark)
    {
        if (value % 9 == 0)
            return light;
        if (value % 7 == 0)
            return dark;
        return mid;
    }

    private static int Hash(int x, int y, int seed)
    {
        unchecked
        {
            int hash = seed + x * 374761393 + y * 668265263;
            hash = (hash ^ (hash >> 13)) * 1274126177;
            return hash ^ (hash >> 16) & int.MaxValue;
        }
    }

    private static string GetLabel(BlockKind kind)
    {
        switch (kind)
        {
            case BlockKind.CoalOre: return "CoalOre";
            case BlockKind.IronOre: return "IronOre";
            case BlockKind.GoldOre: return "GoldOre";
            case BlockKind.DiamondOre: return "DiamondOre";
            case BlockKind.RedstoneOre: return "RedstoneOre";
            default: return kind.ToString();
        }
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
