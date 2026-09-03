using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// Custom Order screen (Task 3). Name a customer, pick a target grade and quantity,
/// press SOLVE, and the solver returns the plant settings that hit that grade. The found
/// settings show with the five output tanks, purity/tensile, a grade badge and campaign
/// figures; Watch this run hands the order to the tour.
///
/// Themed to match the home page (MainMenuController): warm-dark ledger palette, oxide
/// accent, IBM Plex, square flat buttons - so the order flow feels like one product.
/// Built at runtime, self-contained; reads OrderContext / OrderSolver, never edits them,
/// and does not depend on any class defined in a Plant Explorer file.
/// </summary>
public class OrderDashboardController : MonoBehaviour
{
    // ---- palette (from the home page) ----
    static Color Panel, SkyWarm, Bone, Muted, Faint, Oxide, Rule, RuleSoft, TileBg;
    static Color StreamFibre, StreamOil, StreamGas, StreamChar, StreamLoss;
    static bool paletteReady;
    static void InitPalette()
    {
        if (paletteReady) return;
        Panel = Hex("12100D"); SkyWarm = Hex("1A1713"); TileBg = Hex("1A1713");
        Bone = Hex("EDE8DF"); Muted = Hex("8A8177"); Faint = Hex("6E665C");
        Oxide = Hex("C2603A"); Rule = Hex("2A2520"); RuleSoft = Hex("221E1A");
        StreamFibre = Hex("E4DCCD"); StreamOil = Hex("C99A3E"); StreamGas = Hex("6B8F62");
        StreamChar = Hex("2E2823"); StreamLoss = Hex("5A524A");
        paletteReady = true;
    }

    // ---- fonts (IBM Plex, from Resources/Fonts, like the home page) ----
    static TMP_FontAsset Sans, SansBold, Mono, MonoBold;
    static bool fontsReady;
    static void InitFonts()
    {
        if (fontsReady) return;
        Sans = LoadFont("IBMPlexSans-Regular SDF");
        SansBold = LoadFont("IBMPlexSans-SemiBold SDF");
        Mono = LoadFont("IBMPlexMono-Regular SDF");
        MonoBold = LoadFont("IBMPlexMono-Medium SDF");
        fontsReady = true;
    }
    static TMP_FontAsset LoadFont(string name)
    {
        var f = Resources.Load<TMP_FontAsset>("Fonts/" + name);
        if (f == null) Debug.LogWarning("OrderDashboardController: font '" + name + "' missing, using TMP default.");
        return f;
    }

    void Awake()
    {
        InitPalette();
        InitFonts();
        EnsureEventSystem();
        SetupCamera();
        var canvas = BuildCanvas();
        BuildUI(canvas.transform);
    }

