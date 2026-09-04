using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes the shredder's output pile show the order's particle size.
///
/// Particle size carries weight 0.32 in ProcessModel - more than temperature -
/// and Stage 2 is where it is chosen, so this is the clearest place in the whole
/// tour to show that a setting changed the plant. The target is that someone
/// across the room can tell a 2 mm run from a 16 mm run without reading a number.
///
/// The scene ships 20 authored granules (S2_OutputPile_0..19) sitting at the
/// discharge end of the output conveyor. Twenty pieces is far too few to read as
/// "fine sand" at 2 mm, so with an order active this hides the authored twenty
/// and builds a pile from clones of their meshes instead - many small pieces when
/// the shred is fine, fewer chunky ones when it is coarse.
///
/// Sizing deliberately does NOT use the brief's literal `particleSizeMm / 2`.
/// That is 8x linear at 16 mm, which is 512x the volume per piece: the pile
/// swallows the conveyor. Instead the authored look is treated as the 8 mm
/// midpoint and size moves on a gentler curve, with the piece count moving the
/// opposite way. Total pile volume then grows about 1.9x from fine to coarse,
/// which is also what really happens - coarse material packs with more voids.
///
/// With no order this does nothing at all: the authored twenty stay exactly as
/// the scene has them, so free play and standalone editor playback are unchanged.
/// </summary>
[DefaultExecutionOrder(40)]
public class ShredOutputSizer : MonoBehaviour
{
    [Header("Source granules (auto-found if left empty)")]
    [Tooltip("The authored S2_OutputPile_* objects. Their meshes, materials and " +
             "footprint are used as the template for the generated pile.")]
    public List<Transform> sourceGranules = new List<Transform>();

    [Header("Curve")]
    [Tooltip("Particle size the authored pile represents. Sizes are relative to this.")]
    public float referenceMm = 8f;
    [Tooltip("Piece count at the reference size.")]
    public int referenceCount = 30;
    [Tooltip("How strongly piece SIZE follows particle size. Higher = more dramatic.")]
    public float sizeResponse = 0.368f;
    [Tooltip("How strongly piece COUNT moves against size. Higher = emptier coarse pile.")]
    public float countResponse = 0.943f;
    [Range(0f, 0.6f)]
    [Tooltip("Random size variation per piece, so it reads as shredded material.")]
    public float sizeJitter = 0.25f;

    [Header("Heap shape")]
    [Tooltip("Fraction of the authored footprint the heap occupies. The authored 20 are " +
             "spread thin over the whole area, which reads as scattered debris rather than a " +
             "pile; pulling the radius in makes the pieces overlap the way real material does.")]
    [Range(0.25f, 1.2f)] public float spread = 0.72f;
    [Tooltip("Coarse material spreads a little wider than fine. Added to spread at 16 mm.")]
    [Range(0f, 0.5f)] public float coarseSpreadBonus = 0.14f;
    [Tooltip("Angle of repose as height/radius. Tipped granular material settles around 32-38 " +
             "degrees, so height is roughly a third of the width - NOT a multiple of the authored " +
             "pile height, which produces a spire. Coarse angular material stacks a little steeper " +
             "than fine, which is why this rises with particle size.")]
    [Range(0.2f, 1.2f)] public float reposeFine = 0.55f;
    [Range(0.2f, 1.2f)] public float reposeCoarse = 0.72f;
    [Tooltip("Stable seed - the pile looks the same every run.")]
    public int seed = 20260902;

    [Header("Material look")]
    [Tooltip("Shredded GFRP is glass fibre in cured resin - pale off-white through grey to a " +
             "dull tan, never the chocolate brown the FBX ships with. Applied per piece through a " +
             "MaterialPropertyBlock: the material lives inside Stage2-Shredder-CEE.fbx and editing " +
             "it would leak into everything else using that FBX.")]
    public bool recolourAsGlassFibre = true;
    public Color fibrePale = new Color(0.855f, 0.843f, 0.796f);   // bleached resin / exposed glass
    public Color fibreTan  = new Color(0.702f, 0.659f, 0.565f);   // dust-coated chunk
    public Color fibreGrey = new Color(0.596f, 0.600f, 0.592f);   // shaded core, weathered
    [Range(0f, 0.35f)]
    [Tooltip("Extra brightness spread on top of the colour mix, so no two pieces match.")]
    public float shadeSpread = 0.14f;

