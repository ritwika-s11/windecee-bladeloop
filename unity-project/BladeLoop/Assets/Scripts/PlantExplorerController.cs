using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// Plant Explorer — process-control dashboard, light theme.
/// A single left-hand panel holds process inputs (sliders with status LEDs and
/// live info popups) and recovery output (efficiency, output tanks, quality
/// metrics). The reactive 3D kiln shows on the right. Driven live from ProcessModel.
/// </summary>
public class PlantExplorerController : MonoBehaviour
{
    static Color PanelBg, TileBg, Accent, TextMain, TextSub, Line;
    static Color Ok, Warn, Crit, GlassCol, GasCol, CharCol, LossCol, OilCol;
    static bool paletteReady;
    static void InitPalette()
    {
        if (paletteReady) return;
        PanelBg  = Hex("F5F6F8"); TileBg = Hex("FFFFFF"); Accent = Hex("2563EB");
        TextMain = Hex("1E293B"); TextSub = Hex("64748B"); Line = Hex("E2E8F0");
        Ok       = Hex("16A34A"); Warn = Hex("D97706"); Crit = Hex("DC2626");
        GlassCol = Hex("2563EB"); GasCol = Hex("16A34A"); CharCol = Hex("C2703D"); LossCol = Hex("94A3B8"); OilCol = Hex("CA8A04");
        paletteReady = true;
    }

    ProcessModel model = new ProcessModel();

    Image ledTemp, ledRetention, ledFeed, ledParticle;
    TMP_Text effNum, statusVal; Image statusLight;
    Image tankGlass, tankOil, tankSyngas, tankChar, tankLoss;
    TMP_Text pctGlass, pctOil, pctSyngas, pctChar, pctLoss, rateGlass, rateOil, rateSyngas, rateChar, rateLoss;
    TMP_Text purityVal, tensileVal;

    KilnVisualizer kilnViz;

    // Sliders paired with their optimum value, for the Reset-to-optimum button.
    readonly List<(Slider slider, float optimum)> resetTargets = new List<(Slider, float)>();

    void Awake()
    {
        InitPalette();
        EnsureEventSystem();
        var canvas = BuildCanvas();
        BuildUI(canvas.transform);
        kilnViz = Object.FindFirstObjectByType<KilnVisualizer>();
        Recompute();
    }

    Canvas BuildCanvas()
    {
        var go = new GameObject("PlantExplorerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay;
        var s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080); s.matchWidthOrHeight = 0.5f;
        return c;
    }

    void BuildUI(Transform root)
    {
        // Wider left panel (~55%) holding two columns; kiln shows on the right.
        var panel = MakeImage(root, "Panel", PanelBg); panel.raycastTarget = true;
        panel.rectTransform.anchorMin = new Vector2(0, 0); panel.rectTransform.anchorMax = new Vector2(0.72f, 1);
        panel.rectTransform.offsetMin = Vector2.zero; panel.rectTransform.offsetMax = Vector2.zero;

        BuildBackButtons(root);

        // header band
        var eyebrow = MakeText(panel.rectTransform, "eyebrow", "PROCESS CONTROL DASHBOARD \u00b7 PYROLYSIS FACILITY", 12, TextSub, TextAlignmentOptions.TopLeft);
        eyebrow.rectTransform.anchorMin = new Vector2(0,1); eyebrow.rectTransform.anchorMax = new Vector2(1,1); eyebrow.rectTransform.pivot = new Vector2(0.5f,1);
        eyebrow.rectTransform.anchoredPosition = new Vector2(0,-120); eyebrow.rectTransform.sizeDelta = new Vector2(-56, 16); eyebrow.characterSpacing = 4;
        var title = MakeText(panel.rectTransform, "title", "PLANT EXPLORER", 40, Accent, TextAlignmentOptions.TopLeft);
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.anchorMin = new Vector2(0,1); title.rectTransform.anchorMax = new Vector2(1,1); title.rectTransform.pivot = new Vector2(0.5f,1);
        title.rectTransform.anchoredPosition = new Vector2(0,-138); title.rectTransform.sizeDelta = new Vector2(-56, 56);

        // status pill
        var sb = MakeImage(panel.rectTransform, "StatusBlock", TileBg);
        sb.rectTransform.anchorMin = new Vector2(1,1); sb.rectTransform.anchorMax = new Vector2(1,1); sb.rectTransform.pivot = new Vector2(1,1);
        sb.rectTransform.anchoredPosition = new Vector2(-28,-58); sb.rectTransform.sizeDelta = new Vector2(240, 66);
        statusLight = MakeImage(sb.rectTransform, "light", Ok);
        statusLight.rectTransform.anchorMin = new Vector2(0, 0.5f); statusLight.rectTransform.anchorMax = new Vector2(0, 0.5f);
        statusLight.rectTransform.sizeDelta = new Vector2(20, 20); statusLight.rectTransform.anchoredPosition = new Vector2(22, 0);
        var slbl = MakeText(sb.rectTransform, "l", "SYSTEM STATUS", 11, TextSub, TextAlignmentOptions.BottomLeft);
        Anchor(slbl.rectTransform, 0.2f, 0.5f, 1, 0.88f);
        statusVal = MakeText(sb.rectTransform, "v", "OPTIMAL", 18, Ok, TextAlignmentOptions.TopLeft);
        statusVal.fontStyle = FontStyles.Bold; Anchor(statusVal.rectTransform, 0.2f, 0.08f, 1, 0.52f);

        // ---- two content columns inside the panel ----
        var left = new GameObject("LeftCol", typeof(RectTransform)).GetComponent<RectTransform>();
        left.SetParent(panel.transform, false);
        left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0.52f, 1);
        left.offsetMin = new Vector2(28, 28); left.offsetMax = new Vector2(-8, -210);

