using UnityEngine;
using System.Collections.Generic;

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

    // ------------------------------------------------- generalised search -----

    /// <summary>
    /// The same search, against any acceptance test rather than a grade tier.
    /// Added 6 Sep for the Custom Order screen's extra input modes (Akshat, with
    /// Ritwika's sign-off). PURELY ADDITIVE: Solve and SolveGentlest are unchanged
    /// in behaviour - they now route through here, and the self-test still matches
    /// docs/interface-contract.md section 8 exactly.
    ///
    ///   accept            - returns true when a candidate model is good enough.
    ///                       Grade tiers are just one possible test; "purity >= 88"
    ///                       or "tensile >= 80" are others.
    ///   maximiseThroughput- false optimises for the lowest kiln temperature instead.
    ///   fixedParticleMm   - when > 0, particle size is HELD at this value and only
    ///                       temperature, retention and feed are searched. This is
    ///                       the "my shredder only does 10 mm" case.
    ///
    /// Note the feed rate is still capped by MaxFeed(particle) inside the loop, so
    /// no caller can route around the shredder capacity constraint by supplying a
    /// permissive predicate. That coupling is what stops a setting existing that
    /// beats every preset on throughput and quality at once.
    /// </summary>
    public static Result SolveWhere(System.Func<ProcessModel, bool> accept,
                                    bool  maximiseThroughput = true,
                                    float fixedParticleMm    = 0f)
    {
        if (accept == null)
            return new Result { model = null, feasible = false, note = "No target was given." };

        return Search(accept, maximiseThroughput, fixedParticleMm, null);
    }

    // ------------------------------------------------- the trade-off frontier --

    /// <summary>One optimal plan, and what it costs in terms a customer understands.</summary>
    public struct Plan
    {
        public ProcessModel model;
        public float fibreKgH;      // throughput
        public float yieldFrac;     // fibre per kg of blade material (0..1)
    }

    /// <summary>
    /// Every plan that is Pareto-optimal in (throughput, yield) for a target.
    ///
    /// WHY THIS EXISTS. Solve() answers "the most fibre per hour", which silently
    /// picks one side of a trade the customer should be making themselves. Filling
    /// an order fast and using as little blade material as possible pull in
    /// OPPOSITE directions: for a 4,000 t mid-grade order the choice runs from
    /// 34.7 days using 568 blades to 37.2 days using 513 blades. Both ends are
    /// correct - for different customers. Neither the model nor this class should
    /// be the one deciding.
    ///
    /// Returned sorted by descending throughput, so index 0 is the fastest plan
    /// and the last entry is the leanest. Anything not on this list is beaten by
    /// something on it on BOTH counts, which is what makes it safe to let the user
    /// move freely along the list and nowhere else.
    ///
    /// Same grid and the same MaxFeed cap as every other search here.
    /// </summary>
    public static List<Plan> SolveFrontier(Grade targetGrade)
    {
        return FrontierWhere(
            m => OrderContext.GradeOf(m.FiberPurityPct, m.TensileRetentionPct) <= targetGrade,
            0f);
    }

    /// <summary>
    /// The frontier for an arbitrary acceptance test, optionally with the particle
    /// size held fixed (the "my shredder only does 10 mm" case).
    ///
    /// TEMPERATURE AND RETENTION ARE HELD AT THEIR SET-POINTS, and that is not a
    /// shortcut - it is provable from the model. Moving either off 600 C / 35 min
    /// raises OverallDeviation, which lowers the glass share and the purity while
    /// leaving the feed rate untouched. So an off-set-point plan is beaten by its
    /// on-set-point twin on throughput AND yield at once, which is the definition of
    /// being dominated; it can never sit on the frontier. Verified exhaustively in
    /// the editor over 51,324 comparisons spanning the whole envelope: zero cases
    /// where deviating helped on throughput, yield or purity.
    ///
    /// Holding them lets the sweep run at the same 0.1 mm particle resolution the
    /// main solver uses, so the fastest plan on this list is exactly the plan
    /// Solve() returns rather than a coarser approximation of it.
    ///
    /// CAVEAT: this relies on `accept` testing product quality (a grade tier, a
    /// purity or tensile floor) - which is what every caller does. A predicate that
    /// constrained temperature or retention directly would need the full sweep.
    /// </summary>
    public static List<Plan> FrontierWhere(System.Func<ProcessModel, bool> accept,
                                           float fixedParticleMm = 0f)
    {
        var found = new List<Plan>();
        if (accept == null) return found;

        var probe = new ProcessModel
        {
            TempC        = ProcessModel.OptTemp,
            RetentionMin = ProcessModel.OptRetention
        };

        bool  fixedPart = fixedParticleMm > 0f;
        float firstPart = fixedPart ? Mathf.Clamp(fixedParticleMm, MinPart, MaxPart) : MinPart;
        float lastPart  = fixedPart ? firstPart : MaxPart;

        for (float p = firstPart; p <= lastPart + 0.001f; p += PartStep)
        {
            float feedCap = MaxFeed(p);
            probe.ParticleSizeMm = p;

            for (float f = feedCap; f >= MinFeed - 0.001f; f -= FeedStep)
            {
                probe.FeedKgH = f;
                if (!accept(probe)) continue;

                float kg = probe.OutputSplit().GlassKgH;
                found.Add(new Plan
                {
                    model = new ProcessModel
                    {
                        TempC          = ProcessModel.OptTemp,
                        RetentionMin   = ProcessModel.OptRetention,
                        FeedKgH        = f,
                        ParticleSizeMm = p
                    },
                    fibreKgH  = kg,
                    yieldFrac = kg / f
                });
            }
        }

        return ParetoFilter(found);
    }

    /// <summary>Keeps only the plans nothing else beats on both throughput and yield.
    /// Sorted fastest first.</summary>
    static List<Plan> ParetoFilter(List<Plan> all)
    {
        all.Sort((a, b) => b.fibreKgH.CompareTo(a.fibreKgH));

        var front = new List<Plan>();
        float bestYield = float.NegativeInfinity;

        // Walking down by throughput, a plan earns its place only by beating every
        // faster plan on yield - which is exactly the definition of not being dominated.
        foreach (var p in all)
        {
            if (p.yieldFrac > bestYield + 1e-6f)
            {
                front.Add(p);
                bestYield = p.yieldFrac;
            }
        }

        return front;
    }

    static Result Search(Grade targetGrade, bool maximiseThroughput)
    {
        // High=0 < Mid=1 < Low=2, so <= means "at least as good as asked for".
        return Search(m => OrderContext.GradeOf(m.FiberPurityPct, m.TensileRetentionPct) <= targetGrade,
                      maximiseThroughput, 0f, targetGrade);
    }

    static Result Search(System.Func<ProcessModel, bool> accept, bool maximiseThroughput,
                         float fixedParticleMm, Grade? targetGrade)
    {
        var probe = new ProcessModel();
        ProcessModel best = null;
        float bestScore = float.NegativeInfinity;

        // A fixed particle size collapses the outer loop to a single value. Clamped
        // into the envelope so a slider that reads 0.5 mm cannot search outside it.
        bool  fixedPart = fixedParticleMm > 0f;
        float firstPart = fixedPart ? Mathf.Clamp(fixedParticleMm, MinPart, MaxPart) : MinPart;
        float lastPart  = fixedPart ? firstPart : MaxPart;

        for (float p = firstPart; p <= lastPart + 0.001f; p += PartStep)
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
                        if (accept(probe))
                        {
                            chosenFeed = f;
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
            return new Result
            {
                model = null,
                feasible = false,
                note = targetGrade.HasValue ? Infeasible(targetGrade.Value) : InfeasibleGeneric(fixedParticleMm)
            };

        return new Result { model = best, feasible = true, note = string.Empty };
    }

    /// <summary>Infeasibility for the non-grade searches. Still never an error - it
    /// names the physical reason and offers the next thing to try.</summary>
    static string InfeasibleGeneric(float fixedParticleMm)
    {
        if (fixedParticleMm > 0f)
            return "Nothing the kiln can do at " + fixedParticleMm.ToString("0.#") +
                   " mm reaches that target. Coarse feed keeps a cold core however hot or long you run it, " +
                   "so the only way up is a finer grind - which also slows the shredder down.";

        return "That target sits outside what the plant can reach. Fibre purity tops out at " +
               "93% and strength at 90%, both at the design case of 600 °C, 35 min, 6,500 kg/h and 2 mm.";
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
