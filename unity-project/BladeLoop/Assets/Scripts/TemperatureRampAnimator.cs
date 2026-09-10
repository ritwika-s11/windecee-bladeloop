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
    [Tooltip("How much of the heat colour reaches the kiln shell. The authored 0.15 is almost " +
             "invisible; KilnOrderBinding raises it while an order is running so 550, 580 and " +
             "600 C read differently, as Task 3 change 2 asks for.")]
    public float shellHeatStrength = 0.15f;

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
        ApplyOrder();     // before the first Apply, so the cold frame is already scaled
        Apply(0f);
    }

    // ------------------------------------------------- order-driven heat -----
    //
    // The brief asked for tempEnd = OrderContext.Model.TempC so 550 / 580 / 600 look
    // different. On its own that does NOTHING visible: Apply() drives colour and
    // intensity from u, the ramp's 0..1 progress, and tempEnd is only ever read by
    // the text label. Set it alone and the label counts to 550 while the shell still
    // glows exactly as hot as a 600 run.
    //
    // So the setpoint has to scale the CEILING as well. heatPeak is how far up the
    // cool->hot range this order climbs, normalised over a band tight enough that the
    // three presets separate:  550 -> 0.50,  580 -> 0.71,  600 -> 0.86,  620 -> 1.00.
    // Using the solver's full 400-700 range instead would put the presets within 0.17
    // of each other, which is the invisible difference we are trying to fix.
    // Widened from 480 after testing: at 480 the three presets landed on 0.50 / 0.71
    // / 0.86 and still read as the same kiln. 520 spreads them to 0.30 / 0.60 / 0.80,
    // which is a visible difference on the burner ring and nozzles.
    const float HeatFloorC = 520f;   // below this the shell reads as barely lit
    const float HeatFullC  = 620f;   // the authored tempEnd = full glow

    float heatPeak = 1f;

    /// <summary>Everything else in the scene that is emissive and represents HEAT.
    ///
    /// This component only ever drove kilnShellRenderer - and in this scene the ring
    /// and nozzle references are empty, so it drove exactly one renderer at 0.5
    /// emission times 0.15 shell strength. The glow a viewer actually sees comes from
    /// about four hundred units of emission spread across objects it never touched:
    /// S3_Kiln_HotZone alone is 177, the viewport glows are 9 each, the burner
    /// nozzles 8, the flames 6, the decomposing epoxy inside the drum 5.
    ///
    /// Scaling one renderer out of that could never read. These prefixes cover the
    /// heat sources; zone rings, labels, screens and the nitrogen pipework are
    /// deliberately excluded - they are diagram, not temperature.</summary>
    static readonly string[] HeatPrefixes = {
        "S3_Kiln_HotZone", "S3_Kiln_Flame", "FlameCore", "Burner_Flame_",
        "S3_Burner_", "S3_BurnerJacket_", "S3_PS_Flame", "S3_PS_HotEmbers",
        "VP_Glow_", "S3_Inside_DecompEpoxy_", "S3_Cutaway_FeedBed",
        "S3_Cutaway_RefractoryLining", "S3_Cutaway_InnerGlowSurface",
        "Interior_RefractoryGlow", "FeedBed_Zone",
    };

    void ApplyOrder()
    {
        if (!OrderContext.HasOrder || OrderContext.Model == null) return;   // free play untouched
        tempEnd  = OrderContext.Model.TempC;                                // the label counts to the real setpoint
        heatPeak = Mathf.Clamp(Mathf.InverseLerp(HeatFloorC, HeatFullC, tempEnd), 0.28f, 1f);

        // Never fall to nothing: a cool kiln is still a lit kiln, and at 550 C the
        // process is running, just not as hard.
        float k = Mathf.Lerp(0.45f, 1f, heatPeak);
        int touched = 0;

        foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r == null) continue;
            string n = r.gameObject.name;
            bool isHeat = false;
            for (int i = 0; i < HeatPrefixes.Length && !isHeat; i++)
                if (n.StartsWith(HeatPrefixes[i])) isHeat = true;
            if (!isHeat) continue;

            // r.material INSTANCES the material. Writing to sharedMaterial here would
            // dim these assets permanently and leak into every other scene using them.
            var m = r.material;
            if (m == null || !m.HasProperty("_EmissionColor")) continue;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", m.GetColor("_EmissionColor") * k);
            touched++;
        }
        Debug.Log($"[TemperatureRamp] {tempEnd:0} C -> heatPeak {heatPeak:0.00}, emission x{k:0.00} on {touched} renderers.");
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
        // The GLOW is scaled by the order's setpoint; the LABEL is not.
        //
        // An earlier version passed u * heatPeak into this method, which scaled both -
        // so a 550 C run counted up to 288 C on the readout while the panel beside it
        // said 550. The number on the kiln has to be the true setpoint; only how hot
        // it LOOKS depends on how hot it is being run.
        float glow = u * heatPeak;

        Color emit = Color.Lerp(coolColor, hotColor, glow);
        float intensity = Mathf.Lerp(coolIntensity, hotIntensity, glow);
        Color emitFinal = emit * intensity;

        if (kilnMat != null)
        {
            kilnMat.EnableKeyword("_EMISSION");
            // Task 3 change 2 - widen the visual range. At the old fixed 0.15 the shell
            // barely glowed, so a 550 C run and a 600 C run looked identical even though
            // the intensities differ by more than 2x. Exposed so KilnOrderBinding can push
            // it when an order is running, and left at the authored value otherwise.
            kilnMat.SetColor("_EmissionColor", emitFinal * shellHeatStrength);
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
