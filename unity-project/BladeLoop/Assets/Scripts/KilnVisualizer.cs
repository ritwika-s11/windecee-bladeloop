using UnityEngine;

/// <summary>
/// Drives the reactive kiln's glow, light, and spin from a temperature value.
/// Call SetHeat(tempC) whenever the dashboard recomputes.
///
/// Two blended behaviours:
///  - Smooth: across the whole operating range the kiln warms from dull red to bright orange.
///  - Threshold: above dangerTemp it shifts toward alarm-red and brightens sharply.
///
/// Emission-curve approach adapted from Ritwika's TemperatureRampAnimator.
/// Uses instanced material (renderer.material) so it never edits the shared asset.
/// </summary>
public class KilnVisualizer : MonoBehaviour
{
    [Header("Targets")]
    public Renderer[] glowRenderers;   // drum, tyres, hood
    public Light kilnLight;
    public KilnRotator rotator;

    [Header("Operating range (deg C)")]
    public float minTemp = 550f;
    public float maxTemp = 660f;
    public float dangerTemp = 630f;

    [Header("Smooth heat colours")]
    public Color coolColor = new Color(0.55f, 0.12f, 0.03f);  // dull red
    public Color hotColor  = new Color(1.0f,  0.42f, 0.10f);  // bright orange
    public Color dangerColor = new Color(1.0f, 0.15f, 0.05f); // alarm red

    [Header("Intensity")]
    public float coolIntensity = 0.4f;
    public float hotIntensity  = 1.4f;
    public float dangerIntensity = 2.6f;

        Material[] mats;
    float baseRpm = 6f;   // set from retention time
    float heatSpin = 1f;  // multiplier from temperature

    void Awake()
    {
        // instance the materials so we don't touch the shared asset
        if (glowRenderers != null)
        {
            mats = new Material[glowRenderers.Length];
            for (int i = 0; i < glowRenderers.Length; i++)
                if (glowRenderers[i] != null) mats[i] = glowRenderers[i].material;
        }
    }

    /// <summary>Drive all kiln visuals from a temperature in deg C.</summary>
    public void SetHeat(float tempC)
    {
        float u = Mathf.InverseLerp(minTemp, maxTemp, tempC);   // 0..1 smooth
        u = Mathf.Clamp01(u);

        // base smooth ramp
        Color emit = Color.Lerp(coolColor, hotColor, u);
        float intensity = Mathf.Lerp(coolIntensity, hotIntensity, u);

        // threshold: blend toward danger above dangerTemp
        if (tempC > dangerTemp)
        {
            float d = Mathf.InverseLerp(dangerTemp, maxTemp, tempC); // 0..1 into danger
            emit = Color.Lerp(emit, dangerColor, d);
            intensity = Mathf.Lerp(intensity, dangerIntensity, d);
        }

        Color final = emit * intensity;
        if (mats != null)
        {
            foreach (var m in mats)
            {
                if (m == null) continue;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", final);
            }
        }

        if (kilnLight != null)
        {
            kilnLight.color = emit;
            kilnLight.intensity = Mathf.Lerp(1.2f, 3.5f, intensity / dangerIntensity);
        }

        if (rotator != null)
        {
                        // heat adds a small spin multiplier on top of the retention-driven base
            heatSpin = Mathf.Lerp(1f, 1.4f, u);
            rotator.rpm = baseRpm * heatSpin;
        }
        }

    /// <summary>Set base rotation speed from retention time. Shorter retention = material moves through faster = faster spin.</summary>
    public void SetRotation(float retentionMinutes)
    {
        // retention 10..60 min maps to rpm 9..3 (inverse: short retention spins faster)
        float t = Mathf.InverseLerp(10f, 60f, retentionMinutes);
        baseRpm = Mathf.Lerp(20f, 6f, t);
        if (rotator != null) rotator.rpm = baseRpm * heatSpin;
    }

    public ParticleSystem smoke;
    public void SetSeparation(bool ok)
    {
        if (smoke == null) return;
        var main = smoke.main;
        var em = smoke.emission;
        // Same pale smoke both ways; failure just makes it thicker/more turbulent (plant working harder).
        main.startColor = new Color(0.85f, 0.86f, 0.9f, ok ? 0.35f : 0.55f);
        em.rateOverTime = ok ? 7f : 26f;
    }

}
