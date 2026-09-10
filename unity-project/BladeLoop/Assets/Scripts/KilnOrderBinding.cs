using UnityEngine;

/// <summary>
/// Task 3: makes the kiln show the order's temperature and retention.
///
/// Deliberately additive. TemperatureRampAnimator, KilnRotator and
/// AirlockFlowController all work today, and the brief's rule is that free play must be
/// untouched, so this reads their authored values on Start, applies the order on top,
/// and changes nothing at all when no order is running. No existing script is edited.
///
/// Three things move:
///
///   Temperature  - the ramp's tempEnd is hardcoded to 620 C. With an order it becomes
///                  OrderContext.Model.TempC, and the existing colour lerp and label
///                  follow for free.
///
///   Colour range - the authored cool/hot colours are tuned around 620 C, so 550 and 600
///                  land almost on top of each other. The gap is re-mapped so the useful
///                  band is 540-610 rather than 25-620: 600 C reads as bright even orange,
///                  580 C noticeably duller and redder, 550 C visibly under-fired.
///
///   Rotation     - longer retention means slower rotation, per the brief:
///                      rpm = authored * (35 / RetentionMin)
///                  Kept subtle; it is a supporting cue, not the headline.
/// </summary>
[DefaultExecutionOrder(60)]
public class KilnOrderBinding : MonoBehaviour
{
    [Header("Wiring (auto-found if left empty)")]
    public TemperatureRampAnimator ramp;
    public KilnRotator rotator;
    public AirlockFlowController airlock;

    [Header("Feed rate (Task 3 change 4)")]
    [Tooltip("Feed rate the authored airlock timing corresponds to. The presets run " +
             "6,500 / 8,000 / 8,800 kg/h, so the reference run cycles at the authored 6 s and " +
             "the heavier runs push more material through, faster.")]
    public float referenceFeedKgH = 6500f;
    [Tooltip("The presets only span 6,500 to 8,800 kg/h - a 35% range. Split evenly across " +
             "batch size and cycle speed that is about 16% each, which nobody would ever see. " +
             "This exaggerates the difference so the three runs actually read apart on screen, " +
             "the same way the temperature band is re-mapped above. 1 = literal.")]
    public float readabilityExponent = 1.8f;
    [Tooltip("How the extra throughput splits between denser streams and faster-moving " +
             "material. 0.7 leans towards volume, which reads more clearly than motion.")]
    [Range(0f, 1f)] public float amountVsSpeed = 0.7f;
    [Tooltip("How much of the extra throughput goes into shortening the door cycle.\n\n" +
             "Deliberately small. AirlockDoorCycle's phases start at fixed absolute times " +
             "(3 s, 4 s, 4.6 s), so a shorter cycle does not compress the loop evenly - " +
             "accumulation keeps its full 3 s and the whole cut comes out of phase 3, which is " +
             "the discharge into the kiln and the only part the beauty shots actually see. " +
             "Shortening it hard made the charge stream shorter as fast as it made it denser, " +
             "and net flow into the kiln came out flat. Speed is carried by the streams instead.")]
    [Range(0f, 0.6f)] public float cycleShortenExponent = 0.15f;
    [Tooltip("Shortest airlock cycle allowed.\n\n" +
             "AirlockDoorCycle takes cycleLength only as the modulo - its phase boundaries are " +
             "hardcoded at t=3 (upper drop), t=4 (lower discharge) and t=4.6 (N2 purge). So a " +
             "cycle under 4.6 s silently kills the purge and one under 4 s stops the lower door " +
             "opening at all. 4.9 keeps all three phases intact without editing that script.")]
    public float minCycleLength = 4.9f;
    public float maxCycleLength = 7.5f;
    [Tooltip("Clamp so the tower never overflows its chambers or runs dry.")]
    public float minFeedFactor = 0.7f;
    public float maxFeedFactor = 2.0f;

    [Header("Retention")]
    [Tooltip("Retention the authored rpm corresponds to.")]
    public float referenceRetentionMin = 35f;
    [Tooltip("Clamp so the drum never stops or spins comically.")]
    public float minRpmFactor = 0.6f;
    public float maxRpmFactor = 1.6f;

    void Start()
    {
        if (ramp == null)    ramp    = FindAnyObjectByType<TemperatureRampAnimator>();
        if (rotator == null) rotator = FindAnyObjectByType<KilnRotator>();
        if (airlock == null) airlock = FindAnyObjectByType<AirlockFlowController>();

        // No order: leave every authored value exactly as it is.
        if (!OrderContext.HasOrder) return;

        var m = OrderContext.Model;
        if (m == null) return;

        // ---------------------------------------------------------------------
        // TEMPERATURE AND COLOUR ARE NO LONGER DRIVEN FROM HERE.
        //
        // TemperatureRampAnimator.ApplyOrder() now owns them, and it does the job
        // properly. This component used to set tempEnd, tempStart, hotColor,
        // hotIntensity, coolIntensity and shellHeatStrength - and because it runs at
        // execution order 60, AFTER the ramp's own Start, it overwrote that newer work
        // every time.
        //
        // It was also scaling the wrong thing. shellHeatStrength only reaches
        // kilnShellRenderer: one renderer, while the glow a viewer actually sees comes
        // from roughly four hundred units of emission spread across objects this never
        // touched (S3_Kiln_HotZone alone is 177). The ramp's heatPeak covers all of
        // them.
        //
        // And the ramp timing was the worse problem. hotByTime = 28 with a 16 s ramp
        // was chosen when Stage3_Timeline ran 84 s with the kiln shots at 31.5-39 s.
        // After the retime the timeline is ~34.6 s and the kiln is on screen from 0-19 s,
        // so those absolute times held the drum cold through almost the whole stage -
        // including the line at 10.2 s about how hot you run it. Hard-coded seconds do
        // not survive a re-cut, which is exactly the fault I flagged in AirlockDoorCycle.
        //
        // Retention and feed rate below are still this component's job.
        // ---------------------------------------------------------------------

        if (rotator != null)
        {
            float ret = Mathf.Max(m.RetentionMin, 1f);
            float f = Mathf.Clamp(referenceRetentionMin / ret, minRpmFactor, maxRpmFactor);
            rotator.rpm *= f;
        }

        ApplyFeedRate(m);
    }