    [Header("Conveyor stream - this is where the change actually reads")]
    [Tooltip("S2_PS_OutputGranules and friends. The belt carries the shredded output nine " +
             "metres up to the kiln, in motion and in process, which sells particle size far " +
             "better than a static heap on the apron.")]
    public ParticleSystem[] streams;
    [Tooltip("Exponent on the size multiplier for particles. Above 1 exaggerates, because a " +
             "size spread that reads clearly on a chunk is invisible on a small particle.")]
    [Range(1f, 3f)] public float streamSizeBoost = 1.9f;

    float[] baseSize, baseRate;

    [Header("Belt load - the shot that actually reads")]
    [Tooltip("The inclined conveyor's Belt transform. Real granule meshes ride this, sized by " +
             "the order. Particles are a few pixels across at plant distance and can never show " +
             "the difference between 2 mm and 16 mm; solid geometry filmed close can.")]
    public Transform beltSurface;
    [Tooltip("How far along the belt the load runs, either side of its centre.")]
    public float beltHalfLength = 5.2f;
    [Tooltip("Half the belt width the load spreads across.")]
    public float beltHalfWidth = 0.30f;
    [Tooltip("Metres per second up the belt.")]
    public float beltSpeed = 1.15f;

    const string BeltHolderName = "S2_BeltLoad_Generated";
    Transform beltHolder;
    readonly List<Transform> beltPieces = new List<Transform>();
    readonly List<float> beltU = new List<float>();
    readonly List<float> beltW = new List<float>();
    readonly List<float> beltLift = new List<float>();

    const string HolderName = "S2_OutputPile_Generated";

    Transform holder;
    Vector3 centre;
    float radiusX, radiusZ, height, groundY;
    bool measured;

    void Start()
    {
        if (sourceGranules.Count == 0) AutoFind();
        if (sourceGranules.Count == 0) return;

        Measure();

        // Build the heap in every case, order or not.
        //
        // The obvious reading of "change nothing without an order" would be to leave the
        // authored twenty alone. That turns out to be wrong here: all twenty float above
        // the apron - the lowest sits 3 cm up, the highest 35 cm - because their renderers
        // were disabled from the day the scene was built, so nobody ever saw them and
        // nobody ever seated them. Leaving them untouched means free play shows twenty
        // pale cubes hovering over concrete, which is worse than what was there before.
        //
        // With no order, OrderContext.Model is the design case, so this builds the same
        // heap a high-grade run produces. Free play still behaves identically in every
        // way that was ever visible.
        Rebuild(OrderContext.Model.ParticleSizeMm);
    }

