using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes the Stage 3 kiln read as fired steel and painted plant cladding instead of the
/// blue-grey plastic it currently is.
///
/// WHY THIS EXISTS
/// ---------------
/// Testing the build, the drum reads blue-grey on every run, including a 600 C
/// composite-manufacturer order where it should be the hottest thing on screen. That is
/// not the temperature binding failing - the binding is doing its job. Two materials are
/// authored wrong, and between them they cover almost the whole kiln in frame:
///
///     S3_Mat_ShroudSteel   base (0.506, 0.517, 0.547)   metallic 0.78   no maps at all
///     S3_Mat_KilnShell     base (0.520, 0.525, 0.535)   metallic 0.76   normal map only
///
/// Both fail the same two ways.
///
///   1. The base colours are BLUE-BIASED - b > g > r in both. Steel is warm and painted
///      plant cladding is warmer still; those greys are cold before a single light
///      touches them.
///   2. metallic ~0.77 with no mask makes both surfaces near-mirrors, and what they have
///      to reflect is Sky_Stage1_RealHDRI plus fog at (0.71, 0.78, 0.87). A mirror in a
///      dark hall under a blue sky returns blue. Worse, the hotter the kiln gets the more
///      that cold reflection fights the emission TemperatureRampAnimator is adding - so
///      the one thing the stage is about is the thing the material is cancelling.
///
/// Neither material has a base map, so the kiln is also ~9 m of flat untextured colour.
/// That is why its rotation is invisible: there is no surface feature to rotate. Giving
/// the shell a texture makes KilnRotator's work visible for the first time, which matters
/// because rotation is how retention time reads.
///
/// HOW I FOUND IT (worth recording, because I got it wrong first)
/// --------------------------------------------------------------
/// I assumed the blue cylinder was S3_Kiln_MainTube because that is what
/// TemperatureRampAnimator has wired as kilnShellRenderer. Graded it, re-rendered, and
/// almost nothing changed. The kiln has no colliders so a raycast only ever hits the
/// floor; projecting every renderer's bounds into viewport space and sorting by distance
/// showed S3_Shroud_MainShell sitting over the tube for most of the frame. The shroud is
/// what a viewer actually sees. Rendering, not reading, is what caught that.
///
/// WHAT IT CHANGES, AND WHAT IT DELIBERATELY DOES NOT
/// --------------------------------------------------
/// Base map, metallic/smoothness mask, and base tint. That is all.
///
/// EMISSION IS NOT TOUCHED. TemperatureRampAnimator owns _EmissionColor and drives it
/// every frame from the order's setpoint, and I have already broken that once this week
/// by writing fields it owns from a component with a later execution order. This writes a
/// disjoint set of properties on the same instance, so the two cannot collide.
///
/// NO SHARED ASSET IS EDITED. Renderer.material instances per renderer; the two .mat
/// files on disk are untouched, per "never edit a shared material asset to change how one
/// scene looks". Both are Stage-3-only in any case, but the rule does not depend on that.
/// A MaterialPropertyBlock was the other option and cannot be used here: it cannot enable
/// _METALLICSPECGLOSSMAP, which is a material keyword, and without that keyword URP
/// ignores the mask and keeps the uniform 0.77 metallic - which is the bug.
///
/// NO SCENE EDIT. It spawns itself after the scene loads and finds its targets by
/// material name - the same pattern TourControls and OrderPanel already use, because the
/// five tour scenes have a single owner and .unity files do not merge.
///
/// FREE PLAY
/// ---------
/// This runs with or without an order, and that is a considered exception to "guard every
/// change with if (OrderContext.HasOrder)".
///
/// That rule exists so ORDER-DRIVEN BEHAVIOUR cannot leak into free play. This is not
/// behaviour - it is two materials that were authored wrong and look wrong identically in
/// both modes. Guarding it would leave free play showing a blue kiln and the tour showing
/// a steel one, which is worse than either. Set onlyWithOrder = true to hold it to the
/// letter of the rule instead.
/// </summary>
[DefaultExecutionOrder(70)]   // after TemperatureRampAnimator.Start has taken its instance
public class KilnShellGrade : MonoBehaviour
{
    const string SceneName = "Stage3_StoryMode";

    /// <summary>One authored material that needs correcting, and what it should become.</summary>
    [System.Serializable]
    public class Target
    {
        public string materialName;
        public string baseMapResource;
        public string maskMapResource;
        public Vector2 tiling = new Vector2(2f, 4f);
        [Tooltip("Tint at ambient. Warm (r > g > b) in every case - that is the fix.")]
        public Color coldTint = Color.white;
        [Tooltip("Tint at full heat. Fired steel darkens and browns before it glows, so the " +
                 "ramp's emission has something to sit against.")]
        public Color hotTint = Color.white;
        [Range(0f, 1f)] public float metallic = 0.6f;
        [Range(0f, 1f)] public float smoothness = 0.8f;
        [Tooltip("0 for the outer cladding - an insulated casing does not change colour with " +
                 "the process behind it.")]
        [Range(0f, 1f)] public float heatResponse = 1f;

