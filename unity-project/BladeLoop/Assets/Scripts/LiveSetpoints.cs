using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Makes a setpoint change show up in the scene that is already on screen.
///
/// THE PROBLEM THIS SOLVES
/// -----------------------
/// Nothing in the tour is live. Every order-driven visualiser - ShredOutputSizer,
/// KilnOrderBinding, Stage4OrderBinding, TemperatureRampAnimator - reads
/// OrderContext.Model once, in Start(), and never looks again. So changing a value
/// mid-run changes precisely nothing on screen unless something re-applies it.
///
/// WHY NOT JUST CALL THEIR Start() AGAIN
/// -------------------------------------
/// Because almost all of that code is CUMULATIVE, not absolute. It multiplies the
/// authored values in place:
///
///     rotator.rpm *= f;                      KilnOrderBinding
///     cycle.cycleLength /= cycleK;           KilnOrderBinding
///     airlock.room1PileMaxHeight *= pileK;   KilnOrderBinding
///     em.rateOverTimeMultiplier *= amountK;  KilnOrderBinding.ScaleStream
///     m.SetColor("_EmissionColor", m.GetColor("_EmissionColor") * k);
///                                            TemperatureRampAnimator.ApplyOrder
///
/// Running any of those a second time compounds it. Two visits to the kiln at 600 °C
/// would leave the drum spinning at the square of the intended factor and the glow
/// baked down twice. That is why this class never re-runs their code. It either uses
/// a hook the author wrote to be re-entrant, or it tracks the baseline itself and
/// writes ABSOLUTE values.
///
/// NO EXISTING SCRIPT IS MODIFIED. Everything here reads public state or writes
/// public fields, exactly as TourControls and OrderPanel do.
///
/// SCOPE, AND WHY IT IS THIS SMALL
/// -------------------------------
/// Particle size (Shredding) and temperature (Kiln) only.
///
///   - Particle size has the largest weight in ProcessModel (0.32, more than
///     temperature) and ShredOutputSizer.Rebuild(mm) is already documented "safe to
///     call repeatedly". It is the strongest visual in the tour and it costs nothing.
///   - Temperature drives ~400 units of emission across the kiln, which this file
///     re-bakes absolutely. The number on the kiln follows for free, because
///     TemperatureRampAnimator.Apply() reads the public tempEnd every frame.
///   - Feed rate and retention are deliberately EXCLUDED. Both are spread across
///     particle-system multipliers, burst curves, door-cycle lengths and pile heights
///     that were multiplied in place on Start. Making those re-appliable means
///     mirroring a dozen baselines out of someone else's file, and the failure mode
///     is a scene that looks subtly wrong rather than one that throws. Not worth it
///     at this distance from submission.
/// </summary>
public static class LiveSetpoints
{
    // ------------------------------------------------------------- shredding --

    /// <summary>Particle size. The pile re-forms on the spot.
    ///
    /// Rebuild() hides the authored granules and regenerates the heap from scratch
    /// every time, so it is genuinely idempotent - the tenth call gives the same heap
    /// as the first. That is the author's own guarantee, not an assumption.</summary>
    public static void ApplyParticle(float mm)
    {
        if (!OrderContext.HasOrder || OrderContext.Model == null) return;
        OrderContext.Model.ParticleSizeMm = mm;

        foreach (var s in Object.FindObjectsByType<ShredOutputSizer>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (s != null) s.Rebuild(mm);
        }
    }

    // ------------------------------------------------------------------ kiln --

    // MIRRORED FROM TemperatureRampAnimator.HeatPrefixes - keep the two in step.
    //
    // Copied rather than shared because making that field public is an edit to a file
    // that is not mine, and the list is stable: these are the objects that represent
    // HEAT. Zone rings, labels, screens and the nitrogen pipework are excluded there
    // and excluded here, for the same reason - they are diagram, not temperature.
    static readonly string[] HeatPrefixes = {
        "S3_Kiln_HotZone", "S3_Kiln_Flame", "FlameCore", "Burner_Flame_",
        "S3_Burner_", "S3_BurnerJacket_", "S3_PS_Flame", "S3_PS_HotEmbers",
        "VP_Glow_", "S3_Inside_DecompEpoxy_", "S3_Cutaway_FeedBed",
        "S3_Cutaway_RefractoryLining", "S3_Cutaway_InnerGlowSurface",
        "Interior_RefractoryGlow", "FeedBed_Zone",
    };

