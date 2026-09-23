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
[DefaultExecutionOrder(80)]   // after Stage4OrderBinding (60) has rewritten the percentages
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

    [Tooltip("Shrink relative to the authored size. IBM Plex sets larger than the " +
             "LiberationSans these were tuned against, so matching size means going smaller.")]
    [Range(0.5f, 1.2f)] public float fontSizeScale = 0.82f;

    [Tooltip("Letter-spacing. Wide tracking on short uppercase strings is what makes " +
             "technical labelling read as engineering rather than as a caption.")]
    [Range(0f, 30f)] public float characterSpacing = 9f;

    [Tooltip("Weight gain. Zero now: the mono face already carries enough stroke, and " +
             "dilating it was most of why the first attempt looked heavy.")]
    [Range(0f, 0.4f)] public float faceDilate = 0f;

    [Header("Outline")]
    [Tooltip("Just enough to separate glyphs from the background. 0.18 was far too much.")]
    [Range(0f, 1f)] public float outlineWidth = 0.07f;
    public Color outlineColor = new Color(0.03f, 0.03f, 0.04f, 1f);

    [Header("Underlay (soft drop, carries contrast over moving backgrounds)")]
    public bool useUnderlay = true;
    public Color underlayColor = new Color(0f, 0f, 0f, 0.55f);
    [Range(-1f, 1f)] public float underlayOffsetX = 0.06f;
    [Range(-1f, 1f)] public float underlayOffsetY = -0.06f;
    [Range(0f, 1f)] public float underlayDilate = 0.05f;
    [Range(0f, 1f)] public float underlaySoftness = 0.32f;

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

        var face = font != null ? font : BladeLoopTheme.MonoBold;
        if (face == null)
        {
            Debug.LogWarning("[WorldLabelTypography] no font available - labels left as authored.");
            return;
        }

        int styled = 0;

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
            styled++;
        }

        Debug.Log($"[WorldLabelTypography] {styled} world labels set in {face.name} " +
                  $"with outline {outlineWidth:0.00} and " +
                  (useUnderlay ? "a soft underlay." : "no underlay."));
    }
}