    /// <summary>
    /// Task 3 change 4 - feed rate drives the charge flow.
    ///
    /// Throughput is batch size over cycle time, so the order's feed rate is split across
    /// both: bigger batches through the tower AND a shorter door cycle. Doing only one of
    /// them looks like an animation-speed slider rather than a plant running harder.
    ///
    ///     amount * speed = effFactor   -> the two together carry the whole change
    ///
    /// Nothing here edits AirlockDoorCycle or AirlockFlowController. It multiplies their
    /// authored values once, on Start, and only when an order is running - so free play
    /// keeps the exact 6-second CEE-spec cycle it has today.
    /// </summary>
    void ApplyFeedRate(ProcessModel m)
    {
        if (airlock == null) return;

        float raw = Mathf.Max(m.FeedKgH, 1f) / Mathf.Max(referenceFeedKgH, 1f);
        float eff = Mathf.Clamp(Mathf.Pow(raw, Mathf.Max(readabilityExponent, 0.1f)),
                                minFeedFactor, maxFeedFactor);

        float amountK = Mathf.Pow(eff, amountVsSpeed);
        float speedK  = Mathf.Pow(eff, 1f - amountVsSpeed);
        float cycleK  = Mathf.Pow(eff, cycleShortenExponent);

        // ---- faster cycle ----
        var cycle = airlock.cycle;
        if (cycle == null) cycle = FindAnyObjectByType<AirlockDoorCycle>();
        if (cycle != null && cycle.cycleLength > 0.1f)
        {
            cycle.cycleLength = Mathf.Clamp(cycle.cycleLength / cycleK,
                                            minCycleLength, maxCycleLength);
        }

        // ---- bigger batches ----
        // Only part of the way, and capped: the chambers are a fixed size and a pile that
        // pokes through the tower wall would be worse than one that is slightly too small.
        float pileK = Mathf.Lerp(1f, Mathf.Min(amountK, 1.45f), 0.7f);
        airlock.room1PileMaxHeight *= pileK;
        airlock.room2PileMaxHeight *= pileK;
        airlock.pileRadius *= Mathf.Lerp(1f, pileK, 0.5f);

        // ---- denser, quicker streams ----
        ScaleStream(airlock.feedDribble,   amountK, speedK);
        ScaleStream(airlock.dropR1toR2,    amountK, speedK);
        ScaleStream(airlock.dropR2toChute, amountK, speedK);
        ScaleStream(airlock.chuteToKiln,   amountK, speedK);
        // The N2 purge is a safety flush, not charge material - its volume is set by the
        // nitrogen supply, not by how much blade is going through. Speed only.
        ScaleStream(airlock.n2Purge, 1f, speedK);
    }

    static void ScaleStream(ParticleSystem ps, float amountK, float speedK)
    {
        if (ps == null) return;

        var em = ps.emission;
        em.rateOverTimeMultiplier *= amountK;
        em.rateOverDistanceMultiplier *= amountK;

        // Bursts carry the two drop beats, so they have to scale too or the batch handoff
        // stays the same size however hard the plant is running.
        int n = em.burstCount;
        if (n > 0)
        {
            var bursts = new ParticleSystem.Burst[n];
            em.GetBursts(bursts);
            for (int i = 0; i < n; i++)
            {
                var b = bursts[i];
                b.count = ScaleCurve(b.count, amountK);
                bursts[i] = b;
            }
            em.SetBursts(bursts);
        }

        // Simulation speed rather than start speed: it carries lifetime and gravity with it,
        // so the material lands in the same places, just sooner.
        var main = ps.main;
        main.simulationSpeed *= speedK;
        main.maxParticles = Mathf.Clamp(Mathf.RoundToInt(main.maxParticles * amountK), 8, 4000);
    }

    static ParticleSystem.MinMaxCurve ScaleCurve(ParticleSystem.MinMaxCurve c, float k)
    {
        switch (c.mode)
        {
            case ParticleSystemCurveMode.TwoConstants:
                return new ParticleSystem.MinMaxCurve(c.constantMin * k, c.constantMax * k);
            default:
                return new ParticleSystem.MinMaxCurve(c.constant * k);
        }
    }
}
