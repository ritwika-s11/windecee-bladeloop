using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// BladeLoop home page — the order picker, over a live wind farm.
///
/// Built entirely in C# at runtime, like PlantExplorerController: the saved scene
/// holds only the camera, a light, the wind farm and one empty GameObject. Scene
/// files cannot be merged, so keeping the scene tiny is a real safety property.
///
/// DESIGN INTENT, so nobody "tidies" it back into a card grid:
///  - The 3D is the page, not a backdrop. An app about a plant should show the
///    plant. The UI floats over it with a scrim for legibility.
///  - Each tile shows THE FOUR SETTINGS. The presets exist to teach someone what
///    to type into Custom Order, so the settings are the content - not the tonnage.
///  - Each tile shows a live stacked output bar built from OutputSplit(). Low
///    grade's char block is four times high grade's, and you can see that in half
///    a second without reading a number. That is the product's whole argument.
///
/// Every string and number comes from OrderContext. Nothing is typed twice.
///
/// Owner: Ritwika.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    // Dusk palette for the hero. The accent hues are Plant Explorer's, lifted for
    // legibility on dark, so the app still reads as one product.
    static Color Ink, InkSoft, InkFaint, TileBg, TileEdge, Sky;
    static Color Blue, Amber, Grey, Green, Brown, Loss;
    static bool paletteReady;
    static void InitPalette()
    {
        if (paletteReady) return;
        Ink      = Hex("F1F5FB"); InkSoft = Hex("9FB0C7"); InkFaint = Hex("6B7C96");
        TileBg   = Hex("1B2436"); TileEdge = Hex("33415C"); Sky = Hex("1D2942");
        Blue     = Hex("4D8DF0"); Amber = Hex("E0952E"); Grey = Hex("7C8CA3");
        Green    = Hex("57A96B"); Brown = Hex("9A6B4A"); Loss = Hex("5A6479");
        paletteReady = true;
    }
    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

    // ---- typography ----------------------------------------------------------
    // IBM Plex, SIL Open Font License. Assets/Fonts/OFL.txt must travel with them -
    // the licence requires the copyright notice ships with any distribution, and the
    // deliverable is now a Windows executable, which counts.
    //
    // Mono is not decoration. The four settings on each tile are the numbers someone
    // copies into Custom Order, and tabular figures keep the columns aligned between
    // tiles so 6,500 and 8,800 sit under each other. Proportional digits wander.
    static TMP_FontAsset Sans, SansBold, Mono, MonoBold;
    static bool fontsReady;
    static void InitFonts()
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
        // The assets live in Assets/Resources/Fonts/ SPECIFICALLY so Resources.Load
        // resolves them in a player build. Move them out of Resources and the fonts
        // silently vanish from the Windows executable while still working in the
        // editor - which is the worst kind of bug to find on submission day.
        var f = Resources.Load<TMP_FontAsset>("Fonts/" + name);
        if (f == null) Debug.LogWarning($"MainMenuController: font '{name}' not found, falling back to the TMP default.");
        return f;
    }

    /// <summary>High = blue, mid = amber, low = grey. Never red: low grade is a
    /// different customer, not a failure, and the colour must not say otherwise.</summary>
    static Color GradeColor(Grade g) => g == Grade.High ? Blue : (g == Grade.Mid ? Amber : Grey);

    void Start()
    {
        InitPalette();
        InitFonts();

        // Arriving here always ends any run. Without this a static order survives and
        // the next free-play tour comes back split for no visible reason.
        OrderContext.Clear();

        var cam = Camera.main;
        if (cam != null)
        {
            cam.rect = new Rect(0f, 0f, 1f, 1f);   // undo the tour's viewport split
            if (cam.GetComponent<HomeStageDrift>() == null) cam.gameObject.AddComponent<HomeStageDrift>();
        }

        DimHomeTerrain();
        BuildUI();
    }

    /// <summary>Darkens the home page's terrain to dusk, at runtime, for this scene only.
    ///
    /// The terrain is lit for Stage 1's daytime story and reads as bright grass against
    /// a night sky. An earlier version fixed that by editing the material asset itself -
    /// which was wrong twice over: WF_Mat_Terrain is SHARED with Stage 1 through the
    /// wind farm FBX, so it darkened Anirban's scene too; and multiplying a Color scales
    /// alpha along with rgb, so the material also went 42% transparent.
    ///
    /// A MaterialPropertyBlock touches only these renderers, creates no material
    /// instances to leak, and cannot be saved into the asset by accident.</summary>
    void DimHomeTerrain()
    {
        var stage = GameObject.Find("HomeStage");
        if (stage == null) return;

        var block = new MaterialPropertyBlock();
        foreach (Transform child in stage.transform)
        {
            if (!child.name.StartsWith("WF_Terrain")) continue;
            foreach (var rend in child.GetComponentsInChildren<Renderer>())
            {
                var src = rend.sharedMaterial;
                if (src == null) continue;
                string prop = src.HasProperty("_BaseColor") ? "_BaseColor"
                            : (src.HasProperty("_Color") ? "_Color" : null);
                if (prop == null) continue;

                Color c = src.GetColor(prop);
                rend.GetPropertyBlock(block);
                // rgb only - alpha stays exactly as authored
                block.SetColor(prop, new Color(c.r * 0.42f, c.g * 0.42f, c.b * 0.42f, c.a));
                rend.SetPropertyBlock(block);
            }
        }
    }

    // =====================================================================  UI  ==

    void BuildUI()
    {
        var canvasGO = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;   // matches the other 13 canvases in the project

        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem),
                           typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

        var root = (RectTransform)canvasGO.transform;

        // Scrim: the turbines are legible through it, the text is legible over it.
        // Two bands rather than a gradient - URP-safe and cheaper.
        var top = MakeImage(root, "scrimTop", new Color(Sky.r, Sky.g, Sky.b, 0.55f)).rectTransform;
        Anchor(top, 0, 0.52f, 1, 1);
        var bottom = MakeImage(root, "scrimBottom", new Color(0.043f, 0.055f, 0.09f, 0.86f)).rectTransform;
        Anchor(bottom, 0, 0, 1, 0.52f);

        BuildHeader(root);
        BuildTiles(root);
        BuildFooter(root);
    }

    void BuildHeader(RectTransform root)
    {
        var mark = MakeText(root, "wordmark", "BLADELOOP", 30, Ink, TextAlignmentOptions.Left, SansBold);
        mark.characterSpacing = 12f;
        Anchor(mark.rectTransform, 0.055f, 0.885f, 0.5f, 0.955f);

        var context = MakeText(root, "context", "2 MW class  ·  206 turbines  ·  end of service life",
                               17, InkFaint, TextAlignmentOptions.Right, Mono);
        Anchor(context.rectTransform, 0.5f, 0.885f, 0.945f, 0.955f);

        var head = MakeText(root, "headline", "Every blade has a buyer.", 62, Ink, TextAlignmentOptions.Left, SansBold);
        Anchor(head.rectTransform, 0.055f, 0.715f, 0.72f, 0.835f);

        var sub = MakeText(root, "sub", "Pick an order. Watch the plant run it. See what came out.",
                           23, InkSoft, TextAlignmentOptions.Left, Sans);
        Anchor(sub.rectTransform, 0.055f, 0.655f, 0.72f, 0.715f);
    }

    void BuildTiles(RectTransform root)
    {
        const float top = 0.545f, bottom = 0.115f;
        const float left = 0.055f, right = 0.945f, gap = 0.018f;
        float w = ((right - left) - gap * 2f) / 3f;

        for (int i = 0; i < OrderContext.Presets.Length; i++)
        {
            float x0 = left + i * (w + gap);
            BuildTile(root, i, x0, bottom, x0 + w, top);
        }
    }

    void BuildTile(RectTransform root, int index, float x0, float y0, float x1, float y1)
    {
        var p     = OrderContext.Presets[index];
        var order = p.order;
        var m     = p.model;
        var grade = order.targetGrade;
        Color tint = GradeColor(grade);

        var tile = MakeImage(root, "tile_" + grade, TileBg).rectTransform;
        Anchor(tile, x0, y0, x1, y1);

        var cap = MakeImage(tile, "cap", tint).rectTransform;
        Anchor(cap, 0, 0.978f, 1, 1);

        var gradeLbl = MakeText(tile, "grade", OrderContext.GradeLabel(grade), 18, tint, TextAlignmentOptions.Left, SansBold);
        gradeLbl.characterSpacing = 5f;
        Anchor(gradeLbl.rectTransform, 0.07f, 0.875f, 0.6f, 0.955f);

        var qty = MakeText(tile, "qty", $"{order.targetTonnes:N0} t", 18, InkFaint, TextAlignmentOptions.Right, Mono);
        Anchor(qty.rectTransform, 0.5f, 0.875f, 0.93f, 0.955f);

        var buyer = MakeText(tile, "buyer", order.customerType, 24, Ink, TextAlignmentOptions.Left, SansBold);
        Anchor(buyer.rectTransform, 0.07f, 0.775f, 0.93f, 0.875f);

        // THE TEACHING CONTENT. These four numbers are what someone copies into
        // Custom Order. Read straight off the preset's ProcessModel.
        var settings = MakeText(tile, "settings",
            $"{m.TempC:0} °C   ·   {m.RetentionMin:0} min\n{m.FeedKgH:N0} kg/h   ·   {m.ParticleSizeMm:0.#} mm",
            22, tint, TextAlignmentOptions.Left, MonoBold);
        settings.lineSpacing = 14f;
        Anchor(settings.rectTransform, 0.07f, 0.585f, 0.93f, 0.765f);

        var use = MakeText(tile, "use", p.endUse, 16, InkSoft, TextAlignmentOptions.TopLeft, Sans);
        use.enableWordWrapping = true;
        Anchor(use.rectTransform, 0.07f, 0.40f, 0.93f, 0.565f);

        BuildOutputBar(tile, m);

        var split = m.OutputSplit();
        var readout = MakeText(tile, "readout",
            $"{split.GlassPct:0}% fibre  ·  {m.FiberPurityPct:0.0}% purity", 16, InkFaint, TextAlignmentOptions.Left, Mono);
        Anchor(readout.rectTransform, 0.07f, 0.225f, 0.93f, 0.285f);

        int captured = index;
        var btn = MakeButton(tile, "run", "RUN THIS ORDER",
                             grade == Grade.High ? tint : Color.clear,
                             grade == Grade.High ? Hex("0A1220") : Ink,
                             () => RunPreset(captured));
        Anchor(btn, 0.07f, 0.065f, 0.93f, 0.185f);
    }

    /// <summary>Stacked mass-balance bar: fibre, oil, syngas, char, losses. Same five
    /// streams and same colour language as Plant Explorer. This is the tile's most
    /// useful element - the char block visibly quadruples from high grade to low.</summary>
    void BuildOutputBar(RectTransform tile, ProcessModel m)
    {
        var split = m.OutputSplit();
        var track = MakeImage(tile, "bar", Hex("101828")).rectTransform;
        Anchor(track, 0.07f, 0.305f, 0.93f, 0.365f);

        float[] pct = { split.GlassPct, split.OilPct, split.SyngasPct, split.CharPct, split.LossPct };
        Color[] col = { Blue, Amber, Green, Brown, Loss };
        string[] nm = { "fibre", "oil", "syngas", "char", "loss" };

        float total = 0f; foreach (var v in pct) total += v;
        if (total <= 0.01f) return;

        float cursor = 0f;
        for (int i = 0; i < pct.Length; i++)
        {
            float frac = pct[i] / total;
            var seg = MakeImage(track, "seg_" + nm[i], col[i]).rectTransform;
            seg.anchorMin = new Vector2(cursor, 0f);
            seg.anchorMax = new Vector2(cursor + frac, 1f);
            seg.offsetMin = new Vector2(i == 0 ? 0f : 1f, 0f);   // hairline between segments
            seg.offsetMax = Vector2.zero;
            cursor += frac;
        }
    }

    void BuildFooter(RectTransform root)
    {
        // One row only. An earlier version put Plant Explorer on a second row and it
        // collided with the Low Grade tile's Run button - the tiles end at y 0.115.
        var note = MakeText(root, "note", "Or set your own target — the solver finds settings like these.",
                            18, InkFaint, TextAlignmentOptions.Left, Sans);
        Anchor(note.rectTransform, 0.055f, 0.030f, 0.44f, 0.090f);

        var explorer = MakeButton(root, "explorer", "PLANT EXPLORER", Color.clear, InkSoft,
                                  () => SceneManager.LoadScene("PlantExplorer"));
        Anchor(explorer, 0.455f, 0.030f, 0.615f, 0.090f);

        // Sharan's screens. Disabled rather than dead: loading a scene that is not in
        // Build Settings throws.
        var custom = MakeButton(root, "custom", "CUSTOM ORDER", Color.clear, InkFaint, null, enabled: false);
        Anchor(custom, 0.625f, 0.030f, 0.785f, 0.090f);

        var how = MakeButton(root, "how", "HOW IT WORKS", Color.clear, InkFaint, null, enabled: false);
        Anchor(how, 0.795f, 0.030f, 0.945f, 0.090f);
    }

    // ==================================================================  actions ==

    void RunPreset(int index)
    {
        OrderContext.ApplyPreset(index);
        TourRunner.StartRun();
    }

    // ==================================================================  helpers ==

    static void Anchor(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Image MakeImage(Transform parent, string name, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = col;
        img.raycastTarget = false;
        return img;
    }

    static TMP_Text MakeText(Transform parent, string name, string text, float size, Color col,
                             TextAlignmentOptions align, TMP_FontAsset font = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = col; t.alignment = align;
        if (font != null) t.font = font;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        return t;
    }

    RectTransform MakeButton(Transform parent, string name, string label, Color bg, Color fg,
                             UnityEngine.Events.UnityAction onClick, bool enabled = true)
    {
        var img = MakeImage(parent, name, bg == Color.clear ? new Color(1f, 1f, 1f, 0.04f) : bg);
        img.raycastTarget = true;
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable = enabled;
        if (onClick != null) btn.onClick.AddListener(onClick);

        if (bg == Color.clear)
        {
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor = enabled ? TileEdge : new Color(TileEdge.r, TileEdge.g, TileEdge.b, 0.5f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
        }

        var t = MakeText(img.transform, "label", label, 19, enabled ? fg : InkFaint, TextAlignmentOptions.Center, SansBold);
        t.characterSpacing = 4f;
        Anchor(t.rectTransform, 0, 0, 1, 1);

        return img.rectTransform;
    }
}
