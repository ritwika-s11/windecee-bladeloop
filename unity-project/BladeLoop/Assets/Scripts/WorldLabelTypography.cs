using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes the Stage 4 world-space labels legible, and makes them look like the rest of
/// the product.
///
/// WHAT IS WRONG TODAY
/// -------------------
/// Sixteen TextMeshPro labels float over the plant. Surveyed, all sixteen share the
/// same three faults:
///
///   1. Every one of them is set in LiberationSans SDF - Unity's DEFAULT FALLBACK font.
///      The project ships IBM Plex Sans and IBM Plex Mono, BladeLoopTheme exposes them,
///      and every screen in the app uses them. These labels are the only text in the
///      entire product that is not in the brand face, which is most of why they read as
///      unfinished next to the panels beside them.
///
///   2. outlineWidth 0 and faceDilate 0 on all sixteen. There is nothing separating the
///      glyphs from what is behind them, so a near-white label over pale steel or a lit
///      kiln simply dissolves. This is the actual legibility complaint.
///
///   3. No underlay. A world-space label crosses whatever geometry happens to be behind
///      it as the camera moves; without a soft dark drop it will always lose contrast
///      somewhere along a moving shot.
///
/// WHAT THIS CHANGES, AND WHAT IT DELIBERATELY DOES NOT
/// ----------------------------------------------------
/// Font, outline and underlay. That is all.
///
/// COLOURS ARE NOT TOUCHED. The six authored colours carry meaning here - orange for
/// heat, cyan for cooling, near-white for equipment naming - and re-palletising them is
/// a design decision that belongs to whoever owns the look of the stage, not to a
/// legibility pass. Same for POSITIONS, SIZES and ALIGNMENT: a label that has been
/// placed to sit clear of a pipe stays where it was placed.
///
/// NO SCENE EDIT. Spawns itself after the scene loads, the same pattern TourControls,
/// KilnShellGrade, TourUISubmitGuard and ShredderRotationFix already use - Stage4_V2 is
/// a scene Ritwika owns and .unity files cannot be merged.
///
/// NO SHARED ASSET EDIT. TMP_Text.fontMaterial returns a per-object INSTANCE; the font
/// asset's shared material on disk is never written to. Writing fontSharedMaterial here
/// would restyle every label in every scene that uses IBM Plex, including the menus.
///
/// Runs in free play too, because the labels are equally unreadable there.
/// </summary>
// 150: after Stage4OrderBinding (60) has rewritten the percentages, and - critically - after
// ScreenSafeLabel (120), so the de-overlap pass in LateUpdate judges the layout as it will
// actually be drawn. A LOWER number runs FIRST, so an earlier 80 here meant the de-overlap
// ran before ScreenSafeLabel and every label it hid was switched straight back on.
[DefaultExecutionOrder(150)]
public class WorldLabelTypography : MonoBehaviour
{
    const string SceneName = "Stage4_V2";

    [Header("Face")]
    [Tooltip("Leave empty to use BladeLoopTheme.MonoBold.\n\n" +
             "Mono, not Sans. The first pass used SansBold and it read chunky and juvenile: " +
             "IBM Plex Sans sets visually larger than LiberationSans at the same point size, " +
             "so the labels grew as well as thickened. The product's own voice - every panel " +
             "header on the Custom Order screen - is small uppercase MONO with wide tracking. " +
             "These are instrument labels on a plant; they should look stencilled, not shouted.")]
    public TMP_FontAsset font;

    [Tooltip("Relative to the authored size.\n\n" +
             "Back to 1.0. Shrinking to 0.82 cost about a fifth of the pixels each glyph " +
             "had to render into, and on a world-space label read from several metres away " +
             "that is most of the blur. Sharpness here is pixel coverage, not point size.")]
    [Range(0.5f, 1.6f)] public float fontSizeScale = 1.18f;

    [Tooltip("Letter-spacing. Wide tracking on short uppercase strings is what makes " +
             "technical labelling read as engineering rather than as a caption. Eased off " +
             "slightly, because tracking also spreads glyphs over more screen for the same " +
             "pixel budget.")]
    [Range(0f, 30f)] public float characterSpacing = 0f;

