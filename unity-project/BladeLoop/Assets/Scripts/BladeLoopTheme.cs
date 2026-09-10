using UnityEngine;
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

        // Revamped 8 Sep. THIS IS NOW THE SINGLE SOURCE - MainMenuController reads
        // from here rather than keeping a private copy, so the home page and Custom
        // Order cannot drift into looking like two different products.
        //
        // Deep, slightly cool base so the warm stream colours read as emitted rather
        // than painted; three separated surface steps; one vivid accent. The previous
        // warm-charcoal scheme put the page, the panels and the near-black char block
        // within a few points of each other, leaving the data no ground to stand on.
        Bone     = Hex("F2F4F7");   // primary text
        Muted    = Hex("9BA4B0");   // secondary
        Faint    = Hex("5F6A77");   // labels, units
        Oxide    = Hex("FF6B35");   // the one accent
        Rule     = Hex("23272E");   // hairline
        RuleSoft = Hex("1A1D22");
        Panel    = Hex("0A0B0D");   // page
        SkyWarm  = Hex("11141A");   // raised surface

        // Material-true, each lifted enough to hold against the deeper base. Char
        // especially: at #2E2823 it was invisible, which lost the most important
        // comparison in the whole application.
        StreamFibre = Hex("EFE9DB");   // reclaimed glass fibre, off-white
        StreamOil   = Hex("E0A63F");   // pyrolysis oil, amber
        StreamGas   = Hex("74B36C");   // syngas
        StreamChar  = Hex("46403A");   // carbon char
        StreamLoss  = Hex("6B7480");   // fugitive dust and residue

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

    // ---- depth ---------------------------------------------------------------

    static Sprite fadeSprite;

    /// <summary>
    /// A vertical alpha ramp: opaque at the top, transparent at the bottom. Tint it
    /// with Image.color and stretch it to whatever height you need.
    ///
    /// WHY THIS AND NOT ROUNDED CORNERS. The home page header states the case
    /// plainly - "rounded reads consumer app; square reads instrument" - and this
    /// application is an engineering tool being shown to engineers. So depth comes
    /// from VALUE rather than from SHAPE: a light hairline along the top edge of a
    /// raised surface, a barely-there wash down its face. That is how a physical
    /// instrument panel reads under a light from above, and it elevates a dark UI
    /// without softening it into something else.
    ///
    /// One 1x64 texture for the whole application.
    /// </summary>
    public static Sprite VerticalFade()
    {
        if (fadeSprite != null) return fadeSprite;

        const int h = 64;
        var tex = new Texture2D(1, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            hideFlags  = HideFlags.HideAndDontSave
        };

        var px = new Color32[h];
        for (int y = 0; y < h; y++)
        {
            // y = 0 is the BOTTOM of a Unity texture, so the ramp runs upward.
            float t = y / (float)(h - 1);
            // Eased rather than linear: a straight ramp reads as a banded gradient,
            // a curved one reads as light falling off.
            float a = t * t;
            px[y] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);

        fadeSprite = Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
        fadeSprite.hideFlags = HideFlags.HideAndDontSave;
        return fadeSprite;
    }

    static Sprite dashSprite;

    /// <summary>
    /// A horizontal dashed rule: two pixels on, two off, tiling forever. Use it on an
    /// Image with type = Tiled and a height of 1, tinted to whatever alpha you want.
    ///
    /// WHY A TEXTURE AND NOT A ROW OF LITTLE IMAGES. A dashed underline under a 13pt
    /// letterspaced label needs its dashes on exact pixel boundaries or they alias into
    /// a smear that reads as dirt on the screen rather than as an affordance. A point-
    /// filtered 4x1 texture tiled by the UI system lands every dash on a whole pixel,
    /// and costs one draw call instead of thirty GameObjects per label.
    ///
    /// THE POINT OF IT: it marks a term as having a definition behind it, using the one
    /// convention every data tool already shares, without borrowing the underline of a
    /// hyperlink - these are not links and must not offer to navigate anywhere.
    /// </summary>
    public static Sprite DashedRule()
    {
        if (dashSprite != null) return dashSprite;

        const int w = 4;
        var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,      // no bilinear smear between dash and gap
            wrapMode   = TextureWrapMode.Repeat,
            hideFlags  = HideFlags.HideAndDontSave
        };
        tex.SetPixels32(new[]
        {
            new Color32(255, 255, 255, 255),
            new Color32(255, 255, 255, 255),
            new Color32(255, 255, 255, 0),
            new Color32(255, 255, 255, 0)
        });
        tex.Apply(false, true);

        dashSprite = Sprite.Create(tex, new Rect(0, 0, w, 1), new Vector2(0.5f, 0.5f), 100f,
                                   0, SpriteMeshType.FullRect, Vector4.zero);
        dashSprite.hideFlags = HideFlags.HideAndDontSave;
        return dashSprite;
    }

}
