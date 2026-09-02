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

    [Header("Optional: match the conveyor stream to the pile")]
    public ParticleSystem[] streams;

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

        // No order: leave the pile exactly as authored - same twenty pieces, same
        // positions, same sizes. Only their colour is corrected, which is a look fix
        // rather than an order-driven change, so free play still behaves identically.
        if (!OrderContext.HasOrder) { RecolourSourceGranules(); return; }

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

        MatchStreams(scaleMul);
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

    /// <summary>Make the conveyor stream carry the same size material as the pile.</summary>
    void MatchStreams(float scaleMul)
    {
        if (streams == null) return;
        foreach (var ps in streams)
        {
            if (ps == null) continue;
            var main = ps.main;
            var s = main.startSize;
            s.constant = Mathf.Clamp(s.constant * scaleMul, 0.02f, 1.5f);
            main.startSize = s;
        }
    }
}
