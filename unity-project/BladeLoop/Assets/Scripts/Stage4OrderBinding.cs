using UnityEngine;

/// <summary>
/// Task 4: makes Stage 4 show what the customer actually got.
///
/// Same additive pattern as KilnOrderBinding, for the same reason. The three
/// elutriator particle systems, the fibre box and the char drums all work today, and
/// the brief's rule is that free play must be untouched - so this reads their authored
/// values on Start, applies the order on top, and changes nothing at all when no order
/// is running. No existing script is edited.
///
/// Four things move, all driven by ProcessModel.OutputSplit():
///
///   Fibre stream - emission scales with GlassPct. 69% on a high-grade run down to
///                  46% on a low-grade one.
///
///   Char streams - emission scales with CharPct, and this is the headline. The split
///                  runs 5.9% to 26.5%, a genuine 4.5x, so unlike the kiln temperature
///                  and the feed rate this one needs NO exaggeration to read. On a
///                  cement-works run the drums are visibly busy while the fibre box
///                  fills slowly, which is exactly what the brief asks for.
///
///   Fibre colour - FiberPurityPct drives it from clean off-white to grey. A low-grade
///                  run does not just produce less fibre, it produces dirtier fibre.
///
///   Fill levels  - the box and the drums hold their base and grow upward with their
///                  share, so the result is legible even in a still frame.
/// </summary>
[DefaultExecutionOrder(60)]
public class Stage4OrderBinding : MonoBehaviour
{
    [Header("Wiring (auto-found by name if left empty)")]
    public ParticleSystem fibreToBox;
    public ParticleSystem charToDrum0;
    public ParticleSystem charToDrum1;
    [Tooltip("The filled volume inside the fibre box, not the box rim.")]
    public Transform fibreBox;
    public Transform charDrum0;
    public Transform charDrum1;

    [Header("Reference split")]
    [Tooltip("The authored emission rates (fibre 26, char 14) are treated as the MID " +
             "run rather than either extreme, so neither end has to be pushed to an " +
             "absurd value to stay proportional.")]
    public float referenceGlassPct = 58.6f;
    public float referenceCharPct  = 15.5f;

    [Tooltip("Glass only moves 69% -> 46%, a 1.5x span, which is real but soft on " +
             "screen. A mild exponent opens it to about 1.7x so 'still strong' and " +
             "'thinner' actually read apart.")]
    public float fibreResponse = 1.35f;
    [Tooltip("Char already moves 5.9% -> 26.5%, a 4.5x span. Left at 1.0 deliberately: " +
             "this is the one output cue in the whole app that needs no help, and " +
             "exaggerating it would overstate the difference to the customer.")]
    public float charResponse = 1.0f;

    [Tooltip("Clamps so a stream never stops dead or floods the frame.")]
    public float minFactor = 0.22f;
    public float maxFactor = 3.2f;

    // ---------------------------------------------------------- downstream ----
    //
    // The four things above all live on the elutriator, which is on screen for
    // shots 04c and 04d - about 8 seconds of a 46.7 second stage. Everything
    // downstream of it was identical whatever the planner set, so the condenser
    // and oil/syngas shots (another 10 seconds) carried no order information at
    // all.
    //
    // The cyclone is the valuable one. Its dust IS char, so it moves on the same
    // 4.5x span as the drums - and it is visible in the condenser shot, which is
    // where a viewer currently sees nothing change between a high and a low run.
    //
    // Syngas is deliberately NOT driven. It spans 7.9% to 6.8%, a 1.16x, which is
    // below what anyone could see; the only way to make it read would be to
    // exaggerate it, and that would overstate the difference to the customer -
    // the same reason charResponse is pinned at 1.0.
    [Header("Downstream streams")]
    [Tooltip("Cyclone fines. Same char split as the drums, so the same factor.")]
    public ParticleSystem cycloneCharFall;
    public ParticleSystem cycloneCharDrop;
    [Tooltip("Condenser rain and the knock-out drum drain.")]
    public ParticleSystem oilRain;
    public ParticleSystem oilDrain;
    public ParticleSystem koDroplets;

    [Tooltip("The MID run's oil share, matching how referenceGlassPct and " +
             "referenceCharPct are set to the mid case rather than an extreme.")]
    public float referenceOilPct = 14.3f;
    [Tooltip("Oil only moves 15.8% -> 12.7%, a 1.24x span. A mild exponent opens " +
             "it to roughly 1.4x so the rain chamber reads as thinner on a low-grade " +
             "run without pretending the difference is dramatic.")]
    public float oilResponse = 1.6f;

    [Header("Fibre purity tint")]
    public float purityFloorPct   = 72f;
    public float purityCeilingPct = 93f;
    public Color dirtyFibre = new Color(0.560f, 0.535f, 0.480f);
    public Color cleanFibre = new Color(0.930f, 0.918f, 0.878f);

