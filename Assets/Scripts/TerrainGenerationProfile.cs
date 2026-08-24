using UnityEngine;

[CreateAssetMenu(fileName = "TerrainGenerationProfile", menuName = "Cubeits/Terrain Generation Profile")]
public class TerrainGenerationProfile : ScriptableObject
{
    [SerializeField] private int seed = 12345;
    [SerializeField, Min(0.0001f)] private float heightNoiseScale = 0.01f;
    [SerializeField, Range(1, 5)] private int heightOctaves = 1;
    [SerializeField, Range(0.1f, 1f)] private float heightPersistence = 0.5f;
    [SerializeField, Min(1f)] private float heightLacunarity = 2f;
    [SerializeField, Range(0f, 1f)] private float heightFloor = 0f;
    [SerializeField, Range(0f, 1f)] private float heightAmplitude = 1f;
    [SerializeField, Min(1f)] private float treeDensityMultiplier = 1f;
    [SerializeField] private float[] materialWeights = new float[0];

    public int Seed => seed;
    public float HeightNoiseScale => Mathf.Max(0.0001f, heightNoiseScale);
    public int HeightOctaves => Mathf.Clamp(heightOctaves, 1, 5);
    public float HeightPersistence => Mathf.Clamp(heightPersistence, 0.1f, 1f);
    public float HeightLacunarity => Mathf.Max(1f, heightLacunarity);
    public float HeightFloor => Mathf.Clamp01(heightFloor);
    public float HeightAmplitude => Mathf.Clamp01(heightAmplitude);
    public float TreeDensityMultiplier => Mathf.Max(1f, treeDensityMultiplier);
    public float[] MaterialWeights => materialWeights;
    public bool HasMaterialWeights => materialWeights != null && materialWeights.Length > 0;
}
