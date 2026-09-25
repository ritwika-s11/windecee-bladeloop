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
    RectTransform body;          // the scrolling document
    RectTransform chrome;        // pinned over it: Save, Back, and the saved-path note
    CanvasGroup   bodyGroup;
    TMP_Text      savedNote;

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

        // ---- fade layer ------------------------------------------------------
        // The whole report fades in together, chrome included, so the CanvasGroup sits
        // above the scroll split rather than on the scrolling content.
        var fade = MakeRect(root, "Fade");
        fade.anchorMin = Vector2.zero;
        fade.anchorMax = Vector2.one;
        fade.offsetMin = Vector2.zero;
        fade.offsetMax = Vector2.zero;
        bodyGroup = fade.gameObject.AddComponent<CanvasGroup>();
        bodyGroup.alpha = 0f;

        // ---- scroll viewport -------------------------------------------------
        //
        // The report is laid out top-down in 1920x1080 reference space with no regard
        // for where 1080 ends, and it has been overrunning the bottom of the screen -
        // "Fibre quality" and "What the run costs" were already half off. Anything that
        // lengthens it, like the operator-changes block, makes that worse. So the
        // content scrolls, and the layout code below is left exactly as it is.
        var viewport = MakeRect(fade, "Viewport");
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewport.gameObject.AddComponent<RectMask2D>();

        // A scroll wheel event travels UP from whatever graphic it hits. The panel
        // background lives on `root`, which is this object's PARENT, so it could never
        // deliver the event to the ScrollRect below it. This transparent sheet is the
        // raycast target inside the scroll area that makes the wheel work.
        var catcher = viewport.gameObject.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);
        catcher.raycastTarget = true;

        body = MakeRect(viewport, "Body");
        body.anchorMin = new Vector2(0f, 1f);
        body.anchorMax = new Vector2(1f, 1f);
        body.pivot     = new Vector2(0.5f, 1f);
        body.offsetMin = new Vector2(0f, 0f);
        body.offsetMax = new Vector2(0f, 0f);
        body.anchoredPosition = Vector2.zero;
        body.sizeDelta = new Vector2(0f, ScreenH());   // grown to fit in BuildReport

        var scroll = fade.gameObject.AddComponent<ScrollRect>();
        scroll.viewport          = viewport;
        scroll.content           = body;
        scroll.horizontal        = false;
        scroll.vertical          = true;
        scroll.movementType      = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 42f;
        scroll.inertia           = false;   // a document, not a flick list

        // ---- pinned chrome ---------------------------------------------------
        // Save and Back must stay reachable from any scroll position, so they sit
        // OUTSIDE the scrolling content, above it.
        chrome = MakeRect(fade, "Chrome");
        chrome.anchorMin = Vector2.zero;
        chrome.anchorMax = Vector2.one;
        chrome.offsetMin = Vector2.zero;
        chrome.offsetMax = Vector2.zero;
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

        // ---- operator changes, if the run was steered mid-way ---------------
        // Same position as in the downloaded HTML, and for the same reason: it changes
        // how "HOW THE PLANT WAS SET" below should be read. Returns `top` unchanged
        // when nothing was touched, so an untouched run's screen is pixel-identical to
        // what it was before this existed.
        top = BuildOperatorChanges(top);

        // ---- three columns --------------------------------------------------
        float colW = (ScreenW() - Margin * 2f - ColGap * 2f) / 3f;
        float cx   = Margin;

        float c1 = BuildOrderCol (cx,                       colW, top, hasOrder);
        float c2 = BuildPlantCol (cx + (colW + ColGap),     colW, top, m, r);
        float c3 = BuildOutputCol(cx + (colW + ColGap) * 2f, colW, top, s, r.OutputSplit());

        float after = Mathf.Max(c1, Mathf.Max(c2, c3)) + 34f;

        // ---- quality + cost -------------------------------------------------
        Rule(body, Margin, after, ScreenW() - Margin * 2f);
        after += 28f;
        float d1 = BuildQualityCol(cx, colW, after, m);
        float d2 = BuildCostCol(cx + (colW + ColGap), colW * 2f + ColGap, after, hasOrder, m);

        // ---- actions --------------------------------------------------------
        BuildButtons();

        // ---- size the scroll content ----------------------------------------
        // The layout is written top-down in absolute y, so the deepest column IS the
        // document height. The tail allowance clears the pinned action row, which
        // floats over the content rather than being part of it - without it the last
        // line of "what the run costs" can only ever be read from behind a button.
        float deepest = Mathf.Max(d1, d2) + Margin + 110f;
        body.sizeDelta = new Vector2(0f, Mathf.Max(ScreenH(), deepest));
    }

    /// <summary>
    /// What the operator changed while the run was on screen. The screen twin of
    /// OutcomeReport.AppendOperatorChanges - see there for why the report has to say it.
    ///
    /// Two columns rather than three: the changes on the left, their consequence on the
    /// right. The three-column grid below is for the run's standing figures; this is a
    /// before-and-after, and forcing it into thirds would leave an empty column.
    /// </summary>
    float BuildOperatorChanges(float top)
    {
        if (!SetpointLog.Any || SetpointLog.Entry == null) return top;

        var entry = SetpointLog.Entry;
        var final = OrderContext.Model;
        if (final == null) return top;

        float colW = (ScreenW() - Margin * 2f - ColGap) * 0.5f;
        float lx = Margin, rx = Margin + colW + ColGap;

        // Newest first and capped, matching the downloaded report. Uncapped this column
        // would grow without bound and push the three-column grid off the page.
        float ly = Head(lx, colW, top, "CHANGED DURING THE RUN");
        var recent = SetpointLog.Recent(SetpointLog.MaxShown);
        foreach (var c in recent)
        {
            Row(lx, colW, ref ly, c.label + "  ·  " + c.stage,
                SetpointLog.Fmt(c, c.from) + "  →  " + SetpointLog.Fmt(c, c.to));
        }

        int hidden = SetpointLog.Used - recent.Count;
        if (hidden > 0)
        {
            Label(body, "OpHidden",
                  "and " + hidden + (hidden == 1 ? " earlier change" : " earlier changes")
                  + ", not listed",
                  12.5f, BladeLoopTheme.Faint, BladeLoopTheme.Sans, lx, ly, colW, 20f);
            ly += 24f;
        }

        float p0 = entry.FiberPurityPct, p1 = final.FiberPurityPct;
        float f0 = entry.OutputSplit().GlassKgH, f1 = final.OutputSplit().GlassKgH;

        float ry = Head(rx, colW, top, "WHAT IT DID");
        Row(rx, colW, ref ry, "Purity",
            p0.ToString("0.0") + "%  →  " + p1.ToString("0.0") + "%");
        Row(rx, colW, ref ry, "Fibre per hour",
            f0.ToString("N0") + "  →  " + f1.ToString("N0") + " kg/h");
        Row(rx, colW, ref ry, "Grade",
            OrderContext.GradeLabel(OrderContext.GradeOf(p0, entry.TensileRetentionPct))
            + "  →  " +
            OrderContext.GradeLabel(OrderContext.GradeOf(p1, final.TensileRetentionPct)));

        float y = Mathf.Max(ly, ry) + 4f;
        var note = Label(body, "OpNote",
            "The plant is modelled at steady state, so every other figure here is computed "
            + "from the final settings rather than blended across the campaign. This is what "
            + "the run started from.",
            12.5f, BladeLoopTheme.Faint, BladeLoopTheme.Sans,
            Margin, y, ScreenW() - Margin * 2f, 34f);
        note.textWrappingMode = TextWrappingModes.Normal;

        y += 40f;
        Rule(body, Margin, y, ScreenW() - Margin * 2f);
        return y + 28f;
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

            // Three outcomes, not two. Over-delivery used to share the "meets the grade
            // the buyer ordered" wording with an exact hit, which shrugs at the better
            // result - and with setpoints now movable mid-run, deliberately improving
            // the fibre is something a user does on purpose and should be told about.
            if (met && got < target)
            {
                int up = (int)target - (int)got;
                line += "That is " + (up >= 2 ? "two grades" : "a grade")
                      + " better than this buyer asked for, so the order is filled with room to spare.";
            }
            else if (met)
            {
                line += "The fibre meets the grade the buyer ordered.";
            }
            else
            {
                line += "The fibre is still sellable, but not to this buyer — it grades out "
                      + (steps >= 2 ? "two steps" : "one step") + " lower, so it goes to a different market.";
            }
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
            // Named, not bare. This describes the market for the grade that was
            // PRODUCED; unattributed under a verdict that names two grades, it read as
            // a contradiction - "Order filled" for a cement works above a paragraph
            // about structural composite parts.
            var use = Label(body, "VUse",
                  OrderContext.GradeLabel(OrderContext.AchievedGrade) + " fibre:  "
                  + OrderContext.EndUseFor(OrderContext.AchievedGrade),
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
        Setting(x, w, ref y, "Temperature", m.TempC,          r.TempC,          "0",   " °C",   150f);
        Setting(x, w, ref y, "Retention",   m.RetentionMin,   r.RetentionMin,   "0.#", " min",   10f);
        Setting(x, w, ref y, "Feed rate",   m.FeedKgH,        r.FeedKgH,        "0",   " kg/h", 2500f);
        Setting(x, w, ref y, "Particle",    m.ParticleSizeMm, r.ParticleSizeMm, "0.#", " mm",    18f);

        y += 4f;
        var note = Label(body, "DevNote",
            "Each bar runs from the design case at the centre. Longer means further off spec.",
            13f, BladeLoopTheme.Faint, BladeLoopTheme.Sans, x, y, w, 40f);
        note.textWrappingMode = TextWrappingModes.Normal;
        return y + 42f;
    }

    /// <summary>Same five bars as the downloaded report, each with its design-case tick,
    /// so the screen and the HTML tell the same story rather than two different ones.</summary>
    float BuildOutputCol(float x, float w, float y, ProcessModel.Split s, ProcessModel.Split d)
    {
        y = Head(x, w, y, "WHAT THE PLANT MADE");
        string[] names = { "Glass fibre", "Pyrolysis oil", "Syngas", "Carbon char", "Loss" };
        float[]  pct   = { s.GlassPct, s.OilPct, s.SyngasPct, s.CharPct, s.LossPct };
        float[]  dpct  = { d.GlassPct, d.OilPct, d.SyngasPct, d.CharPct, d.LossPct };
        var cols = BladeLoopTheme.StreamColours;

        // Scale across both series, or a design tick can land past the end of its track
        // on any run that under-performs the design case.
        float widest = 0f;
        for (int i = 0; i < pct.Length; i++) widest = Mathf.Max(widest, Mathf.Max(pct[i], dpct[i]));
        if (widest <= 0f) widest = 1f;

        float trackX = x + 138f, trackW = w - 138f - 70f;

        for (int i = 0; i < names.Length; i++)
        {
            Label(body, "S" + i, names[i], 15f, BladeLoopTheme.Muted, BladeLoopTheme.Sans,
                  x, y, 132f, 22f);
            Label(body, "SV" + i, pct[i].ToString("0.0") + "%", 15f, BladeLoopTheme.Bone,
                  BladeLoopTheme.MonoBold, x + w - 62f, y, 62f, 22f);

            Bar(trackX, y + 7f, trackW, 8f, BladeLoopTheme.Rule);
            Bar(trackX, y + 7f, Mathf.Max(2f, trackW * (pct[i] / widest)), 8f, cols[i]);
            // Drawn taller than the track so it reads over both the near-white fibre
            // fill and the near-black char fill.
            Bar(trackX + trackW * (dpct[i] / widest) - 1f, y + 4f, 2f, 14f, BladeLoopTheme.Bone);
            y += 30f;
        }

        y += 6f;
        var tickNote = Label(body, "TickNote",
            "The pale tick on each bar is the design case — where that stream sits with all "
            + "four settings on spec.",
            13f, BladeLoopTheme.Faint, BladeLoopTheme.Sans, x, y, w, 40f);
        tickNote.textWrappingMode = TextWrappingModes.Normal;
        y += 42f;

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
        QualityBar(x, w, ref y, "Purity", m.FiberPurityPct,
                   OrderContext.MidPurity,  OrderContext.HighPurity);
        QualityBar(x, w, ref y, "Tensile retention", m.TensileRetentionPct,
                   OrderContext.MidTensile, OrderContext.HighTensile);
        Row(x, w, ref y, "Efficiency", m.EfficiencyPct + "%");

        y += 4f;
        var note = Label(body, "QNote",
            "Ticks are the mid and high thresholds. Each bar takes the grade that measure "
            + "alone would earn — the shorter one is what capped this run.",
            13f, BladeLoopTheme.Faint, BladeLoopTheme.Sans, x, y, w, 40f);
        note.textWrappingMode = TextWrappingModes.Normal;
        return y + 42f;
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

        // STARTS AT THE SECOND COLUMN, not the left margin. The quality block now ends
        // with a note explaining the threshold ticks, and that note occupies the
        // bottom-left corner this used to sit in - a collision that stays invisible
        // until someone actually presses Save and the path appears underneath it.
        float sx = Margin + (ScreenW() - Margin * 2f - ColGap * 2f) / 3f + ColGap;
        float sw = (rx - (bw + gap)) - sx - 24f;

        savedNote = Label(chrome, "SavedNote", "", 13.5f, BladeLoopTheme.Faint, BladeLoopTheme.Sans,
                          sx, ScreenH() - by - bh + 14f, sw, 40f);
        savedNote.textWrappingMode = TextWrappingModes.Normal;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void BladeLoopDownloadFile(string name, string text);
#endif

    /// <summary>Hands the report to the user.
    ///
    /// The two platforms need genuinely different mechanisms, not a shared one
    /// with a flag. On Windows a file goes to the Desktop and opens in the
    /// browser. In WebGL there IS no disk: File.WriteAllText writes to an
    /// IndexedDB virtual filesystem that succeeds, throws nothing, and leaves the
    /// file somewhere the user can never reach - so the browser build hands the
    /// bytes to the page as a Blob download instead.
    ///
    /// Getting this wrong is invisible in the editor, because the editor is not
    /// WebGL. It only shows up in a deployed build.</summary>
    void SaveReport()
    {
        string html = OutcomeReport.BuildHtml();
        string name = OutcomeReport.FileName();

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            BladeLoopDownloadFile(name, html);
            if (savedNote != null) savedNote.text = "Downloaded  " + name;
        }
        catch (System.Exception e)
        {
            if (savedNote != null) savedNote.text = "Could not download the report: " + e.Message;
            Debug.LogWarning("[OutcomeReport] download failed: " + e);
        }
#else
        try
        {
            string dir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            if (string.IsNullOrEmpty(dir) || !System.IO.Directory.Exists(dir))
                dir = Application.persistentDataPath;

            string path = System.IO.Path.Combine(dir, name);
            System.IO.File.WriteAllText(path, html);

            if (savedNote != null) savedNote.text = "Saved to  " + path;
            Application.OpenURL("file:///" + path.Replace("\\", "/"));
        }
        catch (System.Exception e)
        {
            // Never let a locked folder or a read-only disk take down the ending.
            if (savedNote != null) savedNote.text = "Could not save the report: " + e.Message;
            Debug.LogWarning("[OutcomeReport] save failed: " + e);
        }
#endif
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

    /// <summary>A setting, its design value, the signed gap, and a centre-zero deviation
    /// bar. <paramref name="span"/> is the model's own deviation denominator for this
    /// input (ProcessModel.DevTemp and friends), so a half-width bar is a full-strength
    /// penalty. MIRRORED, like the palette in OutcomeReport: those denominators are not
    /// public constants, so if they change in ProcessModel, change them here too.</summary>
    void Setting(float x, float w, ref float y, string k, float actual, float design,
                 string fmt, string unit, float span)
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
        y += 24f;

        // centre-zero deviation bar
        float half = w * 0.5f;
        Bar(x, y, w, 6f, BladeLoopTheme.Rule);
        float u = Mathf.Clamp(d / Mathf.Max(span, 0.0001f), -1f, 1f);
        float len = Mathf.Abs(u) * half;
        if (len > 1f)
            Bar(u >= 0f ? x + half : x + half - len, y, len, 6f,
                onSpec ? BladeLoopTheme.StreamGas : BladeLoopTheme.Oxide);
        Bar(x + half, y - 2f, 1f, 10f, BladeLoopTheme.Faint);   // the design centre line
        y += 22f;
    }

    /// <summary>One quality measure on a 0-100 scale with the two grade thresholds ticked
    /// on it. Coloured by the tier THAT MEASURE ALONE would earn: a grade needs both to
    /// clear, so when the two bars differ in colour the shorter one is what held the run
    /// back - which is the part a reader can act on.</summary>
    void QualityBar(float x, float w, ref float y, string k, float value, float midBar, float highBar)
    {
        Label(body, "QK_" + k, k, 14.5f, BladeLoopTheme.Muted, BladeLoopTheme.Sans, x, y, w * 0.6f, 22f);
        var v = Label(body, "QV_" + k, value.ToString("0.0") + "%", 15.5f, BladeLoopTheme.Bone,
                      BladeLoopTheme.MonoBold, x + w * 0.6f, y, w * 0.4f, 22f);
        v.alignment = TextAlignmentOptions.TopRight;
        y += 24f;

        Color tier = value >= highBar ? BladeLoopTheme.StreamGas
                   : value >= midBar  ? BladeLoopTheme.Oxide
                                      : BladeLoopTheme.Faint;

        Bar(x, y, w, 8f, BladeLoopTheme.Rule);
        Bar(x, y, Mathf.Max(2f, w * Mathf.Clamp01(value / 100f)), 8f, tier);
        Bar(x + w * (midBar  / 100f) - 1f, y - 3f, 2f, 14f, BladeLoopTheme.Bone);
        Bar(x + w * (highBar / 100f) - 1f, y - 3f, 2f, 14f, BladeLoopTheme.Bone);
        y += 26f;
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
        // `chrome`, not `body`: these are pinned to the window, not to the document.
        var rt = MakeRect(chrome, name);
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
