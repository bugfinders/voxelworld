using System;

using System.IO;
using UnityEditor;
using UnityEngine;

public static class VoxelTextureAssetGenerator
{
    private const int TextureSize = 16;
    private const string RootFolder = "Assets/VoxelTextures";
    private const string TextureFolder = RootFolder + "/Textures";
    private const string MaterialFolder = RootFolder + "/Materials";
    private const string GrassBlockAtlasPath = TextureFolder + "/GrassBlock_16x32.png";
    private const string WorkbenchAtlasPath = TextureFolder + "/Workbench_16x32.png";
    private const string ChestAtlasPath = TextureFolder + "/Chest_16x32.png";


    [InitializeOnLoadMethod]
    private static void QueueCreation()
    {
        EditorApplication.delayCall += CreateIfMissing;
    }


    private static void CreateIfMissing()
    {
        if (HasLegacyTextureAssets())
        {
            MigrateLegacyTextureAssets();
            return;
        }

        CreateBlockTextures();
    }
    private static bool HasLegacyTextureAssets()
    {
        string[] legacyPaths =
        {
            TextureFolder + "/CoalOre_16x16.asset",
            TextureFolder + "/DiamondOre_16x16.asset",
            TextureFolder + "/Dirt_16x16.asset",
            TextureFolder + "/GoldOre_16x16.asset",
            TextureFolder + "/GrassBlock_16x32.asset",
            TextureFolder + "/Grass_16x16.asset",
            TextureFolder + "/IronOre_16x16.asset",
            TextureFolder + "/Leaves_16x16.asset",
            TextureFolder + "/RedstoneOre_16x16.asset",
            TextureFolder + "/Sand_16x16.asset",
            TextureFolder + "/Stone_16x16.asset",
            TextureFolder + "/Wood_16x16.asset",
            TextureFolder + "/Workbench_16x32.asset",
            TextureFolder + "/ClayBrick_16x16.asset",
            TextureFolder + "/ClayBrick_Height_16x16.asset",
            TextureFolder + "/ClayBrick_Normal_16x16.asset",
            TextureFolder + "/StoneBrick_16x16.asset",
            TextureFolder + "/StoneBrick_Height_16x16.asset",
            TextureFolder + "/StoneBrick_Normal_16x16.asset"
        };

        for (int i = 0; i < legacyPaths.Length; i++)
        {
            if (File.Exists(ToAbsolutePath(legacyPaths[i])))
                return true;
        }

        return false;
    }