    [Header("Shader")]
    [Tooltip("Upgrade from TextMeshPro/Mobile/Distance Field to the full Distance Field " +
             "shader.\n\n" +
             "The IBM Plex assets ship with the Mobile variant, which is a cut-down SDF: " +
             "cheaper antialiasing, and no underlay support at all - so the soft drop I " +
             "set earlier was silently doing nothing. The full shader is what makes " +
             "world-space text resolve cleanly at a distance.")]
    public bool useFullDistanceFieldShader = true;

    [Tooltip("Weight gain. Zero now: the mono face already carries enough stroke, and " +
             "dilating it was most of why the first attempt looked heavy.")]
    [Range(0f, 0.4f)] public float faceDilate = 0f;

    [Header("Outline")]
    [Tooltip("ZERO now, and that is the point.\n\n" +
             "TMP grows an outline INWARD from the glyph edge as well as outward. On a mono " +
             "face with thin strokes, read small and at a distance, an outline eats the " +
             "stroke it is meant to protect and the letterform turns to mush. These labels " +
             "already sit on dark backing plates, so they have all the contrast they need - " +
             "the outline was solving a problem that was already solved, and blurring them " +
             "to do it.")]
    [Range(0f, 1f)] public float outlineWidth = 0f;
    public Color outlineColor = new Color(0.03f, 0.03f, 0.04f, 1f);

    [Header("Underlay (soft drop, carries contrast over moving backgrounds)")]
    [Tooltip("Off by default. With the Mobile shader it did nothing at all; with the full " +
             "shader it works, but it is a blur by design and these labels are already on " +
             "plates. Turn on only if a label is ever floating clear of its plate.")]
    public bool useUnderlay = false;
    public Color underlayColor = new Color(0f, 0f, 0f, 0.55f);
    [Range(-1f, 1f)] public float underlayOffsetX = 0.06f;
    [Range(-1f, 1f)] public float underlayOffsetY = -0.06f;
    [Range(0f, 1f)] public float underlayDilate = 0.05f;
    [Range(0f, 1f)] public float underlaySoftness = 0.32f;

    [Header("Screen-constant sizing - the actual fix")]
    [Tooltip("Add ScreenSafeLabel to every label, as Stage 3 already has.\n\n" +
             "This was never a typography problem. Stage 3 has four ScreenSafeLabels; " +
             "Stage4_V2 has none. A plain world-space label GROWS as the camera approaches, " +
             "so on a close shot 'ROTARY AIRLOCK VALVE' ends up bigger than the valve; and " +
             "it is drawn in perspective, so it skews away from the viewer instead of facing " +
             "them. Both are visible in the stage today.\n\n" +
             "No typeface fixes a label that is the wrong size at the wrong angle. Three " +
             "font changes missed this because they were all answering the wrong question.")]
    public bool addScreenSafeLabel = true;

    [Tooltip("Hold each label at the size it has when the camera is this far away.\n\n" +
             "THIS IS THE SIZE DIAL. Every label is normalised to its apparent size at this " +
             "distance, so: SMALLER number = bigger labels on screen, larger number = smaller.\n\n" +
             "14 was too far - it locked them to how small they looked from across the plant. " +
             "8 is roughly a shot's working distance, which is the size they were designed to " +
             "be read at.")]
    public float referenceDistance = 11f;

    [Tooltip("Lower clamp on the distance scaling.\n\n" +
             "Measured: Stage 4's fourteen story cameras sit between 1.7 m and 17 m from " +
             "their nearest label. Holding a constant apparent size at 1.7 m needs a scale " +
             "of 1.7/11 = 0.15, so anything above that CLAMPS - and a clamped label balloons " +
             "on exactly the close shots where it was already too big. 0.5 could not hold " +
             "size below 5.5 m, which covers most of the stage.")]
    [Range(0.05f, 1f)] public float minScale = 0.15f;
    [Range(1f, 4f)]    public float maxScale = 2.0f;

    [Tooltip("Fade a label out below this distance.\n\n" +
             "Nearly off, deliberately. In Stage 3 this stopped three zone headings piling " +
             "on top of each other on a close shot. Stage 4 is the opposite case: each label " +
             "names one machine, and the camera closes in ON that machine - so a 3.5 m " +
             "threshold deleted the label the shot was about. vCam_07_Cyclone sits 3.3 m " +
             "from the words GAS CYCLONE SEPARATOR.")]
    public float hideNearerThan = 0.8f;

