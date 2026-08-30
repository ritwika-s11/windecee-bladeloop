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

    // ── Quality metrics, re-anchored 30 Aug 2026 to published pyrolysis results ──
    // Previously these peaked at 99% purity / 100% tensile retention at the design case,
    // which claims thermal recovery does no damage to the fibre. The literature does not
    // support that: recovered-fibre tensile retention tops out around 90-93% under
    // optimised multi-step pyrolysis, and standard single-step processes cluster at
    // 72-76% (see docs/BLADELOOP-PRODUCT-VISION.md §3 and the CEE source list).
    // Ceilings are now 93% purity / 90% tensile so the design case represents
    // best-in-class recovery rather than perfect recovery. Slopes are scaled by the same
    // factor, so the SHAPE of every response curve is unchanged - only the anchor moved.
    //
    // Resulting design case: 93.0% purity / 90.0% tensile  (best-in-class, 90-93% band)
    //          mid preset:   82.5% purity / 76.5% tensile  (matches the published 76%
    //                                                       two-step wind-blade result)
    //          low preset:   69.8% purity / 58.3% tensile  (deliberately under-driven)
    //
    // "Purity" is our own definition - no published standard expresses recovered fibre
    // quality as a purity %. It means: the mass fraction of recovered material that is
    // fibre, rather than adhered char and resin residue.
    public float FiberPurityPct  => Mathf.Clamp(93f - 17f * OverallDeviation - 19f * DevParticle, 45f, 93f);
    public float TensileRetentionPct => Mathf.Clamp(90f - 36f * DevParticle - 11f * DevTemp - 9f * DevRetention, 28f, 90f);

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

    // ===================================================================================
    // ADDITIVE SECTION — live explanations for the dashboard.
    // Everything below is READ-ONLY: it only reads the existing inputs/derived values.
    // No formula, constant, weight or existing method above has been modified.
    // ===================================================================================

    public enum InputKind { Temp, Retention, Feed, Particle }

    /// <summary>Which input is currently hurting the process most (deviation x its weight).</summary>
    public InputKind DominantInput()
    {
        float t = DevTemp * 0.30f, r = DevRetention * 0.22f, f = DevFeed * 0.16f, p = DevParticle * 0.32f;
        InputKind k = InputKind.Temp; float best = t;
        if (r > best) { best = r; k = InputKind.Retention; }
        if (f > best) { best = f; k = InputKind.Feed; }
        if (p > best) { best = p; k = InputKind.Particle; }
        return k;
    }

    public string InputName(InputKind k) =>
        k == InputKind.Temp ? "Kiln temperature" :
        k == InputKind.Retention ? "Retention time" :
        k == InputKind.Feed ? "Feed rate" : "Particle size";

    public float InputDev(InputKind k) =>
        k == InputKind.Temp ? DevTemp :
        k == InputKind.Retention ? DevRetention :
        k == InputKind.Feed ? DevFeed : DevParticle;

    public Status InputStatus(InputKind k) => StatusFor(InputDev(k));

    /// <summary>Plain-language cause -> effect for one input at its current value.</summary>
    public string CauseEffect(InputKind k)
    {
        switch (k)
        {
            case InputKind.Temp:
                if (DevTemp < 0.15f) return "Kiln at " + TempC.ToString("0") + " °C: resin cracks cleanly, fibre comes out intact.";
                return TempC < OptTemp
                    ? "Kiln " + (OptTemp - TempC).ToString("0") + " °C below target: resin cracks slower, so more stays stuck to the fibre."
                    : "Kiln " + (TempC - OptTemp).ToString("0") + " °C above target: extra heat attacks the fibre and drives carbon into char.";

            case InputKind.Retention:
                if (DevRetention < 0.15f) return "Held " + RetentionMin.ToString("0") + " min: long enough to free the fibre, short enough to avoid over-cooking.";
                return RetentionMin < OptRetention
                    ? "Only " + RetentionMin.ToString("0") + " min in the kiln: resin has less time to release, so fibre leaves dirtier."
                    : RetentionMin.ToString("0") + " min in the kiln: the fibre bakes longer than needed and starts to embrittle.";

            case InputKind.Feed:
                if (DevFeed < 0.15f) return "Feeding " + FeedKgH.ToString("N0") + " kg/h: each particle gets its full designed time in the kiln.";
                return FeedKgH > OptFeed
                    ? "Feeding " + FeedKgH.ToString("N0") + " kg/h: more material sharing the same kiln, so each particle gets less time inside."
                    : "Feeding " + FeedKgH.ToString("N0") + " kg/h: below the design point, so kiln capacity is going unused.";

            default:
                if (DevParticle < 0.15f) return "Feed ground to " + ParticleSizeMm.ToString("0") + " mm: heat reaches every core, so decomposition finishes.";
                return "Feed at " + ParticleSizeMm.ToString("0") + " mm: heat cannot reach the core, so the middle of each chunk never fully decomposes.";
        }
    }

    /// <summary>One line tying the current inputs to what the output bars are doing.</summary>
    public string OutputConsequence()
    {
        var sp = OutputSplit();
        if (OverallDeviation < 0.15f)
            return "Recovery is at the design split: fibre " + sp.GlassPct.ToString("0.0") + "%, losses only " + sp.LossPct.ToString("0.0") + "%.";
        return "That unconverted material has to go somewhere: fibre down to " + sp.GlassPct.ToString("0.0")
             + "% (design 69%), char up to " + sp.CharPct.ToString("0.0") + "%, losses " + sp.LossPct.ToString("0.0") + "%.";
    }

    /// <summary>One line on whether the product still meets quality spec.</summary>
    public string QualityConsequence()
    {
        float pu = FiberPurityPct, te = TensileRetentionPct;
        // Grade tiers, not pass/fail: every output has a buyer, just a different one.
        if (pu > 90f && te > 85f) return "High grade: " + pu.ToString("0.0") + "% pure, " + te.ToString("0") + "% of its original strength — good enough to go back into new composite parts.";
        if (pu > 78f && te > 70f) return "Mid grade: purity " + pu.ToString("0.0") + "%, strength " + te.ToString("0") + "% — not structural, but sells as reinforcing filler for precast concrete.";
        return "Low grade: purity " + pu.ToString("0.0") + "%, strength " + te.ToString("0") + "% — coarse mixed material, co-processed in cement kilns as silica and fuel.";
    }

    /// <summary>Why the SYSTEM STATUS pill is the colour it is.</summary>
    public string StatusReason()
    {
        var st = SystemStatus;
        string dom = InputName(DominantInput()).ToLower();
        if (st == Status.Optimal) return "All four inputs sit close to their set-points, so the plant is running at its design case.";
        if (st == Status.Caution) return "Inputs have drifted from set-point — " + dom + " most of all. The plant still runs, but recovery and quality are being given up.";
        return "Inputs are far from set-point — " + dom + " most of all. Decomposition is incomplete, so product is being lost to char and dust.";
    }

    public struct Explanation { public Status level; public string headline; public List<Diag> rows; }

    /// <summary>Headline + up to four short cause/effect rows describing the current state.</summary>
    public Explanation ExplainNow()
    {
        var ex = new Explanation();
        ex.level = SystemStatus;
        ex.rows = new List<Diag>();

        var dom = DominantInput();
        ex.headline = OverallDeviation < 0.15f
            ? "Every input is on set-point — this is the plant's design case."
            : InputName(dom) + " is what is holding the plant back right now.";

        // 1) the dominant input, always shown
        ex.rows.Add(new Diag { level = InputStatus(dom), text = CauseEffect(dom) });

        // 2) the next-worst input, only if it is genuinely off target
        InputKind second = InputKind.Temp; float bestDev = -1f;
        foreach (InputKind k in new[] { InputKind.Temp, InputKind.Retention, InputKind.Feed, InputKind.Particle })
        {
            if (k == dom) continue;
            float d = InputDev(k);
            if (d > bestDev) { bestDev = d; second = k; }
        }
        if (bestDev >= 0.15f) ex.rows.Add(new Diag { level = InputStatus(second), text = CauseEffect(second) });

        // 3) what that does to the output bars
        ex.rows.Add(new Diag { level = SystemStatus, text = OutputConsequence() });

        // 4) what it does to product quality
        float pu = FiberPurityPct, te = TensileRetentionPct;
        Status qs = (pu > 90f && te > 85f) ? Status.Optimal : ((pu > 78f && te > 70f) ? Status.Caution : Status.Critical);
        ex.rows.Add(new Diag { level = qs, text = QualityConsequence() });

        return ex;
    }

    // ---- info-popup text for the OUTPUT side (mirrors the input popups) ----

    public string EfficiencyInfo()
    {
        return "Process efficiency is how close all four inputs sit to their set-points, weighted by how much each one matters "
             + "(particle size 32%, temperature 30%, retention 22%, feed 16%). Right now it reads " + EfficiencyPct
             + "%. " + (OverallDeviation < 0.15f ? "Nothing is pulling it down." : InputName(DominantInput()) + " is pulling it down the most.");
    }

    public string GlassInfo()
    {
        var sp = OutputSplit();
        return "Reclaimed E-glass fibre — the product that goes on to cement feedstock. At the design case it is about 69% of the feed; "
             + "it now reads " + sp.GlassPct.ToString("0.0") + "% (" + sp.GlassKgH.ToString("N0") + " kg/h). "
             + (DevParticle >= 0.15f || OverallDeviation >= 0.15f
                ? "Whatever the kiln fails to decompose leaves as char or dust instead of clean fibre, so this bar falls first."
                : "Conditions are on target, so almost nothing is diverted away from it.");
    }

    public string OilInfo()
    {
        var sp = OutputSplit();
        return "Pyrolytic oil condensed from the vapour — stored and burned as plant fuel. About 16% of the feed at the design case, "
             + "now " + sp.OilPct.ToString("0.0") + "% (" + sp.OilKgH.ToString("N0") + " kg/h). It shrinks slowly as conditions drift, because less resin is cracked into condensable vapour.";
    }

    public string SyngasInfo()
    {
        var sp = OutputSplit();
        return "Light combustible gas piped back to the kiln burners — this is what lets the plant part-fuel itself. About 8% of the feed at the design case, "
             + "now " + sp.SyngasPct.ToString("0.0") + "% (" + sp.SyngasKgH.ToString("N0") + " kg/h).";
    }

    public string CharInfo()
    {
        var sp = OutputSplit();
        return "Fixed-carbon residue. About 6% of the feed when everything is on target, now " + sp.CharPct.ToString("0.0")
             + "% (" + sp.CharKgH.ToString("N0") + " kg/h). "
             + (sp.CharPct > 10f
                ? "It is high because resin that should have cracked into gas and oil is instead staying behind as solid carbon on the fibre."
                : "It stays low while decomposition is completing properly.");
    }

    public string LossInfo()
    {
        var sp = OutputSplit();
        return "Mass that never becomes product: fugitive dust past the baghouse, moisture flash-off and residue stuck inside the plant. "
             + "It is taken out of the feed first, so everything else shares what is left. Baseline is about 1.5%; it now reads "
             + sp.LossPct.ToString("0.0") + "% (" + sp.LossKgH.ToString("N0") + " kg/h). Coarser feed is the main thing that pushes it up.";
    }

    public string PurityInfo()
    {
        return "The share of the reclaimed material that is fibre rather than adhered char or resin residue. High grade needs above 90%; it now reads "
             + FiberPurityPct.ToString("0.0") + "%. "
             + (DevParticle >= 0.15f ? "Coarse feed hurts it most, because undecomposed cores leave residue on the fibre." : "Temperature and retention are the two things that move it.");
    }

    public string TensileInfo()
    {
        return "How much of its original strength the recovered fibre keeps. Thermal recovery always costs some strength — around 90% is best-in-class, and it now reads "
             + TensileRetentionPct.ToString("0") + "%. Coarse feed does the most damage, because undecomposed cores leave residue that has to be burned off harder.";
    }
}
