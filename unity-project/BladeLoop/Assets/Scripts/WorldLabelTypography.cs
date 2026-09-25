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
    /// <summary>Scenes that get the full treatment: font, plates, screen-constant sizing.</summary>
    static readonly string[] RestyleScenes = { "Stage4_V2" };

    /// <summary>
    /// Scenes that get the DE-OVERLAP ONLY.
    ///
    /// Stage 3's four zone headings already have ScreenSafeLabel and their own look, which
    /// Ritwika owns - restyling them is not mine to do. But they overprint each other on
    /// the wide shots: ZONE 1 PREHEAT, ZONE 2 MELTING and ZONE 3 CHAR CRACK sit along one
    /// drum, so from anywhere off-axis they collapse into each other and none of the three
    /// can be read.
    ///
    /// The de-overlap pass is scene-agnostic - it only hides a label whose screen rect
    /// collides with a nearer one - so it solves that without touching a single authored
    /// value.
    /// </summary>
    static readonly string[] DeOverlapOnlyScenes = { "Stage3_StoryMode" };

    /// <summary>False in de-overlap-only scenes: no font swap, no plates, no resizing.</summary>
    bool restyle = true;

    [Header("De-overlap-only scenes")]
    [Tooltip("Lower ScreenSafeLabel's near-fade in those scenes, so a heading is not deleted " +
             "just for being close to the camera. Overlap is now detected by testing overlap, " +
             "so the distance rule it used as a proxy is redundant.")]
    public bool relaxNearFade = true;
    [Tooltip("Distance below which a label still fades. Small: only when the camera is " +
             "practically inside the equipment.")]
    public float nearFade = 1.2f;

    [Header("Face")]
    [Tooltip("Leave empty to use BladeLoopTheme.Sans (IBM Plex Sans Regular).\n\n" +
             "Sans, not Mono. Mono was tried and sets about 20% wider per character, which " +
             "overflowed the backing plates and made the labels look detached from the " +
             "machinery. SansBold was tried before that and read heavy. Regular Sans is close " +
             "enough in width to the LiberationSans this was authored against that everything " +
             "still fits, while matching the brand face the rest of the UI uses.")]
    public TMP_FontAsset font;

    [Tooltip("Relative to the authored size.\n\n" +
             "Shrinking this below 1 costs pixel coverage, and on a world-space label read " +
             "from several metres away that reads as blur rather than as smaller text - " +
             "sharpness here is pixel coverage, not point size. 1.18 is authored size plus " +
             "a little, which is where it looked right on screen.")]
    [Range(0.5f, 1.6f)] public float fontSizeScale = 1.18f;

    [Tooltip("Letter-spacing. ZERO - no extra tracking at all.\n\n" +
             "Wide tracking on short uppercase strings does read as engineering rather than " +
             "as a caption, and earlier versions of this used it. It was taken back out: " +
             "tracking spreads the same glyphs over more screen for the same pixel budget, " +
             "and these labels are read small and at a distance, where pixel coverage is " +
             "what sharpness actually is. The plates now supply the 'designed' look that the " +
             "tracking was reaching for.")]
    [Range(0f, 30f)] public float characterSpacing = 0f;

    [Header("Shader")]
    [Tooltip("Upgrade from TextMeshPro/Mobile/Distance Field to the full Distance Field " +
             "shader.\n\n" +
             "The IBM Plex assets ship with the Mobile variant, which is a cut-down SDF: " +
             "cheaper antialiasing, and no underlay support at all - so the soft drop I " +
             "set earlier was silently doing nothing. The full shader is what makes " +
             "world-space text resolve cleanly at a distance.")]
    public bool useFullDistanceFieldShader = true;

    [Tooltip("Weight gain. Zero: the face already carries enough stroke, and dilating it " +
             "was most of why the first attempt looked heavy.")]
    [Range(0f, 0.4f)] public float faceDilate = 0f;

    [Header("Outline")]
    [Tooltip("ZERO now, and that is the point.\n\n" +
             "TMP grows an outline INWARD from the glyph edge as well as outward. On a text " +
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
             "14 was too far - it locked them to how small they looked from across the plant, " +
             "and 8 overshot the other way. 11 is roughly a shot's working distance, which is " +
             "the size they were designed to be read at.")]
    public float referenceDistance = 11f;

    [Tooltip("Lower clamp on the distance scaling. THIS CLAMP IS THE 'WHY IS THAT LABEL " +
             "HUGE' BUG - keep it low.\n\n" +
             "Constant apparent size needs scale = depth/referenceDistance. The moment that " +
             "falls below this clamp the label STOPS SHRINKING as the camera closes in, and " +
             "balloons. At 0.15 the clamp bit below 1.65 m of depth, and Stage 4 has labels " +
             "much nearer than that - measured on vCam_08_Condenser:\n\n" +
             "    ROTARY AIRLOCK VALVE    depth 0.7 m    61 px tall   (norm is 26)\n" +
             "    RECLAIMED GLASS FIBRE   vCam_04       116 px tall   4.5x\n\n" +
             "Swept across all fourteen Stage 4 cameras and every label, counting lines over " +
             "35 px per line:\n\n" +
             "    minScale 0.15   19 offenders   worst 116 px\n" +
             "    minScale 0.06    2 offenders   worst  47 px\n" +
             "    minScale 0.03    0 offenders\n\n" +
             "So 0.03. It holds true size down to 0.33 m of depth, which is nearer than any " +
             "camera gets, and still guards against the depth-to-zero blow-up the clamp is " +
             "actually for. An earlier note here claimed the nearest label was 1.7 m; that " +
             "came from measuring with the wrong camera aim.")]
    [Range(0.02f, 1f)] public float minScale = 0.03f;
    [Range(1f, 4f)]    public float maxScale = 2.0f;

    [Tooltip("Fade a label out below this distance.\n\n" +
             "Nearly off, deliberately. In Stage 3 this stopped three zone headings piling " +
             "on top of each other on a close shot. Stage 4 is the opposite case: each label " +
             "names one machine, and the camera closes in ON that machine - so a 3.5 m " +
             "threshold deleted the label the shot was about. vCam_07_Cyclone sits 3.3 m " +
             "from the words GAS CYCLONE SEPARATOR.")]
    public float hideNearerThan = 0.8f;

    [Tooltip("How far outside the frame a label may be dragged back in, in viewport units. " +
             "9 = off (the old unbounded clamp).\n\n" +
             "THE MOST IMPORTANT VALUE IN THIS FILE. A label that names a machine has one " +
             "job: to sit beside that machine. Measured across Stage 4's fourteen shots, " +
             "with this off, only 13 of 171 label placements had their anchor genuinely in " +
             "frame - 166 were drawn somewhere other than where they were authored:\n\n" +
             "    vCam_01  PYROLYSIS OIL      anchor x 6.16   drawn 0.60   moved 5.58\n" +
             "    vCam_02  INDUCED DRAFT FAN  anchor 9.0,7.9  drawn 0.60   moved 11.01\n" +
             "    vCam_04d KILN DRUM          anchor -10.7    drawn 0.12   moved 11.57\n\n" +
             "An anchor six screen-widths away is not 'slightly clipped'. Those labels were " +
             "being hauled across the frame and parked at the edge with their leader lines " +
             "pointing at nothing, which is what made the stage look cluttered and wrong.\n\n" +
             "I rejected this limit once because it leaves some shots with no labels at all. " +
             "That was the wrong reading: a shot with no labelled equipment in view SHOULD " +
             "show no labels. Empty is correct; lying is not. 0.15 still rescues a heading " +
             "that is genuinely just off the edge, which is what the clamp was built for.")]
    [Range(0.05f, 9f)] public float maxRescue = 0.15f;

    [Tooltip("Most labels to show at once. The nearest win, because the nearest machine is " +
             "the one the shot is on.\n\n" +
             "This is the crowding fix. Measured with no cap, Stage 4 shows between 7 and 13 " +
             "labels on every one of its fourteen shots - a column of names down the edge, " +
             "most of them for equipment off screen. Capping the count cannot empty a shot " +
             "the way a rescue limit can: it only ever removes the least relevant.")]
    [Range(1, 16)] public int maxVisible = 6;

    [Tooltip("Fade a label out beyond this distance.\n\n" +
             "INFINITY, i.e. OFF, and deliberately so. This was the first attempt at the " +
             "overlap problem and measuring killed it: at 9 m it still left 6 to 12 labels " +
             "competing on the close shots, and stripped BOTH wide establishing shots to " +
             "nothing, because how far a machine is says nothing about whether it is the " +
             "subject. The de-overlap pass below replaced it. Left exposed in case a future " +
             "scene wants a hard far cutoff.")]
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
    [Tooltip("When two labels collide on screen, move the farther one clear - and only hide " +
             "it if there is nowhere to move it to. See nudgeInsteadOfHiding below.\n\n" +
             "Sixteen labels along one plant means that from most angles several line up " +
             "behind each other and overprint - the unreadable pile-up at the top of frame.\n\n" +
             "A distance cutoff was the obvious fix and it is wrong: measured per camera it " +
             "still left 6 to 12 labels competing on the close shots, and stripped BOTH wide " +
             "establishing shots down to nothing, because how far a machine is says nothing " +
             "about whether it is the subject. Overlap is the thing that actually hurts " +
             "legibility, so overlap is what this tests. Nearest wins, because the nearest " +
             "machine is the one the shot is on.")]
    public bool deOverlap = true;

    [Tooltip("Padding around each label's screen rect, in viewport units.\n\n" +
             "Small on purpose, but note this was cut from 0.012 twice while chasing the " +
             "wrong cause. The rects really were touching when the words did not - not " +
             "because the padding was large, but because the rect came from the renderer's " +
             "world AABB, which over-reported the width of a billboarded label by up to " +
             "2.8x. That is fixed in ScreenRect, which now measures the text itself, so this " +
             "value is doing only what it says.\n\n" +
             "Keep it small anyway: each of Stage 3's three headings names a different kiln " +
             "zone, so pushing one aside costs more than letting two sit close.")]
    [Range(0f, 0.1f)] public float overlapPadding = 0.003f;

    [Tooltip("When two labels collide, step the farther one up or down a line instead of " +
             "deleting it.\n\n" +
             "Hiding was the original behaviour and it is too blunt for Stage 3: three " +
             "headings name three kiln zones, and losing one loses a third of the " +
             "explanation. There is empty frame above and below them, so there is somewhere " +
             "to put it. Hiding is still the fallback when there is not.")]
    public bool nudgeInsteadOfHiding = true;

    [Tooltip("One step, in viewport units. The headings measure about 0.063 tall, so 0.075 " +
             "clears a full line with a little air.")]
    [Range(0.02f, 0.25f)] public float nudgeStep = 0.075f;

    [Tooltip("Tried as +1, -1, +2, -2, ... so a label moves the shortest distance that " +
             "clears, and never off the edge of frame.")]
    [Range(1, 5)] public int maxNudgeSteps = 3;

    [Tooltip("Off to compare against the authored look without recompiling.")]
    public bool apply = true;

    [Tooltip("Write to the log whenever a label is hidden, and what hid it.\n\n" +
             "OFF for the submitted build. It earned its keep - it is how we proved which " +
             "Stage 4 labels were being dropped rather than guessing from screenshots - but " +
             "it fires every two seconds per hidden label, and a shipped player's log should " +
             "be readable. Turn it back on the moment a label misbehaves again.")]
    public bool diagnose = false;

    /// <summary>Does this rect collide with anything already placed this frame?</summary>
    bool Clashes(Rect r)
    {
        for (int i = 0; i < keptRects.Count; i++)
            if (keptRects[i].Overlaps(r)) return true;
        return false;
    }

    /// <summary>First line of a label's text, for logging.</summary>
    static string OneLine(TMP_Text t)
    {
        if (t == null || string.IsNullOrEmpty(t.text)) return "(empty)";
        int nl = t.text.IndexOf('\n');
        return nl > 0 ? t.text.Substring(0, nl) : t.text;
    }

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
        bool full = System.Array.IndexOf(RestyleScenes, s.name) >= 0;
        bool thin = System.Array.IndexOf(DeOverlapOnlyScenes, s.name) >= 0;
        if (!full && !thin) return;
        if (FindAnyObjectByType<WorldLabelTypography>() != null) return;

        var c = new GameObject("~WorldLabelTypography").AddComponent<WorldLabelTypography>();
        c.restyle = full;
    }

    // -------------------------------------------------------------------- apply ----
    void Start()
    {
        if (!apply) return;

        // De-overlap-only scene: collect the labels and stop. No font swap, no plates, no
        // resizing - Stage 3's headings keep every authored value, they just stop
        // overprinting each other.
        if (!restyle)
        {
            var thin = new System.Collections.Generic.List<TextMeshPro>(
                FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            managed = thin.ToArray();
            addBackingPlate = false;

            // Relax ScreenSafeLabel's NEAR FADE.
            //
            // Stage 3's zone headings are authored with hideNearerThan 4.5 and fadeBand 2,
            // so anything within 6.5 m vanishes. ZONE 2 MELTING sits at the middle of the
            // drum, which makes it the closest label to almost every kiln camera:
            //
            //     vCam_S3_10_CutawayInterior   5.2 m   faded
            //     vCam_S3_11_GlassFibersClose  5.2 m   faded
            //     vCam_S3_09_BurnersUnder      6.0 m   faded
            //     vCam_S3_07_Zone2_Melting     6.5 m   faded   <- the shot ABOUT zone 2
            //
            // So the middle heading is missing from the stage, and has been since long
            // before the de-overlap pass existed. I wrote that fade to stop the three
            // headings piling up on close shots, which was the right problem and the wrong
            // tool - it deletes the label the shot is about. Overlap is now handled by
            // testing actual overlap, so the distance rule is redundant and only harmful.
            if (relaxNearFade)
            {
                int relaxed = 0;
                foreach (var t in managed)
                {
                    if (t == null) continue;
                    var ssl = t.GetComponent<ScreenSafeLabel>();
                    if (ssl == null || ssl.hideNearerThan <= nearFade) continue;
                    ssl.hideNearerThan = nearFade;
                    ssl.fadeBand = Mathf.Min(ssl.fadeBand, 0.8f);
                    relaxed++;
                }
                if (relaxed > 0)
                    Debug.Log($"[WorldLabelTypography] relaxed the near-fade on {relaxed} label(s) to {nearFade} m.");
            }

            Debug.Log($"[WorldLabelTypography] de-overlap only: watching {managed.Length} label(s).");
            return;
        }

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
                ssl.maxRescue         = maxRescue;
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
        if (managed == null || managed.Length == 0) return;

        // Plates for labels that were not active when Start ran.
        //
        // Three of the sixteen - LIGHT VAPOUR RISES, HEAVY FIBRES FALL and ANOXIC
        // PYROLYSIS ZONE - are switched on later by the timeline, on the beat they
        // describe. An inactive TextMeshPro has no layout, so its textBounds are zero and
        // MakePlate bails; building plates once in Start left those three as bare white
        // text among thirteen plated ones, which is exactly how it shipped.
        //
        // Cheap: it only looks at labels that still have no plate, and each one is dealt
        // with the first frame it becomes visible.
        if (addBackingPlate)
            foreach (var t in managed)
                if (t != null && t.gameObject.activeInHierarchy && t.transform.Find("~Plate") == null)
                    MakePlate(t);

        if (!deOverlap) return;
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
            if (!ScreenRect(cam, t, out Rect rect)) continue;

            rect.xMin -= overlapPadding; rect.xMax += overlapPadding;
            rect.yMin -= overlapPadding; rect.yMax += overlapPadding;

            // STEP IT ASIDE BEFORE HIDING IT.
            //
            // On the wide shots the three kiln headings genuinely do collide - they name
            // points 2.9 m apart along one drum, read from 12 to 20 m away, and the words
            // are wider than the gaps. Measured on vCam_S3_01_WideReveal:
            //
            //     ZONE 3 CHAR CRACK   x 0.254 - 0.431
            //     ZONE 2 MELTING      x 0.390 - 0.526     overlaps ZONE 3 by 0.041
            //     ZONE 1 PREHEAT      x 0.520 - 0.662     overlaps ZONE 2 by 0.006
            //
            // So this is not a false positive to be tuned away - the padding has already
            // been cut twice chasing that idea. The labels really are on top of each other,
            // and hiding one costs a whole kiln zone. Moving it up or down a line costs
            // nothing: there is empty frame directly above and below.
            //
            // Nearest keeps the authored height; the ones behind it step off it.
            bool clash = Clashes(rect);
            int step = 0;

            var ssl2 = t.GetComponent<ScreenSafeLabel>();
            if (clash && nudgeInsteadOfHiding && ssl2 != null)
            {
                // Viewport height of one nudge, converted to world units at this label's
                // depth: the frustum is 2*depth*tan(fov/2) tall there.
                float depth = cam.WorldToViewportPoint(t.transform.position).z;
                float frustum = 2f * Mathf.Max(depth, 0.01f) *
                                Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

                for (int s = 1; s <= maxNudgeSteps && clash; s++)
                {
                    for (int sign = 1; sign >= -1 && clash; sign -= 2)
                    {
                        var moved = rect;
                        moved.y += sign * s * nudgeStep;
                        if (moved.yMin < 0.02f || moved.yMax > 0.98f) continue;  // off frame
                        if (Clashes(moved)) continue;
                        rect = moved;
                        step = sign * s;
                        clash = false;
                    }
                }

                if (step != 0)
                    t.transform.position += cam.transform.up * (step * nudgeStep * frustum);
            }

            // Count cap. keptRects is built nearest-first, so by the time it is full the
            // labels in it are the nearest ones - the machinery the shot is actually on.
            if (!clash && maxVisible > 0 && keptRects.Count >= maxVisible)
            {
                r.enabled = false;
                SetChildren(t.transform, false);
                if (diagnose && Time.frameCount % 120 == 0)
                    Debug.Log($"[WorldLabelTypography] hid '{OneLine(t)}' - over the {maxVisible}-label cap.");
                continue;
            }

            if (clash)
            {
                r.enabled = false;                 // nowhere to put it - a nearer label wins
                // Say so in the log. Chasing this by eye through a build cost several
                // rounds; a build writes a Player.log, so let it write down what it did.
                if (diagnose && Time.frameCount % 120 == 0)
                    Debug.Log($"[WorldLabelTypography] hid '{OneLine(t)}' - overlaps a nearer label.");
            }
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
            // Take the shader off Assets/Resources/PlateUnlit.mat rather than asking
            // Shader.Find for it.
            //
            // Shader.Find only resolves in a player build if something already references
            // that shader; nothing in the project used URP/Unlit, so the build stripped it
            // and this returned null. In the editor it looked perfect - in a build there
            // were no plates, no accent rules, and because the outline and face dilate are
            // off on the grounds that the plates carry the contrast, the labels would have
            // shipped as bare white text over the kiln. Caught by Ritwika on review.
            //
            // That material exists to keep the shader compiled in. Reading the shader from
            // it means this cannot fail even if the asset is later renamed away from the
            // name Shader.Find expects.
            var src = Resources.Load<Material>("PlateUnlit");
            var sh = src != null ? src.shader : Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null)
            {
                Debug.LogWarning("[WorldLabelTypography] URP/Unlit unavailable - no backing plates.");
                return;
            }
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

    /// <summary>
    /// The WORDS projected into viewport space. False if behind the camera.
    ///
    /// This used to project the renderer's world bounds - Renderer.bounds, an
    /// AXIS-ALIGNED box in WORLD space. For a billboarded label that is the wrong shape
    /// twice over: the box circumscribes a quad that is rotated to face the camera, and
    /// then all eight of its corners are projected and unioned. Measured against the
    /// actual text quad on Stage 3's headings, it over-reported the width by up to 2.8x
    /// and the height by about 1.6x:
    ///
    ///     vCam_S3_11   ZONE 1 PREHEAT   AABB 0.917 wide   text 0.325 wide   x2.82
    ///     vCam_S3_10   ZONE 1 PREHEAT   AABB 0.265 wide   text 0.182 wide   x1.46
    ///     vCam_S3_08   ZONE 1 PREHEAT   AABB 0.162 wide   text 0.132 wide   x1.22
    ///
    /// So labels were being hidden for colliding with space their neighbour was not
    /// occupying. Hiding one is a heavy penalty - each of Stage 3's three names a
    /// different kiln zone - so the test has to be the words themselves.
    ///
    /// textBounds is in the label's LOCAL frame, and the label is a flat quad, so four
    /// corners at z=0 describe it exactly.
    /// </summary>
    static bool ScreenRect(Camera cam, TMP_Text t, out Rect rect)
    {
        rect = default;
        var b = t.textBounds;
        if (b.size.x < 1e-5f) return false;

        var tr = t.transform;
        float x0 = 9f, x1 = -9f, y0 = 9f, y1 = -9f;
        bool any = false;
        for (int i = 0; i < 4; i++)
        {
            var local = new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                                    (i & 2) == 0 ? b.min.y : b.max.y,
                                    0f);
            var vp = cam.WorldToViewportPoint(tr.TransformPoint(local));
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