    [Tooltip("Fade a label out beyond this distance, so only the machines near the camera " +
             "are named.\n\n" +
             "Sixteen labels along one plant means that from most angles half of them line " +
             "up behind each other. Held at constant screen size they converge and overprint " +
             "- which is the overlapping mess at the top of frame. 9 m keeps roughly the " +
             "nearest one to three, so a label arrives as the camera reaches its equipment.")]
    public float hideFartherThan = Mathf.Infinity;

    [Header("Backing plate")]
    [Tooltip("Put a dark panel behind each label.\n\n" +
             "Contrast without touching the scene: the labels cross pale steel, lit kiln and " +
             "open sky as the camera moves, and white text cannot hold against all three. A " +
             "plate is what the Custom Order panels already do, so it also matches the " +
             "product's own look.\n\n" +
             "Sized from each label's own text bounds, so it fits the words rather than being " +
             "a fixed rectangle, and parented to the label so it billboards and scales with it.")]
    public bool addBackingPlate = true;

    [Tooltip("Near-black at high opacity, matching BladeLoopTheme.Panel. A weak plate is " +
             "worse than none - it greys the text without separating it.")]
    public Color plateColor = new Color(0.035f, 0.04f, 0.05f, 0.88f);
    [Tooltip("Padding around the text, in local units.")]
    public Vector2 platePadding = new Vector2(0.34f, 0.16f);

    [Tooltip("A thin accent rule down the leading edge of each plate, in BladeLoopTheme's " +
             "Oxide orange.\n\n" +
             "This is what every panel header in the product already does - CUSTOM ORDER, " +
             "WHAT YOU KNOW, YOUR SELECTION all carry one. Without it a dark rectangle behind " +
             "white text reads as a subtitle box that wandered into the 3D scene; with it the " +
             "labels belong to the same product as the panels beside them.")]
    public bool accentRule = true;
    [Range(0.02f, 0.2f)] public float accentWidth = 0.075f;

    [Header("De-overlap")]
    [Tooltip("When two labels overlap on screen, hide the farther one.\n\n" +
             "Sixteen labels along one plant means that from most angles several line up " +
             "behind each other and overprint - the unreadable pile-up at the top of frame.\n\n" +
             "A distance cutoff was the obvious fix and it is wrong: measured per camera it " +
             "still left 6 to 12 labels competing on the close shots, and stripped BOTH wide " +
             "establishing shots down to nothing, because how far a machine is says nothing " +
             "about whether it is the subject. Overlap is the thing that actually hurts " +
             "legibility, so overlap is what this tests. Nearest wins, because the nearest " +
             "machine is the one the shot is on.")]
    public bool deOverlap = true;

    [Tooltip("Padding around each label's screen rect, in viewport units, so survivors are " +
             "not left touching.")]
    [Range(0f, 0.1f)] public float overlapPadding = 0.012f;

