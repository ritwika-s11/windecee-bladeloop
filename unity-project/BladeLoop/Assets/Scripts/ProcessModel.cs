using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Process-quality model for the Plant Explorer (CEE process-control framing).
/// Four inputs — kiln temperature, retention time, feed rate, residual oxygen —
/// each with an optimal set-point. Outputs a composite process efficiency,
/// system status, a recovered-material split (glass / syngas / char), product
/// quality metrics, and a live diagnostics list.
///
/// Numbers are a control-quality model (how close inputs are to their design
/// optimum), not a thermodynamic one. Mass split is spec-consistent:
/// glass / syngas / char (no pyrolytic-oil fraction).
/// </summary>
public class ProcessModel
{
    public float TempC        = 600f;
    public float RetentionMin = 35f;
    public float FeedKgH      = 6500f;
    public float OxygenPct    = 0f;

    public const float OptTemp = 600f, OptRetention = 35f, OptFeed = 6500f, OptOxygen = 0f;

    static float Clamp01(float v) => Mathf.Clamp01(v);

    public float DevTemp      => Clamp01(Mathf.Abs(TempC - OptTemp) / 150f);
    public float DevRetention => Clamp01(Mathf.Abs(RetentionMin - OptRetention) / 10f);
    public float DevFeed      => Clamp01(Mathf.Abs(FeedKgH - OptFeed) / 2500f);
    public float DevOxygen    => Clamp01(OxygenPct / 8f);

    public float OverallDeviation =>
        Clamp01(DevTemp * 0.30f + DevRetention * 0.22f + DevFeed * 0.16f + DevOxygen * 0.32f);

    public int EfficiencyPct => Mathf.RoundToInt((1f - OverallDeviation) * 100f);

    public enum Status { Optimal, Caution, Critical }
    public static Status StatusFor(float dev) => dev < 0.15f ? Status.Optimal : (dev < 0.45f ? Status.Caution : Status.Critical);
    public Status SystemStatus => StatusFor(OverallDeviation);

    public struct Split { public float GlassPct, SyngasPct, CharPct; }
    public Split OutputSplit()
    {
        float D = OverallDeviation;
        float glass  = Mathf.Clamp(70f - 26f * D - 10f * DevOxygen, 30f, 72f);
        float syngas = Mathf.Clamp(24f - 6f  * D,                    8f, 25f);
        float charv  = Mathf.Clamp(6f  + 24f * D + 10f * DevOxygen,  6f, 42f);
        float sum = glass + syngas + charv;
        return new Split { GlassPct = glass / sum * 100f, SyngasPct = syngas / sum * 100f, CharPct = charv / sum * 100f };
    }

    public float FiberPurityPct  => Mathf.Clamp(99f - 18f * OverallDeviation - 20f * DevOxygen, 50f, 99.4f);
    public float TensileRetentionPct => Mathf.Clamp(100f - 55f * DevOxygen - 12f * DevTemp - 10f * DevRetention, 30f, 100f);

    public Status LedTemp      => StatusFor(DevTemp);
    public Status LedRetention => StatusFor(DevRetention);
    public Status LedFeed      => StatusFor(DevFeed);
    public Status LedOxygen    => OxygenPct < 0.4f ? Status.Optimal : (OxygenPct < 2.8f ? Status.Caution : Status.Critical);

    public struct Diag { public Status level; public string text; }
    public List<Diag> Diagnostics()
    {
        var list = new List<Diag>();

        if (DevTemp < 0.15f) list.Add(new Diag { level = Status.Optimal, text = "Kiln holding near 600 \u00b0C \u2014 clean resin cracking, glass fibre intact." });
        else if (DevTemp < 0.45f) list.Add(new Diag { level = Status.Caution, text = TempC < OptTemp ? "Kiln running cooler than target \u2014 resin breakdown slowing." : "Kiln running hotter than target \u2014 added thermal stress on the glass fibre." });
        else list.Add(new Diag { level = Status.Critical, text = TempC < OptTemp ? "Kiln critically cold \u2014 resin not fully cracking, fibre purity falling." : "Kiln critically hot \u2014 fibre strength degrading, char output rising." });

        if (DevRetention < 0.15f) list.Add(new Diag { level = Status.Optimal, text = "Retention on target \u2014 fibres fully freed of resin without over-cooking." });
        else if (DevRetention < 0.45f) list.Add(new Diag { level = Status.Caution, text = RetentionMin < OptRetention ? "Retention short \u2014 some resin may remain bound to the fibre." : "Retention long \u2014 fibre exposed to heat longer than needed." });
        else list.Add(new Diag { level = Status.Critical, text = RetentionMin < OptRetention ? "Retention critically short \u2014 expect incomplete breakdown and low purity." : "Retention critically long \u2014 expect embrittlement and more char." });

        if (DevFeed >= 0.45f) list.Add(new Diag { level = Status.Critical, text = FeedKgH > OptFeed ? "Feed far above capacity \u2014 kiln residence time per particle is cut short." : "Feed far below capacity \u2014 plant running well under design throughput." });
        else if (DevFeed >= 0.15f) list.Add(new Diag { level = Status.Caution, text = FeedKgH > OptFeed ? "Feed above target \u2014 approaching kiln throughput limits." : "Feed below target \u2014 throughput under design point." });

        if (DevOxygen < 0.05f) list.Add(new Diag { level = Status.Optimal, text = "Atmosphere at 0% oxygen \u2014 no oxidation risk to the reclaimed fibre." });
        else if (DevOxygen < 0.35f) list.Add(new Diag { level = Status.Caution, text = "Trace oxygen in the drum \u2014 check the nitrogen purge." });
        else list.Add(new Diag { level = Status.Critical, text = "Significant oxygen ingress \u2014 fibres at risk of combustion, major strength loss." });

        return list;
    }
}
