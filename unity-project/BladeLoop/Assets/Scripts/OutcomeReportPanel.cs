using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

/// <summary>
/// The ending. When the tour finishes, the stats panel grows left across the whole
/// screen and becomes the run report.
///
/// ---------------------------------------------------------------------------
///  WHY A SEPARATE COMPONENT
///
///  The obvious implementation is to widen OrderPanel itself. That file is 900
///  lines of single-column layout with a masked viewport, a ScrollRect and a
///  pinned hint box, all measured against a 28%-wide column - reflowing it into
///  three columns would mean rewriting the part of it we most recently got right.
///
///  So this builds its OWN panel in the SAME colour, starting at exactly the split
///  edge OrderPanel occupies and sliding to the left screen edge. On screen it is
///  the stats bar expanding; in code nothing about OrderPanel changes. OrderPanel
///  is torn down only AFTER the cover is complete, so the 3D view never flashes
///  back to full width behind a half-drawn panel.
///
///  NOT A SCENE. Staying in Stage 4 keeps the last frame behind the panel and
///  avoids a load, so the report appears instantly and the run is still visible
///  at the edges while it slides.
/// ---------------------------------------------------------------------------
/// </summary>
public class OutcomeReportPanel : MonoBehaviour
{
    public static OutcomeReportPanel Instance { get; private set; }

    const float SlideTime = 0.55f;
    const float Margin    = 84f;
    const float ColGap    = 44f;

    RectTransform root;
    RectTransform body;          // everything that fades in after the slide
    CanvasGroup   bodyGroup;
    TMP_Text      savedNote;
    string        lastSavedPath;

    public static void Show()
    {
        if (Instance != null) return;
        var go = new GameObject("~OutcomeReport (runtime)");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<OutcomeReportPanel>();
    }

    void Awake()
    {
        BladeLoopTheme.Init();
        EnsureEventSystem();
        // Skip / Next / Previous belong to a tour that has just ended. Retire them
        // before the first frame, so they never appear over the report.
        TourControls.Suppress();
        BuildShell();
        StartCoroutine(Slide());
    }

