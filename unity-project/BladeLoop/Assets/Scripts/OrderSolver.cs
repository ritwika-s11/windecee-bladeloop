using UnityEngine;

/// <summary>
/// Runs ProcessModel BACKWARDS: given a target grade, find the plant settings
/// that reach it with the highest fibre throughput.
///
/// ProcessModel is pure arithmetic with no Unity dependencies, so a brute-force
/// grid search is entirely adequate - a couple of million evaluations of simple
/// float maths, well under a second. No cleverness required, and a readable
/// search is worth more to us than a fast one.
///
/// Implements docs/interface-contract.md section 3.
/// Owner: Ritwika (taken from Akshat 31 Aug).
///
/// THE IMPORTANT PART IS MaxFeed. Without the shredder capacity constraint the
/// solver returns 600C / 35min / 9000 kg/h / 0.5mm for EVERY grade - 5,911 kg/h
/// at high grade, beating all three presets on throughput AND quality. That
/// setting would disprove the product's own argument that there is no single
/// right answer. See docs/BLADELOOP-PRODUCT-VISION.md section 4.
/// </summary>
public static class OrderSolver
{
    public struct Result
    {
        public ProcessModel model;      // the settings found; null when !feasible
        public bool         feasible;
        public string       note;       // plain-language reason when !feasible
    }

    // ------------------------------------------- shredder capacity constraint --

    // Finer shredding is slower shredding, so particle size caps feed rate.
    //
    // k = 1106.1 is the exact fit through (2mm, 6500) and (16mm, 8800), the two
    // anchor presets. Do NOT round it to 1100: at 1100 the low preset (16mm,
    // 8800 kg/h) comes out infeasible by 13 kg/h and the solver rejects one of
    // our own presets.
    const float FeedAtOptimum = 6500f;
    const float CapacityK     = 1106.1f;

    public static float MaxFeed(float particleMm)
    {
        particleMm = Mathf.Max(particleMm, 0.1f);   // guard Log(0)
        return Mathf.Clamp(
            FeedAtOptimum + CapacityK * Mathf.Log(particleMm / ProcessModel.OptParticle),
            MinFeed, HardMaxFeed);
    }

    // ------------------------------------------------------------ the grid ----

    const float MinTemp = 400f, MaxTemp = 700f, TempStep = 10f;
    const float MinRet  = 30f,  MaxRet  = 45f,  RetStep  = 1f;
    const float MinFeed = 4000f, HardMaxFeed = 9000f, FeedStep = 100f;
    const float MinPart = 1f,   MaxPart = 20f,  PartStep = 0.1f;

    /// <summary>Highest fibre throughput that still reaches targetGrade.</summary>
    public static Result Solve(Grade targetGrade) => Search(targetGrade, maximiseThroughput: true);

    /// <summary>Lowest kiln temperature that still reaches targetGrade.
    /// Same envelope, different objective - useful when energy matters more
    /// than speed.</summary>
    public static Result SolveGentlest(Grade targetGrade) => Search(targetGrade, maximiseThroughput: false);

    static Result Search(Grade targetGrade, bool maximiseThroughput)
    {
        var probe = new ProcessModel();
        ProcessModel best = null;
        float bestScore = float.NegativeInfinity;

        for (float p = MinPart; p <= MaxPart + 0.001f; p += PartStep)
        {
            float feedCap = MaxFeed(p);
            probe.ParticleSizeMm = p;

            for (float t = MinTemp; t <= MaxTemp + 0.001f; t += TempStep)
            {
                probe.TempC = t;

                for (float r = MinRet; r <= MaxRet + 0.001f; r += RetStep)
                {
                    probe.RetentionMin = r;

                    // Fibre throughput rises monotonically with feed rate across the
                    // whole envelope (the extra material outweighs the quality the
                    // deviation costs). So for these three fixed values, the best
                    // feed is simply the HIGHEST one that still makes grade - scan
                    // down from the shredder cap and stop at the first hit. Turns a
                    // ~40-step inner loop into a handful, which is the difference
                    // between a solve you notice and one you don't.
                    float chosenFeed = float.NaN;
                    for (float f = feedCap; f >= MinFeed - 0.001f; f -= FeedStep)
                    {
                        probe.FeedKgH = f;
                        if (OrderContext.GradeOf(probe.FiberPurityPct, probe.TensileRetentionPct) <= targetGrade)
                        {
                            chosenFeed = f;   // High=0 < Mid=1 < Low=2, so <= means "at least as good"
                            break;
                        }
                    }
                    if (float.IsNaN(chosenFeed)) continue;   // nothing at this p/t/r makes grade

                    probe.FeedKgH = chosenFeed;

                    // Both objectives are maximised. For "gentlest", weight temperature
                    // heavily and use throughput only to break ties between equal temps.
                    float score = maximiseThroughput
                        ? probe.OutputSplit().GlassKgH
                        : -t * 10000f + probe.OutputSplit().GlassKgH;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = new ProcessModel
                        {
                            TempC = t, RetentionMin = r, FeedKgH = chosenFeed, ParticleSizeMm = p
                        };
                    }
                }
            }
        }

        if (best == null)
            return new Result { model = null, feasible = false, note = Infeasible(targetGrade) };

        return new Result { model = best, feasible = true, note = string.Empty };
    }

    /// <summary>Readable explanation shown to the user - never an error message.
    /// Displayed verbatim by the Custom Order screen.</summary>
    static string Infeasible(Grade targetGrade)
    {
        string g = targetGrade == Grade.High ? "high" : targetGrade == Grade.Mid ? "mid" : "low";
        return "No settings in the plant's operating envelope reach " + g +
               " grade. Finer shredding raises quality but caps how fast material can be fed, "
             + "so beyond a point the two cannot both be satisfied. Try accepting a lower grade.";
    }

    // ------------------------------------------------------------ helpers -----

    /// <summary>Fibre throughput a given setting would produce, without changing
    /// any shared state. Handy for the outcome report's "compared with high grade"
    /// line.</summary>
    public static float ThroughputOf(float tempC, float retentionMin, float feedKgH, float particleMm)
    {
        var m = new ProcessModel
        {
            TempC = tempC, RetentionMin = retentionMin,
            FeedKgH = feedKgH, ParticleSizeMm = particleMm
        };
        return m.OutputSplit().GlassKgH;
    }

    /// <summary>True when a setting is inside the shredder capacity envelope.
    /// The Custom Order screen uses this to clamp the feed slider as the
    /// particle-size slider moves.</summary>
    public static bool IsFeasibleSetting(float feedKgH, float particleMm) =>
        feedKgH <= MaxFeed(particleMm) + 0.5f;
}