    // Same band as TemperatureRampAnimator. Below the floor the shell reads as barely
    // lit; at the ceiling it is the authored full glow. The app's solver allows
    // 400-700 °C, but normalising over that range puts every realistic setpoint within
    // 0.17 of its neighbour, which is the invisible difference the band exists to fix.
    const float HeatFloorC = 520f;
    const float HeatFullC  = 620f;

    static float HeatK(float tempC)
    {
        float peak = Mathf.Clamp(Mathf.InverseLerp(HeatFloorC, HeatFullC, tempC), 0.28f, 1f);
        return Mathf.Lerp(0.45f, 1f, peak);   // never falls to nothing: a cool kiln is still lit
    }

    // The emission each captured renderer had at the moment this class FIRST touched
    // it - which is after TemperatureRampAnimator.ApplyOrder has already baked the
    // entry temperature in. Storing the base and always writing base * ratio is what
    // makes repeated changes exact: there is no running product to drift.
    static readonly Dictionary<Renderer, Color> baseEmission = new Dictionary<Renderer, Color>();
    static float  capturedAtC;       // the temperature baseEmission represents
    static string capturedScene;     // captures belong to one loaded scene

    /// <summary>Drop the cache when the scene changes. The Renderers in it are about to
    /// be destroyed, and a stale entry would be a null key holding a dead colour.</summary>
    public static void ForgetScene()
    {
        baseEmission.Clear();
        capturedScene = null;
    }

    /// <summary>Kiln temperature.
    ///
    /// Two visible consequences, both correct by construction:
    ///   - the number on the kiln, because TemperatureRampAnimator.Apply() lerps to
    ///     the public tempEnd every Update and Update still runs at timeScale 0;
    ///   - the glow, re-baked here from the captured baseline.</summary>
    public static void ApplyTemperature(float tempC)
    {
        if (!OrderContext.HasOrder || OrderContext.Model == null) return;
        OrderContext.Model.TempC = tempC;

        // The label's target. Public field, read fresh every frame - no re-entry needed.
        foreach (var ramp in Object.FindObjectsByType<TemperatureRampAnimator>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (ramp != null) ramp.tempEnd = tempC;
        }

        RebakeGlow(tempC);
    }

    static void RebakeGlow(float tempC)
    {
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (capturedScene != scene) { baseEmission.Clear(); capturedScene = scene; }

        // First touch in this scene: whatever is on the materials now represents the
        // temperature the stage was ENTERED at, because ApplyOrder baked it in Start.
        bool firstTouch = baseEmission.Count == 0;
        if (firstTouch)
        {
            capturedAtC = SetpointLog.Entry != null ? SetpointLog.Entry.TempC : tempC;
            Capture();
            if (baseEmission.Count == 0) return;   // not the kiln stage; nothing to do
        }

        float ratio = HeatK(tempC) / Mathf.Max(HeatK(capturedAtC), 0.0001f);

        foreach (var kv in baseEmission)
        {
            var r = kv.Key;
            if (r == null) continue;
            // sharedMaterial would dim the asset permanently and leak into every other
            // scene using it. TemperatureRampAnimator instanced these already, so this
            // gets the same instance back rather than creating a second one.
            var m = r.material;
            if (m == null || !m.HasProperty("_EmissionColor")) continue;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", kv.Value * ratio);
        }
    }

    static void Capture()
    {
        foreach (var r in Object.FindObjectsByType<Renderer>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r == null) continue;
            string n = r.gameObject.name;
            bool isHeat = false;
            for (int i = 0; i < HeatPrefixes.Length && !isHeat; i++)
                if (n.StartsWith(HeatPrefixes[i])) isHeat = true;
            if (!isHeat) continue;

            var m = r.material;
            if (m == null || !m.HasProperty("_EmissionColor")) continue;
            baseEmission[r] = m.GetColor("_EmissionColor");
        }
    }

    // ------------------------------------------------------------- consequence --

    /// <summary>Tell the rest of the app a setting moved.
    ///
    /// The right-hand ledger recomputes purity, yield and fibre-per-hour straight from
    /// OrderContext.Model, so one Refresh is the whole of the "live numbers" half of
    /// this feature. Null-guarded because free play runs without a panel.</summary>
    public static void NotifyChanged()
    {
        if (OrderPanel.Instance != null) OrderPanel.Instance.Refresh();
    }
}