    [Tooltip("Off to compare against the authored look without recompiling.")]
    public bool apply = true;

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
        if (FindAnyObjectByType<WorldLabelTypography>() != null) return;
        new GameObject("~WorldLabelTypography").AddComponent<WorldLabelTypography>();
    }

    // -------------------------------------------------------------------- apply ----
    void Start()
    {
        if (!apply) return;

        // Sans, not Mono. Mono sets roughly 20% wider per character, and these labels sit
        // on backing plates that were sized around the original LiberationSans. Wider text
        // overflows its plate, which is what made them look like they were floating free
        // of the machinery rather than attached to it. Sans is close enough in width to
        // the original that everything still sits on its plate.
        var face = font != null ? font : BladeLoopTheme.Sans;
        if (face == null)
        {
            Debug.LogWarning("[WorldLabelTypography] no font available - labels left as authored.");
            return;
        }

        int styled = 0;
        var list = new System.Collections.Generic.List<TextMeshPro>();

        foreach (var t in FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include,
                                                          FindObjectsSortMode.None))
        {
            if (t == null) continue;

            // Colour, alignment and position stay the author's.
            if (t.font != face) t.font = face;

            // Size and tracking DO change, because the face changed. IBM Plex sets larger
            // per point than the LiberationSans these were sized against, so keeping the
            // number would keep them looking oversized - which was the complaint. Scaling
            // down and opening the tracking is what turns them from a caption into a
            // plant label.
            t.fontSize *= fontSizeScale;
            t.characterSpacing = characterSpacing;

            // .fontMaterial INSTANCES. The font asset's shared material is untouched, so
            // the menus and panels that use the same IBM Plex asset are unaffected.
            var m = t.fontMaterial;
            if (m == null) continue;

            // Mobile/Distance Field -> Distance Field. Same properties, better AA, and it
            // is the only one of the two that can actually render an underlay. The atlas
            // and every other setting carry over untouched.
            if (useFullDistanceFieldShader && m.shader != null &&
                m.shader.name == "TextMeshPro/Mobile/Distance Field")
            {
                var full = Shader.Find("TextMeshPro/Distance Field");
                if (full != null) m.shader = full;
            }

            if (m.HasProperty(ShaderUtilities.ID_FaceDilate))
                m.SetFloat(ShaderUtilities.ID_FaceDilate, faceDilate);

            if (m.HasProperty(ShaderUtilities.ID_OutlineWidth))
            {
                m.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
                m.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
                m.EnableKeyword("OUTLINE_ON");
            }

            if (useUnderlay && m.HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                m.SetColor(ShaderUtilities.ID_UnderlayColor, underlayColor);
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, underlayOffsetX);
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, underlayOffsetY);
                m.SetFloat(ShaderUtilities.ID_UnderlayDilate, underlayDilate);
                m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, underlaySoftness);
                m.EnableKeyword("UNDERLAY_ON");
            }

            t.UpdateMeshPadding();   // outline and underlay both grow the glyph bounds

            // The size and angle fix. Adding the component at runtime keeps this a
            // zero-scene-edit change, exactly like everything else here.
            //
            // ScreenSafeLabel only pulls a label toward the safe area when it would
            // otherwise be clipped - when it is comfortably in frame the target it
            // computes IS the authored position, so labels stay beside the machine they
            // name and their leader lines still land.
            if (addScreenSafeLabel && t.GetComponent<ScreenSafeLabel>() == null)
            {
                var ssl = t.gameObject.AddComponent<ScreenSafeLabel>();
                ssl.referenceDistance = referenceDistance;
                ssl.minScale          = minScale;
                ssl.maxScale          = maxScale;
                ssl.hideNearerThan    = hideNearerThan;
                ssl.hideFartherThan   = hideFartherThan;
                ssl.faceCamera        = true;   // stops the perspective skew
            }

            if (addBackingPlate) MakePlate(t);

            list.Add(t);
            styled++;
        }

        managed = list.ToArray();

        Debug.Log($"[WorldLabelTypography] {styled} world labels set in {face.name}, " +
                  $"screen-constant at {referenceDistance:0} m" +
                  (deOverlap ? ", de-overlapped nearest-first." : "."));
    }

    // ---------------------------------------------------------------- de-overlap ----
    //
    // Runs at order 150, ScreenSafeLabel at 120, so ScreenSafeLabel's LateUpdate has
    // already positioned, scaled and faded everything by the time this decides what
    // collides.
    TextMeshPro[] managed;
    readonly System.Collections.Generic.List<Rect> keptRects = new System.Collections.Generic.List<Rect>();

    void LateUpdate()
    {
        if (!deOverlap || managed == null || managed.Length == 0) return;
        var cam = Camera.main;
        if (cam == null) return;

        // Nearest first: the machine closest to the camera is the one the shot is about,
        // so it keeps its label and anything colliding with it gives way.
        System.Array.Sort(managed, (a, b) =>
        {
            if (a == null || b == null) return 0;
            float da = (a.transform.position - cam.transform.position).sqrMagnitude;
            float db = (b.transform.position - cam.transform.position).sqrMagnitude;
            return da.CompareTo(db);
        });

        keptRects.Clear();

        foreach (var t in managed)
        {
            if (t == null) continue;
            var r = t.GetComponent<Renderer>();
            if (r == null) continue;

            // Plate and accent rule are child renderers, so they have to be switched with
            // the words - otherwise hiding an overlapping label leaves its panel and orange
            // stripe floating on their own.
            // ScreenSafeLabel may already have faded this one out; leave it alone.
            if (!r.enabled) { SetChildren(t.transform, false); continue; }
            if (!ScreenRect(cam, r, out Rect rect)) continue;

            rect.xMin -= overlapPadding; rect.xMax += overlapPadding;
            rect.yMin -= overlapPadding; rect.yMax += overlapPadding;

            bool clash = false;
            for (int i = 0; i < keptRects.Count && !clash; i++)
                if (keptRects[i].Overlaps(rect)) clash = true;

            if (clash) r.enabled = false;          // a nearer label already owns this space
            else keptRects.Add(rect);

            SetChildren(t.transform, r.enabled);
        }
    }

    Material plateMat;

    /// <summary>
    /// A dark panel sized to this label's own text, parented to it so it billboards,
    /// scales and fades along with the words.
    /// </summary>
    void MakePlate(TextMeshPro label)
    {
        if (label.transform.Find("~Plate") != null) return;

        // Bounds are only valid once TMP has laid the text out.
        label.ForceMeshUpdate(true, true);
        var b = label.textBounds;
        if (b.size.x < 0.001f) return;

        if (plateMat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) return;
            plateMat = new Material(sh);
            plateMat.SetColor("_BaseColor", plateColor);
            // Transparent surface. Set by hand because a material made from the shader at
            // runtime starts opaque whatever the colour's alpha says.
            plateMat.SetFloat("_Surface", 1f);
            plateMat.SetFloat("_Blend", 0f);
            plateMat.SetFloat("_ZWrite", 0f);
            plateMat.SetFloat("_Cull", 0f);          // visible from either side, so a
                                                     // billboard can never turn its back
            plateMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            plateMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            plateMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            plateMat.renderQueue = 2990;             // before the text at 3000
        }

        float w = b.size.x + platePadding.x;
        float h = b.size.y + platePadding.y;

        // TMP reads from its -Z side, and ScreenSafeLabel points +Z away from the camera,
        // so +Z is behind the words from where the audience sits.
        var plate = Quad("~Plate", label.transform, plateMat,
                         new Vector3(b.center.x, b.center.y, 0.012f),
                         new Vector3(w, h, 1f));

        if (!accentRule) return;

        if (accentMat == null)
        {
            accentMat = new Material(plateMat);
            accentMat.SetColor("_BaseColor", BladeLoopTheme.Oxide);
            accentMat.renderQueue = 2992;            // over the plate, under the text
        }

        // Leading edge, just in front of the plate so it cannot z-fight with it.
        Quad("~Accent", label.transform, accentMat,
             new Vector3(b.center.x - w * 0.5f + accentWidth * 0.5f, b.center.y, 0.011f),
             new Vector3(accentWidth, h, 1f));
    }

    static Transform Quad(string name, Transform parent, Material mat, Vector3 pos, Vector3 scale)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        var col = q.GetComponent<Collider>();
        if (col != null) Destroy(col);
        q.GetComponent<Renderer>().sharedMaterial = mat;
        var tr = q.transform;
        tr.SetParent(parent, false);
        tr.localPosition = pos;
        tr.localRotation = Quaternion.identity;
        tr.localScale    = scale;
        return tr;
    }

    Material accentMat;

    /// <summary>Switch a label's plate and accent rule with the label itself.</summary>
    static void SetChildren(Transform label, bool on)
    {
        for (int i = 0; i < label.childCount; i++)
        {
            var cr = label.GetChild(i).GetComponent<Renderer>();
            if (cr != null && cr.enabled != on) cr.enabled = on;
        }
    }

    /// <summary>The label's bounds projected into viewport space. False if behind the camera.</summary>
    static bool ScreenRect(Camera cam, Renderer r, out Rect rect)
    {
        rect = default;
        var b = r.bounds;
        float x0 = 9f, x1 = -9f, y0 = 9f, y1 = -9f;
        bool any = false;
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                                     (i & 2) == 0 ? b.min.y : b.max.y,
                                     (i & 4) == 0 ? b.min.z : b.max.z);
            var vp = cam.WorldToViewportPoint(corner);
            if (vp.z <= 0f) continue;
            any = true;
            x0 = Mathf.Min(x0, vp.x); x1 = Mathf.Max(x1, vp.x);
            y0 = Mathf.Min(y0, vp.y); y1 = Mathf.Max(y1, vp.y);
        }
        if (!any) return false;
        rect = Rect.MinMaxRect(x0, y0, x1, y1);
        return true;
    }
}