    private static void MigrateLegacyTextureAssets()
    {
        CreateBlockTextures();
        UpdateEditableTextureReferences();

        string[] legacyPaths =
        {
            TextureFolder + "/CoalOre_16x16.asset",
            TextureFolder + "/DiamondOre_16x16.asset",
            TextureFolder + "/Dirt_16x16.asset",
            TextureFolder + "/GoldOre_16x16.asset",
            TextureFolder + "/GrassBlock_16x32.asset",
            TextureFolder + "/Grass_16x16.asset",
            TextureFolder + "/IronOre_16x16.asset",
            TextureFolder + "/Leaves_16x16.asset",
            TextureFolder + "/RedstoneOre_16x16.asset",
            TextureFolder + "/Sand_16x16.asset",
            TextureFolder + "/Stone_16x16.asset",
            TextureFolder + "/Wood_16x16.asset",
            TextureFolder + "/Workbench_16x32.asset",
            TextureFolder + "/ClayBrick_16x16.asset",
            TextureFolder + "/ClayBrick_Height_16x16.asset",
            TextureFolder + "/ClayBrick_Normal_16x16.asset",
            TextureFolder + "/StoneBrick_16x16.asset",
            TextureFolder + "/StoneBrick_Height_16x16.asset",
            TextureFolder + "/StoneBrick_Normal_16x16.asset"
        };

        for (int i = 0; i < legacyPaths.Length; i++)
        {
            if (File.Exists(ToAbsolutePath(legacyPaths[i])))
                AssetDatabase.DeleteAsset(legacyPaths[i]);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static string ToAbsolutePath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }

    private static Texture2D LoadOrCreatePngTexture(string assetPath, Func<Texture2D> builder, bool clamp)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (texture != null)
        {
            ConfigureTextureImporter(assetPath, assetPath.Contains("_Normal_"), clamp);
            return texture;
        }

        string absolutePath = ToAbsolutePath(assetPath);
        if (!File.Exists(absolutePath))
        {
            Texture2D generated = builder();
            File.WriteAllBytes(absolutePath, generated.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(generated);
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        ConfigureTextureImporter(assetPath, assetPath.Contains("_Normal_"), clamp);
        texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (texture == null)
            throw new InvalidOperationException("Failed to import texture: " + assetPath);
        return texture;
    }

    private static void ConfigureTextureImporter(string assetPath, bool normalMap, bool clamp)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normalMap && !assetPath.Contains("_Height_");
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        importer.SaveAndReimport();
    }

    private static void UpdateEditableTextureReferences()
    {
        UpdateItemTextureReference("Assets/Crafting/Items/ClayBrick.asset", TextureFolder + "/ClayBrick_16x16.png");
        UpdateItemTextureReference("Assets/Crafting/Items/StoneBrick.asset", TextureFolder + "/StoneBrick_16x16.png");
        UpdateRecipeTextureReference("Assets/Crafting/Recipes/StoneBrick.asset", TextureFolder + "/StoneBrick_16x16.png");

        UpdateRecipeTextureReference("Assets/Crafting/Recipes/ClayBrick.asset", TextureFolder + "/ClayBrick_16x16.png");
        UpdateItemTextureReference("Assets/Crafting/Items/Chest.asset", TextureFolder + "/Chest_16x32.png");
        UpdateRecipeTextureReference("Assets/Crafting/Recipes/Chest.asset", TextureFolder + "/Chest_16x32.png");
    }

    private static void UpdateItemTextureReference(string assetPath, string texturePath)
    {
        PlaceableItemAsset item = AssetDatabase.LoadAssetAtPath<PlaceableItemAsset>(assetPath);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (item == null || texture == null)
            return;

        SerializedObject serializedItem = new SerializedObject(item);
        SerializedProperty icon = serializedItem.FindProperty("icon");
        if (icon == null)
            return;

        icon.objectReferenceValue = texture;
        serializedItem.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
    }

    private static void UpdateRecipeTextureReference(string assetPath, string texturePath)
    {
        CraftingRecipeAsset recipe = AssetDatabase.LoadAssetAtPath<CraftingRecipeAsset>(assetPath);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (recipe == null || texture == null)
            return;

        SerializedObject serializedRecipe = new SerializedObject(recipe);
        SerializedProperty icon = serializedRecipe.FindProperty("outputIcon");
        if (icon == null)
            return;

        icon.objectReferenceValue = texture;
        serializedRecipe.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recipe);
    }




    private enum BlockKind
    {
        Grass,
        Dirt,
        Stone,
        ClayBrick,
        StoneBrick,
        Sand,
        Wood,
        Leaves,
        CoalOre,
        IronOre,
        GoldOre,
        DiamondOre,
        RedstoneOre,
        Chest
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
            string texturePath = $"{TextureFolder}/{label}_16x16.png";
            string materialPath = $"{MaterialFolder}/{label}.mat";

            Texture2D texture = LoadOrCreatePngTexture(texturePath, () => BuildTexture(kind, label), false);

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

            if (kind == BlockKind.ClayBrick || kind == BlockKind.StoneBrick)
            {
                material.SetTexture("_ParallaxMap", null);
                material.SetFloat("_Parallax", 0f);
                material.SetTexture("_BumpMap", null);
                material.SetFloat("_BumpScale", 1f);
            }

            EditorUtility.SetDirty(material);
        }

        Texture2D grassBlockAtlas = LoadOrCreatePngTexture(GrassBlockAtlasPath, BuildGrassBlockAtlas, true);

        Material grassMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Grass.mat");
        grassMaterial.shader = shader;
        grassMaterial.SetTexture("_BaseMap", grassBlockAtlas);
        grassMaterial.SetColor("_BaseColor", Color.white);
        grassMaterial.SetFloat("_Smoothness", 0.05f);
        EditorUtility.SetDirty(grassMaterial);