    /// <summary>The stage scenes carry their own EventSystem, but a scene that
    /// happens not to would leave every button here dead with no error.</summary>
    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(go);
    }

    // ------------------------------------------------------------------ shell --

    void BuildShell()
    {
        var canvasGo = new GameObject("OutcomeReportCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // 1200, not 900. TourControls also uses 900, and equal sorting orders leave
        // the draw order to chance - which is how the dead Previous button ended up
        // painting OVER this panel rather than under it.
        canvas.sortingOrder = 1200;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        // Starts exactly where OrderPanel's left edge is, in the same colour, so
        // frame one of this panel is pixel-identical to the panel it replaces.
        root = MakeRect(canvasGo.transform, "ReportPanel");
        root.anchorMin = new Vector2(OrderContext.TourSplitWidth, 0f);
        root.anchorMax = new Vector2(1f, 1f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        var bg = root.gameObject.AddComponent<Image>();
        bg.color = BladeLoopTheme.Panel;
        bg.raycastTarget = true;                // this panel DOES eat clicks

        body = MakeRect(root, "Body");
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = Vector2.zero;
        body.offsetMax = Vector2.zero;
        bodyGroup = body.gameObject.AddComponent<CanvasGroup>();
        bodyGroup.alpha = 0f;
    }

    IEnumerator Slide()
    {
        float t = 0f;
        float from = OrderContext.TourSplitWidth;
        while (t < SlideTime)
        {
            t += Time.unscaledDeltaTime;                 // the story may be paused
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / SlideTime));
            root.anchorMin = new Vector2(Mathf.Lerp(from, 0f, u), 0f);
            yield return null;
        }
        root.anchorMin = new Vector2(0f, 0f);

        // Only now: the screen is fully covered, so releasing the split camera
        // cannot show as a jump.
        OrderPanel.Teardown();

        BuildReport();

        float f = 0f;
        while (f < 0.32f)
        {
            f += Time.unscaledDeltaTime;
            bodyGroup.alpha = Mathf.Clamp01(f / 0.32f);
            yield return null;
        }
        bodyGroup.alpha = 1f;
    }

    // ----------------------------------------------------------------- report --

    void BuildReport()
    {
        var m = OrderContext.Model ?? OrderContext.DesignCase();
        var s = m.OutputSplit();
        var r = OrderContext.Reference;
        bool hasOrder = OrderContext.HasOrder;

        float top = Margin;

        // ---- masthead -------------------------------------------------------
        Label(body, "Brand", "BLADELOOP", 26f, BladeLoopTheme.Bone, BladeLoopTheme.MonoBold,
              Margin, top, 620f, 40f);
        top += 40f;
        Label(body, "Sub", "RUN REPORT  ·  " + System.DateTime.Now.ToString("d MMMM yyyy, HH:mm"),
              15f, BladeLoopTheme.Muted, BladeLoopTheme.Sans, Margin, top, 720f, 26f);
        top += 46f;
        Rule(body, Margin, top, ScreenW() - Margin * 2f);
        top += 30f;

        // ---- verdict --------------------------------------------------------
        top = BuildVerdict(top, hasOrder);

        // ---- three columns --------------------------------------------------
        float colW = (ScreenW() - Margin * 2f - ColGap * 2f) / 3f;
        float cx   = Margin;

        float c1 = BuildOrderCol (cx,                       colW, top, hasOrder);
        float c2 = BuildPlantCol (cx + (colW + ColGap),     colW, top, m, r);
        float c3 = BuildOutputCol(cx + (colW + ColGap) * 2f, colW, top, s);

        float after = Mathf.Max(c1, Mathf.Max(c2, c3)) + 34f;

        // ---- quality + cost -------------------------------------------------
        Rule(body, Margin, after, ScreenW() - Margin * 2f);
        after += 28f;
        float d1 = BuildQualityCol(cx, colW, after, m);
        float d2 = BuildCostCol(cx + (colW + ColGap), colW * 2f + ColGap, after, hasOrder, m);

        // ---- actions --------------------------------------------------------
        BuildButtons();
    }

    float BuildVerdict(float top, bool hasOrder)
    {
        string title, line;
        Color accent;

        if (!hasOrder)
        {
            title = "DESIGN CASE";
            line  = "This run was not started from an order, so it is shown against the plant's design settings.";
            accent = BladeLoopTheme.Faint;
        }
        else
        {
            Grade target = OrderContext.Active.targetGrade;
            Grade got    = OrderContext.AchievedGrade;
            bool met     = OrderContext.MeetsTarget;
            int steps    = (int)got - (int)target;

            title = met ? "ORDER FILLED" : "TARGET MISSED";
            accent = met ? BladeLoopTheme.StreamGas : BladeLoopTheme.Oxide;
            line = "Asked for " + OrderContext.GradeLabel(target)
                 + ", produced " + OrderContext.GradeLabel(got) + ".  ";
            line += met
                ? "The fibre meets the grade the buyer ordered."
                : "The fibre is still sellable, but not to this buyer — it grades out "
                  + (steps >= 2 ? "two steps" : "one step") + " lower, so it goes to a different market.";
        }

        float w = ScreenW() - Margin * 2f;

        var band = MakeRect(body, "VerdictBand");
        band.anchorMin = new Vector2(0f, 1f);
        band.anchorMax = new Vector2(0f, 1f);
        band.pivot     = new Vector2(0f, 1f);
        band.anchoredPosition = new Vector2(Margin, -top);
        band.sizeDelta = new Vector2(w, 112f);
        var bimg = band.gameObject.AddComponent<Image>();
        bimg.color = BladeLoopTheme.SkyWarm;
        bimg.raycastTarget = false;

        var edge = MakeRect(band, "Accent");
        edge.anchorMin = new Vector2(0f, 0f);
        edge.anchorMax = new Vector2(0f, 1f);
        edge.pivot     = new Vector2(0f, 0.5f);
        edge.sizeDelta = new Vector2(3f, 0f);
        var eimg = edge.gameObject.AddComponent<Image>();
        eimg.color = accent;
        eimg.raycastTarget = false;

        Label(body, "VTitle", title, 21f, accent, BladeLoopTheme.MonoBold,
              Margin + 24f, top + 20f, w - 48f, 30f);
        var bodyTxt = Label(body, "VBody", line, 16.5f, BladeLoopTheme.Bone, BladeLoopTheme.Sans,
              Margin + 24f, top + 54f, w - 48f, 46f);
        bodyTxt.textWrappingMode = TextWrappingModes.Normal;

        top += 112f + 30f;

        if (hasOrder)
        {
            var use = Label(body, "VUse", OrderContext.EndUseFor(OrderContext.AchievedGrade),
                  15f, BladeLoopTheme.Muted, BladeLoopTheme.Sans, Margin, top, w, 44f);
            use.textWrappingMode = TextWrappingModes.Normal;
            top += 50f;
        }
        return top;
    }

    float BuildOrderCol(float x, float w, float y, bool hasOrder)
    {
        y = Head(x, w, y, "THE ORDER");
        if (!hasOrder) { Row(x, w, ref y, "Not started from an order", ""); return y; }
        var o = OrderContext.Active;
        Row(x, w, ref y, "Buyer type",      o.customerType);
        Row(x, w, ref y, "Grade requested", OrderContext.GradeLabel(o.targetGrade));
        Row(x, w, ref y, "Fibre requested", o.targetTonnes.ToString("0.#") + " t");
        return y;
    }

    float BuildPlantCol(float x, float w, float y, ProcessModel m, ProcessModel r)
    {
        y = Head(x, w, y, "HOW THE PLANT WAS SET");
        Setting(x, w, ref y, "Temperature", m.TempC,          r.TempC,          "0",   " °C");
        Setting(x, w, ref y, "Retention",   m.RetentionMin,   r.RetentionMin,   "0.#", " min");
        Setting(x, w, ref y, "Feed rate",   m.FeedKgH,        r.FeedKgH,        "0",   " kg/h");
        Setting(x, w, ref y, "Particle",    m.ParticleSizeMm, r.ParticleSizeMm, "0.#", " mm");
        return y;
    }

    float BuildOutputCol(float x, float w, float y, ProcessModel.Split s)
    {
        y = Head(x, w, y, "WHAT THE PLANT MADE");
        string[] names = { "Glass fibre", "Pyrolysis oil", "Syngas", "Carbon char", "Loss" };
        float[]  pct   = { s.GlassPct, s.OilPct, s.SyngasPct, s.CharPct, s.LossPct };
        var cols = BladeLoopTheme.StreamColours;

        float widest = 0f;
        for (int i = 0; i < pct.Length; i++) widest = Mathf.Max(widest, pct[i]);
        if (widest <= 0f) widest = 1f;

        for (int i = 0; i < names.Length; i++)
        {
            Label(body, "S" + i, names[i], 15f, BladeLoopTheme.Muted, BladeLoopTheme.Sans,
                  x, y, 132f, 22f);
            Label(body, "SV" + i, pct[i].ToString("0.0") + "%", 15f, BladeLoopTheme.Bone,
                  BladeLoopTheme.MonoBold, x + w - 62f, y, 62f, 22f);

            float trackX = x + 138f, trackW = w - 138f - 70f;
            Bar(trackX, y + 7f, trackW, 8f, BladeLoopTheme.Rule);
            Bar(trackX, y + 7f, Mathf.Max(2f, trackW * (pct[i] / widest)), 8f, cols[i]);
            y += 30f;
        }

        y += 6f;
        var note = Label(body, "LossNote",
            "Loss is feed that never becomes product — fines carried off with the gas, dust, "
            + "and residue left in the plant. Near 1.5% on spec, up to 10% as the feed coarsens.",
            13f, BladeLoopTheme.Faint, BladeLoopTheme.Sans, x, y, w, 60f);
        note.textWrappingMode = TextWrappingModes.Normal;
        return y + 62f;
    }

    float BuildQualityCol(float x, float w, float y, ProcessModel m)
    {
        y = Head(x, w, y, "FIBRE QUALITY");
        Row(x, w, ref y, "Purity",            m.FiberPurityPct.ToString("0.0") + "%");
        Row(x, w, ref y, "Tensile retention", m.TensileRetentionPct.ToString("0.0") + "%");
        Row(x, w, ref y, "Efficiency",        m.EfficiencyPct + "%");
        return y;
    }

    float BuildCostCol(float x, float w, float y, bool hasOrder, ProcessModel m)
    {
        y = Head(x, w, y, "WHAT THE RUN COSTS");
        if (!hasOrder) { Row(x, w, ref y, "No order, so no campaign to size", ""); return y; }
        Row(x, w, ref y, "Blade material", OrderContext.FeedTonnesLabel);
        Row(x, w, ref y, "Blades",         OrderContext.BladesLabel);
        Row(x, w, ref y, "Turbines",       OrderContext.TurbinesLabel);
        Row(x, w, ref y, "Campaign",       OrderContext.CampaignLabel);
        return y;
    }

    // ---------------------------------------------------------------- actions --

    void BuildButtons()
    {
        float bw = 224f, bh = 52f, gap = 16f;
        float rx = ScreenW() - Margin - bw;
        float by = Margin - 6f;

        MakeButton("Btn_Menu", "Back to menu", rx, by, bw, bh, false, BackToMenu);
        MakeButton("Btn_Save", "Save report",  rx - (bw + gap), by, bw, bh, true, SaveReport);

        savedNote = Label(body, "SavedNote", "", 13.5f, BladeLoopTheme.Faint, BladeLoopTheme.Sans,
                          Margin, ScreenH() - by - bh + 14f, ScreenW() - Margin * 2f - (bw * 2f + gap + 40f), 40f);
        savedNote.textWrappingMode = TextWrappingModes.Normal;
    }

    void SaveReport()
    {
        try
        {
            string dir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            if (string.IsNullOrEmpty(dir) || !System.IO.Directory.Exists(dir))
                dir = Application.persistentDataPath;

            string path = System.IO.Path.Combine(dir, OutcomeReport.FileName());
            System.IO.File.WriteAllText(path, OutcomeReport.BuildHtml());
            lastSavedPath = path;

            if (savedNote != null) savedNote.text = "Saved to  " + path;
            Application.OpenURL("file:///" + path.Replace("\\", "/"));
        }
        catch (System.Exception e)
        {
            // Never let a locked folder or a read-only disk take down the ending.
            if (savedNote != null) savedNote.text = "Could not save the report: " + e.Message;
            Debug.LogWarning("[OutcomeReport] save failed: " + e);
        }
    }

    void BackToMenu()
    {
        var seq = TourSceneSequencer.Active;
        if (seq != null) seq.EndTour();

        OrderPanel.Teardown();
        Instance = null;
        SceneManager.LoadScene("MainMenu");
        Destroy(gameObject);
    }

    // ----------------------------------------------------------------- atoms ---

    static float ScreenW() => 1920f;    // canvas reference space, not pixels
    static float ScreenH() => 1080f;

    float Head(float x, float w, float y, string text)
    {
        Label(body, "H_" + text, text, 12.5f, BladeLoopTheme.Muted, BladeLoopTheme.MonoBold,
              x, y, w, 20f);
        Rule(body, x, y + 24f, w);
        return y + 38f;
    }

    void Row(float x, float w, ref float y, string k, string v)
    {
        Label(body, "K_" + k + y, k, 14.5f, BladeLoopTheme.Muted, BladeLoopTheme.Sans, x, y, w * 0.55f, 22f);
        if (!string.IsNullOrEmpty(v))
        {
            var t = Label(body, "V_" + k + y, v, 15.5f, BladeLoopTheme.Bone, BladeLoopTheme.MonoBold,
                          x + w * 0.55f, y, w * 0.45f, 22f);
            t.alignment = TextAlignmentOptions.TopRight;
        }
        y += 28f;
    }

    void Setting(float x, float w, ref float y, string k, float actual, float design, string fmt, string unit)
    {
        Label(body, "SK_" + k, k, 14.5f, BladeLoopTheme.Muted, BladeLoopTheme.Sans, x, y, w * 0.5f, 22f);
        var v = Label(body, "SV_" + k, actual.ToString(fmt) + unit, 15.5f, BladeLoopTheme.Bone,
                      BladeLoopTheme.MonoBold, x + w * 0.5f, y, w * 0.5f, 22f);
        v.alignment = TextAlignmentOptions.TopRight;
        y += 21f;

        float d = actual - design;
        bool onSpec = Mathf.Abs(d) < 0.05f;
        string delta = onSpec ? "on spec" : (d > 0f ? "+" : "−") + Mathf.Abs(d).ToString(fmt) + unit;

        Label(body, "SD_" + k, "design " + design.ToString(fmt) + unit, 12.5f,
              BladeLoopTheme.Faint, BladeLoopTheme.Sans, x, y, w * 0.5f, 18f);
        var dt = Label(body, "SDD_" + k, delta, 12.5f,
              onSpec ? BladeLoopTheme.StreamGas : BladeLoopTheme.Oxide,
              BladeLoopTheme.Sans, x + w * 0.5f, y, w * 0.5f, 18f);
        dt.alignment = TextAlignmentOptions.TopRight;
        y += 27f;
    }

    void Bar(float x, float y, float w, float h, Color c)
    {
        var rt = MakeRect(body, "Bar");
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
    }

    void Rule(Transform parent, float x, float y, float w)
    {
        var rt = MakeRect(parent, "Rule");
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, 1f);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = BladeLoopTheme.Rule;
        img.raycastTarget = false;
    }

    TMP_Text Label(Transform parent, string name, string text, float size, Color col,
                   TMP_FontAsset font, float x, float y, float w, float h)
    {
        var rt = MakeRect(parent, name);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);

        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = col;
        if (font != null) t.font = font;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.raycastTarget = false;
        t.richText = true;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    void MakeButton(string name, string text, float x, float yFromBottom, float w, float h,
                    bool primary, UnityEngine.Events.UnityAction onClick)
    {
        var rt = MakeRect(body, name);
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot     = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(x, yFromBottom);
        rt.sizeDelta = new Vector2(w, h);

        var img = rt.gameObject.AddComponent<Image>();
        img.color = primary ? new Color(0.93f, 0.93f, 0.93f, 1f) : BladeLoopTheme.SkyWarm;

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var lab = Label(rt, name + "_l", text, 20f,
                        primary ? new Color(0.118f, 0.161f, 0.216f) : BladeLoopTheme.Bone,
                        BladeLoopTheme.SansBold, 0f, 0f, w, h);
        lab.alignment = TextAlignmentOptions.Center;
        var lrt = lab.rectTransform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

        if (!primary)
        {
            var ol = MakeRect(rt, "Outline");
            ol.anchorMin = Vector2.zero; ol.anchorMax = Vector2.one;
            ol.offsetMin = Vector2.zero; ol.offsetMax = Vector2.zero;
            var oimg = ol.gameObject.AddComponent<Image>();
            oimg.color = new Color(1f, 1f, 1f, 0.10f);
            oimg.raycastTarget = false;
            ol.SetAsFirstSibling();
        }
    }

    static RectTransform MakeRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
}
