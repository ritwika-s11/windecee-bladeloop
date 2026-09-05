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