        var right = new GameObject("RightCol", typeof(RectTransform)).GetComponent<RectTransform>();
        right.SetParent(panel.transform, false);
        right.anchorMin = new Vector2(0.52f, 0); right.anchorMax = new Vector2(1, 1);
        right.offsetMin = new Vector2(8, 28); right.offsetMax = new Vector2(-28, -210);

        BuildInputsColumn(left);
        BuildOutputsColumn(right);
    }

    void BuildInputsColumn(RectTransform col)
    {
        var inHdr = MakeText(col, "inHdr", "PROCESS INPUTS", 12, TextSub, TextAlignmentOptions.TopLeft);
        Anchor(inHdr.rectTransform, 0, 0.95f, 1, 1); inHdr.characterSpacing = 3;

        var host = new GameObject("Sliders", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
        host.SetParent(col, false); Anchor(host, 0, 0.14f, 1, 0.90f);
        var vlg = host.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 14; vlg.childControlWidth = true; vlg.childForceExpandWidth = true; vlg.childControlHeight = true; vlg.childForceExpandHeight = false; vlg.childAlignment = TextAnchor.UpperCenter;

        ledTemp      = MakeSlider(host, "Kiln temperature", "\u00b0C", 400, 700, 600, v => { model.TempC = v; Recompute(); }, () => model.TempInfo(), () => model.TempStatus);
        ledRetention = MakeSlider(host, "Retention time", "min", 30, 45, 35, v => { model.RetentionMin = v; Recompute(); }, () => model.RetentionInfo(), () => model.RetentionStatus);
        ledFeed      = MakeSlider(host, "Feed rate", "kg/h", 4000, 9000, 6500, v => { model.FeedKgH = v; Recompute(); }, () => model.FeedInfo(), () => model.FeedStatus);
        ledParticle  = MakeSlider(host, "Particle size", "mm", 1, 20, 2, v => { model.ParticleSizeMm = v; Recompute(); }, () => model.ParticleInfo(), () => model.ParticleStatus);

        BuildExplainCard(col);   // additive: live "what's happening" card in the empty space below
    }

    // ---------------- ADDITIVE: live explanation card ----------------
    // Sits in the previously empty white space under the sliders. Purely presentational:
    // it reads ProcessModel and never writes to it.
    TMP_Text explHeadline; Image explHeadDot;
    Image[] explDots = new Image[4]; TMP_Text[] explTexts = new TMP_Text[4]; GameObject[] explRows = new GameObject[4];

    void BuildExplainCard(RectTransform col)
    {
        var card = MakeImage(col, "ExplainCard", TileBg);
        Anchor(card.rectTransform, 0, 0.005f, 1, 0.455f);

        var pad = new GameObject("p", typeof(RectTransform)).GetComponent<RectTransform>();
        pad.SetParent(card.transform, false); Stretch(pad); Inset(pad, 16, 14);

        var hdr = MakeText(pad, "hdr", "WHAT'S HAPPENING", 12, TextSub, TextAlignmentOptions.TopLeft);
        Anchor(hdr.rectTransform, 0, 0.88f, 1, 1f); hdr.characterSpacing = 3;

        // Small Reset button, top-right of the card, aligned with the WHAT'S HAPPENING header.
        // Restores all sliders to the design set-point (one-click recovery from CAUTION/CRITICAL).
        {
            var rb = new GameObject("ResetButton", typeof(RectTransform), typeof(Image), typeof(Button));
            rb.transform.SetParent(pad, false);
            var rt = rb.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(0, 4); rt.sizeDelta = new Vector2(92, 30);
            rb.GetComponent<Image>().color = Accent;
            rb.GetComponent<Button>().onClick.AddListener(ResetToOptimum);
            var rl = MakeText(rt, "lbl", "Reset", 13, Color.white, TextAlignmentOptions.Center); rl.fontStyle = FontStyles.Bold;
        }

        explHeadDot = MakeImage(pad, "hdot", Ok);
        explHeadDot.rectTransform.anchorMin = new Vector2(0, 0.80f); explHeadDot.rectTransform.anchorMax = new Vector2(0, 0.80f);
        explHeadDot.rectTransform.pivot = new Vector2(0, 1); explHeadDot.rectTransform.sizeDelta = new Vector2(9, 9);
        explHeadDot.rectTransform.anchoredPosition = new Vector2(0, -3);

        explHeadline = MakeText(pad, "head", "", 14, TextMain, TextAlignmentOptions.TopLeft);
        explHeadline.fontStyle = FontStyles.Bold; explHeadline.enableWordWrapping = true;
        Anchor(explHeadline.rectTransform, 0, 0.62f, 1, 0.86f);
        explHeadline.rectTransform.offsetMin = new Vector2(16, 0);

        for (int i = 0; i < 4; i++)
        {
            float top = 0.585f - i * 0.148f;
            var row = new GameObject("row" + i, typeof(RectTransform));
            row.transform.SetParent(pad, false);
            var rr = row.GetComponent<RectTransform>();
            Anchor(rr, 0, top - 0.138f, 1, top);
            explRows[i] = row;

            var dot = MakeImage(rr, "d", Ok);
            dot.rectTransform.anchorMin = new Vector2(0, 1); dot.rectTransform.anchorMax = new Vector2(0, 1);
            dot.rectTransform.pivot = new Vector2(0, 1); dot.rectTransform.sizeDelta = new Vector2(7, 7);
            dot.rectTransform.anchoredPosition = new Vector2(1, -5);
            explDots[i] = dot;

            var t = MakeText(rr, "t", "", 12.5f, TextSub, TextAlignmentOptions.TopLeft);
            t.enableWordWrapping = true; Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(16, 0);
            explTexts[i] = t;
        }
    }

    Color StatusColor(ProcessModel.Status s) =>
        s == ProcessModel.Status.Optimal ? Ok : (s == ProcessModel.Status.Caution ? Warn : Crit);

    void UpdateExplainCard()
    {
        if (explHeadline == null) return;
        var ex = model.ExplainNow();
        explHeadline.text = ex.headline;
        explHeadDot.color = StatusColor(ex.level);
        for (int i = 0; i < explRows.Length; i++)
        {
            bool on = i < ex.rows.Count;
            if (explRows[i].activeSelf != on) explRows[i].SetActive(on);
            if (!on) continue;
            explTexts[i].text = ex.rows[i].text;
            explDots[i].color = StatusColor(ex.rows[i].level);
        }
    }

    void BuildOutputsColumn(RectTransform col)
    {
        var outHdr = MakeText(col, "outHdr", "RECOVERY OUTPUT", 12, TextSub, TextAlignmentOptions.TopLeft);
        Anchor(outHdr.rectTransform, 0, 0.95f, 1, 1); outHdr.characterSpacing = 3;

        var effCap = MakeText(col, "ec", "PROCESS EFFICIENCY", 11, TextSub, TextAlignmentOptions.TopLeft);
        Anchor(effCap.rectTransform, 0, 0.88f, 1, 0.93f);
        effNum = MakeText(col, "en", "100", 40, Ok, TextAlignmentOptions.TopLeft); effNum.fontStyle = FontStyles.Bold;
        Anchor(effNum.rectTransform, 0, 0.76f, 0.75f, 0.90f);
        // efficiency unit is baked into the number text in Recompute so it can't drift

        var tanks = new GameObject("tanks", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
        tanks.SetParent(col, false); Anchor(tanks, 0, 0.20f, 1, 0.78f);
        var hlg = tanks.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10; hlg.childControlWidth = true; hlg.childForceExpandWidth = true; hlg.childControlHeight = true; hlg.childForceExpandHeight = true;
        BuildTank(tanks, "Glass fibre", GlassCol, out tankGlass, out pctGlass, out rateGlass);
        BuildTank(tanks, "Oil",         OilCol,   out tankOil,   out pctOil,   out rateOil);
        BuildTank(tanks, "Syngas",      GasCol,   out tankSyngas, out pctSyngas, out rateSyngas);
        BuildTank(tanks, "Char dust",   CharCol,  out tankChar,  out pctChar,  out rateChar);
        BuildTank(tanks, "Losses",      LossCol,  out tankLoss,  out pctLoss,  out rateLoss);

        var q = new GameObject("quality", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
        q.SetParent(col, false); Anchor(q, 0, 0.0f, 1, 0.21f);
        var qh = q.GetComponent<VerticalLayoutGroup>(); qh.spacing = 8; qh.childControlWidth = true; qh.childForceExpandWidth = true; qh.childControlHeight = true; qh.childForceExpandHeight = true;
        purityVal  = BuildMetric(q, "FIBRE PURITY", "99.0%");
        tensileVal = BuildMetric(q, "TENSILE RETENTION", "100%");

        // ---- ADDITIVE: info buttons on the output side (same popup system as the sliders) ----
        AddInfoAt(col, new Vector2(0, 0.885f), new Vector2(0, 0.885f), new Vector2(196, 0),
                  "Process efficiency", () => model.EfficiencyInfo(), () => model.SystemStatus);

        AddTankInfo(tanks, "Glass fibre", () => model.GlassInfo());
        AddTankInfo(tanks, "Oil",         () => model.OilInfo());
        AddTankInfo(tanks, "Syngas",      () => model.SyngasInfo());
        AddTankInfo(tanks, "Char dust",   () => model.CharInfo());
        AddTankInfo(tanks, "Losses",      () => model.LossInfo());

        AddMetricInfo(q, "FIBRE PURITY", "Fibre purity", () => model.PurityInfo());
        AddMetricInfo(q, "TENSILE RETENTION", "Tensile retention", () => model.TensileInfo());
    }

    /// <summary>Places a 20x20 info button at an explicit spot inside a parent rect.</summary>
    void AddInfoAt(RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 offset,
                   string title, System.Func<string> info, System.Func<ProcessModel.Status> status)
    {
        var wrap = new GameObject("iw", typeof(RectTransform)).GetComponent<RectTransform>();
        wrap.SetParent(parent, false);
        wrap.anchorMin = aMin; wrap.anchorMax = aMax; wrap.pivot = new Vector2(1, 1);
        wrap.sizeDelta = new Vector2(20, 20); wrap.anchoredPosition = offset;
        MakeInfoButton(wrap, title, info, status);
    }

    void AddTankInfo(RectTransform tanks, string label, System.Func<string> info)
    {
        var tank = tanks.Find("tank_" + label) as RectTransform;
        if (tank == null) return;
        AddInfoAt(tank, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4, -3),
                  label, info, () => model.SystemStatus);
    }

    void AddMetricInfo(RectTransform q, string key, string title, System.Func<string> info)
    {
        var m = q.Find("m_" + key) as RectTransform;
        if (m == null) return;
        AddInfoAt(m, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-6, -4),
                  title, info, () => model.SystemStatus);
    }

    void BuildTank(Transform parent, string label, Color col, out Image fill, out TMP_Text pct, out TMP_Text rate)
    {
        var tank = MakeImage(parent, "tank_" + label, TileBg);
        var lbl = MakeText(tank.rectTransform, "l", label, 14, TextMain, TextAlignmentOptions.Top);
        lbl.fontStyle = FontStyles.Bold; Anchor(lbl.rectTransform, 0, 0.88f, 1, 0.99f);
        var body = MakeImage(tank.rectTransform, "body", Hex("EEF1F5"));
        body.rectTransform.anchorMin = new Vector2(0.24f, 0.24f); body.rectTransform.anchorMax = new Vector2(0.76f, 0.85f);
        body.rectTransform.offsetMin = Vector2.zero; body.rectTransform.offsetMax = Vector2.zero;
        fill = MakeImage(body.rectTransform, "fill", col);
        fill.rectTransform.anchorMin = new Vector2(0, 0); fill.rectTransform.anchorMax = new Vector2(1, 0.7f);
        fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;
        pct = MakeText(tank.rectTransform, "p", "70%", 18, TextMain, TextAlignmentOptions.Center); pct.fontStyle = FontStyles.Bold;
        Anchor(pct.rectTransform, 0, 0.12f, 1, 0.22f);
        rate = MakeText(tank.rectTransform, "r", "4550 kg/h", 12, TextSub, TextAlignmentOptions.Center);
        Anchor(rate.rectTransform, 0, 0.02f, 1, 0.11f);
    }

    TMP_Text BuildMetric(Transform parent, string label, string val)
    {
        var m = MakeImage(parent, "m_" + label, TileBg);
        var l = MakeText(m.rectTransform, "k", label, 12, TextSub, TextAlignmentOptions.Left);
        Anchor(l.rectTransform, 0.08f, 0, 0.58f, 1);
        var v = MakeText(m.rectTransform, "v", val, 18, Ok, TextAlignmentOptions.Right); v.fontStyle = FontStyles.Bold;
        Anchor(v.rectTransform, 0.55f, 0, 0.92f, 1);
        return v;
    }

    void Recompute()
    {
        SetLed(ledTemp, model.LedTemp);
        SetLed(ledRetention, model.LedRetention);
        SetLed(ledFeed, model.LedFeed);
        SetLed(ledParticle, model.LedParticle);

        int eff = model.EfficiencyPct;
        var st = model.SystemStatus;
        Color sc = st == ProcessModel.Status.Optimal ? Ok : (st == ProcessModel.Status.Caution ? Warn : Crit);
        effNum.text = eff.ToString() + "<size=50%> %</size>"; effNum.color = sc;
        statusLight.color = sc; statusVal.color = sc;
        statusVal.text = st == ProcessModel.Status.Optimal ? "OPTIMAL" : (st == ProcessModel.Status.Caution ? "CAUTION" : "CRITICAL");

        var sp = model.OutputSplit();
        SetTank(tankGlass, pctGlass, rateGlass, sp.GlassPct);
        SetTank(tankOil, pctOil, rateOil, sp.OilPct);
        SetTank(tankSyngas, pctSyngas, rateSyngas, sp.SyngasPct);
        SetTank(tankChar, pctChar, rateChar, sp.CharPct);
        SetTank(tankLoss, pctLoss, rateLoss, sp.LossPct);

        purityVal.text = model.FiberPurityPct.ToString("0.0") + "%";
        purityVal.color = model.FiberPurityPct > 95 ? Ok : (model.FiberPurityPct > 80 ? Warn : Crit);
        tensileVal.text = model.TensileRetentionPct.ToString("0") + "%";
        tensileVal.color = model.TensileRetentionPct > 90 ? Ok : (model.TensileRetentionPct > 70 ? Warn : Crit);

        if (infoPopup != null && infoPopup.activeSelf && liveInfo != null) {
            infoPopupBody.text = liveInfo();
            var s2 = liveStatus();
            infoPopupTitle.color = s2 == ProcessModel.Status.Optimal ? Ok : (s2 == ProcessModel.Status.Caution ? Warn : Crit);
        }

        if (kilnViz != null) { kilnViz.SetHeat(model.TempC); kilnViz.SetRotation(model.RetentionMin); }

        UpdateExplainCard();   // additive: refresh the live explanation card
    }

    void SetTank(Image fill, TMP_Text pct, TMP_Text rate, float pctVal)
    {
        fill.rectTransform.anchorMax = new Vector2(1, Mathf.Clamp01(pctVal / 100f));
        fill.rectTransform.offsetMax = Vector2.zero;
        pct.text = pctVal.ToString("0.0") + "%";
        rate.text = Mathf.RoundToInt(model.FeedKgH * pctVal / 100f).ToString("N0") + " kg/h";
    }

    void SetLed(Image led, ProcessModel.Status s)
    {
        led.color = s == ProcessModel.Status.Optimal ? Ok : (s == ProcessModel.Status.Caution ? Warn : Crit);
    }

    Image MakeSlider(Transform parent, string label, string unit, float min, float max, float val,
                     UnityEngine.Events.UnityAction<float> onChange, System.Func<string> info, System.Func<ProcessModel.Status> status)
    {
        var cell = MakeImage(parent, label + "Cell", TileBg);
        var cle = cell.gameObject.AddComponent<LayoutElement>(); cle.preferredHeight = 78; cle.minHeight = 78;
        var pad = new GameObject("p", typeof(RectTransform)).GetComponent<RectTransform>();
        pad.SetParent(cell.transform, false); Stretch(pad); Inset(pad, 14, 8);

        var led = MakeImage(pad, "led", Ok);
        led.rectTransform.anchorMin = new Vector2(0, 1); led.rectTransform.anchorMax = new Vector2(0, 1); led.rectTransform.pivot = new Vector2(0, 1);
        led.rectTransform.anchoredPosition = new Vector2(0, -4); led.rectTransform.sizeDelta = new Vector2(9, 9);

        var lab = MakeText(pad, "l", label, 13, TextMain, TextAlignmentOptions.TopLeft);
        Anchor(lab.rectTransform, 0, 0.58f, 0.66f, 1); lab.rectTransform.offsetMin = new Vector2(16, 0);
        var valTxt = MakeText(pad, "v", "", 14, Accent, TextAlignmentOptions.TopRight); valTxt.fontStyle = FontStyles.Bold;
        Anchor(valTxt.rectTransform, 0.45f, 0.58f, 0.9f, 1);

        MakeInfoButton(pad, label, info, status);

        var sGO = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        var sr = sGO.GetComponent<RectTransform>(); sr.SetParent(pad, false);
        Anchor(sr, 0, 0.10f, 1, 0.46f);
        var bgImg = MakeImage(sr, "bg", Line); Stretch(bgImg.rectTransform);
        var fillArea = new GameObject("FillArea", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(sr, false); Stretch(fillArea);
        var fill = MakeImage(fillArea, "Fill", Accent); fill.rectTransform.anchorMin = new Vector2(0,0); fill.rectTransform.anchorMax = new Vector2(0,1);
        fill.rectTransform.sizeDelta = new Vector2(8, 0);
        var handleArea = new GameObject("HandleArea", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(sr, false); Stretch(handleArea);
        var handle = MakeImage(handleArea, "Handle", TextMain); handle.rectTransform.sizeDelta = new Vector2(16, 16);

        var sl = sGO.GetComponent<Slider>();
        sl.fillRect = fill.rectTransform; sl.handleRect = handle.rectTransform; sl.targetGraphic = handle;
        sl.direction = Slider.Direction.LeftToRight; sl.minValue = min; sl.maxValue = max; sl.wholeNumbers = false; sl.value = val;
        sl.onValueChanged.AddListener(v => { valTxt.text = FormatVal(v, unit); onChange(v); });
        valTxt.text = FormatVal(val, unit);
        resetTargets.Add((sl, val));   // remember this slider's optimum for the Reset button
        return led;
    }

    // Restores every slider to its design set-point (fires each slider's onValueChanged,
    // which updates the model, LEDs, tanks, quality metrics and kiln automatically).
    void ResetToOptimum()
    {
        foreach (var t in resetTargets)
            t.slider.value = t.optimum;
    }


    string FormatVal(float v, string unit)
    {
        if (unit == "kg/h") return v.ToString("N0");
        if (unit == "%")    return v.ToString("0.0") + "%";
        return v.ToString("0") + " " + unit;
    }

    void MakeInfoButton(Transform parent, string title, System.Func<string> info, System.Func<ProcessModel.Status> status)
    {
        var go = new GameObject("Info", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1,1); rt.anchorMax = new Vector2(1,1); rt.pivot = new Vector2(1,1);
        rt.anchoredPosition = new Vector2(0, -2); rt.sizeDelta = new Vector2(20, 20);
        go.GetComponent<Image>().color = Line;
        var t = MakeText(rt, "i", "i", 13, TextMain, TextAlignmentOptions.Center); t.fontStyle = FontStyles.Bold | FontStyles.Italic;
        go.GetComponent<Button>().onClick.AddListener(() => ToggleInfoPopup(rt, title, info, status));
    }

    GameObject infoPopup; GameObject infoPanel; TMP_Text infoPopupTitle, infoPopupBody; string infoPopupFor;
    System.Func<string> liveInfo; System.Func<ProcessModel.Status> liveStatus;

    void ToggleInfoPopup(RectTransform anchor, string title, System.Func<string> info, System.Func<ProcessModel.Status> status)
    {
        if (infoPopup == null) BuildInfoPopup();
        if (infoPopup.activeSelf && infoPopupFor == title) { infoPopup.SetActive(false); liveInfo = null; return; }
        infoPopupFor = title; liveInfo = info; liveStatus = status;
        infoPopupTitle.text = title; infoPopupBody.text = info();
        var s = status();
        infoPopupTitle.color = s == ProcessModel.Status.Optimal ? Ok : (s == ProcessModel.Status.Caution ? Warn : Crit);
        infoPopup.SetActive(true); infoPopup.transform.SetAsLastSibling();
        var panel = infoPanel.GetComponent<RectTransform>();
        panel.position = anchor.TransformPoint(new Vector3(0, -anchor.rect.height - 2f, 0));
        panel.anchoredPosition += new Vector2(-296, 0);
    }

    void BuildInfoPopup()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>();
        var root = new GameObject("InfoPopup", typeof(RectTransform)); root.transform.SetParent(canvas.transform, false);
        Stretch(root.GetComponent<RectTransform>());
        var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdrop.transform.SetParent(root.transform, false); Stretch(backdrop.GetComponent<RectTransform>());
        backdrop.GetComponent<Image>().color = new Color(0,0,0,0.01f);
        backdrop.GetComponent<Button>().onClick.AddListener(() => { infoPopup.SetActive(false); liveInfo = null; });

        var go = new GameObject("Panel", typeof(RectTransform), typeof(Image)); go.transform.SetParent(root.transform, false);
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(320, 150); rt.pivot = new Vector2(0, 1);
        go.GetComponent<Image>().color = TileBg;
        var sh = go.AddComponent<UnityEngine.UI.Shadow>(); sh.effectColor = new Color(0.1f,0.14f,0.2f,0.25f); sh.effectDistance = new Vector2(0,-5);
        infoPanel = go;
        var pad = new GameObject("p", typeof(RectTransform)).GetComponent<RectTransform>(); pad.SetParent(go.transform, false); Stretch(pad); Inset(pad, 18, 16);
        infoPopupTitle = MakeText(pad, "t", "", 15, Accent, TextAlignmentOptions.TopLeft); infoPopupTitle.fontStyle = FontStyles.Bold; Anchor(infoPopupTitle.rectTransform, 0,0.80f,0.9f,1);
        infoPopupBody = MakeText(pad, "b", "", 13, TextSub, TextAlignmentOptions.TopLeft); infoPopupBody.enableWordWrapping = true; Anchor(infoPopupBody.rectTransform, 0,0,1,0.76f);
        var xgo = new GameObject("x", typeof(RectTransform), typeof(Image), typeof(Button)); xgo.transform.SetParent(go.transform, false);
        var xrt = xgo.GetComponent<RectTransform>(); xrt.anchorMin = new Vector2(1,1); xrt.anchorMax = new Vector2(1,1); xrt.pivot = new Vector2(1,1);
        xrt.anchoredPosition = new Vector2(-8,-8); xrt.sizeDelta = new Vector2(22,22); xgo.GetComponent<Image>().color = Line;
        MakeText(xrt, "xt", "\u00d7", 16, TextMain, TextAlignmentOptions.Center);
        xgo.GetComponent<Button>().onClick.AddListener(() => { infoPopup.SetActive(false); liveInfo = null; });
        root.SetActive(false); infoPopup = root;
    }

    Image MakeImage(Transform parent, string name, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.color = col;
        if (col == TileBg) { var sh = go.AddComponent<UnityEngine.UI.Shadow>(); sh.effectColor = new Color(0.08f,0.12f,0.2f,0.10f); sh.effectDistance = new Vector2(0,-2); }
        return img;
    }

    TMP_Text MakeText(Transform parent, string name, string text, float size, Color col, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size * 1.18f; t.color = col; t.alignment = align; t.raycastTarget = false;
        Stretch(t.rectTransform);
        return t;
    }

    void BuildBackButtons(Transform root)
    {
        // Menu button (existing spec: 150x46, TileBg, label 18 bold) at top-left.
        MakeNavButton(root, "BackButton", "\u2190  Menu", new Vector2(30, -30),
                      TileBg, TextMain, () => SceneManager.LoadScene("MainMenu"));

        // Stage navigation lives on the kiln itself: markers trace the material's
        // path inlet -> body -> outlet (Shredder -> Reactor -> Separation).
        BuildKilnStageMarkers(root);
    }

    // Shared nav-button factory: keeps Menu + stage buttons on one spec (150x46, label 18 bold).
    void MakeNavButton(Transform root, string name, string label, Vector2 pos,
                       Color bg, Color fg, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(root, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(150, 46);
        go.GetComponent<Image>().color = bg;
        go.GetComponent<Button>().onClick.AddListener(onClick);
        var t = MakeText(rt, "lbl", label, 18, fg, TextAlignmentOptions.Center); t.fontStyle = FontStyles.Bold;
    }

    // Stage markers pinned over the kiln (right ~28% of screen). Anchored to the canvas
    // right edge and stacked top->bottom so they trace inlet -> body -> outlet without
    // resolution-fragile world-to-screen math. Each marker loads its stage scene.
    // Stage markers as a horizontal row above the kiln (right side). Left-to-right they
    // trace the material path Shredder -> Reactor -> Separation; Reactor is 'current'.
    // Anchored top-right of the canvas so the row sits over the kiln's horizontal span,
    // below the SYSTEM STATUS pill, leaving the kiln clear beneath.
    // Stage markers as a vertical stack sitting over the kiln zone (right ~28%),
    // horizontally centred on the kiln and running top->bottom to trace the material
    // path Shredder -> Reactor -> Separation. Reactor is 'current'. Pills are wide so
    // names never wrap; anchored to canvas centre-x of the kiln zone.
    // Stage markers as a large vertical stack occupying the kiln zone (right ~28%),
    // vertically centred to share the kiln's centre and horizontally centred on it.
    // Three EQUAL doorways (no 'current' state) - this dashboard is a navigation hub:
    // the user jumps to any stage and comes back. Top->bottom traces the material path
    // Shredder -> Reactor -> Separation.
    // Stage markers as a large vertical stack in the empty band just LEFT of the kiln
    // (between the output panel and the kiln), vertically centred to line up with the
    // kiln's height without covering it. Three EQUAL doorways (no 'current' state) -
    // this dashboard is a navigation hub: the user jumps to any stage and comes back.
    // Top->bottom traces the material path Shredder -> Reactor -> Separation.
    // Stage markers as a large vertical stack sitting on TOP of the kiln zone (right ~28%).
    // The kiln drum sits low, so the stack is anchored to the top of that zone and centred
    // on the kiln's x - it sits above the drum, not covering it. Three EQUAL doorways (no
    // 'current' state): this dashboard is a navigation hub - jump to any stage and come back.
    // Top->bottom traces the material path Shredder -> Reactor -> Separation.
    // Stage markers as a large vertical stack sitting on TOP of the kiln zone (right ~28%),
    // centred on the kiln's x and anchored to the top so they sit above the drum.
    // Three EQUAL doorways (no 'current' state): this dashboard is a navigation hub -
    // jump to any stage and come back. Top->bottom traces Shredder -> Reactor -> Separation.
    // Stage markers as a large vertical stack sitting high on the kiln zone (right ~28%),
    // centred on the kiln's x and anchored near the top, well clear of the drum below.
    // Three EQUAL doorways (no 'current' state): this dashboard is a navigation hub -
    // jump to any stage and come back. Top->bottom traces Shredder -> Reactor -> Separation.
    void BuildKilnStageMarkers(Transform root)
    {
        // x ~= 0.86 = kiln centre; anchored high so markers sit above the drum with clearance.
        var col = new GameObject("KilnStageStack", typeof(RectTransform)).GetComponent<RectTransform>();
        col.SetParent(root, false);
        col.anchorMin = new Vector2(0.86f, 1); col.anchorMax = new Vector2(0.86f, 1); col.pivot = new Vector2(0.5f, 1);
        col.sizeDelta = new Vector2(240, 320); col.anchoredPosition = new Vector2(0, -40);

        // Evenly spaced down the stack (pill height 84, pitch 108).
        MakeStageMarker(col, "Mk2", "2", "Shredder",  "Feed inlet", new Vector2(0,  -42),
                        () => SceneManager.LoadScene("Stage2_StoryMode"));
        MakeStageMarker(col, "Mk3", "3", "Reactor",   "Pyrolysis kiln", new Vector2(0, -150),
                        () => SceneManager.LoadScene("Stage3_StoryMode"));
        MakeStageMarker(col, "Mk4", "4", "Separation", "Discharge", new Vector2(0, -258),
                        () => SceneManager.LoadScene("Stage4_StoryMode"));
    }

    // One kiln marker = numbered circle pin + a label pill (name + sublabel).
    // current=true renders it accent-filled (the reactor this dashboard controls).
    // One stage marker for the horizontal row: numbered pin + name + sublabel.
    // current=true renders accent-filled (the reactor this dashboard controls).
    // One stage marker: numbered pin (left) + name + sublabel, placed at pos in the stack.
    // current=true renders accent-filled (the reactor this dashboard controls). Wide pill
    // (188px) so 'Separation' etc. never wrap.
    // One stage marker: numbered pin (left) + name + sublabel, placed at pos in the stack.
    // All markers are equal doorways (no current state). Large pill (210x84) sized to sit
    // alongside the kiln; names never wrap.
    void MakeStageMarker(Transform col, string name, string num, string title, string sub,
                         Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(col, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(210, 84); rt.anchoredPosition = pos;
        go.GetComponent<Image>().color = TileBg;
        go.GetComponent<Button>().onClick.AddListener(onClick);

        // Numbered circle pin on the left of the pill.
        var pin = MakeImage(rt, "pin", Accent);
        pin.rectTransform.anchorMin = new Vector2(0, 0.5f); pin.rectTransform.anchorMax = new Vector2(0, 0.5f);
        pin.rectTransform.pivot = new Vector2(0, 0.5f);
        pin.rectTransform.sizeDelta = new Vector2(40, 40); pin.rectTransform.anchoredPosition = new Vector2(14, 0);
        var pn = MakeText(pin.rectTransform, "n", num, 18, Color.white, TextAlignmentOptions.Center);
        pn.fontStyle = FontStyles.Bold;

        // Title + sublabel stacked to the right of the pin.
        var tt = MakeText(rt, "t", title, 18, TextMain, TextAlignmentOptions.Left); tt.fontStyle = FontStyles.Bold;
        tt.enableWordWrapping = false; tt.overflowMode = TextOverflowModes.Overflow;
        Anchor(tt.rectTransform, 0, 0.46f, 1, 0.94f); tt.rectTransform.offsetMin = new Vector2(66, 0); tt.rectTransform.offsetMax = new Vector2(-12, 0);
        var st = MakeText(rt, "s", sub, 12.5f, TextSub, TextAlignmentOptions.Left);
        st.enableWordWrapping = false; st.overflowMode = TextOverflowModes.Overflow;
        Anchor(st.rectTransform, 0, 0.08f, 1, 0.48f); st.rectTransform.offsetMin = new Vector2(66, 0); st.rectTransform.offsetMax = new Vector2(-12, 0);
    }



    void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }

    static void Stretch(RectTransform r){ r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=Vector2.zero; r.offsetMax=Vector2.zero; }
    static void Inset(RectTransform r, float x, float y){ r.offsetMin=new Vector2(x,y); r.offsetMax=new Vector2(-x,-y); }
    static void Anchor(RectTransform r, float xmin,float ymin,float xmax,float ymax){ r.anchorMin=new Vector2(xmin,ymin); r.anchorMax=new Vector2(xmax,ymax); r.offsetMin=Vector2.zero; r.offsetMax=Vector2.zero; }
    static Color Hex(string h){ ColorUtility.TryParseHtmlString("#"+h, out var c); return c; }
}
