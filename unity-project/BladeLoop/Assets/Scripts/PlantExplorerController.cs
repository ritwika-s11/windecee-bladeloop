using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Self-building interactive dashboard for the BladeLoop pyrolysis plant.
/// Attach to a single empty GameObject in PlantExplorer.unity — it constructs
/// its entire Canvas/UI in code on Awake and drives every value live from
/// PlantModel. No prefabs, no scene wiring required.
///
/// Design principles (per team handover):
///  - Motion is meaningful only: metric numbers lerp to new values, the verdict
///    banner eases between green/red. No bounce/sparkle.
///  - All animation uses Time.unscaledDeltaTime, so it never freezes if a
///    stage's pause sets Time.timeScale = 0 (matches ExploreOrbitCamera).
/// </summary>
public class PlantExplorerController : MonoBehaviour
{
    static Color PanelBg, TileBg, Accent, TextMain, TextSub, Hot, Good, Bad, GlassCol, GasCol, CharCol;
    static bool paletteReady;
    static void InitPalette()
    {
        if (paletteReady) return;
        PanelBg  = Hex("F5F6F8"); TileBg = Hex("FFFFFF"); Accent = Hex("2563EB");
        TextMain = Hex("1E293B"); TextSub = Hex("475569"); Hot    = Hex("D97706");;
        Good     = Hex("16A34A"); Bad    = Hex("DC2626");
        GlassCol = Hex("3B82F6"); GasCol = Hex("D97706"); CharCol = Hex("6B7280");;
        paletteReady = true;
    }

    PlantModel model = new PlantModel();

    TMP_Text feedVal, kilnVal, burnerVal, co2Val;
    TMP_Text verdictTitle, verdictSub, verdictNum;
    Image    verdictBg;
    Image    glassSeg, gasSeg, charSeg;
    TMP_Text glassLbl, gasLbl, charLbl;

    float aFeed, aKiln, aBurner, aCo2;
    float tFeed, tKiln, tBurner, tCo2;
    Color verdictTarget;
    KilnVisualizer kilnViz;

    void Awake()
    {
        InitPalette();
        EnsureEventSystem();
        var canvas = BuildCanvas();
        BuildUI(canvas.transform);
        Recompute();
        kilnViz = Object.FindFirstObjectByType<KilnVisualizer>();
        aFeed = tFeed; aKiln = tKiln; aBurner = tBurner; aCo2 = tCo2;;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime * 6f;
        aFeed   = Mathf.Lerp(aFeed,   tFeed,   dt);
        aKiln   = Mathf.Lerp(aKiln,   tKiln,   dt);
        aBurner = Mathf.Lerp(aBurner, tBurner, dt);
        aCo2    = Mathf.Lerp(aCo2,    tCo2,    dt);

        feedVal.text   = Mathf.RoundToInt(aFeed).ToString("N0");
        burnerVal.text = Mathf.RoundToInt(aBurner).ToString("N0");
        co2Val.text    = Mathf.RoundToInt(aCo2).ToString("N0");
        kilnVal.text   = aKiln.ToString("0.0") + " \u00d7 " + (aKiln * (float)model.LengthToDiameter).ToString("0.0");

        verdictBg.color = Color.Lerp(verdictBg.color, verdictTarget, dt);
    }

    void Recompute()
    {
        var o = model.Compute();
        double co2 = model.Co2AvoidedTonnesYr(o.ElectricalKW, PlantModel.GridDE);

        tFeed   = (float)o.FeedRateKgH;
        tKiln   = (float)o.KilnDiameterM;
        tBurner = (float)o.GrossBurnerKW;
        tCo2    = (float)co2;

        bool ok = o.IsEnergyAutonomous;
        verdictTarget = ok ? Good : Bad;
        verdictTitle.text = ok ? "Energy autonomy achieved" : "Autonomy lost \u2014 plant draws from grid";
        verdictSub.text   = "Generates " + o.ElectricalKW.ToString("N0") +
                            " kW \u00b7 shredders need " + o.ShredderLoadKW.ToString("N0") + " kW";
        verdictNum.text   = (o.NetElectricalMarginKW >= 0 ? "+" : "") +
                            o.NetElectricalMarginKW.ToString("N0") + " kW";

        float total = (float)(o.GlassTonnesYr + o.SyngasTonnesYr + o.CharTonnesYr);
        SetFlex(glassSeg, (float)o.GlassTonnesYr  / total);
        SetFlex(gasSeg,   (float)o.SyngasTonnesYr / total);
        SetFlex(charSeg,  (float)o.CharTonnesYr   / total);
        glassLbl.text = "Glass " + (o.GlassTonnesYr / 1000.0).ToString("0.0") + "k t";
        gasLbl.text   = "Gas "   + (o.SyngasTonnesYr / 1000.0).ToString("0.0") + "k t";
        charLbl.text  = "Char "  + (o.CharTonnesYr / 1000.0).ToString("0.0") + "k t";

        if (kilnViz != null) { kilnViz.SetHeat((float)model.PyrolysisTempC); kilnViz.SetRotation((float)model.RetentionMinutes); }
    }

