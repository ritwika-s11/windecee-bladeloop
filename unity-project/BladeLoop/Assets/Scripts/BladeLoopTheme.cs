using UnityEngine;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// The one place the app's colours and fonts live.
///
/// ---------------------------------------------------------------------------
///  WHY THIS FILE EXISTS
///
///  The home page palette is currently private inside MainMenuController.
///  handover-sharan.md tells Sharan to "copy those constants" into Custom Order
///  and the Outcome Report; the order panel below needs the same colours as a
///  third surface. Three hand-copied sets of hex strings drift the moment one
///  of them is tweaked, and the symptom - a panel that is subtly the wrong
///  shade next to the tour - is the kind of thing nobody files a bug for.
///
///  This is the same argument Ritwika already accepted for
///  OrderContext.TourSplitWidth: two systems owned by different people have to
///  agree on a number, so the number gets exactly one home.
///
///  ADOPTION: this file is additive and changes nobody's code. MainMenuController
///  can keep its private copy indefinitely and nothing breaks. The intent is that
///  it and Sharan's screens eventually read from here instead - at which point
///  the hexes below become the single source and the private copies go.
///
///  Values are copied verbatim from MainMenuController.InitPalette() as of
///  d6f9432. If you change one here, change it there too until adoption happens.
/// ---------------------------------------------------------------------------
///
/// Owner: Akshat.
/// </summary>
public static class BladeLoopTheme
{
    // ---- surfaces and type ---------------------------------------------------
    public static Color Bone     { get; private set; }   // primary text on dark
    public static Color Muted    { get; private set; }   // secondary text
    public static Color Faint    { get; private set; }   // labels, units
    public static Color Oxide    { get; private set; }   // the one accent
    public static Color Rule     { get; private set; }   // dividers
    public static Color RuleSoft { get; private set; }
    public static Color Panel    { get; private set; }   // panel background
    public static Color SkyWarm  { get; private set; }

    // ---- the five output streams, coloured as the materials actually are -----
    public static Color StreamFibre { get; private set; }
    public static Color StreamOil   { get; private set; }
    public static Color StreamGas   { get; private set; }
    public static Color StreamChar  { get; private set; }
    public static Color StreamLoss  { get; private set; }

    static bool paletteReady;

    public static void InitPalette()
    {
        if (paletteReady) return;

        Bone     = Hex("EDE8DF");
        Muted    = Hex("8A8177");
        Faint    = Hex("6E665C");
        Oxide    = Hex("C2603A");
        Rule     = Hex("2A2520");
        RuleSoft = Hex("221E1A");
        Panel    = Hex("12100D");
        SkyWarm  = Hex("1A1713");

        StreamFibre = Hex("E4DCCD");   // reclaimed glass fibre, off-white
        StreamOil   = Hex("C99A3E");   // pyrolysis oil, amber
        StreamGas   = Hex("6B8F62");   // syngas
        StreamChar  = Hex("2E2823");   // carbon char, near black
        StreamLoss  = Hex("5A524A");   // fugitive dust and residue

        paletteReady = true;
    }

    public static Color Hex(string h)
    {
        ColorUtility.TryParseHtmlString("#" + h, out var c);
        return c;
    }

    /// <summary>Stream colours in the order the output bars are drawn:
    /// fibre, oil, syngas, char, loss.</summary>
    public static Color[] StreamColours
    {
        get
        {
            InitPalette();
            return new[] { StreamFibre, StreamOil, StreamGas, StreamChar, StreamLoss };
        }
    }

    // ---- typography ----------------------------------------------------------
    // IBM Plex, SIL Open Font License. The assets live in Assets/Resources/Fonts/
    // SPECIFICALLY so Resources.Load resolves them in a player build - moving them
    // somewhere tidier silently breaks fonts in the WebGL build only.

    public static TMP_FontAsset Sans     { get; private set; }
    public static TMP_FontAsset SansBold { get; private set; }
    public static TMP_FontAsset Mono     { get; private set; }
    public static TMP_FontAsset MonoBold { get; private set; }

    static bool fontsReady;

    public static void InitFonts()
    {
        if (fontsReady) return;

        Sans     = LoadFont("IBMPlexSans-Regular SDF");
        SansBold = LoadFont("IBMPlexSans-SemiBold SDF");
        Mono     = LoadFont("IBMPlexMono-Regular SDF");
        MonoBold = LoadFont("IBMPlexMono-Medium SDF");

        fontsReady = true;
    }

    static TMP_FontAsset LoadFont(string name)
    {
        var f = Resources.Load<TMP_FontAsset>("Fonts/" + name);
        if (f == null)
            Debug.LogWarning($"[BladeLoopTheme] Font '{name}' not found in Resources/Fonts - " +
                             "TextMeshPro will fall back to its default.");
        return f;
    }

    /// <summary>Palette and fonts in one call. Safe to call repeatedly.</summary>
    public static void Init()
    {
        InitPalette();
        InitFonts();
    }

    // ---- rounded surfaces ----------------------------------------------------

    static readonly Dictionary<int, Sprite> roundedCache = new Dictionary<int, Sprite>();

    /// <summary>
    /// A white 9-sliced sprite with rounded corners, for use with Image.type =
    /// Sliced. Tint it with Image.color as usual.
    ///
    /// WHY THIS EXISTS. Every surface built from a bare Image is a hard-edged
    /// rectangle, and a screen made entirely of hard rectangles reads as a form
    /// however good the type and spacing are. uGUI has no corner radius, so the
    /// standard answer is a sliced sprite - generated here rather than shipped as
    /// an asset so it needs no import, no meta file and no one else's approval.
    ///
    /// Cached per radius: a handful of small textures for the whole application.
    /// Anti-aliased on the diagonal, so it holds up when the sprite is scaled.
    /// </summary>
    public static Sprite Rounded(int radius)
    {
        radius = Mathf.Clamp(radius, 1, 48);
        Sprite cached;
        if (roundedCache.TryGetValue(radius, out cached) && cached != null) return cached;

        // Two-pixel middle band is all a sliced sprite needs; the corners carry the shape.
        int size = radius * 2 + 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            hideFlags  = HideFlags.HideAndDontSave
        };

        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance past the corner arc, in pixels. Negative inside.
                float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius));
                float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius));
                float d  = (dx > 0f && dy > 0f) ? Mathf.Sqrt(dx * dx + dy * dy) - radius
                                                : Mathf.Max(dx, dy) - radius;
                // One pixel of coverage falloff gives a clean edge at any scale.
                float a = Mathf.Clamp01(0.5f - d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);

        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                                   100f, 0, SpriteMeshType.FullRect,
                                   new Vector4(radius, radius, radius, radius));
        sprite.hideFlags = HideFlags.HideAndDontSave;
        roundedCache[radius] = sprite;
        return sprite;
    }
}
