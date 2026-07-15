using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

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
        PanelBg  = Hex("0A0E1A"); TileBg = Hex("10162A"); Accent = Hex("4A7CFF");
        TextMain = Hex("FFFFFF"); TextSub = Hex("B0B8C7"); Hot    = Hex("FF8B15");
        Good     = Hex("3FB56B"); Bad    = Hex("E5534B");
        GlassCol = Hex("6FA8DC"); GasCol = Hex("E8926B"); CharCol = Hex("9AA0A6");
        paletteReady = true;
    }

    PlantModel model = new PlantModel();

    TMP_Text feedVal, kilnVal, burnerVal, co2Val;
    TMP_Text verdictTitle, verdictSub, verdictNum;
    Image    verdictBg;
    Image    glassSeg, gasSeg, charSeg;
    TMP_Text glassLbl, gasLbl, charLbl;
    RectTransform sepMarker;
    TMP_Text sepMsg;

    float aFeed, aKiln, aBurner, aCo2;
    float tFeed, tKiln, tBurner, tCo2;
    Color verdictTarget;

    void Awake()
    {
        InitPalette();
        EnsureEventSystem();
        var canvas = BuildCanvas();
        BuildUI(canvas.transform);
        Recompute();
        aFeed = tFeed; aKiln = tKiln; aBurner = tBurner; aCo2 = tCo2;
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

        float f = (float)model.FluidizingVelocity;
        float pct = Mathf.Clamp01(f / 0.05f);
        sepMarker.anchorMin = new Vector2(pct, 0);
        sepMarker.anchorMax = new Vector2(pct, 1);
        sepMarker.anchoredPosition = Vector2.zero;
        if (f < PlantModel.CharTerminalVelocity) {
            sepMsg.text = "Gas too slow \u2014 char settles with the glass. Contaminated product.";
            sepMsg.color = Bad;
        } else if (f > PlantModel.GlassTerminalVelocity) {
            sepMsg.text = "Gas too fast \u2014 glass fibres blow out with the char. Yield lost.";
            sepMsg.color = Bad;
        } else {
            sepMsg.text = "Char lifts out, glass falls through. Clean separation.";
            sepMsg.color = Good;
        }
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
        var bg = MakeImage(root, "BG", PanelBg);
        Stretch(bg.rectTransform);

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
        content.SetParent(root, false);
        content.anchorMin = new Vector2(0.5f, 1); content.anchorMax = new Vector2(0.5f, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = new Vector2(0, -40);
        content.sizeDelta = new Vector2(1500, 0);
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 18; vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true; vlg.childForceExpandHeight = false;

        var title = MakeText(content, "Title", "PLANT EXPLORER", 40, Accent, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold; SetH(title.rectTransform, 56);
        var sub = MakeText(content, "Sub", "Live parametric model \u00b7 change an input, the whole plant recomputes", 18, TextSub, TextAlignmentOptions.Center);
        SetH(sub.rectTransform, 26);

        var sliders = MakeRow(content, "Sliders", 120);
        MakeSlider(sliders, "Capacity", "t/yr", 20000, 100000, 52000, v => { model.AnnualCapacityTonnes = v; Recompute(); });
        MakeSlider(sliders, "Pyrolysis", "\u00b0C", 550, 660, 600, v => { model.PyrolysisTempC = v; Recompute(); });
        MakeSlider(sliders, "WHRB eff", "%", 10, 35, 22, v => { model.WHRBEfficiency = v / 100.0; Recompute(); });
        MakeSlider(sliders, "Retention", "min", 10, 60, 30, v => { model.RetentionMinutes = v; Recompute(); });

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

        var cards = MakeRow(content, "Cards", 130);
        feedVal   = MakeCard(cards, "FEED RATE",   "kg/h");
        kilnVal   = MakeCard(cards, "KILN DRUM",   "m \u2300 \u00d7 length");
        burnerVal = MakeCard(cards, "BURNER",      "kW gross");
        co2Val    = MakeCard(cards, "CO\u2082 AVOIDED", "t/yr \u00b7 DE grid");

        var massCard = MakeImage(content, "MassCard", TileBg); SetH(massCard.rectTransform, 110);
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

        var sepCard = MakeImage(content, "SepCard", TileBg); SetH(sepCard.rectTransform, 130);
        var sc = new GameObject("sc", typeof(RectTransform)).GetComponent<RectTransform>();
        sc.SetParent(sepCard.transform, false); Stretch(sc); Inset(sc, 20, 14);
        var st = MakeText(sc, "st", "SEPARATION WINDOW  (char 0.0032  <  gas  <  glass 0.0368 m/s)", 15, TextSub, TextAlignmentOptions.TopLeft);
        Anchor(st.rectTransform, 0,0.72f,1,1);
        var track = MakeImage(sc, "track", Hex("05070E")); Anchor(track.rectTransform, 0,0.42f,1,0.6f);
        var safe = MakeImage(track.rectTransform, "safe", Hex("173A25"));
        safe.rectTransform.anchorMin = new Vector2(0.0032f/0.05f, 0);
        safe.rectTransform.anchorMax = new Vector2(0.0368f/0.05f, 1);
        safe.rectTransform.offsetMin = Vector2.zero; safe.rectTransform.offsetMax = Vector2.zero;
        var marker = MakeImage(track.rectTransform, "marker", TextMain);
        marker.rectTransform.sizeDelta = new Vector2(4, 14);
        sepMarker = marker.rectTransform;
        var slRow = MakeRow(sc, "sepSlider", 44); Anchor(slRow, 0,0.02f,1,0.36f);
        MakeSlider(slRow, "Fluidizing", "m/s\u00d71000", 1, 50, 15, v => { model.FluidizingVelocity = v/1000.0; Recompute(); });
        sepMsg = MakeText(sc, "sepMsg", "", 14, Good, TextAlignmentOptions.Left);
        Anchor(sepMsg.rectTransform, 0,0,1,0.02f); sepMsg.rectTransform.sizeDelta = new Vector2(0,22);
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

    void MakeSlider(Transform parent, string label, string unit, float min, float max, float val, UnityEngine.Events.UnityAction<float> onChange)
    {
        var cell = MakeImage(parent, label + "Cell", TileBg);
        var pad = new GameObject("p", typeof(RectTransform)).GetComponent<RectTransform>();
        pad.SetParent(cell.transform, false); Stretch(pad); Inset(pad, 14, 10);

        var lab = MakeText(pad, "l", label, 15, TextMain, TextAlignmentOptions.TopLeft);
        Anchor(lab.rectTransform, 0,0.62f,0.6f,1);
        var valTxt = MakeText(pad, "v", "", 15, Accent, TextAlignmentOptions.TopRight); valTxt.fontStyle = FontStyles.Bold;
        Anchor(valTxt.rectTransform, 0.4f,0.62f,1,1);
        var unitTxt = MakeText(pad, "u", unit, 12, TextSub, TextAlignmentOptions.BottomLeft);
        Anchor(unitTxt.rectTransform, 0,0,1,0.32f);

        var sGO = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        var sr = sGO.GetComponent<RectTransform>(); sr.SetParent(pad, false);
        Anchor(sr, 0,0.30f,1,0.60f);
        var bgImg = MakeImage(sr, "bg", Hex("05070E")); Stretch(bgImg.rectTransform);
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
        return img;
    }

    TMP_Text MakeText(Transform parent, string name, string text, float size, Color col, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = col; t.alignment = align;
        t.raycastTarget = false;
        Stretch(t.rectTransform);
        return t;
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