    [Header("Fill levels")]
    [Tooltip("How much of the vertical scale responds. 1 = fully proportional; less " +
             "keeps the containers reading as containers rather than collapsing.")]
    [Range(0f, 1f)] public float fillResponse = 0.62f;
    public float minFill = 0.30f;
    public float maxFill = 1.25f;

    void Start()
    {
        if (fibreToBox  == null) fibreToBox  = FindPS("EL_PS_FibreToBox");
        if (charToDrum0 == null) charToDrum0 = FindPS("EL_PS_CharToDrum_0");
        if (charToDrum1 == null) charToDrum1 = FindPS("EL_PS_CharToDrum_1");
        if (fibreBox == null) fibreBox = FindT("EL_Fib_Box");
        if (charDrum0 == null) charDrum0 = FindT("EL_Char_Drum_0");
        if (charDrum1 == null) charDrum1 = FindT("EL_Char_Drum_1");

        // FindInactivePS, not FindPS. Four of these five live under V2_GasCutaway,
        // which is switched OFF until the cutaway trigger fires, and GameObject.Find
        // skips inactive objects - so FindPS returns null and the stream is silently
        // never scaled. Scaling them here is still correct: the emission module keeps
        // the value, so it is already right when the cutaway turns them on.
        if (cycloneCharFall == null) cycloneCharFall = FindInactivePS("V2GC_PS_CycCharFall");
        if (cycloneCharDrop == null) cycloneCharDrop = FindInactivePS("V2_PS_CharDrop");
        if (oilRain    == null) oilRain    = FindInactivePS("V2_PS_OilRain");
        if (oilDrain   == null) oilDrain   = FindInactivePS("PF_13_OilDrain");
        if (koDroplets == null) koDroplets = FindInactivePS("V2GC_PS_KODroplets");

        // No order: leave every authored value exactly as it is.
        if (!OrderContext.HasOrder) return;

        var m = OrderContext.Model;
        if (m == null) return;
        var split = m.OutputSplit();

        float fibreK = Mathf.Clamp(
            Mathf.Pow(Mathf.Max(split.GlassPct, 0.1f) / Mathf.Max(referenceGlassPct, 0.1f), fibreResponse),
            minFactor, maxFactor);
        float charK = Mathf.Clamp(
            Mathf.Pow(Mathf.Max(split.CharPct, 0.1f) / Mathf.Max(referenceCharPct, 0.1f), charResponse),
            minFactor, maxFactor);

        ScaleStream(fibreToBox,  fibreK);
        ScaleStream(charToDrum0, charK);
        ScaleStream(charToDrum1, charK);

        // Cyclone fines are char, so they ride the same factor as the drums.
        ScaleStream(cycloneCharFall, charK);
        ScaleStream(cycloneCharDrop, charK);

        float oilK = Mathf.Clamp(
            Mathf.Pow(Mathf.Max(split.OilPct, 0.1f) / Mathf.Max(referenceOilPct, 0.1f), oilResponse),
            minFactor, maxFactor);
        ScaleStream(oilRain,    oilK);
        ScaleStream(oilDrain,   oilK);
        ScaleStream(koDroplets, oilK);

        // ---- fibre colour by purity ----
        // Less fibre AND dirtier fibre is the honest story of a low-grade run.
        float pure = Mathf.InverseLerp(purityFloorPct, purityCeilingPct, m.FiberPurityPct);
        Color tint = Color.Lerp(dirtyFibre, cleanFibre, pure);
        TintParticles(fibreToBox, tint);
        TintRenderer(fibreBox, tint);

        // ---- fill levels ----
        SetFill(fibreBox, split.GlassPct / Mathf.Max(referenceGlassPct, 0.1f));
        SetFill(charDrum0, split.CharPct / Mathf.Max(referenceCharPct, 0.1f));
        SetFill(charDrum1, split.CharPct / Mathf.Max(referenceCharPct, 0.1f));

        // ---- the labels were quoting the design case as fixed text ----
        // "CARBON CHAR . 6%" is the HIGH-grade figure baked in. On a cement-works run
        // the scene would show heavy char flowing into the drums while the sign next to
        // it read 6%, which is worse than having no number at all.
        if (drivePercentLabels) RewritePercentLabels(split);
        if (driveTemperatureLabels) RewriteTemperatureLabels(m);
    }

    [Header("Output labels")]
    [Tooltip("The four Stage 4 cards quote fixed percentages (70 / 16 / 8 / 6) - the " +
             "design case. Rewrite them from the actual split so the text agrees with " +
             "what the particles are doing.")]
    public bool drivePercentLabels = true;

