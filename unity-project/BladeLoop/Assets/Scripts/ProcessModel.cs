using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Process-quality model for the Plant Explorer (CEE process-control framing).
/// Four inputs — kiln temperature, retention time, feed rate, particle size —
/// each with an optimal set-point. Outputs a composite process efficiency,
/// system status, a recovered-material split (glass / syngas / char / losses),
/// product quality metrics, and a live diagnostics list.
///
/// Mass balance is CLOSED: glass + syngas + char + losses = feed rate.
/// "Losses" = input mass lost to fugitive dust past the baghouse, moisture
/// flash-off, and adhered residue on internal surfaces (kg/h). It sits at a
/// small baseline at optimum and climbs as decomposition worsens (mainly with
/// coarser particle size).
/// </summary>
public class ProcessModel
{
    public float TempC          = 600f;
    public float RetentionMin   = 35f;
    public float FeedKgH        = 6500f;
    public float ParticleSizeMm = 2f;

    public const float OptTemp = 600f, OptRetention = 35f, OptFeed = 6500f, OptParticle = 2f;

    // Loss fraction of feed: baseline at optimum, plus a climb driven by coarse
    // particles (main cause) and overall process deviation.
    const float BaseLossFrac = 0.015f;  // ~1.5% at optimum
    const float MaxLossFrac  = 0.10f;   // cap ~10%

    static float Clamp01(float v) => Mathf.Clamp01(v);

    public float DevTemp      => Clamp01(Mathf.Abs(TempC - OptTemp) / 150f);
    public float DevRetention => Clamp01(Mathf.Abs(RetentionMin - OptRetention) / 10f);
    public float DevFeed      => Clamp01(Mathf.Abs(FeedKgH - OptFeed) / 2500f);
    // Particle size: 2 mm is optimal. Bigger = incomplete decomposition. Normalised over 2->20 mm.
    public float DevParticle  => Clamp01(Mathf.Max(0f, ParticleSizeMm - OptParticle) / 18f);

    public float OverallDeviation =>
        Clamp01(DevTemp * 0.30f + DevRetention * 0.22f + DevFeed * 0.16f + DevParticle * 0.32f);

    public int EfficiencyPct => Mathf.RoundToInt((1f - OverallDeviation) * 100f);

    public enum Status { Optimal, Caution, Critical }
    public static Status StatusFor(float dev) => dev < 0.15f ? Status.Optimal : (dev < 0.45f ? Status.Caution : Status.Critical);
    public Status SystemStatus => StatusFor(OverallDeviation);

    /// <summary>Loss fraction of the feed (0..1), driven mostly by particle size.</summary>
    public float LossFraction => Mathf.Clamp(BaseLossFrac + (MaxLossFrac - BaseLossFrac) * (0.75f * DevParticle + 0.25f * OverallDeviation), BaseLossFrac, MaxLossFrac);

    /// <summary>Output split in kg/h. Losses come out of the feed first; the rest
    /// is shared by glass/syngas/char. All four always sum to FeedKgH.</summary>
    public struct Split { public float GlassPct, OilPct, SyngasPct, CharPct, LossPct; public float GlassKgH, OilKgH, SyngasKgH, CharKgH, LossKgH; }
    public Split OutputSplit()
    {
        float D = OverallDeviation;
        // Product proportions among the RECOVERED (non-loss) stream.
        // Baseline (optimum): fibre 70 / oil 16 / syngas 8 / char 6  (per CEE reference).
        float glass  = Mathf.Clamp(70f - 26f * D - 12f * DevParticle, 30f, 72f);
        float oil    = Mathf.Clamp(16f - 6f  * D,                     4f, 17f);
        float syngas = Mathf.Clamp(8f  - 2f  * D,                     3f, 9f);
        float charv  = Mathf.Clamp(6f  + 24f * D + 12f * DevParticle, 6f, 42f);
        float psum = glass + oil + syngas + charv;

        float lossFrac = LossFraction;                 // fraction of feed lost
        float recovFrac = 1f - lossFrac;               // fraction that becomes product

        // kg/h
        float lossKg   = FeedKgH * lossFrac;
        float glassKg  = FeedKgH * recovFrac * (glass  / psum);
        float oilKg    = FeedKgH * recovFrac * (oil    / psum);
        float syngasKg = FeedKgH * recovFrac * (syngas / psum);
        float charKg   = FeedKgH * recovFrac * (charv  / psum);

        return new Split {
            GlassPct  = glassKg  / FeedKgH * 100f,
            OilPct    = oilKg    / FeedKgH * 100f,
            SyngasPct = syngasKg / FeedKgH * 100f,
            CharPct   = charKg   / FeedKgH * 100f,
            LossPct   = lossKg   / FeedKgH * 100f,
            GlassKgH  = glassKg, OilKgH = oilKg, SyngasKgH = syngasKg, CharKgH = charKg, LossKgH = lossKg
        };
    }