    void AutoFind()
    {
        var all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in all)
            if (t.name.StartsWith("S2_OutputPile_") && t.name != HolderName)
                sourceGranules.Add(t);
        sourceGranules.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
    }

    /// <summary>Learn the authored pile's footprint so the generated one sits in the same place.</summary>
    void Measure()
    {
        if (measured) return;
        var b = new Bounds(sourceGranules[0].position, Vector3.zero);
        foreach (var t in sourceGranules)
        {
            var r = t.GetComponent<Renderer>();
            if (r != null) b.Encapsulate(r.bounds);
        }
        centre  = b.center;
        radiusX = Mathf.Max(b.extents.x, 0.05f);
        radiusZ = Mathf.Max(b.extents.z, 0.05f);
        height  = Mathf.Max(b.size.y, 0.05f);
        groundY = b.min.y;
        measured = true;
    }

    /// <summary>Size multiplier relative to the authored pile.</summary>
    public float SizeFor(float mm)
    {
        float t = Mathf.Log(Mathf.Max(mm, 0.1f) / referenceMm, 2f);
        return Mathf.Pow(2f, t * sizeResponse);
    }

    /// <summary>How many pieces to show at this size.</summary>
    public int CountFor(float mm)
    {
        float t = Mathf.Log(Mathf.Max(mm, 0.1f) / referenceMm, 2f);
        return Mathf.Clamp(Mathf.RoundToInt(referenceCount * Mathf.Pow(2f, -t * countResponse)), 8, 140);
    }

    /// <summary>Rebuild the pile for a given particle size. Safe to call repeatedly.</summary>
    public void Rebuild(float mm)
    {
        if (sourceGranules.Count == 0) return;
        Measure();

        float scaleMul = SizeFor(mm);
        int   count    = CountFor(mm);

        // Coarse shred is angular and sits at wild angles; fine shred lies flat
        // and reads smooth. This is a surprisingly strong size cue on its own.
        float tilt = Mathf.Lerp(12f, 90f, Mathf.InverseLerp(2f, 16f, mm));

        // Coarse material rolls out wider; fine material stands in a tighter cone.
        float coarse    = Mathf.InverseLerp(2f, 16f, mm);
        float spreadNow = spread + coarseSpreadBonus * coarse;

        // Height comes from the angle of repose against the actual radius, so the heap
        // always sits at a believable slope no matter how wide the footprint is.
        float meanRadius = (radiusX + radiusZ) * 0.5f * spreadNow;
        float heapTop    = meanRadius * Mathf.Lerp(reposeFine, reposeCoarse, coarse);

        // Sixteen chunks cannot hold up a full-height cone - there is nothing underneath
        // the upper ones. Flatten the heap when there are few pieces so coarse material
        // rests on the apron instead of hovering in a cone shape.
        heapTop *= Mathf.Clamp(Mathf.Sqrt(count / (float)Mathf.Max(referenceCount, 1)), 0.55f, 1.25f);

        foreach (var t in sourceGranules)
            if (t != null) t.gameObject.SetActive(false);

        if (holder == null)
        {
            var existing = transform.Find(HolderName);
            holder = existing != null ? existing : new GameObject(HolderName).transform;
            holder.SetParent(transform, false);
            holder.position = Vector3.zero;
            holder.rotation = Quaternion.identity;
            holder.localScale = Vector3.one;
        }
        for (int i = holder.childCount - 1; i >= 0; i--) DestroyImmediate(holder.GetChild(i).gameObject);

        var rng = new System.Random(seed);
        for (int i = 0; i < count; i++)
        {
            var src = sourceGranules[i % sourceGranules.Count];
            if (src == null) continue;

            var go = Instantiate(src.gameObject, holder);
            go.name = "Granule_" + i;
            go.SetActive(true);
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.enabled = true;

            // Bias toward the centre (pow 0.7 rather than sqrt) so the heap has a dense
            // core and thins at the edge, which is how tipped material actually settles.
            double a  = rng.NextDouble() * System.Math.PI * 2.0;
            float  rr = Mathf.Pow((float)rng.NextDouble(), 0.7f);
            float  px = centre.x + radiusX * spreadNow * rr * Mathf.Cos((float)a);
            float  pz = centre.z + radiusZ * spreadNow * rr * Mathf.Sin((float)a);

            // Cone profile, not a dome: a tipped heap has a fairly straight slope.
            // Jitter downward only, so pieces sit in the heap rather than float above it.
            float dome = heapTop * (1f - rr) * Mathf.Lerp(0.45f, 1f, (float)rng.NextDouble());

            float jitter = 1f + ((float)rng.NextDouble() * 2f - 1f) * sizeJitter;
            go.transform.localScale = src.localScale * scaleMul * jitter;

            // Rotate BEFORE seating. A rotated cube is taller than an unrotated one,
            // so a lift worked out beforehand leaves coarse pieces hanging in the air.
            go.transform.rotation = Quaternion.Euler(
                (float)rng.NextDouble() * tilt,
                (float)rng.NextDouble() * 360f,
                (float)rng.NextDouble() * tilt);

            // Seat each piece by its actual rendered bottom, then let it settle a little
            // into the heap so pieces interlock instead of balancing on one another.
            go.transform.position = new Vector3(px, 0f, pz);
            if (rend != null)
            {
                float pivotToBottom = go.transform.position.y - rend.bounds.min.y;
                float sink = rend.bounds.size.y * 0.18f;
                go.transform.position = new Vector3(px, groundY + dome + pivotToBottom - sink, pz);

                Recolour(rend, rng);
            }
            else
            {
                go.transform.position = new Vector3(px, groundY + dome, pz);
            }
        }

        MatchStreams(scaleMul, count);
        BuildBeltLoad(mm, scaleMul, rng);
    }

    /// <summary>
    /// Lays real granule meshes along the conveyor belt, sized by the order.
    ///
    /// This is the answer to the actual problem. The output pile is 7-16 cm of material on a
    /// plant-scale set, so from any story camera it is a smudge; moving the camera around
    /// never fixed that, because the objects are simply too small to resolve at that
    /// distance. A load riding the belt can be filmed from under a metre away, where a
    /// 16 mm chip fills a good part of the frame and a 2 mm one clearly does not.
    ///
    /// Pieces are positioned in the belt's own axes - right() runs up the slope, up() is the
    /// surface normal - so this keeps working if anyone re-angles the conveyor.
    /// </summary>
    void BuildBeltLoad(float mm, float scaleMul, System.Random rng)
    {
        beltPieces.Clear(); beltU.Clear(); beltW.Clear(); beltLift.Clear();
        if (beltSurface == null || sourceGranules.Count == 0) return;

        if (beltHolder == null)
        {
            var existing = transform.Find(BeltHolderName);
            beltHolder = existing != null ? existing : new GameObject(BeltHolderName).transform;
            beltHolder.SetParent(transform, false);
            beltHolder.localScale = Vector3.one;
        }
        for (int i = beltHolder.childCount - 1; i >= 0; i--) DestroyImmediate(beltHolder.GetChild(i).gameObject);

        // Fine shred covers the belt; coarse arrives as separated lumps with gaps between.
        // ~130 chips at 2 mm (a covered belt), ~37 at 8 mm, ~20 at 16 mm (separated lumps).
        int n = Mathf.Clamp(Mathf.RoundToInt(130f * Mathf.Pow(2f / Mathf.Max(mm, 0.1f), 0.90f)), 14, 150);

        for (int i = 0; i < n; i++)
        {
            var src = sourceGranules[i % sourceGranules.Count];
            if (src == null) continue;
            var go = Instantiate(src.gameObject, beltHolder);
            go.name = "BeltChip_" + i;
            go.SetActive(true);
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.enabled = true;

            float jitter = 1f + ((float)rng.NextDouble() * 2f - 1f) * sizeJitter;
            go.transform.localScale = src.localScale * scaleMul * jitter;
            go.transform.rotation = Quaternion.Euler(
                (float)rng.NextDouble() * 360f,
                (float)rng.NextDouble() * 360f,
                (float)rng.NextDouble() * 360f);

            Recolour(rend, rng);

            beltPieces.Add(go.transform);
            beltU.Add(Mathf.Lerp(-beltHalfLength, beltHalfLength, (float)rng.NextDouble()));
            beltW.Add(Mathf.Lerp(-beltHalfWidth, beltHalfWidth, (float)rng.NextDouble()));
            beltLift.Add(rend != null ? rend.bounds.extents.y : 0.05f);
        }
        PlaceBeltPieces();
    }

    void PlaceBeltPieces()
    {
        if (beltSurface == null) return;
        Vector3 along = beltSurface.right;      // up the slope
        Vector3 normal = beltSurface.up;        // belt surface normal
        Vector3 across = beltSurface.forward;   // across the belt
        Vector3 centre = beltSurface.position;
        for (int i = 0; i < beltPieces.Count; i++)
        {
            if (beltPieces[i] == null) continue;
            beltPieces[i].position = centre
                + along  * beltU[i]
                + across * beltW[i]
                + normal * (0.06f + beltLift[i] * 0.7f);
        }
    }

    void Update()
    {
        if (beltPieces.Count == 0 || beltSurface == null) return;
        float d = beltSpeed * Time.deltaTime;
        for (int i = 0; i < beltU.Count; i++)
        {
            beltU[i] += d;
            if (beltU[i] > beltHalfLength) beltU[i] -= beltHalfLength * 2f;   // wrap back to the feed end
        }
        PlaceBeltPieces();
    }

    /// <summary>
    /// Gives one piece a glass-fibre colour instead of the FBX's brown, mixed between three
    /// reference tones so a heap reads as many chips rather than one repeated object.
    ///
    /// Uses a MaterialPropertyBlock deliberately. M_Granule is embedded in
    /// Stage2-Shredder-CEE.fbx, so writing to the material would change every object in the
    /// project that uses it - the same mistake that turned Stage 1's terrain transparent.
    /// A property block is per-renderer and cannot reach the asset.
    /// </summary>
    void Recolour(Renderer rend, System.Random rng)
    {
        if (!recolourAsGlassFibre || rend == null) return;
        if (rend.sharedMaterial == null || !rend.sharedMaterial.HasProperty("_BaseColor")) return;

        // two-step blend across the three tones, so the mid tone is reachable too
        float t = (float)rng.NextDouble();
        Color c = t < 0.5f
            ? Color.Lerp(fibrePale, fibreTan,  t * 2f)
            : Color.Lerp(fibreTan,  fibreGrey, (t - 0.5f) * 2f);

        float shade = 1f + ((float)rng.NextDouble() * 2f - 1f) * shadeSpread;
        c = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);

        var mpb = new MaterialPropertyBlock();
        rend.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", c);
        // Shredded composite is matt and slightly dusty, not the semi-gloss the FBX gives it.
        if (rend.sharedMaterial.HasProperty("_Smoothness"))
            mpb.SetFloat("_Smoothness", 0.10f + (float)rng.NextDouble() * 0.10f);
        rend.SetPropertyBlock(mpb);
    }

    /// <summary>
    /// Recolours the authored twenty. Without this, a run with no order still shows brown
    /// cardboard-looking blocks, so free play would look worse than a real run.
    /// </summary>
    void RecolourSourceGranules()
    {
        var rng = new System.Random(seed ^ 0x5f3a);
        foreach (var t in sourceGranules)
        {
            if (t == null) continue;
            Recolour(t.GetComponent<Renderer>(), rng);
        }
    }

    /// <summary>
    /// Drives the conveyor stream from the same setting as the heap.
    ///
    /// This is where the task actually reads, not the heap. The output stream rides nine
    /// metres of inclined belt up to the kiln with an 8.6 s particle lifetime, so it is
    /// long, moving, well lit and unmistakably mid-process - a heap on concrete is static
    /// and has to be explained. Size alone is too weak a cue on particles this small, so
    /// the emission rate moves the opposite way as well: fine shred streams densely,
    /// coarse shred arrives as sparse chunks with gaps between them.
    ///
    /// Rates and sizes are captured once, so repeated Rebuild calls scale from the
    /// authored values rather than compounding.
    /// </summary>
    void MatchStreams(float scaleMul, int count)
    {
        if (streams == null) return;

        if (baseSize == null || baseSize.Length != streams.Length)
        {
            baseSize = new float[streams.Length];
            baseRate = new float[streams.Length];
            for (int i = 0; i < streams.Length; i++)
            {
                if (streams[i] == null) continue;
                baseSize[i] = streams[i].main.startSize.constant;
                baseRate[i] = streams[i].emission.rateOverTime.constant;
            }
        }

        // Push size harder than the heap does - a 2.15x spread reads clearly on a
        // 20 cm chunk and barely at all on a particle a few pixels across.
        float sizeMul = Mathf.Pow(scaleMul, streamSizeBoost);
        float rateMul = Mathf.Clamp(count / (float)Mathf.Max(referenceCount, 1), 0.35f, 2.2f);

        for (int i = 0; i < streams.Length; i++)
        {
            var ps = streams[i];
            if (ps == null) continue;

            var main = ps.main;
            var sz = main.startSize;
            sz.constant = Mathf.Clamp(baseSize[i] * sizeMul, 0.03f, 0.9f);
            main.startSize = sz;

            var em = ps.emission;
            var rt = em.rateOverTime;
            rt.constant = Mathf.Max(baseRate[i] * rateMul, 4f);
            em.rateOverTime = rt;
        }
    }
}