        Material workbenchMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Workbench.mat");
        if (workbenchMaterial != null)
        {
            Texture2D workbenchAtlas = LoadOrCreatePngTexture(WorkbenchAtlasPath, BuildWorkbenchAtlas, true);

            workbenchMaterial.shader = shader;
            workbenchMaterial.SetTexture("_BaseMap", workbenchAtlas);
            workbenchMaterial.SetColor("_BaseColor", Color.white);
            workbenchMaterial.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(workbenchMaterial);
        }

        UpdateEditableTextureReferences();

        Material chestMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Chest.mat");
        if (chestMaterial != null)
        {
            Texture2D chestAtlas = LoadOrCreatePngTexture(ChestAtlasPath, BuildChestAtlas, true);
            chestMaterial.shader = shader;
            chestMaterial.SetTexture("_BaseMap", chestAtlas);
            chestMaterial.SetColor("_BaseColor", Color.white);
            chestMaterial.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(chestMaterial);
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
            Color color = y < TextureSize ? SampleWorkbenchSide(x, y % TextureSize) : SampleWorkbenchTop(x, y % TextureSize);
            pixels[y * TextureSize + x] = color;
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D BuildChestAtlas()
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize * 2, TextureFormat.RGBA32, false, false)
        {
            name = "Chest_16x32",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0
        };

        Color[] pixels = new Color[TextureSize * TextureSize * 2];
        for (int y = 0; y < TextureSize * 2; y++)
        for (int x = 0; x < TextureSize; x++)
        {
            Color color = y < TextureSize ? SampleChestSide(x, y % TextureSize) : SampleChestTop(x, y % TextureSize);
            pixels[y * TextureSize + x] = color;
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static Color SampleChestSide(int x, int y)
    {
        Color color = Sample(BlockKind.Chest, x, y);
        Color darkWood = new Color(0.20f, 0.08f, 0.025f, 1f);
        Color lidWood = new Color(0.70f, 0.38f, 0.10f, 1f);
        if (y >= 12)
            color = lidWood;
        if (y == 11 || x == 1 || x == TextureSize - 2)
            color = darkWood;
        if (x >= TextureSize / 2 - 1 && x <= TextureSize / 2 && y >= 5 && y <= 8)
            color = new Color(0.88f, 0.65f, 0.18f, 1f);
        return color;
    }

    private static Color SampleChestTop(int x, int y)
    {
        int value = Hash(x, y, 1571);
        Color color = Palette(value, new Color(0.62f, 0.32f, 0.08f), new Color(0.82f, 0.49f, 0.14f), new Color(0.36f, 0.15f, 0.035f));
        if (x == 1 || x == TextureSize - 2 || y == 1 || y == TextureSize - 2)
            color = new Color(0.20f, 0.08f, 0.025f, 1f);
        else if (x == TextureSize / 2 - 1 || x == TextureSize / 2)
            color *= 0.82f;
        return new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), 1f);
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
            case BlockKind.ClayBrick:
                baseColor = SampleBrick(x, y, new Color(0.36f, 0.14f, 0.09f), new Color(0.68f, 0.29f, 0.16f));
                break;
            case BlockKind.StoneBrick:
                baseColor = SampleBrick(x, y, new Color(0.20f, 0.22f, 0.25f), new Color(0.40f, 0.43f, 0.47f));
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
            case BlockKind.Chest:
                baseColor = Palette(value, new Color(0.58f, 0.29f, 0.07f), new Color(0.80f, 0.48f, 0.13f), new Color(0.34f, 0.14f, 0.03f));
                if (y >= 12)
                    baseColor *= 1.12f;
                if (y == 11)
                    baseColor = new Color(0.20f, 0.08f, 0.025f, 1f);
                if (x >= 7 && x <= 8 && y >= 5 && y <= 8)
                    baseColor = new Color(0.88f, 0.65f, 0.18f, 1f);
                break;
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

    private static Color SampleBrick(int x, int y, Color mortar, Color body)
    {
        int row = y / 4;
        int localY = y % 4;
        int offset = row % 2 == 0 ? 0 : 4;
        int localX = (x + offset) % 8;
        return localY == 0 || localX == 0 ? mortar : body;
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
