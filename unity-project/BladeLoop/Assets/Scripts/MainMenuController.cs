using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// BladeLoop home page — an order ledger over a live wind farm.
///
/// Built entirely in C# at runtime, like PlantExplorerController. The saved scene
/// holds only the camera, a light, the wind farm and one empty GameObject. Unity
/// scene files cannot be merged, so a tiny scene is a real safety property.
///
/// WHY IT LOOKS LIKE THIS. An earlier version was three rounded cards in slate and
/// blue, and it read as a generated SaaS pricing page rather than an engineering
/// tool. The differences here are deliberate, so please do not "tidy" them back:
///
///  - NO CARDS. Hairline rules and numbered rows. A card grid is the single
///    strongest template signal, and this is less markup, not more.
///  - NO BLUE. Bone, oxide and warm charcoal, taken from rust, concrete and dusk
///    instead of from a CSS framework's default palette.
///  - MATERIAL-TRUE STREAM COLOURS. Recovered fibre really is off-white and char
///    really is near-black, so the low-grade row visibly darkens where the char
///    block grows. More honest than blue-for-fibre, and it carries the argument.
///  - SQUARE CORNERS. Rounded reads consumer app; square reads instrument.
///  - NO MARKETING HEADLINE. The thesis sits quietly in the footer.
///  - THE STATUS LINE IS TRUE. Everything in it is computed. An earlier draft said
///    "FEED HOPPER EMPTY", which was invented - there is no hopper in the model.
///    Decoration dressed as data is worse than no chrome at all.
///
/// Every string and number comes from OrderContext. Nothing is typed twice.
///
/// Owner: Ritwika.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    // ---- palette -------------------------------------------------------------
    static Color Bone, Muted, Faint, Oxide, Rule, RuleSoft, Panel, SkyWarm;
    static Color StreamFibre, StreamOil, StreamGas, StreamChar, StreamLoss;
    static bool paletteReady;
    static void InitPalette()
    {
        if (paletteReady) return;
        Bone  = Hex("EDE8DF"); Muted = Hex("8A8177"); Faint = Hex("6E665C");
        Oxide = Hex("C2603A"); Rule  = Hex("2A2520"); RuleSoft = Hex("221E1A");
        Panel = Hex("12100D"); SkyWarm = Hex("1A1713");
        // The five streams, coloured as the materials actually are.
        StreamFibre = Hex("E4DCCD");   // reclaimed glass fibre, off-white
        StreamOil   = Hex("C99A3E");   // pyrolysis oil, amber
        StreamGas   = Hex("6B8F62");   // syngas
        StreamChar  = Hex("2E2823");   // carbon char, near black
        StreamLoss  = Hex("5A524A");   // fugitive dust and residue
        paletteReady = true;
    }
    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

    // ---- typography ----------------------------------------------------------
    // IBM Plex, SIL Open Font License. Assets/Fonts/OFL.txt must ship with any
    // distribution - the Windows executable counts.
    //
    // The assets live in Assets/Resources/Fonts/ SPECIFICALLY so Resources.Load
    // resolves them in a player build. Move them somewhere tidier and the fonts
    // vanish from the .exe while still working in the editor.
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
        var f = Resources.Load<TMP_FontAsset>("Fonts/" + name);
        if (f == null) Debug.LogWarning($"MainMenuController: font '{name}' missing, using the TMP default.");
        return f;
    }

    void Start()
    {
        InitPalette();
        InitFonts();

        // Arriving here always ends any run - and Clear() records what just ran, so
        // the status line can report it below.
        OrderContext.Clear();

        SetupStageLook();
        DimHomeTerrain();
        BuildUI();
    }

    /// <summary>Camera framing, sky colour, fog and key light for the home page.
    ///
    /// Deliberately in code rather than serialised into the scene. Setting these in the
    /// editor without marking anything dirty means a later scene save quietly writes the
    /// old values back - which happened, and cost half an hour of "why is the sky still
    /// navy". In code they are reproducible, versioned, and visible in a diff.
    ///
    /// The palette here must stay warm. An earlier version left the sky navy while the
    /// ledger was warm charcoal, and the join across the middle of the screen read as two
    /// different designs meeting.</summary>
    void SetupStageLook()
    {
        var cam = Camera.main;
        if (cam == null) return;

        cam.rect = new Rect(0f, 0f, 1f, 1f);   // undo the tour's viewport split

        // THE SHOT. Only the top ~34% of the screen is sky - the ledger covers the rest -
        // so this frames ONE turbine's rotor in that band with its tower running down
        // behind the ledger. A whole farm cannot fit in a 16-degree slice; trying to show
        // one made every turbine a distant stick.
        //
        // WF_Turbine_01 is the only full-size one (60 m, centred -13.5/30/-12.8). The other
        // ten are ~20 m background pieces.
        //
        // Yaw pushes it right of centre, away from the wordmark, the statement and the
        // call to action, which all live in the left half. Measured, not judged by eye:
        // projecting the turbine's eight bounds corners at this pose puts it across
        // viewport x 0.575 to 0.923, centre 0.739. Going further looked tempting but
        // -78 deg puts its right edge at 0.989 and the rotor kisses the screen edge.
        //
        // Yaw only moves it horizontally, so the tower still meets the ledger seam.
        // If you change this, re-check the right edge stays clear of 1.0.
        cam.transform.position = new Vector3(44f, 11f, -58f);
        cam.transform.rotation = Quaternion.Euler(-5.5f, -74f, 0f);
        cam.fieldOfView = 46f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 500f;

        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = SkyWarm;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Hex("2A2620");
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = SkyWarm;          // fog must match the sky or the far
        RenderSettings.fogStartDistance = 45f;      // turbines cut out against a hard edge
        RenderSettings.fogEndDistance = 190f;       // and the terrain edge stops being visible

        var lightGO = GameObject.Find("Directional Light");
        if (lightGO != null)
        {
            var key = lightGO.GetComponent<Light>();
            key.color = Hex("FFC98F");              // low warm sun
            key.intensity = 1.05f;
            key.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(11f, 205f, 0f);
        }

        // The drift must be told to re-read the pose AFTER we have moved the camera.
        // It may already exist on the camera from the saved scene, and script execution
        // order between two components is undefined - if its Start ran first it captured
        // the old pose and would drag the camera back there every frame.
        var drift = cam.GetComponent<HomeStageDrift>();
        if (drift == null) drift = cam.gameObject.AddComponent<HomeStageDrift>();
        drift.CaptureBase();
    }

    /// <summary>Darkens the home page's terrain to dusk, at runtime, for this scene only.
    ///
    /// The terrain is lit for Stage 1's daytime story and reads as bright grass against
    /// a night sky. An earlier version fixed that by editing the material asset, which
    /// was wrong twice: WF_Mat_Terrain is SHARED with Stage 1 through the wind farm FBX,
    /// so it darkened Anirban's scene too; and multiplying a Color scales alpha with rgb,
    /// so it also went 42% transparent. A property block touches only these renderers and
    /// cannot leak into the asset.</summary>
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
                block.SetColor(prop, new Color(c.r * 0.38f, c.g * 0.38f, c.b * 0.38f, c.a));  // rgb only
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
        scaler.matchWidthOrHeight = 0.5f;

        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem),
                           typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

        var root = (RectTransform)canvasGO.transform;

        // Two bands: the turbines stay visible up top, the ledger gets a solid ground.
        // Ground is FULLY OPAQUE. At 0.94 the turbines ghosted through the ledger and
        // read as a rendering fault rather than atmosphere.
        var haze = MakeImage(root, "haze", new Color(Panel.r, Panel.g, Panel.b, 0.30f)).rectTransform;
        Anchor(haze, 0, 0.655f, 1, 1);
        // Seam moved down with the hero: the call to action and its three entry
        // modes belong to the sky half, the examples to the ledger half.
        var ground = MakeImage(root, "ground", Panel).rectTransform;
        Anchor(ground, 0, 0, 1, 0.600f);
        // A single hairline where the sky meets the ledger, so the join is deliberate.
        var seam = MakeImage(root, "seam", Rule).rectTransform;
        Anchor(seam, 0, 0.5995f, 1, 0.6015f);

        BuildMasthead(root);
        BuildPlanCTA(root);      // the tool, above the seam, in the hero
        BuildLedger(root);       // the examples, below it
        BuildFooter(root);
    }

    /// <summary>The primary action, promoted out of the footer into the hero.
    ///
    /// It sat in a row of equal-width footer links, which said "here are some other
    /// places you could go" - the opposite of the truth. Planning a run IS the
    /// product; the three rows below are worked examples of it. So it moves above
    /// the seam, next to the statement, at a size nothing else on the page competes
    /// with, and the ledger becomes what it always was: evidence.</summary>
    void BuildPlanCTA(RectTransform root)
    {
        // Dropped clear of the statement rather than tucked under it - the heading
        // is the page's sentence, and the button is what you do about it, so they
        // should not read as one block.
        var hit = MakeImage(root, "planCta", Oxide).rectTransform;
        Anchor(hit, 0.055f, 0.652f, 0.262f, 0.730f);
        hit.GetComponent<Image>().raycastTarget = true;

        var btn = hit.gameObject.AddComponent<Button>();
        btn.targetGraphic = hit.GetComponent<Image>();
        var c = btn.colors;
        c.normalColor      = Color.white;
        c.highlightedColor = new Color(1.14f, 1.09f, 1.05f, 1f);
        c.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f);
        c.fadeDuration     = 0.08f;
        btn.colors = c;
        btn.onClick.AddListener(() => SceneManager.LoadScene("OrderDashboard"));

        var lbl = MakeText(hit.transform, "planLbl", "PLAN A RUN   →", 22, Hex("15110E"),
                           TextAlignmentOptions.Center, MonoBold);
        lbl.characterSpacing = 4f;
        Anchor(lbl.rectTransform, 0f, 0f, 1f, 1f);

        // The three ways a planner arrives at this screen, in their own words. This
        // is a promise about Akshat's Custom Order screen - if those modes are not
        // in the build by submission, cut this line rather than ship a page that
        // offers something the next screen does not.
        var modes = MakeText(root, "planModes",
            "I know my order   ·   I have blades in the yard   ·   I'm limited to one particle size",
            17, Muted, TextAlignmentOptions.Left, Sans);
        Anchor(modes.rectTransform, 0.055f, 0.612f, 0.60f, 0.647f);
    }

    void BuildMasthead(RectTransform root)
    {
        var mark = MakeText(root, "wordmark", "BLADELOOP", 26, Bone, TextAlignmentOptions.Left, Mono);
        mark.characterSpacing = 16f;
        Anchor(mark.rectTransform, 0.055f, 0.900f, 0.5f, 0.955f);

        // One short oxide rule. The only piece of pure decoration on the page.
        var tick = MakeImage(root, "tick", Oxide).rectTransform;
        Anchor(tick, 0.055f, 0.888f, 0.088f, 0.892f);

        // The feedstock read-out that used to sit top right is gone. It quoted one
        // preset's figures as though they described the app, and a planner arriving
        // here has not chosen an order yet - so the numbers answered a question
        // nobody had asked. The same figures appear, correctly and for the order
        // actually loaded, on the first panel of every run.

        // Sits just under the oxide rule. An earlier version left a tenth of the screen
        // empty between the wordmark and this line, which read as a layout mistake.
        var statement = MakeText(root, "statement",
            "Recovered glass fibre, sorted by what it can become.", 34, Bone, TextAlignmentOptions.Left, Sans);
        Anchor(statement.rectTransform, 0.055f, 0.795f, 0.78f, 0.878f);
    }

    /// <summary>Right edge of everything in the ledger half - rows, rules, the footer
    /// button - matching the page margin on the left.
    ///
    /// This was briefly pulled in to 0.72 to close the gap between a buyer's name and
    /// its RUN button. That was the wrong fix: the gap existed because the settings
    /// and output columns had been removed from the middle, and shrinking the table
    /// to hide a hole just left the whole page hanging off the left edge. The middle
    /// is filled again instead - see BuildRow.</summary>
    const float LedgerRight = 0.945f;

    void BuildLedger(RectTransform root)
    {
        BuildColumnHeads(root);

        // Pulled down to make room for the worked-examples label and the taller
        // hero above the seam. Rows lay out in their own 0..1 space, so their
        // contents rescale with them and nothing inside needs touching.
        const float top = 0.512f, bottom = 0.135f;
        float rowH = (top - bottom) / OrderContext.Presets.Length;

        for (int i = 0; i < OrderContext.Presets.Length; i++)
        {
            float y1 = top - i * rowH;
            BuildRow(root, i, y1 - rowH, y1, drawRuleBelow: i < OrderContext.Presets.Length - 1);
        }
    }

    void BuildColumnHeads(RectTransform root)
    {
        // Names what the three rows are. Below the seam now, so it reads as the
        // heading of the ledger rather than as a second hero line.
        var lead = MakeText(root, "examplesLead",
                            "WORKED EXAMPLES — WATCH A RUN END TO END", 13, Faint,
                            TextAlignmentOptions.Left, Mono);
        lead.characterSpacing = 6f;
        Anchor(lead.rectTransform, 0.055f, 0.545f, 0.70f, 0.577f);

        var head = MakeImage(root, "headRule", Rule).rectTransform;
        Anchor(head, 0.055f, 0.5225f, LedgerRight, 0.525f);

        // BUYER / PLANT SETTINGS / OUTPUT are gone with the columns they labelled.
        // One remaining column does not need a header - the rows say what they are.
    }

    void BuildRow(RectTransform root, int index, float y0, float y1, bool drawRuleBelow)
    {
        var p     = OrderContext.Presets[index];
        var order = p.order;
        var m     = p.model;
        var grade = order.targetGrade;

        // Run length for this example. Computed locally from the preset rather than
        // read from OrderContext.CampaignDays, because that property needs an order
        // to be ACTIVE and this is only drawing a card - applying a preset here to
        // read one number would clobber whatever the user is doing.
        // Same arithmetic: order tonnage divided by the fibre recovered per hour.
        var split      = m.OutputSplit();
        float fibreKgH = split.GlassKgH;
        float days     = fibreKgH > 0.01f ? order.targetTonnes * 1000f / fibreKgH / 24f : 0f;

        // The whole row is the button. No pill, no fill - the hover tint is the affordance.
        //
        // Stops at LedgerRight, not the page margin. With the settings and output
        // columns gone the row had a thousand empty pixels between the buyer's name
        // and its RUN button, which read as a missing column rather than as space.
        var row = MakeImage(root, "row_" + grade, new Color(1f, 1f, 1f, 0f)).rectTransform;
        Anchor(row, 0.05f, y0, LedgerRight, y1);
        row.GetComponent<Image>().raycastTarget = true;
        var btn = row.gameObject.AddComponent<Button>();
        btn.targetGraphic = row.GetComponent<Image>();
        var colors = btn.colors;
        colors.normalColor      = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.045f);
        colors.pressedColor     = new Color(Oxide.r, Oxide.g, Oxide.b, 0.14f);
        colors.selectedColor    = new Color(1f, 1f, 1f, 0.045f);
        btn.colors = colors;
        int captured = index;
        btn.onClick.AddListener(() => RunPreset(captured));

        if (drawRuleBelow)
        {
            var r = MakeImage(root, "rule_" + grade, RuleSoft).rectTransform;
            Anchor(r, 0.055f, y0, LedgerRight, y0 + 0.0018f);
        }

        // 01 / 02 / 03 - the first row is the recommended one, so it carries the accent.
        var num = MakeText(row, "num", $"{index + 1:00}", 22, index == 0 ? Oxide : Faint,
                           TextAlignmentOptions.Left, Mono);
        AnchorIn(row, num.rectTransform, 0.005f, 0.52f, 0.05f, 0.78f);

        // Size AND lead time, because that pair is how a planner identifies a job.
        // Rounded to whole days here - the exact figure is on the run's own panel.
        var tag = MakeText(row, "tag",
                           $"{OrderContext.GradeLabel(grade)}  ·  {order.targetTonnes:N0} t  ·  {days:0} DAYS",
                           13, Muted, TextAlignmentOptions.Left, Mono);
        tag.characterSpacing = 5f;
        AnchorIn(row, tag.rectTransform, 0.05f, 0.70f, 0.42f, 0.88f);

        var buyer = MakeText(row, "buyer", order.customerType, 25, Bone, TextAlignmentOptions.Left, SansBold);
        AnchorIn(row, buyer.rectTransform, 0.05f, 0.42f, 0.44f, 0.70f);

        var use = MakeText(row, "use", p.endUse, 16, Faint, TextAlignmentOptions.TopLeft, Sans);
        use.enableWordWrapping = true;
        AnchorIn(row, use.rectTransform, 0.05f, 0.12f, 0.44f, 0.42f);

        // The four plant settings and the purity/strength readout stay gone - twelve
        // numbers a row, thirty-six on screen, before the reader had decided anything.
        //
        // The BAR comes back, though, and it is the reason the row can span the full
        // page again. It carries no text at all: five coloured blocks whose widths
        // are the mass balance. High grade's char block is a sliver, low grade's is a
        // quarter of the bar, and you read that difference without reading a number.
        // Content in the middle is what a wide row needs; numbers are not the only
        // kind of content.
        BuildOutputBar(row, m);

        // An explicit target. The whole row is clickable, but a bare arrow did not say
        // so - people did not know where to press.
        bool primary = index == 0;
        var runBg = MakeImage(row, "runBtn", primary ? Oxide : new Color(1f, 1f, 1f, 0.05f)).rectTransform;
        AnchorIn(row, runBg, 0.862f, 0.40f, 0.995f, 0.66f);
        if (!primary)
        {
            var edge = runBg.gameObject.AddComponent<Outline>();
            edge.effectColor = Hex("4A4238");
            edge.effectDistance = new Vector2(1.2f, -1.2f);
        }
        var runLbl = MakeText(runBg, "runLabel", "RUN  →", 17,
                              primary ? Hex("15110E") : Bone, TextAlignmentOptions.Center, MonoBold);
        runLbl.characterSpacing = 3f;
        Anchor(runLbl.rectTransform, 0, 0, 1, 1);
    }

    /// <summary>Stacked mass-balance bar: fibre, oil, syngas, char, losses, straight from
    /// OutputSplit(). The most useful element on the page - high grade's char block is
    /// 5.9%, low grade's is 26.5%, and you can see that without reading a number.</summary>
    void BuildOutputBar(RectTransform row, ProcessModel m)
    {
        // Wider and thicker than before, and vertically centred in the row. It is no
        // longer one element among four in a crowded right-hand block - it is the
        // only thing between the buyer and the RUN button, so it has to hold that
        // span on its own.
        var split = m.OutputSplit();
        var track = MakeImage(row, "bar", Hex("1A1713")).rectTransform;
        AnchorIn(row, track, 0.475f, 0.40f, 0.800f, 0.545f);

        float[] pct = { split.GlassPct, split.OilPct, split.SyngasPct, split.CharPct, split.LossPct };
        Color[] col = { StreamFibre, StreamOil, StreamGas, StreamChar, StreamLoss };
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
            seg.offsetMin = new Vector2(i == 0 ? 0f : 1.5f, 0f);   // hairline gap between streams
            seg.offsetMax = Vector2.zero;
            cursor += frac;
        }
    }

    void BuildFooter(RectTransform root)
    {
        var rule = MakeImage(root, "footRule", Rule).rectTransform;
        Anchor(rule, 0.055f, 0.1225f, LedgerRight, 0.125f);

        // The thesis line is gone from the footer. It was a claim set in small caps
        // at the bottom of a page that now makes the same point structurally - one
        // farm, three buyers, three outcomes, sitting in the rows above it. Stated
        // AND demonstrated was one too many. It still opens How It Works, which is
        // where someone has actually asked to be told what we think.

        // Only How It Works remains down here. Plan a run was promoted into the hero
        // (BuildPlanCTA) and Plant Explorer no longer has a home-page entry - its
        // scene is untouched and still loadable by name, it is simply not offered.
        // Background reading belongs in the footer; the tool does not.
        //
        // How It Works was parked as non-interactive until its scene existed -
        // loading a scene that is not in Build Settings throws - and that scene
        // landed with Sharan's PR #55, so the link is switched on.
        // Flush with the ledger's right edge, so it lines up with the RUN buttons
        // stacked above it rather than floating off on its own margin.
        const float wSecondary = 0.118f;
        MakeLink(root, "how", "HOW IT WORKS", LedgerRight - wSecondary, 0.072f, LedgerRight, 0.122f,
                 () => SceneManager.LoadScene("HowItWorks"), true);
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
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    /// <summary>Anchor in the PARENT's fractional space. Rows are their own rect, so the
    /// children inside them use 0..1 of the row rather than of the screen.</summary>
    static void AnchorIn(RectTransform parent, RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        => Anchor(rt, xMin, yMin, xMax, yMax);

    static Image MakeImage(Transform parent, string name, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = col; img.raycastTarget = false;
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
        t.enableWordWrapping = false; t.raycastTarget = false;
        return t;
    }

    /// <summary>Footer buttons.
    ///
    /// These were underlined text and nobody read them as clickable. They are boxes now,
    /// matching the secondary RUN buttons in the rows so the whole page has ONE button
    /// language. Still square and flat - the point was never "no affordance", it was
    /// "no card chrome".</summary>
    void MakeLink(RectTransform root, string name, string label, float x0, float y0, float x1, float y1,
                  UnityEngine.Events.UnityAction onClick, bool enabled, bool accent = false)
    {
        // Accent used to mean "same faint box, oxide outline", which read as barely
        // different from the secondary one beside it. The primary action of the page
        // should look like the primary action of a row, so it now borrows the same
        // solid-oxide treatment the 01 RUN button uses.
        var fill = accent ? Oxide
                          : (enabled ? new Color(1f, 1f, 1f, 0.05f) : new Color(1f, 1f, 1f, 0.02f));
        var hit = MakeImage(root, name, fill).rectTransform;
        Anchor(hit, x0, y0, x1, y1);
        hit.GetComponent<Image>().raycastTarget = true;

        var btn = hit.gameObject.AddComponent<Button>();
        btn.targetGraphic = hit.GetComponent<Image>();
        btn.interactable = enabled;
        if (onClick != null) btn.onClick.AddListener(onClick);
        if (enabled)
        {
            var colors = btn.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 2.4f);   // multiplied against fill
            colors.pressedColor     = new Color(1f, 0.75f, 0.6f, 3f);
            btn.colors = colors;
        }

        // No outline on the accent button - a filled block does not need an edge,
        // and drawing one over oxide just muddies it.
        if (!accent)
        {
            var edge = hit.gameObject.AddComponent<Outline>();
            edge.effectColor = enabled ? Hex("4A4238") : RuleSoft;
            edge.effectDistance = new Vector2(1.2f, -1.2f);
        }

        var t = MakeText(hit.transform, "label", label, 15,
                         accent ? Hex("15110E") : (enabled ? Bone : Faint),
                         TextAlignmentOptions.Center, MonoBold);
        t.characterSpacing = 3f;
        Anchor(t.rectTransform, 0, 0, 1, 1);
    }
}