    static void RewritePercentLabels(ProcessModel.Split s)
    {
        var labels = FindObjectsByType<TMPro.TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in labels)
        {
            if (t == null || string.IsNullOrEmpty(t.text)) continue;
            string up = t.text.ToUpperInvariant();
            float pct;
            if      (up.Contains("GLASS FIBRE")) pct = s.GlassPct;
            else if (up.Contains("CARBON CHAR")) pct = s.CharPct;
            else if (up.Contains("PYROLYSIS OIL")) pct = s.OilPct;
            else if (up.Contains("SYNGAS")) pct = s.SyngasPct;
            else continue;

            // keep whatever wording and separator the card already uses; swap only the number
            int pc = t.text.LastIndexOf('%');
            if (pc < 0) continue;
            int i = pc - 1;
            while (i >= 0 && (char.IsDigit(t.text[i]) || t.text[i] == '.')) i--;
            if (i == pc - 1) continue;                       // no number found, leave it alone
            t.text = t.text.Substring(0, i + 1) + Mathf.RoundToInt(pct) + t.text.Substring(pc);
        }
    }

    [Tooltip("Three Stage 4 signs quote 600 C - the design case - as fixed text. The " +
             "kiln setpoint is the planner's headline control (550/580/600 on the " +
             "presets, 400-700 on a custom order), so on any other run these signs " +
             "contradict both the order panel and the kiln the viewer just watched.")]
    public bool driveTemperatureLabels = true;

    /// <summary>Rewrites the kiln setpoint into the three signs that quote it.
    ///
    /// Only the FIRST three-digit number in each label is replaced, which is what makes
    /// this safe on the two awkward ones:
    ///   "... / 600 C  .  0% OXYGEN"      -> the 0% is single-digit, untouched
    ///   "... N2 PURGED . 600->50 C"      -> the 50 is two-digit, untouched
    /// The condenser's 45 C is a fixed equipment setpoint, not an order setting, so it
    /// is deliberately not in this list. Nor is the kiln drum's 1.5 RPM.</summary>
    static void RewriteTemperatureLabels(ProcessModel m)
    {
        int setpoint = Mathf.RoundToInt(m.TempC);
        var labels = FindObjectsByType<TMPro.TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in labels)
        {
            if (t == null || string.IsNullOrEmpty(t.text)) continue;
            string up = t.text.ToUpperInvariant();
            bool quotesKilnTemp = up.Contains("ANOXIC PYROLYSIS ZONE")
                               || up.Contains("DISCHARGE HOOD")
                               || up.Contains("WATER-JACKETED SCREW");
            if (!quotesKilnTemp) continue;

            // count-limited Replace is an instance method, not a static one
            var rx = new System.Text.RegularExpressions.Regex(@"\d{3}");
            t.text = rx.Replace(t.text, setpoint.ToString(), 1);
        }
    }

    static ParticleSystem FindPS(string n)
    {
        var go = GameObject.Find(n);
        return go != null ? go.GetComponent<ParticleSystem>() : null;
    }
    static Transform FindT(string n)
    {
        var go = GameObject.Find(n);
        return go != null ? go.transform : null;
    }

    /// <summary>Like FindPS, but sees objects that are switched off. The gas-train
    /// effects live under V2_GasCutaway and are inactive until the cutaway trigger
    /// fires, so GameObject.Find cannot reach them.</summary>
    static ParticleSystem FindInactivePS(string n)
    {
        var all = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].gameObject.name == n) return all[i];
        return null;
    }

    static void ScaleStream(ParticleSystem ps, float k)
    {
        if (ps == null) return;
        var em = ps.emission;
        em.rateOverTimeMultiplier *= k;
        em.rateOverDistanceMultiplier *= k;

        int n = em.burstCount;
        if (n > 0)
        {
            var bursts = new ParticleSystem.Burst[n];
            em.GetBursts(bursts);
            for (int i = 0; i < n; i++)
            {
                var b = bursts[i];
                b.count = ScaleCurve(b.count, k);
                bursts[i] = b;
            }
            em.SetBursts(bursts);
        }

        // headroom, or a heavy char run clips against maxParticles and stops looking heavy
        var main = ps.main;
        main.maxParticles = Mathf.Clamp(Mathf.RoundToInt(main.maxParticles * Mathf.Max(k, 1f)), 32, 4000);
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

    // Property block, never the shared material - the particle materials are reused
    // across Stage 4 and editing them would leak into free play and other scenes.
    static void TintParticles(ParticleSystem ps, Color c)
    {
        if (ps == null) return;
        var main = ps.main;
        main.startColor = c;
    }

    static void TintRenderer(Transform t, Color c)
    {
        if (t == null) return;
        var r = t.GetComponent<Renderer>();
        if (r == null) return;
        var mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", c);
        r.SetPropertyBlock(mpb);
    }

    /// <summary>Grows the contents upward from a fixed base, so the container does not
    /// sink into the floor or float above it as the level changes.</summary>
    void SetFill(Transform t, float ratio)
    {
        if (t == null) return;
        var r = t.GetComponent<Renderer>();
        if (r == null) return;

        float baseY = r.bounds.min.y;
        float k = Mathf.Clamp(Mathf.Lerp(1f, ratio, fillResponse), minFill, maxFill);

        var s = t.localScale;
        t.localScale = new Vector3(s.x, s.y * k, s.z);

        // re-measure and correct, because the pivot is not always the base
        float drop = r.bounds.min.y - baseY;
        t.position -= new Vector3(0f, drop, 0f);
    }
}