    public float FiberPurityPct  => Mathf.Clamp(99f - 18f * OverallDeviation - 20f * DevParticle, 50f, 99.4f);
    public float TensileRetentionPct => Mathf.Clamp(100f - 40f * DevParticle - 12f * DevTemp - 10f * DevRetention, 30f, 100f);

    public Status LedTemp      => StatusFor(DevTemp);
    public Status LedRetention => StatusFor(DevRetention);
    public Status LedFeed      => StatusFor(DevFeed);
    public Status LedParticle  => StatusFor(DevParticle);

    // Per-input live description (shown in each slider's info popup, updates with value).
    public string TempInfo()
    {
        if (DevTemp < 0.15f) return "Holding near 600 \u00b0C \u2014 clean resin cracking, glass fibre intact.";
        if (DevTemp < 0.45f) return TempC < OptTemp ? "Cooler than target \u2014 resin breakdown is slowing." : "Hotter than target \u2014 extra thermal stress on the glass fibre.";
        return TempC < OptTemp ? "Critically cold \u2014 resin not fully cracking, fibre purity falling." : "Critically hot \u2014 fibre strength degrading, char output rising.";
    }
    public string RetentionInfo()
    {
        if (DevRetention < 0.15f) return "On target \u2014 fibres fully freed of resin without over-cooking.";
        if (DevRetention < 0.45f) return RetentionMin < OptRetention ? "Short of target \u2014 some resin may stay bound to the fibre." : "Above target \u2014 fibre held in the heat longer than needed.";
        return RetentionMin < OptRetention ? "Critically short \u2014 incomplete breakdown, low purity." : "Critically long \u2014 embrittlement and more char.";
    }
    public string FeedInfo()
    {
        if (DevFeed < 0.15f) return "At design throughput \u2014 kiln residence time per particle is ideal.";
        if (DevFeed < 0.45f) return FeedKgH > OptFeed ? "Above target \u2014 approaching kiln throughput limits." : "Below target \u2014 running under design point.";
        return FeedKgH > OptFeed ? "Far above capacity \u2014 residence time per particle cut short." : "Far below capacity \u2014 well under design throughput.";
    }
    public string ParticleInfo()
    {
        if (DevParticle < 0.15f) return "~2 mm feedstock \u2014 even heat penetration, complete decomposition, clean fibre.";
        if (DevParticle < 0.45f) return "Coarser than 2 mm \u2014 heat reaches the core more slowly, decomposition less complete.";
        return "Far too coarse \u2014 particle cores don't fully decompose: poor fibre quality and more waste.";
    }
    public Status TempStatus => LedTemp;
    public Status RetentionStatus => LedRetention;
    public Status FeedStatus => LedFeed;
    public Status ParticleStatus => LedParticle;

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

        if (DevParticle < 0.15f) list.Add(new Diag { level = Status.Optimal, text = "Feedstock at ~2 mm \u2014 even heat penetration, minimal losses." });
        else if (DevParticle < 0.45f) list.Add(new Diag { level = Status.Caution, text = "Feedstock coarser than 2 mm \u2014 decomposition less complete, losses rising." });
        else list.Add(new Diag { level = Status.Critical, text = "Feedstock far too coarse \u2014 cores don't decompose: poor fibre quality, high losses." });

        return list;
    }
}
