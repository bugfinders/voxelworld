using UnityEngine;

public sealed class DayNightCycle : MonoBehaviour
{
    private const float DefaultTimeOfDay = 0.25f;
    private const float MinimumDayDurationSeconds = 10f;

    [SerializeField] private Light sun;
    [SerializeField] private float dayDurationSeconds = 600f;
    [SerializeField, Range(0f, 1f)] private float timeOfDay = DefaultTimeOfDay;
    [SerializeField] private float daySunIntensity = 1f;
    [SerializeField] private float nightSunIntensity = 0.03f;
    [SerializeField] private float dayAmbientIntensity = 1f;
    [SerializeField] private float nightAmbientIntensity = 0.2f;
    [SerializeField] private float sunYaw = -30f;

    private static readonly Color DaySunColor = new Color(1f, 0.956f, 0.839f);
    private static readonly Color NightSunColor = new Color(0.22f, 0.3f, 0.5f);
    private static readonly Color DayAmbientColor = new Color(0.75f, 0.82f, 1f);
    private static readonly Color NightAmbientColor = new Color(0.08f, 0.12f, 0.22f);

    /// <summary>
    /// Gets the current normalized time of day, where 0.25 is 6:00 AM and 0.75 is 6:00 PM.
    /// </summary>
    public float TimeOfDay => timeOfDay;

    private void Awake()
    {
        if (sun == null)
            sun = GetComponent<Light>();

        ApplyLighting();
    }

    private void Update()
    {
        float duration = Mathf.Max(MinimumDayDurationSeconds, dayDurationSeconds);
        timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime / duration, 1f);
        ApplyLighting();
    }

    /// <summary>
    /// Sets the normalized time of day and immediately applies its lighting state.
    /// </summary>
    public void SetTimeOfDay(float normalizedTime)
    {
        timeOfDay = Mathf.Repeat(normalizedTime, 1f);
        ApplyLighting();
    }

    private void ApplyLighting()
    {
        float daylight = Mathf.Clamp01(Mathf.Sin((timeOfDay - 0.25f) * Mathf.PI * 2f));
        float transition = SmoothStep(daylight);

        if (sun != null)
        {
            float sunAngle = timeOfDay * 360f - 90f;
            sun.transform.rotation = Quaternion.Euler(sunAngle, sunYaw, 0f);
            sun.intensity = Mathf.Lerp(nightSunIntensity, daySunIntensity, transition);
            sun.color = Color.Lerp(NightSunColor, DaySunColor, transition);
        }

        RenderSettings.ambientIntensity = Mathf.Lerp(nightAmbientIntensity, dayAmbientIntensity, transition);
        RenderSettings.ambientLight = Color.Lerp(NightAmbientColor, DayAmbientColor, transition);
    }

    private static float SmoothStep(float value)
    {
        return value * value * (3f - 2f * value);
    }
}