    void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Panel;
    }

    Canvas BuildCanvas()
    {
        var go = new GameObject("OrderDashboardCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay;
        var sc = go.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
        return c;
    }

    void BuildUI(Transform root)
    {
        var panel = MakeImage(root, "Panel", Panel); panel.raycastTarget = true;
        Anchor(panel.rectTransform, 0, 0, 1, 1);

        // Masthead: wordmark-style eyebrow + statement, oxide tick, Menu top-right.
        var eyebrow = MakeText(panel.rectTransform, "eyebrow", "CUSTOM ORDER", 22, Bone, TextAlignmentOptions.Left, Mono);
        eyebrow.characterSpacing = 14f;
        Anchor(eyebrow.rectTransform, 0.055f, 0.90f, 0.6f, 0.955f);
        var tick = MakeImage(panel.rectTransform, "tick", Oxide);
        Anchor(tick.rectTransform, 0.055f, 0.888f, 0.088f, 0.892f);
        var statement = MakeText(panel.rectTransform, "statement", "Name an outcome. The plant finds the recipe.", 30, Bone, TextAlignmentOptions.Left, Sans);
        Anchor(statement.rectTransform, 0.055f, 0.815f, 0.8f, 0.882f);

        MakeButton(panel.rectTransform, "MenuButton", "\u2190  MENU", new Vector2(0.86f,0.90f), new Vector2(0.945f,0.945f),
                   false, () => SceneManager.LoadScene("MainMenu"));

        BuildOrderForm(panel.rectTransform);
    }

    // ---- order form state ----
    TMP_InputField customerInput;
    Grade selectedGrade = Grade.Mid;
    readonly Image[] gradeChips = new Image[3];
    readonly TMP_Text[] gradeChipLabels = new TMP_Text[3];
    Slider qtySlider; TMP_Text qtyValue;
    Button solveButton; TMP_Text solveLabel; Image solveBg;

    void BuildOrderForm(RectTransform panel)
    {
        // LEFT COLUMN (~33%): the order panel. Labels sit directly above their controls
        // (not far to the left) so the column reads tight and aligned.
        const float lx0 = 0.055f, lx1 = 0.36f;

        var head = MakeImage(panel, "formRule", Rule);
        Anchor(head.rectTransform, lx0, 0.80f, lx1, 0.802f);

        // Customer
        StackLabel(panel, "CUSTOMER", lx0, lx1, 0.755f);
        var inputGO = new GameObject("CustomerInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputGO.transform.SetParent(panel, false);
        var irt = inputGO.GetComponent<RectTransform>();
        Anchor(irt, lx0, 0.705f, lx1, 0.745f);
        var inImg = inputGO.GetComponent<Image>(); inImg.color = SkyWarm; inImg.raycastTarget = true;
        var inEdge = inputGO.AddComponent<Outline>(); inEdge.effectColor = Rule; inEdge.effectDistance = new Vector2(1.2f,-1.2f);
        var viewport = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(irt, false); Anchor(viewport, 0, 0, 1, 1); viewport.offsetMin = new Vector2(14, 2); viewport.offsetMax = new Vector2(-14, -2);
        var inputText = MakeText(viewport, "Text", "", 16, Bone, TextAlignmentOptions.Left, Sans);
        var placeholder = MakeText(viewport, "Placeholder", "Custom order", 16, Faint, TextAlignmentOptions.Left, Sans);
        placeholder.fontStyle = FontStyles.Italic;
        customerInput = inputGO.GetComponent<TMP_InputField>();
        customerInput.textViewport = viewport; customerInput.textComponent = inputText; customerInput.placeholder = placeholder; customerInput.text = "";

        // Grade (three chips across the column)
        StackLabel(panel, "GRADE", lx0, lx1, 0.635f);
        string[] names = { "HIGH", "MID", "LOW" };
        Grade[] grades = { Grade.High, Grade.Mid, Grade.Low };
        float span = lx1 - lx0, gap = 0.01f, cw = (span - 2*gap) / 3f;
        for (int i = 0; i < 3; i++)
        {
            int idx = i; Grade g = grades[i];
            var chip = new GameObject("grade_" + names[i], typeof(RectTransform), typeof(Image), typeof(Button));
            chip.transform.SetParent(panel, false);
            var chrt = chip.GetComponent<RectTransform>();
            float x = lx0 + i*(cw+gap);
            Anchor(chrt, x, 0.585f, x + cw, 0.625f);
            var chipImg = chip.GetComponent<Image>(); chipImg.raycastTarget = true; gradeChips[i] = chipImg;
            var edge = chip.AddComponent<Outline>(); edge.effectColor = Hex("4A4238"); edge.effectDistance = new Vector2(1.2f,-1.2f);
            var cl = MakeText(chrt, "l", names[i], 14, Bone, TextAlignmentOptions.Center, MonoBold); cl.characterSpacing = 2f;
            gradeChipLabels[i] = cl;
            chip.GetComponent<Button>().onClick.AddListener(() => SelectGrade(g, idx));
        }
        SelectGrade(Grade.Mid, 1);

        // Quantity (slider + value on one row under the label)
        StackLabel(panel, "QUANTITY", lx0, lx1, 0.515f);
        var sGO = new GameObject("QtySlider", typeof(RectTransform), typeof(Slider));
        sGO.transform.SetParent(panel, false);
        var srt = sGO.GetComponent<RectTransform>(); Anchor(srt, lx0, 0.475f, lx1 - 0.08f, 0.505f);
        var qbg = MakeImage(srt, "bg", Rule); Anchor(qbg.rectTransform,0,0,1,1); qbg.raycastTarget = true;
        var qfillArea = new GameObject("FillArea", typeof(RectTransform)).GetComponent<RectTransform>(); qfillArea.SetParent(srt,false); Anchor(qfillArea,0,0,1,1);
        var qfill = MakeImage(qfillArea, "Fill", Oxide); qfill.rectTransform.anchorMin=new Vector2(0,0); qfill.rectTransform.anchorMax=new Vector2(0,1); qfill.rectTransform.sizeDelta=new Vector2(8,0);
        var qhandleArea = new GameObject("HandleArea", typeof(RectTransform)).GetComponent<RectTransform>(); qhandleArea.SetParent(srt,false); Anchor(qhandleArea,0,0,1,1);
        var qhandle = MakeImage(qhandleArea, "Handle", Bone); qhandle.rectTransform.sizeDelta=new Vector2(14,20); qhandle.raycastTarget = true;
        qtySlider = sGO.GetComponent<Slider>();
        qtySlider.fillRect=qfill.rectTransform; qtySlider.handleRect=qhandle.rectTransform; qtySlider.targetGraphic=qhandle;
        qtySlider.direction=Slider.Direction.LeftToRight; qtySlider.minValue=1000; qtySlider.maxValue=10000; qtySlider.wholeNumbers=true; qtySlider.value=4000;
        qtyValue = MakeText(panel, "qtyv", "4,000 t", 16, Oxide, TextAlignmentOptions.Right, MonoBold);
        Anchor(qtyValue.rectTransform, lx1 - 0.075f, 0.472f, lx1, 0.508f);
                qtySlider.onValueChanged.AddListener(v => {
            qtyValue.text = v.ToString("N0") + " t";
            // Quantity only scales the campaign figures (not settings/tanks/quality). If a
            // plan is already on screen, refresh it live so THIS ORDER TAKES tracks the qty.
            if (slidersBuilt && resultsArea != null && resultsArea.gameObject.activeSelf && !solving)
                RecomputeFromSliders();
        });;

        // SOLVE (full column width)
        var solveGO = new GameObject("SolveButton", typeof(RectTransform), typeof(Image), typeof(Button));
        solveGO.transform.SetParent(panel, false);
        var solrt = solveGO.GetComponent<RectTransform>();
        Anchor(solrt, lx0, 0.39f, lx1, 0.45f);
        solveBg = solveGO.GetComponent<Image>(); solveBg.color = Oxide; solveBg.raycastTarget = true;
        solveButton = solveGO.GetComponent<Button>(); solveButton.targetGraphic = solveBg;
        solveButton.onClick.AddListener(OnSolve);
        solveLabel = MakeText(solrt, "l", "S O L V E", 17, Hex("15110E"), TextAlignmentOptions.Center, MonoBold); solveLabel.characterSpacing = 4f;
    }

    // Label sitting directly above its control, left-aligned to the column.
    void StackLabel(RectTransform panel, string text, float x0, float x1, float y)
    {
        var l = MakeText(panel, "lbl_" + text, text, 13, Faint, TextAlignmentOptions.Left, Mono); l.characterSpacing = 4f;
        Anchor(l.rectTransform, x0, y, x1, y + 0.04f);
    }

    void MakeFieldLabel(RectTransform panel, string text, float y)
    {
        var l = MakeText(panel, "lbl_" + text, text, 13, Faint, TextAlignmentOptions.Left, Mono); l.characterSpacing = 5f;
        Anchor(l.rectTransform, 0.055f, y, 0.19f, y + 0.05f);
    }

    void SelectGrade(Grade g, int idx)
    {
        selectedGrade = g;
        for (int i = 0; i < 3; i++)
        {
            bool on = i == idx;
            gradeChips[i].color = on ? Oxide : new Color(1f,1f,1f,0.04f);
            gradeChipLabels[i].color = on ? Hex("15110E") : Bone;
        }
        // Grade chips only change the selection. The plan on the right updates only when the
        // user presses SOLVE (the four settings sliders stay live once a plan is shown).
    }

    // ---- solve ----
    bool solving;
    void OnSolve()
    {
        if (solving) return;
        StartCoroutine(SolveRoutine());
    }

    System.Collections.IEnumerator SolveRoutine()
    {
        solving = true;
        solveButton.interactable = false;
        solveBg.color = Faint;
        solveLabel.text = "SOLVING\u2026"; solveLabel.color = Bone;
        yield return null; yield return null;

        var result = OrderSolver.Solve(selectedGrade);
        if (!result.feasible)
        {
            ShowInfeasible(result.note);
        }
        else
        {
            string name = string.IsNullOrWhiteSpace(customerInput.text) ? "Custom order" : customerInput.text.Trim();
            var order = new Order(name, "Custom order", selectedGrade, qtySlider.value);
            OrderContext.SetOrder(order, result.model);
            ShowSolvedPlan(result.model);
        }

        solving = false;
        solveButton.interactable = true;
        solveBg.color = Oxide;
        solveLabel.text = "S O L V E"; solveLabel.color = Hex("15110E");
    }

    // ---- solved plan ----
    RectTransform resultsArea;
    TMP_Text foundSettingsText, infeasibleText;
    readonly List<GameObject> planWidgets = new List<GameObject>();
    Image[] tankFills = new Image[5];
    TMP_Text[] tankPct = new TMP_Text[5];
    TMP_Text[] tankRate = new TMP_Text[5];
    TMP_Text purityVal, tensileVal, campaignText;
    Image gradeBadge; TMP_Text gradeBadgeText;
    static readonly string[] StreamNames = { "Glass", "Oil", "Syngas", "Char", "Losses" };

    void EnsureResultsArea()
    {
        if (resultsArea != null) return;
        var panel = Object.FindFirstObjectByType<Canvas>().transform.Find("Panel") as RectTransform;

        var divider = MakeImage(panel, "colRule", Rule);
        Anchor(divider.rectTransform, 0.385f, 0.05f, 0.386f, 0.80f);

        var area = new GameObject("Results", typeof(RectTransform)).GetComponent<RectTransform>();
        area.SetParent(panel, false);
        Anchor(area, 0.42f, 0.05f, 0.945f, 0.80f);
        resultsArea = area;

        var hdr = MakeText(area, "hdr", "THESE SETTINGS DELIVER IT", 13, Faint, TextAlignmentOptions.TopLeft, Mono);
        hdr.characterSpacing = 5f; Anchor(hdr.rectTransform, 0, 0.95f, 1, 1f);

        foundSettingsText = MakeText(area, "found", "", 24, Bone, TextAlignmentOptions.Left, MonoBold);
        Anchor(foundSettingsText.rectTransform, 0, 0.88f, 1, 0.94f);

        infeasibleText = MakeText(area, "infeasible", "", 17, Oxide, TextAlignmentOptions.TopLeft, Sans);
        infeasibleText.enableWordWrapping = true; Anchor(infeasibleText.rectTransform, 0, 0.6f, 1, 0.94f);

        // YOU WOULD GET — five tanks
        var youGet = MakeText(area, "youget", "YOU WOULD GET", 13, Faint, TextAlignmentOptions.TopLeft, Mono);
        youGet.characterSpacing = 5f; Anchor(youGet.rectTransform, 0, 0.63f, 1, 0.68f); planWidgets.Add(youGet.gameObject);

        var tanksRow = new GameObject("tanks", typeof(RectTransform)).GetComponent<RectTransform>();
        tanksRow.SetParent(area, false); Anchor(tanksRow, 0, 0.36f, 0.66f, 0.62f); planWidgets.Add(tanksRow.gameObject);
        Color[] cols = { StreamFibre, StreamOil, StreamGas, StreamChar, StreamLoss };
        float tw = 0.175f, tgap = 0.03f;
        for (int i = 0; i < 5; i++) BuildTank(tanksRow, i, StreamNames[i], cols[i], i*(tw+tgap), tw);

        // purity / tensile (right sub-column, beside tanks)
        var pl = MakeText(area, "pl", "FIBRE PURITY", 12, Faint, TextAlignmentOptions.Left, Mono); pl.characterSpacing=3f;
        Anchor(pl.rectTransform, 0.70f, 0.58f, 1f, 0.63f); planWidgets.Add(pl.gameObject);
        purityVal = MakeText(area, "pv", "", 30, Bone, TextAlignmentOptions.Left, MonoBold);
        Anchor(purityVal.rectTransform, 0.70f, 0.50f, 1f, 0.58f); planWidgets.Add(purityVal.gameObject);
        var tl = MakeText(area, "tl", "TENSILE RETENTION", 12, Faint, TextAlignmentOptions.Left, Mono); tl.characterSpacing=3f;
        Anchor(tl.rectTransform, 0.70f, 0.43f, 1f, 0.48f); planWidgets.Add(tl.gameObject);
        tensileVal = MakeText(area, "tv", "", 30, Bone, TextAlignmentOptions.Left, MonoBold);
        Anchor(tensileVal.rectTransform, 0.70f, 0.35f, 1f, 0.43f); planWidgets.Add(tensileVal.gameObject);

        // THIS ORDER TAKES — campaign figures. Even vertical spacing: tanks bottom (0.36),
        // header (0.25-0.30), campaign (0.08-0.22) - matched ~0.06 gaps above and below.
        var takesHdr = MakeText(area, "takes", "THIS ORDER TAKES", 13, Faint, TextAlignmentOptions.TopLeft, Mono);
        takesHdr.characterSpacing = 5f; Anchor(takesHdr.rectTransform, 0, 0.25f, 0.66f, 0.30f); planWidgets.Add(takesHdr.gameObject);
        campaignText = MakeText(area, "campaign", "", 18, Muted, TextAlignmentOptions.TopLeft, Mono);
        campaignText.lineSpacing = 10f; Anchor(campaignText.rectTransform, 0, 0.08f, 0.66f, 0.22f); planWidgets.Add(campaignText.gameObject);

        // grade badge + Watch this run (lower right)
        gradeBadge = MakeImage(area, "badge", Oxide); Anchor(gradeBadge.rectTransform, 0.70f, 0.20f, 1f, 0.27f); planWidgets.Add(gradeBadge.gameObject);
        gradeBadgeText = MakeText(gradeBadge.rectTransform, "bt", "", 16, Hex("15110E"), TextAlignmentOptions.Center, MonoBold); gradeBadgeText.characterSpacing=2f;

        MakeButton(area, "WatchButton", "WATCH THIS RUN  \u2192", new Vector2(0.70f,0.06f), new Vector2(1f,0.16f), true, OnWatchRun, out watchWidget);
        planWidgets.Add(watchWidget);

        resultsArea.gameObject.SetActive(false);
    }

    GameObject watchWidget;

    void BuildTank(RectTransform row, int index, string label, Color col, float x, float w)
    {
        var tank = new GameObject("tank_" + label, typeof(RectTransform)).GetComponent<RectTransform>();
        tank.SetParent(row, false);
        tank.anchorMin = new Vector2(x, 0); tank.anchorMax = new Vector2(x + w, 1); tank.offsetMin = Vector2.zero; tank.offsetMax = Vector2.zero;

        var lbl = MakeText(tank, "l", label, 12, Muted, TextAlignmentOptions.Top, Mono); lbl.characterSpacing=1f;
        Anchor(lbl.rectTransform, 0, 0.88f, 1, 1f);
        var body = MakeImage(tank, "body", Hex("1A1713")); Anchor(body.rectTransform, 0.12f, 0.30f, 0.88f, 0.86f);
        var fill = MakeImage(body.rectTransform, "fill", col);
        fill.rectTransform.anchorMin=new Vector2(0,0); fill.rectTransform.anchorMax=new Vector2(1,0.5f); fill.rectTransform.offsetMin=Vector2.zero; fill.rectTransform.offsetMax=Vector2.zero;
        var grad = fill.gameObject.AddComponent<OrderUIGradient>(); grad.top=new Color(1f,1f,1f,1f); grad.bottom=new Color(0.7f,0.7f,0.7f,1f);
        tankFills[index] = fill;
        var pct = MakeText(tank, "p", "", 15, Bone, TextAlignmentOptions.Center, MonoBold); Anchor(pct.rectTransform,0,0.14f,1,0.28f); tankPct[index]=pct;
        var rate = MakeText(tank, "r", "", 10, Faint, TextAlignmentOptions.Center, Mono); Anchor(rate.rectTransform,0,0.02f,1,0.13f); tankRate[index]=rate;
    }

    // ---- editable-after-solve settings sliders ----
    Slider sTemp, sRet, sFeed, sPart;
    TMP_Text vTemp, vRet, vFeed, vPart;
    bool slidersBuilt, suppressRecompute;

    Slider BuildSettingSlider(RectTransform parent, string label, float min, float max, bool whole,
                              float x0, float x1, float y, out TMP_Text valueText)
    {
        var lab = MakeText(parent, "sl_" + label, label, 12, Faint, TextAlignmentOptions.Left, Mono); lab.characterSpacing = 3f;
        Anchor(lab.rectTransform, x0, y + 0.055f, x1, y + 0.10f);
        valueText = MakeText(parent, "sv_" + label, "", 16, Bone, TextAlignmentOptions.Right, MonoBold);
        Anchor(valueText.rectTransform, x1 - 0.16f, y + 0.052f, x1, y + 0.10f);

        var sGO = new GameObject("set_" + label, typeof(RectTransform), typeof(Slider));
        sGO.transform.SetParent(parent, false);
        var srt = sGO.GetComponent<RectTransform>(); Anchor(srt, x0, y, x1, y + 0.045f);
        var bg = MakeImage(srt, "bg", Rule); Anchor(bg.rectTransform,0,0,1,1); bg.raycastTarget = true;
        var fillArea = new GameObject("FillArea", typeof(RectTransform)).GetComponent<RectTransform>(); fillArea.SetParent(srt,false); Anchor(fillArea,0,0,1,1);
        var fill = MakeImage(fillArea, "Fill", Oxide); fill.rectTransform.anchorMin=new Vector2(0,0); fill.rectTransform.anchorMax=new Vector2(0,1); fill.rectTransform.sizeDelta=new Vector2(6,0);
        var handleArea = new GameObject("HandleArea", typeof(RectTransform)).GetComponent<RectTransform>(); handleArea.SetParent(srt,false); Anchor(handleArea,0,0,1,1);
        var handle = MakeImage(handleArea, "Handle", Bone); handle.rectTransform.sizeDelta=new Vector2(12,16); handle.raycastTarget = true;
        var sl = sGO.GetComponent<Slider>();
        sl.fillRect=fill.rectTransform; sl.handleRect=handle.rectTransform; sl.targetGraphic=handle;
        sl.direction=Slider.Direction.LeftToRight; sl.minValue=min; sl.maxValue=max; sl.wholeNumbers=whole;
        return sl;
    }

    // Live recompute from the four setting sliders. Feed is bounded by particle size
    // (OrderSolver.MaxFeed) - the non-negotiable coupling. Rebuilds the model, refreshes
    // tanks / quality / badge / campaign, so editing after solve is a live sandbox.
    void RecomputeFromSliders()
    {
        if (suppressRecompute) return;

        // Bind feed's max to the current particle size.
        float maxFeed = OrderSolver.MaxFeed(sPart.value);
        if (sFeed.maxValue != maxFeed)
        {
            sFeed.maxValue = maxFeed;
            if (sFeed.value > maxFeed) sFeed.value = maxFeed;   // clamp current value down
        }

        var m = new ProcessModel {
            TempC = sTemp.value, RetentionMin = sRet.value,
            FeedKgH = sFeed.value, ParticleSizeMm = sPart.value
        };

        vTemp.text = m.TempC.ToString("0") + " \u00b0C";
        vRet.text  = m.RetentionMin.ToString("0") + " min";
        vFeed.text = m.FeedKgH.ToString("N0");
        vPart.text = m.ParticleSizeMm.ToString("0.#") + " mm";

        // Re-anchor the order to the edited model so campaign figures recompute.
        string name = string.IsNullOrWhiteSpace(customerInput.text) ? "Custom order" : customerInput.text.Trim();
        OrderContext.SetOrder(new Order(name, "Custom order", selectedGrade, qtySlider.value), m);
        PaintPlan(m);
    }


    void ShowSolvedPlan(ProcessModel m)
    {
        EnsureResultsArea();
        resultsArea.gameObject.SetActive(true);
        infeasibleText.gameObject.SetActive(false);
        foreach (var w in planWidgets) w.SetActive(true);

        if (!slidersBuilt)
        {
            foundSettingsText.gameObject.SetActive(false);
            // Two rows of two sliders in the top band (0.62-0.90), above YOU WOULD GET (0.55).
            sTemp = BuildSettingSlider(resultsArea, "KILN TEMP",     400, 700, true, 0.00f, 0.31f, 0.83f, out vTemp);
            sRet  = BuildSettingSlider(resultsArea, "RETENTION",     30,  45,  true, 0.35f, 0.66f, 0.83f, out vRet);
            sFeed = BuildSettingSlider(resultsArea, "FEED RATE",     4000,9000,true, 0.00f, 0.31f, 0.66f, out vFeed);
            sPart = BuildSettingSlider(resultsArea, "PARTICLE SIZE", 1,   20,  false,0.35f, 0.66f, 0.66f, out vPart);
            sTemp.onValueChanged.AddListener(_ => RecomputeFromSliders());
            sRet.onValueChanged.AddListener(_ => RecomputeFromSliders());
            sFeed.onValueChanged.AddListener(_ => RecomputeFromSliders());
            sPart.onValueChanged.AddListener(_ => RecomputeFromSliders());
            slidersBuilt = true;
        }

        suppressRecompute = true;
        sPart.value = m.ParticleSizeMm;
        sFeed.maxValue = OrderSolver.MaxFeed(m.ParticleSizeMm);
        sTemp.value = m.TempC; sRet.value = m.RetentionMin; sFeed.value = m.FeedKgH;
        vTemp.text = m.TempC.ToString("0") + " \u00b0C"; vRet.text = m.RetentionMin.ToString("0") + " min";
        vFeed.text = m.FeedKgH.ToString("N0"); vPart.text = m.ParticleSizeMm.ToString("0.#") + " mm";
        suppressRecompute = false;

        PaintPlan(m);
    }

    // Paints tanks / quality / badge / campaign for a model. Used by both the initial
    // solve and the live slider edits.
    void PaintPlan(ProcessModel m)
    {
        var sp = m.OutputSplit();
        float[] pcts = { sp.GlassPct, sp.OilPct, sp.SyngasPct, sp.CharPct, sp.LossPct };
        float[] rates = { sp.GlassKgH, sp.OilKgH, sp.SyngasKgH, sp.CharKgH, sp.LossKgH };
        // Scale every fill against a FIXED reference (not against glass), so the glass bar
        // itself moves with its share instead of being pinned full. 75% headroom keeps the
        // dominant glass bar tall without ever clipping the top.
        const float refPct = 75f;
        for (int i = 0; i < 5; i++)
        {
            float frac = Mathf.Clamp01(pcts[i] / refPct);
            tankFills[i].rectTransform.anchorMax = new Vector2(1, Mathf.Max(0.02f, frac));
            tankPct[i].text = pcts[i].ToString("0.0") + "%";
            tankRate[i].text = rates[i].ToString("N0");
        }

        purityVal.text = m.FiberPurityPct.ToString("0.0") + "%";
        tensileVal.text = m.TensileRetentionPct.ToString("0") + "%";

        var g = OrderContext.GradeOf(m.FiberPurityPct, m.TensileRetentionPct);
        gradeBadge.color = g == Grade.High ? StreamGas : (g == Grade.Mid ? Oxide : Faint);
        gradeBadgeText.text = OrderContext.GradeLabel(g).ToUpperInvariant();

        campaignText.text =
            OrderContext.FeedTonnesNeeded.ToString("N0") + " t feedstock   \u00b7   " + OrderContext.BladesNeeded.ToString("N0") + " blades\n" +
            OrderContext.TurbinesNeeded.ToString("N0") + " turbines   \u00b7   " + OrderContext.CampaignDays.ToString("0.0") + " days at 24/7";
    }

    void ShowInfeasible(string note)
    {
        EnsureResultsArea();
        resultsArea.gameObject.SetActive(true);
        foundSettingsText.gameObject.SetActive(false);
        foreach (var w in planWidgets) w.SetActive(false);
        infeasibleText.gameObject.SetActive(true);
        infeasibleText.text = string.IsNullOrEmpty(note)
            ? "No settings in the operating envelope reach that grade at this quantity. Try a coarser target."
            : note;
    }

    void OnWatchRun()
    {
        // OrderContext.SetOrder was already called in the solve. Hand off to the tour.
        TourRunner.StartRun();
    }

    // ---- helpers ----
    void MakeButton(Transform parent, string name, string label, Vector2 aMin, Vector2 aMax, bool primary, UnityEngine.Events.UnityAction onClick)
    { MakeButton(parent, name, label, aMin, aMax, primary, onClick, out _); }

    void MakeButton(Transform parent, string name, string label, Vector2 aMin, Vector2 aMax, bool primary, UnityEngine.Events.UnityAction onClick, out GameObject made)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>(); img.color = primary ? Oxide : new Color(1f,1f,1f,0.05f);
        var edge = go.AddComponent<Outline>(); edge.effectColor = primary ? Oxide : Hex("4A4238"); edge.effectDistance = new Vector2(1.2f,-1.2f);
        go.GetComponent<Button>().onClick.AddListener(onClick);
        var t = MakeText(rt, "l", label, 15, primary ? Hex("15110E") : Bone, TextAlignmentOptions.Center, MonoBold); t.characterSpacing = 3f;
        made = go;
    }

    Image MakeImage(Transform parent, string name, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.color = col; img.raycastTarget = false;
        return img;
    }

    TMP_Text MakeText(Transform parent, string name, string text, float size, Color col, TextAlignmentOptions align, TMP_FontAsset font = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = col; t.alignment = align; t.raycastTarget = false;
        t.enableWordWrapping = false;
        if (font != null) t.font = font;
        Stretch(t.rectTransform);
        return t;
    }

    void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }

    static void Stretch(RectTransform r){ r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=Vector2.zero; r.offsetMax=Vector2.zero; }
    static void Anchor(RectTransform r, float xmin,float ymin,float xmax,float ymax){ r.anchorMin=new Vector2(xmin,ymin); r.anchorMax=new Vector2(xmax,ymax); r.offsetMin=Vector2.zero; r.offsetMax=Vector2.zero; }
    static Color Hex(string h){ ColorUtility.TryParseHtmlString("#"+h, out var c); return c; }
}

// Self-contained gradient (copied so this screen depends on no Plant Explorer class).
public class OrderUIGradient : UnityEngine.UI.BaseMeshEffect
{
    public Color top = new Color(1f,1f,1f,1f);
    public Color bottom = new Color(0.7f,0.7f,0.7f,1f);
    public override void ModifyMesh(UnityEngine.UI.VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;
        var verts = new System.Collections.Generic.List<UnityEngine.UIVertex>();
        vh.GetUIVertexStream(verts);
        float minY=float.MaxValue, maxY=float.MinValue;
        for (int i=0;i<verts.Count;i++){ float y=verts[i].position.y; if(y<minY)minY=y; if(y>maxY)maxY=y; }
        float h=Mathf.Max(0.0001f, maxY-minY);
        for (int i=0;i<verts.Count;i++){ var v=verts[i]; float t=(v.position.y-minY)/h; v.color=v.color*Color.Lerp(bottom,top,t); verts[i]=v; }
        vh.Clear(); vh.AddUIVertexTriangleStream(verts);
    }
}