    Canvas BuildCanvas()
    {
        var go = new GameObject("PlantExplorerCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        var s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.matchWidthOrHeight = 0.5f;
        return c;
    }

    void BuildUI(Transform root)
    {
        var bg = MakeImage(root, "BG", PanelBg); bg.raycastTarget = false;
        bg.rectTransform.anchorMin = new Vector2(0f, 0f); bg.rectTransform.anchorMax = new Vector2(0.70f, 1f);;
        bg.rectTransform.offsetMin = Vector2.zero; bg.rectTransform.offsetMax = Vector2.zero;
        BuildBackButton(root);

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
        content.SetParent(root, false);
        content.anchorMin = new Vector2(0.35f, 1f); content.anchorMax = new Vector2(0.35f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = new Vector2(0, -70);
        content.sizeDelta = new Vector2(1080, 0);
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 30; vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
        var csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var title = MakeText(content, "Title", "PLANT EXPLORER", 40, Accent, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold; SetH(title.rectTransform, 56);
        var sub = MakeText(content, "Sub", "Live parametric model \u00b7 change an input, the whole plant recomputes", 18, TextSub, TextAlignmentOptions.Center);
        SetH(sub.rectTransform, 26);

        var sliders = MakeRow(content, "Sliders", 140);
        MakeSlider(sliders, "Capacity", "t/yr", 20000, 100000, 52000, v => { model.AnnualCapacityTonnes = v; Recompute(); }, "Annual throughput of blade material. It's the master lever \u2014 raising it increases the feed rate, which cascades into a bigger kiln, more burner demand and a tighter energy margin. Baseline 52,000 t/yr.");
        MakeSlider(sliders, "Pyrolysis", "\u00b0C", 550, 660, 600, v => { model.PyrolysisTempC = v; Recompute(); }, "Sets the kiln reactor temperature. Higher temperature cracks the resin faster and makes the kiln glow hotter, but adds thermal stress on the glass fibres and raises burner demand. 600 \u00b0C is the balance point.");
        MakeSlider(sliders, "WHRB eff", "%", 10, 35, 22, v => { model.WHRBEfficiency = v / 100.0; Recompute(); }, "How efficiently the Waste Heat Recovery Boiler turns leftover heat into electricity. This decides whether the plant powers itself \u2014 below about 20% the shredders can't be run on-site and the plant draws from the grid. Baseline 22%.");
        MakeSlider(sliders, "Retention", "min", 10, 60, 30, v => { model.RetentionMinutes = v; Recompute(); }, "How long material stays inside the kiln. Longer retention needs a physically bigger kiln and more fuel; too short and the resin doesn't fully crack. Drives the drum rotation speed. Baseline 30 min.");

        var banner = MakeImage(content, "Verdict", Good); SetH(banner.rectTransform, 92);
        var bl = new GameObject("V", typeof(RectTransform)).GetComponent<RectTransform>();
        bl.SetParent(banner.transform, false); Stretch(bl); Inset(bl, 24, 16);
        verdictBg = banner;
        verdictTitle = MakeText(bl, "VT", "", 22, Hex("F4FFF4"), TextAlignmentOptions.TopLeft); verdictTitle.fontStyle = FontStyles.Bold;
        Anchor(verdictTitle.rectTransform, 0,1,0.7f,1);
        verdictSub = MakeText(bl, "VS", "", 15, Hex("DDF0DD"), TextAlignmentOptions.BottomLeft);
        Anchor(verdictSub.rectTransform, 0,0,0.7f,0.55f);
        verdictNum = MakeText(bl, "VN", "", 30, Hex("F4FFF4"), TextAlignmentOptions.MidlineRight); verdictNum.fontStyle = FontStyles.Bold;
        Anchor(verdictNum.rectTransform, 0.7f,0,1,1);

        var cards = MakeRow(content, "Cards", 150);
        feedVal   = MakeCard(cards, "FEED RATE",   "kg/h");
        kilnVal   = MakeCard(cards, "KILN DRUM",   "m \u2300 \u00d7 length");
        burnerVal = MakeCard(cards, "BURNER",      "kW gross");
        co2Val    = MakeCard(cards, "CO<sub>2</sub> AVOIDED", "t/yr \u00b7 DE grid");

        var massCard = MakeImage(content, "MassCard", TileBg); SetH(massCard.rectTransform, 140);
        var mc = new GameObject("mc", typeof(RectTransform)).GetComponent<RectTransform>();
        mc.SetParent(massCard.transform, false); Stretch(mc); Inset(mc, 20, 16);
        var mt = MakeText(mc, "mt", "MASS BALANCE", 15, TextSub, TextAlignmentOptions.TopLeft);
        Anchor(mt.rectTransform, 0,0.72f,1,1);
        var barRow = new GameObject("bar", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
        barRow.SetParent(mc, false); Anchor(barRow, 0,0.32f,1,0.64f);
        var hlg = barRow.GetComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = true; hlg.childForceExpandWidth = true; hlg.spacing = 3;
        glassSeg = MakeSeg(barRow, GlassCol); gasSeg = MakeSeg(barRow, GasCol); charSeg = MakeSeg(barRow, CharCol);
        var lblRow = new GameObject("lbls", typeof(RectTransform)).GetComponent<RectTransform>();
        lblRow.SetParent(mc, false); Anchor(lblRow, 0,0,1,0.28f);
        glassLbl = MakeText(lblRow, "g", "", 13, GlassCol, TextAlignmentOptions.Left);   Anchor(glassLbl.rectTransform, 0,0,0.33f,1);
        gasLbl   = MakeText(lblRow, "a", "", 13, GasCol,   TextAlignmentOptions.Center); Anchor(gasLbl.rectTransform, 0.33f,0,0.66f,1);
        charLbl  = MakeText(lblRow, "c", "", 13, CharCol,  TextAlignmentOptions.Right);  Anchor(charLbl.rectTransform, 0.66f,0,1,1);

        // Separation moved to its own Stage 4 dashboard (SeparationExplorer).
    }

    RectTransform MakeRow(Transform parent, string name, float height)
    {
        var r = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        var h = r.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 16; h.childControlWidth = true; h.childForceExpandWidth = true;
        h.childControlHeight = true; h.childForceExpandHeight = true;
        SetH(r, height);
        return r;
    }

    Image MakeSeg(Transform parent, Color col)
    {
        var img = MakeImage(parent, "seg", col);
        img.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        return img;
    }
    void SetFlex(Image seg, float frac)
    {
        var le = seg.GetComponent<LayoutElement>();
        le.flexibleWidth = Mathf.Max(0.001f, frac);
    }

    void MakeSlider(Transform parent, string label, string unit, float min, float max, float val, UnityEngine.Events.UnityAction<float> onChange, string info = null)
    {
        var cell = MakeImage(parent, label + "Cell", TileBg);
        var pad = new GameObject("p", typeof(RectTransform)).GetComponent<RectTransform>();
        pad.SetParent(cell.transform, false); Stretch(pad); Inset(pad, 14, 10);

        var lab = MakeText(pad, "l", label, 15, TextMain, TextAlignmentOptions.TopLeft);
        Anchor(lab.rectTransform, 0,0.62f,0.55f,1);
        var valTxt = MakeText(pad, "v", "", 15, Accent, TextAlignmentOptions.TopRight); valTxt.fontStyle = FontStyles.Bold;
        Anchor(valTxt.rectTransform, 0.4f,0.62f,0.86f,1);
        var unitTxt = MakeText(pad, "u", unit, 12, TextSub, TextAlignmentOptions.BottomLeft);
        Anchor(unitTxt.rectTransform, 0,0,1,0.32f);

        if (!string.IsNullOrEmpty(info)) MakeInfoButton(pad, label, info);

        var sGO = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        var sr = sGO.GetComponent<RectTransform>(); sr.SetParent(pad, false);
        Anchor(sr, 0,0.30f,1,0.60f);
        var bgImg = MakeImage(sr, "bg", Hex("E2E8F0")); Stretch(bgImg.rectTransform);
        var fillArea = new GameObject("FillArea", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(sr, false); Stretch(fillArea);
        var fill = MakeImage(fillArea, "Fill", Accent); fill.rectTransform.anchorMin = new Vector2(0,0); fill.rectTransform.anchorMax = new Vector2(0,1);
        fill.rectTransform.sizeDelta = new Vector2(10, 0);
        var handleArea = new GameObject("HandleArea", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(sr, false); Stretch(handleArea);
        var handle = MakeImage(handleArea, "Handle", TextMain);
        handle.rectTransform.sizeDelta = new Vector2(16, 16);

        var sl = sGO.GetComponent<Slider>();
        sl.fillRect = fill.rectTransform;
        sl.handleRect = handle.rectTransform;
        sl.targetGraphic = handle;
        sl.direction = Slider.Direction.LeftToRight;
        sl.minValue = min; sl.maxValue = max; sl.wholeNumbers = false; sl.value = val;
        sl.onValueChanged.AddListener(v => { valTxt.text = FormatVal(v, unit); onChange(v); });
        valTxt.text = FormatVal(val, unit);
    }

    void MakeInfoButton(Transform parent, string title, string info)
    {
        var go = new GameObject("Info", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1,1); rt.anchorMax = new Vector2(1,1); rt.pivot = new Vector2(1,1);
        rt.anchoredPosition = new Vector2(0, 2); rt.sizeDelta = new Vector2(22, 22);
        var img = go.GetComponent<Image>(); img.color = Hex("E2E8F0");
        var t = MakeText(rt, "i", "i", 14, TextSub, TextAlignmentOptions.Center); t.fontStyle = FontStyles.Bold | FontStyles.Italic;
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(() => ToggleInfoPopup(rt, title, info));
    }

    GameObject infoPopup;
    TMP_Text infoPopupTitle, infoPopupBody;
    string infoPopupFor;

    void ToggleInfoPopup(RectTransform anchor, string title, string body)
    {
        if (infoPopup == null) BuildInfoPopup();
        if (infoPopup.activeSelf && infoPopupFor == title) { infoPopup.SetActive(false); return; }
        infoPopupFor = title;
        infoPopupTitle.text = title;
        infoPopupBody.text = body;
        infoPopup.SetActive(true);
        infoPopup.transform.SetAsLastSibling();
        // position the floating panel just below the info button
        var panel = infoPanel.GetComponent<RectTransform>();
        panel.position = anchor.TransformPoint(new Vector3(0, -anchor.rect.height - 2f, 0));
        // nudge left so the 320-wide panel stays on-screen under the button
        panel.anchoredPosition += new Vector2(-296, 0);
    }

    GameObject infoPanel;

    void BuildInfoPopup()
    {
        var canvas = Object.FindFirstObjectByType<Canvas>();

        // Root holds a full-screen invisible backdrop (click to dismiss) + the panel.
        var root = new GameObject("InfoPopup", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        var rrt = root.GetComponent<RectTransform>(); Stretch(rrt);

        var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdrop.transform.SetParent(root.transform, false);
        Stretch(backdrop.GetComponent<RectTransform>());
        backdrop.GetComponent<Image>().color = new Color(0,0,0,0.01f); // nearly invisible but catches clicks
        backdrop.GetComponent<Button>().onClick.AddListener(() => infoPopup.SetActive(false));

        // Floating panel
        var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(320, 160);
        rt.pivot = new Vector2(0, 1);
        var img = go.GetComponent<Image>(); img.color = Hex("1E293B");
        var sh = go.AddComponent<UnityEngine.UI.Shadow>();
        sh.effectColor = new Color(0,0,0,0.4f); sh.effectDistance = new Vector2(0,-5);
        infoPanel = go;

        var pad = new GameObject("p", typeof(RectTransform)).GetComponent<RectTransform>();
        pad.SetParent(go.transform, false); Stretch(pad); Inset(pad, 18, 16);
        infoPopupTitle = MakeText(pad, "t", "", 16, Hex("FFFFFF"), TextAlignmentOptions.TopLeft);
        infoPopupTitle.fontStyle = FontStyles.Bold; Anchor(infoPopupTitle.rectTransform, 0,0.82f,0.9f,1);
        infoPopupBody = MakeText(pad, "b", "", 13.5f, Hex("CBD5E1"), TextAlignmentOptions.TopLeft);
        infoPopupBody.enableWordWrapping = true; Anchor(infoPopupBody.rectTransform, 0,0,1,0.78f);

        var xgo = new GameObject("x", typeof(RectTransform), typeof(Image), typeof(Button));
        xgo.transform.SetParent(go.transform, false);
        var xrt = xgo.GetComponent<RectTransform>();
        xrt.anchorMin = new Vector2(1,1); xrt.anchorMax = new Vector2(1,1); xrt.pivot = new Vector2(1,1);
        xrt.anchoredPosition = new Vector2(-8,-8); xrt.sizeDelta = new Vector2(22,22);
        xgo.GetComponent<Image>().color = Hex("334155");
        MakeText(xrt, "xt", "\u00d7", 16, Hex("FFFFFF"), TextAlignmentOptions.Center);
        xgo.GetComponent<Button>().onClick.AddListener(() => infoPopup.SetActive(false));

        root.SetActive(false);
        infoPopup = root;
    }


    string FormatVal(float v, string unit)
    {
        if (unit == "t/yr") return (v/1000f).ToString("0") + "k";
        if (unit == "%")    return v.ToString("0") + "%";
        if (unit.StartsWith("m/s")) return (v/1000f).ToString("0.000");
        return v.ToString("0");
    }

    TMP_Text MakeCard(Transform parent, string label, string unit)
    {
        var card = MakeImage(parent, label + "Card", TileBg);
        var pad = new GameObject("p", typeof(RectTransform)).GetComponent<RectTransform>();
        pad.SetParent(card.transform, false); Stretch(pad); Inset(pad, 16, 14);
        var lab = MakeText(pad, "l", label, 14, TextSub, TextAlignmentOptions.TopLeft);
        Anchor(lab.rectTransform, 0,0.7f,1,1);
        var val = MakeText(pad, "v", "0", 34, TextMain, TextAlignmentOptions.Left); val.fontStyle = FontStyles.Bold;
        val.enableAutoSizing = true; val.fontSizeMax = 34; val.fontSizeMin = 15; val.enableWordWrapping = false;
        Anchor(val.rectTransform, 0,0.28f,1,0.72f);
        var u = MakeText(pad, "u", unit, 12, TextSub, TextAlignmentOptions.BottomLeft);
        Anchor(u.rectTransform, 0,0,1,0.28f);
        return val;
    }

    Image MakeImage(Transform parent, string name, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.color = col;
        if (col == TileBg) {
            var sh = go.AddComponent<UnityEngine.UI.Shadow>();
            sh.effectColor = new Color(0.08f, 0.12f, 0.2f, 0.13f);
            sh.effectDistance = new Vector2(0f, -3f);
        }
        return img;
    }

    TMP_Text MakeText(Transform parent, string name, string text, float size, Color col, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size * 1.18f; t.color = col; t.alignment = align;
        t.raycastTarget = false;
        Stretch(t.rectTransform);
        return t;
    }

    void BuildBackButton(Transform root)
    {
        var go = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(root, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(30, -30); rt.sizeDelta = new Vector2(150, 46);
        var img = go.GetComponent<Image>(); img.color = TileBg;
        var btn = go.GetComponent<Button>();
        var cb = btn.colors;
        cb.normalColor = TileBg; cb.highlightedColor = Accent; cb.pressedColor = new Color(0.23f,0.39f,0.8f,1f);
        cb.selectedColor = TileBg; cb.fadeDuration = 0.15f;
        btn.colors = cb;
        btn.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));
        var t = MakeText(rt, "lbl", "\u2190  Menu", 18, TextMain, TextAlignmentOptions.Center);
        t.fontStyle = FontStyles.Bold;

        // Cross-link to the Separation Explorer (Stage 4 dashboard)
        var sgo = new GameObject("SeparationLink", typeof(RectTransform), typeof(Image), typeof(Button));
        sgo.transform.SetParent(root, false);
        var srt = sgo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(0, 1); srt.pivot = new Vector2(0, 1);
        srt.anchoredPosition = new Vector2(190, -30); srt.sizeDelta = new Vector2(200, 46);
        sgo.GetComponent<Image>().color = Accent;
        var sbtn = sgo.GetComponent<Button>();
        sbtn.onClick.AddListener(() => SceneManager.LoadScene("SeparationExplorer"));
        var stx = MakeText(srt, "lbl", "Separation \u2192", 18, Hex("FFFFFF"), TextAlignmentOptions.Center);
        stx.fontStyle = FontStyles.Bold;
    }

        void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }

    static void Stretch(RectTransform r){ r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=Vector2.zero; r.offsetMax=Vector2.zero; }
    static void Inset(RectTransform r, float x, float y){ r.offsetMin=new Vector2(x,y); r.offsetMax=new Vector2(-x,-y); }
    static void SetH(RectTransform r, float h){ var le = r.GetComponent<LayoutElement>(); if (le==null) le = r.gameObject.AddComponent<LayoutElement>(); le.preferredHeight=h; le.minHeight=h; }
    static void Anchor(RectTransform r, float xmin,float ymin,float xmax,float ymax){ r.anchorMin=new Vector2(xmin,ymin); r.anchorMax=new Vector2(xmax,ymax); r.offsetMin=Vector2.zero; r.offsetMax=Vector2.zero; }
    static Color Hex(string h){ ColorUtility.TryParseHtmlString("#"+h, out var c); return c; }
}
