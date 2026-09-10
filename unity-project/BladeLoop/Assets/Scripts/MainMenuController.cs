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
/// REVAMPED 8 Sep (Akshat, with Ritwika's sign-off to change the look). Her original
/// rationale is kept below because most of it still holds and the reasoning is good;
/// where this version departs, it says so and why.
///
///  - NO CARDS. Hairline rules and space, never boxes. A card grid is the single
///    strongest template signal. KEPT - the three columns are separated by gaps and
///    rules, not containers.
///  - MATERIAL-TRUE STREAM COLOURS. Recovered fibre really is off-white and char
///    really is near-black. KEPT, and now load-bearing: the three mass-balance bars
///    stand vertically side by side, so char growing from a sliver to a quarter of
///    the column is the product's argument made measurable by eye.
///  - SQUARE CORNERS. Rounded reads consumer app; square reads instrument. KEPT
///    throughout, on both this screen and Custom Order.
///  - NO MARKETING HEADLINE, and EVERY FIGURE COMPUTED. KEPT. Nothing on this page
///    is written where it could be derived.
///
///  - "NO BLUE / warm charcoal" is the one that changed. The warm scheme put the
///    page, the panels and the near-black char block within a few points of each
///    other, so the data had no ground to stand on. The base is now deep and
///    slightly cool, which makes the warm stream colours read as emitted rather
///    than painted, and the accent is brighter because at 20% of the screen the old
///    oxide was doing a highlight's job without a highlight's contrast.
///
///  - THREE COLUMNS, NOT THREE ROWS. The row layout gave each example a thin strip
///    and left about a third of the page empty. See BuildColumns.
///
/// Owner: Ritwika (composition and palette reworked by Akshat, 8 Sep).
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

        // ONE SOURCE. These used to be hard-coded here and copied again into
        // BladeLoopTheme, so the home page and Custom Order could - and did - drift
        // apart. The values live in BladeLoopTheme now; this reads them.
        BladeLoopTheme.InitPalette();
        Bone  = BladeLoopTheme.Bone;   Muted = BladeLoopTheme.Muted;
        Faint = BladeLoopTheme.Faint;  Oxide = BladeLoopTheme.Oxide;
        Rule  = BladeLoopTheme.Rule;   RuleSoft = BladeLoopTheme.RuleSoft;
        Panel = BladeLoopTheme.Panel;  SkyWarm  = BladeLoopTheme.SkyWarm;

        StreamFibre = BladeLoopTheme.StreamFibre;
        StreamOil   = BladeLoopTheme.StreamOil;
        StreamGas   = BladeLoopTheme.StreamGas;
        StreamChar  = BladeLoopTheme.StreamChar;
        StreamLoss  = BladeLoopTheme.StreamLoss;

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

        // The three farm figures count up as the page settles. They are the first
        // thing the eye lands on, so it is the one place motion earns its keep.
        for (int i = 0; i < farmFigures.Count; i++)
            StartCoroutine(CountUp(farmFigures[i], farmTargets[i], 0.7f, 0.15f + i * 0.08f));
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
        Anchor(haze, 0, SeamY, 1, 1);
        // Seam moved down with the hero: the call to action and its three entry
        // modes belong to the sky half, the examples to the ledger half.
        var ground = MakeImage(root, "ground", Panel).rectTransform;
        Anchor(ground, 0, 0, 1, SeamY);

        // The two bands were not reading. Between the haze at 30% and the opaque
        // ground there was a hard step from one flat tone to another, so at 1600x900
        // the "sky half" looked like the same charcoal as the ledger and the
        // composition described above was invisible.
        //
        // A short gradient carries the sky down INTO the ledger instead: opaque at
        // the seam, gone about a fifth of the way up. Value, not shape - the sky now
        // falls off toward the ground the way it does outdoors, and the seam becomes
        // the end of a transition rather than a line between two greys.
        var fall = MakeImage(root, "skyFall", Panel).rectTransform;
        Anchor(fall, 0, SeamY, 1, SeamY + 0.20f);
        var fallImg = fall.GetComponent<Image>();
        fallImg.sprite = BladeLoopTheme.VerticalFade();
        fallImg.type   = Image.Type.Simple;
        // The ramp is opaque at its top, so flip it: dense at the seam, clear above.
        fall.localScale = new Vector3(1f, -1f, 1f);

        // A single hairline where the sky meets the ledger, so the join is deliberate.
        var seam = MakeImage(root, "seam", Rule).rectTransform;
        Anchor(seam, 0, SeamY - 0.0005f, 1, SeamY + 0.0015f);

        // Light from above: one bright hairline along the top of the ledger. This is
        // how a physical panel reads under an overhead source, and it is the whole
        // depth vocabulary for this screen - square corners, no shadows, just value.
        var lip = MakeImage(root, "groundLip", new Color(1f, 1f, 1f, 0.055f)).rectTransform;
        Anchor(lip, 0, SeamY - 0.0015f, 1, SeamY - 0.0005f);

        BuildHeader(root);
        BuildColumns(root);
        BuildFooterLink(root);
    }

    // =========================================================== composition ==
    //
    // THREE COLUMNS, NOT THREE ROWS.
    //
    // The previous arrangement gave each worked example a thin horizontal strip and
    // left roughly a third of the page empty. Turning them into tall columns does
    // three things at once: it fills the frame, it makes the mass-balance bar large
    // enough to be the page's main visual instead of a detail, and - because the
    // bars now run VERTICALLY side by side - it lets you compare the three splits
    // directly. High grade's char block is a sliver and low grade's is a quarter of
    // the column; standing next to each other, that difference is the argument, and
    // you get it before reading a word.
    //
    // Ritwika's six rules are kept exactly: no cards (columns are separated by
    // hairlines and space, not boxes), no blue, material-true stream colours, square
    // corners, no marketing headline, and every figure computed rather than written.

    const float SeamY = 0.718f;

    void BuildHeader(RectTransform root)
    {
        var mark = MakeText(root, "wordmark", "BLADELOOP", 24, Bone,
                            TextAlignmentOptions.Left, Mono);
        mark.characterSpacing = 16f;
        Anchor(mark.rectTransform, 0.055f, 0.918f, 0.45f, 0.968f);
        Anchor(MakeImage(root, "tick", Oxide).rectTransform, 0.055f, 0.904f, 0.086f, 0.908f);

        var st = MakeText(root, "statement",
                          "Recovered glass fibre, sorted by what it can become.",
                          38, Bone, TextAlignmentOptions.TopLeft, Sans);
        st.textWrappingMode = TextWrappingModes.Normal;
        st.lineSpacing = 6f;
        Anchor(st.rectTransform, 0.055f, 0.828f, 0.560f, 0.898f);

        // The action sits to the left, directly under the sentence it answers.
        //
        // REMOVED 9 Sep: three entry-mode chips ("I know my order", "I have blades in
        // the yard", "I'm limited to one size") used to sit in this row. All three did
        // the same thing - load OrderDashboard - so they were three buttons wearing one
        // button's job, and Custom Order asks that exact question as its own first step
        // anyway. Answering it twice, once on a page that cannot act on the answer, is
        // worse than not asking.
        BuildCta(root, "planCta", "PLAN A RUN   →", 0.055f, 0.752f, 0.243f, 0.818f, true,
                 () => SceneManager.LoadScene("OrderDashboard"));
    }

    /// <summary>Shared button. Primary is a solid accent block; secondary is a quiet
    /// hairline that lifts on hover, so the page has exactly one loud control.</summary>
    void BuildCta(RectTransform root, string name, string label,
                  float x0, float y0, float x1, float y1, bool primary,
                  UnityEngine.Events.UnityAction onClick)
    {
        var hit = MakeImage(root, name, primary ? Oxide : new Color(1f, 1f, 1f, 0.045f)).rectTransform;
        Anchor(hit, x0, y0, x1, y1);
        hit.GetComponent<Image>().raycastTarget = true;

        if (!primary)
        {
            var e = hit.gameObject.AddComponent<Outline>();
            e.effectColor = Rule;
            e.effectDistance = new Vector2(1.1f, -1.1f);
        }

        var btn = hit.gameObject.AddComponent<Button>();
        btn.targetGraphic = hit.GetComponent<Image>();
        var c = btn.colors;
        c.normalColor      = Color.white;
        c.highlightedColor = primary ? new Color(1.14f, 1.09f, 1.05f, 1f)
                                     : new Color(2.4f, 2.4f, 2.4f, 1f);
        c.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f);
        c.fadeDuration     = 0.09f;
        btn.colors = c;
        btn.onClick.AddListener(onClick);

        var lbl = MakeText(hit.transform, "l", label, primary ? 21 : 14,
                           primary ? Hex("120A06") : Muted,
                           TextAlignmentOptions.Center, primary ? MonoBold : Sans);
        if (primary) lbl.characterSpacing = 4f;
        Anchor(lbl.rectTransform, 0.04f, 0f, 0.96f, 1f);
    }

    /// <summary>
    /// A telemetry strip in the header's empty right half: what one wind farm gives
    /// you, computed from preset 0. All three presets consume the same feedstock by
    /// design, so this is the page's premise stated once - the rows below are what
    /// you can do with it. Counts up as the page settles.
    /// </summary>
    void BuildTelemetry(RectTransform root)
    {
        var p = OrderContext.Presets[0];
        var m = p.model;
        float frac  = m.OutputSplit().GlassKgH / m.FeedKgH;
        float feedT = frac > 0.0001f ? p.order.targetTonnes / frac : 0f;
        int blades  = Mathf.RoundToInt(feedT / OrderContext.BladeMassTonnes);
        int turbines= Mathf.RoundToInt(blades / (float)OrderContext.BladesPerTurbine);

        var cap = MakeText(root, "telCap", "ONE DECOMMISSIONED FARM", 12, Faint,
                           TextAlignmentOptions.Right, Mono);
        cap.characterSpacing = 5f;
        Anchor(cap.rectTransform, 0.60f, 0.905f, 0.945f, 0.940f);

        float[] xs   = { 0.618f, 0.756f, 0.876f };
        string[] lb  = { "TONNES", "BLADES", "TURBINES" };
        float[] tgt  = { feedT, blades, turbines };

        for (int i = 0; i < 3; i++)
        {
            var v = MakeText(root, "telVal" + i, "0", 34, Bone,
                             TextAlignmentOptions.Right, MonoBold);
            Anchor(v.rectTransform, xs[i] - 0.10f, 0.832f, xs[i] + 0.069f, 0.898f);
            farmFigures.Add(v);
            farmTargets.Add(tgt[i]);

            var l = MakeText(root, "telLbl" + i, lb[i], 11, Faint,
                             TextAlignmentOptions.Right, Mono);
            l.characterSpacing = 4f;
            Anchor(l.rectTransform, xs[i] - 0.10f, 0.806f, xs[i] + 0.069f, 0.834f);
        }

        Anchor(MakeImage(root, "telRule", Rule).rectTransform,
               0.618f, 0.792f, 0.945f, 0.7935f);
    }

    void BuildColumns(RectTransform root)
    {
        var lead = MakeText(root, "examplesLead",
                            "WORKED EXAMPLES   —   ONE FARM, THREE OUTCOMES", 13, Faint,
                            TextAlignmentOptions.Left, Mono);
        lead.characterSpacing = 6f;
        Anchor(lead.rectTransform, 0.055f, 0.664f, 0.70f, 0.696f);

        Anchor(MakeImage(root, "headRule", Rule).rectTransform,
               0.055f, 0.6520f, 0.945f, 0.6545f);

        const float x0 = 0.055f, x1 = 0.945f, gap = 0.022f;
        float w = (x1 - x0 - 2f * gap) / 3f;

        for (int i = 0; i < OrderContext.Presets.Length; i++)
        {
            float x = x0 + i * (w + gap);
            var col = BuildColumn(root, i, x, x + w);
            StartCoroutine(EnterRoutine(col, i * 0.07f));
        }
    }

    /// <summary>One worked example as a column. Returns its rect so it can be eased in.</summary>
    RectTransform BuildColumn(RectTransform root, int index, float x0, float x1)
    {
        var p     = OrderContext.Presets[index];
        var order = p.order;
        var m     = p.model;
        var grade = order.targetGrade;

        var split      = m.OutputSplit();
        float fibreKgH = split.GlassKgH;
        float days     = fibreKgH > 0.01f ? order.targetTonnes * 1000f / fibreKgH / 24f : 0f;

        // The whole column is the button, as the whole row was before.
        var col = MakeImage(root, "col_" + grade, new Color(1f, 1f, 1f, 0f)).rectTransform;
        Anchor(col, x0, 0.072f, x1, 0.638f);
        col.GetComponent<Image>().raycastTarget = true;

        var btn = col.gameObject.AddComponent<Button>();
        btn.targetGraphic = col.GetComponent<Image>();
        var cc = btn.colors;
        cc.normalColor      = new Color(1f, 1f, 1f, 0f);
        cc.highlightedColor = new Color(1f, 1f, 1f, 0.035f);
        cc.pressedColor     = new Color(Oxide.r, Oxide.g, Oxide.b, 0.12f);
        cc.selectedColor    = new Color(1f, 1f, 1f, 0.035f);
        cc.fadeDuration     = 0.10f;
        btn.colors = cc;
        int captured = index;
        btn.onClick.AddListener(() => RunPreset(captured));

        // Accent along the column's top edge on hover - the marker reads as
        // underlining the whole column rather than pointing at one row.
        var hoverEdge = MakeImage(col, "hoverEdge", new Color(Oxide.r, Oxide.g, Oxide.b, 0f));
        AnchorIn(col, hoverEdge.rectTransform, 0f, 0.982f, 1f, 0.990f);

        // Focus. Hovering one column dims the other two, so the bar you are reading
        // stands alone against the page instead of competing with its neighbours.
        // Comparison stops being something you do by scanning and becomes something
        // the screen does for you - and it is the only interaction here that could
        // not be a static image.
        var group = col.gameObject.AddComponent<CanvasGroup>();
        colGroups.Add(group);

        int self = index;
        var trig = col.gameObject.AddComponent<EventTrigger>();
        AddHover(trig, EventTriggerType.PointerEnter, () => { FadeTo(hoverEdge, 1f); Focus(self); });
        AddHover(trig, EventTriggerType.PointerExit,  () => { FadeTo(hoverEdge, 0f); Focus(-1); });

        var num = MakeText(col, "num", $"{index + 1:00}", 20,
                           index == 0 ? Oxide : Faint, TextAlignmentOptions.Left, Mono);
        AnchorIn(col, num.rectTransform, 0f, 0.930f, 0.20f, 0.990f);

        var tag = MakeText(col, "tag", OrderContext.GradeLabel(grade), 13, Muted,
                           TextAlignmentOptions.Right, Mono);
        tag.characterSpacing = 5f;
        AnchorIn(col, tag.rectTransform, 0.30f, 0.930f, 1f, 0.985f);

        var buyer = MakeText(col, "buyer", order.customerType, 27, Bone,
                             TextAlignmentOptions.TopLeft, SansBold);
        buyer.textWrappingMode = TextWrappingModes.Normal;
        AnchorIn(col, buyer.rectTransform, 0f, 0.815f, 1f, 0.915f);

        var use = MakeText(col, "use", p.endUse, 15, Faint,
                           TextAlignmentOptions.TopLeft, Sans);
        use.textWrappingMode = TextWrappingModes.Normal;
        AnchorIn(col, use.rectTransform, 0f, 0.715f, 1f, 0.805f);

        var figs = MakeText(col, "figs", $"{order.targetTonnes:N0} t   ·   {days:0} DAYS",
                            15, Muted, TextAlignmentOptions.Left, MonoBold);
        AnchorIn(col, figs.rectTransform, 0f, 0.660f, 0.50f, 0.706f);

        BuildVerticalBar(col, m, index);

        bool primary = index == 0;
        var runBg = MakeImage(col, "runBtn",
                              primary ? Oxide : new Color(1f, 1f, 1f, 0.05f)).rectTransform;
        AnchorIn(col, runBg, 0f, 0.005f, 1f, 0.075f);
        if (!primary)
        {
            var e = runBg.gameObject.AddComponent<Outline>();
            e.effectColor = Hex("4A4238");
            e.effectDistance = new Vector2(1.2f, -1.2f);
        }
        var runLbl = MakeText(runBg, "runLabel", "RUN   →", 16,
                              primary ? Hex("15110E") : Bone, TextAlignmentOptions.Center, MonoBold);
        runLbl.characterSpacing = 3f;
        Anchor(runLbl.rectTransform, 0, 0, 1, 1);

        return col;
    }

    /// <summary>
    /// The mass balance, standing up. Fibre at the bottom, losses at the top, each
    /// stream labelled with its own percentage beside the block it belongs to.
    ///
    /// Vertical is the whole point: three of these side by side turn "high grade is
    /// cleaner" from a claim into something you measure with your eye.
    /// </summary>
    void BuildVerticalBar(RectTransform col, ProcessModel m, int index)
    {
        var split = m.OutputSplit();
        float[] pct = { split.GlassPct, split.OilPct, split.SyngasPct, split.CharPct, split.LossPct };
        Color[] c   = { StreamFibre, StreamOil, StreamGas, StreamChar, StreamLoss };
        string[] nm = { "FIBRE", "OIL", "SYNGAS", "CHAR", "LOSS" };

        float total = 0f; foreach (var v in pct) total += v;
        if (total <= 0.01f) return;

        // A presence, not a strip. Wide enough that the blocks are the column's
        // subject, with the legend beside it rather than floating against it.
        const float barTop = 0.615f, barBot = 0.105f, barSpan = barTop - barBot;

        var track = MakeImage(col, "bar", SkyWarm).rectTransform;
        AnchorIn(col, track, 0f, barBot, 0.46f, barTop);

        float cursor = 0f;
        for (int i = 0; i < pct.Length; i++)
        {
            float frac = pct[i] / total;
            var seg = MakeImage(track, "seg_" + nm[i], c[i]).rectTransform;
            seg.anchorMin = new Vector2(0f, cursor);
            seg.anchorMax = new Vector2(1f, cursor);          // grown by the animation
            seg.offsetMin = new Vector2(0f, i == 0 ? 0f : 1.5f);
            seg.offsetMax = Vector2.zero;
            StartCoroutine(GrowSegmentV(seg, cursor, cursor + frac, 0.40f,
                                        index * 0.07f + i * 0.045f));
            cursor += frac;
        }

        // The legend sits on FIXED rows rather than at each block's midpoint. Two
        // reasons, and the second is the important one: midpoints left big holes
        // wherever a stream was large, and - because every column uses the same five
        // rows - FIBRE now sits on the same line in all three, so the figures can be
        // read straight across as well as down. The comparison is the whole point.
        string[] order = { "LOSS", "CHAR", "SYNGAS", "OIL", "FIBRE" };
        int[]    src   = { 4, 3, 2, 1, 0 };                  // top of the bar downward
        const float rowH = 0.072f;

        for (int r = 0; r < 5; r++)
        {
            float ry = barTop - 0.045f - r * rowH;
            int s = src[r];

            var sw = MakeImage(col, "sw_" + order[r], c[s]);
            AnchorIn(col, sw.rectTransform, 0.52f, ry + 0.012f, 0.545f, ry + 0.040f);

            var nameT = MakeText(col, "ln_" + order[r], order[r], 13, Muted,
                                 TextAlignmentOptions.Left, Mono);
            nameT.characterSpacing = 2f;
            AnchorIn(col, nameT.rectTransform, 0.565f, ry, 0.80f, ry + 0.052f);

            var valT = MakeText(col, "lv_" + order[r], $"{pct[s]:0.0}", 15, Bone,
                                TextAlignmentOptions.Right, MonoBold);
            AnchorIn(col, valT.rectTransform, 0.78f, ry, 1f, ry + 0.052f);
        }

        var unit = MakeText(col, "barUnit", "% OF FEED", 11, Faint,
                            TextAlignmentOptions.Right, Mono);
        unit.characterSpacing = 3f;
        AnchorIn(col, unit.rectTransform, 0.52f, barTop - 0.002f, 1f, barTop + 0.042f);
    }

    /// <summary>Vertical twin of GrowSegment - blocks rise from the bottom.</summary>
    System.Collections.IEnumerator GrowSegmentV(RectTransform seg, float from, float to,
                                                float seconds, float delay)
    {
        float t = -delay;
        while (t < seconds)
        {
            if (seg == null) yield break;
            t += Time.unscaledDeltaTime;
            if (t < 0f) { yield return null; continue; }
            float u = Mathf.Clamp01(t / seconds);
            u = 1f - Mathf.Pow(1f - u, 3f);
            seg.anchorMax = new Vector2(1f, Mathf.Lerp(from, to, u));
            yield return null;
        }
        if (seg != null) seg.anchorMax = new Vector2(1f, to);
    }

    void BuildFooterLink(RectTransform root)
    {
        MakeLink(root, "how", "HOW IT WORKS", 0.055f, 0.018f, 0.173f, 0.058f,
                 () => SceneManager.LoadScene("HowItWorks"), true);
    }

    /// <summary>
    /// One wind farm, three outcomes - stated as figures rather than as a sentence.
    ///
    /// The hero's right half was empty except for a cropped rotor, and the page as a
    /// whole carried almost no numbers: an engineering tool whose landing page shows
    /// two small figures reads as a brochure. These are the three that matter, they
    /// are the same for all three presets by design, and that identity IS the
    /// argument - the same farm feeds every row below.
    ///
    /// Computed locally from preset 0 rather than through OrderContext's campaign
    /// properties, which need an order to be ACTIVE. Applying a preset here to read
    /// three numbers would clobber whatever the user has set up. Same arithmetic.
    /// </summary>
    void BuildFarmFigures(RectTransform root)
    {
        var p = OrderContext.Presets[0];
        var m = p.model;
        float fibreFrac = m.OutputSplit().GlassKgH / m.FeedKgH;
        float feedT     = fibreFrac > 0.0001f ? p.order.targetTonnes / fibreFrac : 0f;
        int   blades    = Mathf.RoundToInt(feedT / OrderContext.BladeMassTonnes);
        int   turbines  = Mathf.RoundToInt(blades / (float)OrderContext.BladesPerTurbine);

        var lead = MakeText(root, "farmLead", "EVERY ROW BELOW DRAWS ON THE SAME FARM",
                            13, Faint, TextAlignmentOptions.Left, Mono);
        lead.characterSpacing = 6f;
        Anchor(lead.rectTransform, 0.055f, 0.836f, 0.60f, 0.862f);

        // Three figures on one baseline. Mono, so the digits sit on a shared grid and
        // the eye reads them as a set rather than as three separate labels.
        float[] xs   = { 0.055f, 0.185f, 0.315f };
        string[] val = { $"{feedT:N0}", $"{blades:N0}", $"{turbines:N0}" };
        string[] lbl = { "TONNES OF BLADE", "BLADES", "TURBINES" };

        for (int i = 0; i < 3; i++)
        {
            var v = MakeText(root, "farmVal" + i, val[i], 44, Bone,
                             TextAlignmentOptions.Left, MonoBold);
            Anchor(v.rectTransform, xs[i], 0.762f, xs[i] + 0.125f, 0.828f);
            farmFigures.Add(v);
            farmTargets.Add(i == 0 ? feedT : (i == 1 ? blades : turbines));

            var l = MakeText(root, "farmLbl" + i, lbl[i], 12, Faint,
                             TextAlignmentOptions.Left, Mono);
            l.characterSpacing = 4f;
            Anchor(l.rectTransform, xs[i], 0.734f, xs[i] + 0.125f, 0.760f);
        }
    }

    readonly System.Collections.Generic.List<TMP_Text> farmFigures =
        new System.Collections.Generic.List<TMP_Text>();
    readonly System.Collections.Generic.List<float> farmTargets =
        new System.Collections.Generic.List<float>();

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
            var built = BuildRow(root, i, y1 - rowH, y1,
                                 drawRuleBelow: i < OrderContext.Presets.Length - 1);
            StartCoroutine(EnterRoutine(built, i * 0.06f));
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

    /// <summary>Returns the row's rect so the caller can animate it in.</summary>
    RectTransform BuildRow(RectTransform root, int index, float y0, float y1, bool drawRuleBelow)
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
        colors.fadeDuration     = 0.10f;
        btn.colors = colors;
        int captured = index;
        btn.onClick.AddListener(() => RunPreset(captured));

        // An accent that arrives on the left edge when the row is under the cursor.
        // The tint alone said "something is here"; this says WHICH row, and it is the
        // same oxide marker the open step carries on Custom Order, so the two screens
        // indicate focus the same way. Starts transparent and is faded by hand -
        // a Button can only tint one graphic, and that one is already the row.
        var hoverEdge = MakeImage(row, "hoverEdge", new Color(Oxide.r, Oxide.g, Oxide.b, 0f));
        AnchorIn(row, hoverEdge.rectTransform, 0f, 0.12f, 0.0035f, 0.88f);

        var trig = row.gameObject.AddComponent<EventTrigger>();
        AddHover(trig, EventTriggerType.PointerEnter, () => FadeTo(hoverEdge, 1f));
        AddHover(trig, EventTriggerType.PointerExit,  () => FadeTo(hoverEdge, 0f));

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
        BuildOutputBar(row, m, index);

        // An explicit target. The whole row is clickable, but a bare arrow did not say
        // so - people did not know where to press.
        bool primary = index == 0;
        var runBg = MakeImage(row, "runBtn", primary ? Oxide : new Color(1f, 1f, 1f, 0.05f)).rectTransform;
        AnchorIn(row, runBg, 0.872f, 0.40f, 0.995f, 0.66f);
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

        return row;
    }

    /// <summary>Stacked mass-balance bar: fibre, oil, syngas, char, losses, straight from
    /// OutputSplit(). The most useful element on the page - high grade's char block is
    /// 5.9%, low grade's is 26.5%, and you can see that without reading a number.</summary>
    void BuildOutputBar(RectTransform row, ProcessModel m, int index)
    {
        // Wider and thicker than before, and vertically centred in the row. It is no
        // longer one element among four in a crowded right-hand block - it is the
        // only thing between the buyer and the RUN button, so it has to hold that
        // span on its own.
        var split = m.OutputSplit();

        // Wider, and pushed left into the gap the removed columns left behind. This is
        // the most informative element on the page and it was running at a third of
        // the width available to it, with three hundred empty pixels to its left.
        var track = MakeImage(row, "bar", Hex("1A1713")).rectTransform;
        AnchorIn(row, track, 0.415f, 0.46f, 0.845f, 0.635f);

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
            seg.anchorMax = new Vector2(cursor, 1f);          // grown in by the animation
            seg.offsetMin = new Vector2(i == 0 ? 0f : 1.5f, 0f);   // hairline gap between streams
            seg.offsetMax = Vector2.zero;
            StartCoroutine(GrowSegment(seg, cursor, cursor + frac, 0.34f, index * 0.06f + i * 0.035f));

            // The number, under its own block. An engineer reading this page wants the
            // split, not an impression of it - and it is what makes the char column
            // legible as "6% here, 26% there" rather than "a bit bigger".
            if (pct[i] >= 4f)
            {
                var lab = MakeText(row, "pct_" + nm[i], $"{pct[i]:0}", 15, Muted,
                                   TextAlignmentOptions.Center, Mono);
                AnchorIn(row, lab.rectTransform,
                         0.415f + cursor * 0.430f, 0.30f,
                         0.415f + (cursor + frac) * 0.430f, 0.44f);
            }
            cursor += frac;
        }

        // Names the five streams once, under the first row only - repeating it three
        // times would be noise, and the colours are consistent down the column.
        if (index == 0)
        {
            var key = MakeText(row, "barKey", "FIBRE   ·   OIL   ·   SYNGAS   ·   CHAR   ·   LOSS   %",
                               11, Faint, TextAlignmentOptions.Left, Mono);
            key.characterSpacing = 3f;
            AnchorIn(row, key.rectTransform, 0.415f, 0.68f, 0.845f, 0.80f);
        }
    }

    /// <summary>Segments grow from the left as the page settles, in stream order, so
    /// the mass balance reads as an accumulation rather than appearing whole.</summary>
    System.Collections.IEnumerator GrowSegment(RectTransform seg, float from, float to,
                                               float seconds, float delay)
    {
        float t = -delay;
        while (t < seconds)
        {
            if (seg == null) yield break;
            t += Time.unscaledDeltaTime;
            if (t < 0f) { yield return null; continue; }
            float u = Mathf.Clamp01(t / seconds);
            u = 1f - Mathf.Pow(1f - u, 3f);
            seg.anchorMax = new Vector2(Mathf.Lerp(from, to, u), 1f);
            yield return null;
        }
        if (seg != null) seg.anchorMax = new Vector2(to, 1f);
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

    // ===================================================================  motion ==
    //
    // Hand-rolled rather than a package: a shared dependency would land in everyone's
    // manifest and cost the team a re-import for about forty lines of easing.
    // Everything here runs on unscaledDeltaTime and survives its target being
    // destroyed, so a scene change mid-fade cannot throw.

    static void AddHover(EventTrigger trig, EventTriggerType type, System.Action fn)
    {
        var e = new EventTrigger.Entry { eventID = type };
        e.callback.AddListener(_ => fn());
        trig.triggers.Add(e);
    }

    void FadeTo(Image img, float alpha) => StartCoroutine(FadeRoutine(img, alpha, 0.13f));

    readonly System.Collections.Generic.List<CanvasGroup> colGroups =
        new System.Collections.Generic.List<CanvasGroup>();
    Coroutine focusRoutine;

    /// <summary>Brings one column forward and pushes the rest back. Pass -1 to clear.</summary>
    void Focus(int index)
    {
        if (focusRoutine != null) StopCoroutine(focusRoutine);
        focusRoutine = StartCoroutine(FocusRoutine(index));
    }

    System.Collections.IEnumerator FocusRoutine(int index)
    {
        var from = new float[colGroups.Count];
        var to   = new float[colGroups.Count];
        for (int i = 0; i < colGroups.Count; i++)
        {
            if (colGroups[i] == null) yield break;
            from[i] = colGroups[i].alpha;
            to[i]   = (index < 0 || i == index) ? 1f : 0.32f;
        }

        const float dur = 0.16f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            u = u * u * (3f - 2f * u);
            for (int i = 0; i < colGroups.Count; i++)
            {
                if (colGroups[i] == null) yield break;
                colGroups[i].alpha = Mathf.Lerp(from[i], to[i], u);
            }
            yield return null;
        }
        for (int i = 0; i < colGroups.Count; i++)
            if (colGroups[i] != null) colGroups[i].alpha = to[i];
    }

    /// <summary>Counts a figure up to its value. Eased out, so it decelerates into the
    /// number rather than stopping dead - and always lands exactly on the target.</summary>
    System.Collections.IEnumerator CountUp(TMP_Text txt, float target, float seconds, float delay)
    {
        if (txt == null) yield break;
        txt.text = "0";

        float t = -delay;
        while (t < seconds)
        {
            if (txt == null) yield break;
            t += Time.unscaledDeltaTime;
            if (t < 0f) { yield return null; continue; }
            float u = Mathf.Clamp01(t / seconds);
            u = 1f - Mathf.Pow(1f - u, 3f);
            txt.text = Mathf.Round(Mathf.Lerp(0f, target, u)).ToString("N0");
            yield return null;
        }
        if (txt != null) txt.text = Mathf.Round(target).ToString("N0");
    }

    System.Collections.IEnumerator FadeRoutine(Image img, float target, float seconds)
    {
        if (img == null) yield break;
        float from = img.color.a, t = 0f;
        while (t < seconds)
        {
            if (img == null) yield break;
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / seconds);
            u = u * u * (3f - 2f * u);                      // smoothstep
            var c = img.color; c.a = Mathf.Lerp(from, target, u); img.color = c;
            yield return null;
        }
        if (img != null) { var c = img.color; c.a = target; img.color = c; }
    }

    /// <summary>
    /// Rows arrive rather than appear: a short rise and fade, staggered down the
    /// ledger. Sixty milliseconds apart is enough to read as a sequence without
    /// making anyone wait - the last row is settled 180 ms after the first.
    /// </summary>
    System.Collections.IEnumerator EnterRoutine(RectTransform rt, float delay)
    {
        // Reuse the group the column already carries for focus dimming - a second
        // CanvasGroup on the same object multiplies with the first, so the column
        // would enter at 0 and never reach full opacity.
        var cg = rt.GetComponent<CanvasGroup>() ?? rt.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        var home = rt.anchoredPosition;
        rt.anchoredPosition = home + new Vector2(0f, -14f);

        float t = -delay;
        const float dur = 0.32f;
        while (t < dur)
        {
            if (cg == null || rt == null) yield break;
            t += Time.unscaledDeltaTime;
            if (t < 0f) { yield return null; continue; }
            float u = Mathf.Clamp01(t / dur);
            u = 1f - Mathf.Pow(1f - u, 3f);                 // ease-out cubic
            cg.alpha = u;
            rt.anchoredPosition = home + new Vector2(0f, Mathf.Lerp(-14f, 0f, u));
            yield return null;
        }
        if (cg != null) cg.alpha = 1f;
        if (rt != null) rt.anchoredPosition = home;
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
