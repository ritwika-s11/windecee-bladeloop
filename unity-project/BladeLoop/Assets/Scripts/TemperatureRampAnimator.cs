using UnityEngine;
using UnityEngine.Playables;
using TMPro;

/// <summary>
/// Ramps kiln emission intensity and updates a temperature display
/// over a specified Timeline time window. Used for the "furnace heats up" beat.
/// </summary>
public class TemperatureRampAnimator : MonoBehaviour
{
    [Header("Timeline")]
    public PlayableDirector director;
    public float rampStartTime = 4f;
    public float rampEndTime  = 14f;

    [Header("Targets to heat")]
    public Renderer kilnShellRenderer;
    public Renderer burnerRingRenderer;
    public Renderer[] burnerNozzleRenderers;

    [Header("Emission curve")]
    public Color coolColor = new Color(0.6f, 0.15f, 0.05f);
    public Color hotColor  = new Color(1.0f, 0.55f, 0.15f);
    public float coolIntensity = 0.3f;
    public float hotIntensity  = 6.0f;

    [Header("Temperature label (optional)")]
    public TextMeshPro temperatureLabel;
    public float tempStart = 25f;   // room ambient
    public float tempEnd   = 620f;  // 600 avg per spec

    Material kilnMat;
    Material ringMat;
    Material[] nozMats;

    void Start()
    {
        // Instance materials so we don't corrupt shared assets
        if (kilnShellRenderer != null) kilnMat = kilnShellRenderer.material;
        if (burnerRingRenderer != null) ringMat = burnerRingRenderer.material;
        if (burnerNozzleRenderers != null)
        {
            nozMats = new Material[burnerNozzleRenderers.Length];
            for (int i = 0; i < burnerNozzleRenderers.Length; i++)
                if (burnerNozzleRenderers[i] != null) nozMats[i] = burnerNozzleRenderers[i].material;
        }
        Apply(0f);
    }

    void Update()
    {
        if (director == null) return;
        float t = (float)director.time;
        float u = Mathf.InverseLerp(rampStartTime, rampEndTime, t);
        u = Mathf.SmoothStep(0f, 1f, u);
        Apply(u);
    }

    void Apply(float u)
    {
        Color emit = Color.Lerp(coolColor, hotColor, u);
        float intensity = Mathf.Lerp(coolIntensity, hotIntensity, u);
        Color emitFinal = emit * intensity;

        if (kilnMat != null)
        {
            kilnMat.EnableKeyword("_EMISSION");
            kilnMat.SetColor("_EmissionColor", emitFinal * 0.15f); // subtle heat on shell
        }
        if (ringMat != null)
        {
            ringMat.EnableKeyword("_EMISSION");
            ringMat.SetColor("_EmissionColor", emit * (intensity * 1.4f));
        }
        if (nozMats != null)
        {
            foreach (var m in nozMats)
            {
                if (m == null) continue;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emit * (intensity * 2.0f));
            }
        }

        if (temperatureLabel != null)
        {
            float temp = Mathf.Lerp(tempStart, tempEnd, u);
            temperatureLabel.text = temp.ToString("0") + " C";
        }
    }
}