        [System.NonSerialized] public List<Material> mats = new List<Material>();
    }

    [Header("Scope")]
    [Tooltip("Hold this to the letter of 'free play must be unchanged' and leave the kiln " +
             "blue when no order is running.")]
    public bool onlyWithOrder = false;

    [Header("Materials to correct")]
    public Target[] targets =
    {
        // The outer casing. Covers most of the drum in the wide shots, so this is the one
        // the viewer actually reads as "the kiln". Painted insulated cladding: warm plant
        // grey, and barely metallic at all - which is what stops it mirroring the sky.
        new Target {
            materialName    = "S3_Mat_ShroudSteel",
            baseMapResource = "T_S3_Shroud_base",
            maskMapResource = "T_S3_Shroud_mask",
            tiling          = new Vector2(1.5f, 1f),
            coldTint        = new Color(1.02f, 0.985f, 0.930f),
            hotTint         = new Color(0.98f, 0.920f, 0.845f),
            metallic        = 0.22f,
            smoothness      = 0.55f,
            heatResponse    = 0.35f,
        },
        // The drum itself, seen through the shroud's cutaway and at the ends. Fired steel:
        // dark, warm, scaled, with weld seams and rolling marks so the rotation reads.
        new Target {
            materialName    = "S3_Mat_KilnShell",
            baseMapResource = "T_S3_KilnShell_base",
            maskMapResource = "T_S3_KilnShell_mask",
            tiling          = new Vector2(1f, 2f),
            coldTint        = new Color(1.05f, 0.980f, 0.900f),
            hotTint         = new Color(0.95f, 0.800f, 0.660f),
            metallic        = 0.55f,
            smoothness      = 0.80f,
            heatResponse    = 1f,
        },
    };

    [Header("Wiring (auto-found)")]
    public TemperatureRampAnimator ramp;

    // ---------------------------------------------------------------- injection ----
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnLoaded(Scene s, LoadSceneMode mode)
    {
        if (s.name != SceneName) return;
        if (FindAnyObjectByType<KilnShellGrade>() != null) return;
        new GameObject("~KilnShellGrade").AddComponent<KilnShellGrade>();
    }

    // -------------------------------------------------------------------- apply ----
    void Start()
    {
        if (onlyWithOrder && !OrderContext.HasOrder) { enabled = false; return; }
        if (ramp == null) ramp = FindAnyObjectByType<TemperatureRampAnimator>();

        var all = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int graded = 0;

        foreach (var t in targets)
        {
            if (t == null || string.IsNullOrEmpty(t.materialName)) continue;
            var baseMap = string.IsNullOrEmpty(t.baseMapResource) ? null : Resources.Load<Texture2D>(t.baseMapResource);
            var maskMap = string.IsNullOrEmpty(t.maskMapResource) ? null : Resources.Load<Texture2D>(t.maskMapResource);

            foreach (var r in all)
            {
                if (r == null) continue;
                bool hit = false;
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length && !hit; i++)
                    // instances append " (Instance)", so match on prefix
                    if (shared[i] != null && shared[i].name.StartsWith(t.materialName)) hit = true;
                if (!hit) continue;

                // .material INSTANCES. The .mat asset on disk is not written to.
                foreach (var m in r.materials)
                {
                    if (m == null || !m.name.StartsWith(t.materialName)) continue;

                    if (baseMap != null)
                    {
                        m.SetTexture("_BaseMap", baseMap);
                        m.SetTextureScale("_BaseMap", t.tiling);
                    }
                    if (maskMap != null)
                    {
                        m.SetTexture("_MetallicGlossMap", maskMap);
                        m.SetTextureScale("_MetallicGlossMap", t.tiling);
                        // Without this keyword URP ignores the map and keeps the uniform
                        // 0.77 metallic, which is the whole bug.
                        m.EnableKeyword("_METALLICSPECGLOSSMAP");
                        m.SetFloat("_SmoothnessTextureChannel", 0f);   // alpha of the metallic map
                    }
                    m.SetFloat("_Metallic", t.metallic);
                    m.SetFloat("_Smoothness", t.smoothness);
                    t.mats.Add(m);
                    graded++;
                }
            }
        }

        Apply(0f);
        Debug.Log($"[KilnShellGrade] corrected {graded} material instance(s) across " +
                  $"{targets.Length} authored material(s): metallic ~0.77 -> painted/steel, " +
                  $"base maps attached, blue-biased tints warmed.");
    }

    void Update()
    {
        if (ramp == null || ramp.director == null) return;
        float t = (float)ramp.director.time;
        Apply(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ramp.rampStartTime, ramp.rampEndTime, t)));
    }

    void Apply(float u)
    {
        foreach (var t in targets)
        {
            if (t == null || t.mats == null) continue;
            Color c = Color.Lerp(t.coldTint, t.hotTint, u * t.heatResponse);
            for (int i = 0; i < t.mats.Count; i++)
                if (t.mats[i] != null) t.mats[i].SetColor("_BaseColor", c);
        }
    }
}
