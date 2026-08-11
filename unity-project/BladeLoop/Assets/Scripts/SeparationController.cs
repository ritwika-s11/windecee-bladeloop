using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Stage 4 Separation Explorer. Interactive dashboard focused on the cyclone
/// classifier: drag the fluidizing velocity and watch the separation window,
/// terminal velocities, and (via SeparationVisualizer) a 3D cyclone react.
///
/// Reuses PlantModel for all physics — the separation validity, terminal
/// velocities, and cyclone geometry all come from the spec-derived model.
/// Self-builds its UI in code (no prefabs), same pattern as PlantExplorerController.
/// </summary>
public class SeparationController : MonoBehaviour
{
    PlantModel model = new PlantModel();

    // palette (light theme, matches PlantExplorer)
    Color PanelBg, TileBg, Accent, TextMain, TextSub, Good, Bad, CharCol, GlassCol;

    // UI refs updated on Recompute
    TMP_Text fluidVal, charVtVal, glassVtVal, statusMsg, cycDiaVal, cycHtVal;
    TMP_Text psVal, effVal, dpVal, glassStreamVal, charStreamVal;
    RectTransform windowMarker;
    Image statusBar;
    Image safeBand;

    SeparationVisualizer viz;

    void Awake()
    {
        InitPalette();
        viz = Object.FindFirstObjectByType<SeparationVisualizer>();
        BuildUI(BuildCanvas());
        Recompute();
    }

    void InitPalette()
    {
        PanelBg  = Hex("F5F6F8"); TileBg = Hex("FFFFFF"); Accent = Hex("2563EB");
        TextMain = Hex("1E293B"); TextSub = Hex("475569");
        Good     = Hex("16A34A"); Bad    = Hex("DC2626");
        CharCol  = Hex("6B7280"); GlassCol = Hex("3B82F6");
    }

    Transform BuildCanvas()
    {
        var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        EnsureEventSystem();
        return cgo.transform;
    }

    void BuildUI(Transform root)
    {
        // Left-side panel (leave right ~30% for the cyclone), same split as PlantExplorer
        var bg = MakeImage(root, "BG", PanelBg);
        bg.rectTransform.anchorMin = new Vector2(0f, 0f); bg.rectTransform.anchorMax = new Vector2(0.70f, 1f);
        bg.rectTransform.offsetMin = Vector2.zero; bg.rectTransform.offsetMax = Vector2.zero;
        bg.raycastTarget = false;

        var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(root, false);
        content.anchorMin = new Vector2(0.35f, 1); content.anchorMax = new Vector2(0.35f, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = new Vector2(0, -40);
        content.sizeDelta = new Vector2(1080, 0);
        var vlayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlayout.spacing = 16; vlayout.childControlWidth = true; vlayout.childControlHeight = true;
        vlayout.childForceExpandWidth = true; vlayout.padding = new RectOffset(20,20,20,20);
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Back button
        var backGO = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
        backGO.transform.SetParent(root, false);
        var brt = backGO.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0,1); brt.anchorMax = new Vector2(0,1); brt.pivot = new Vector2(0,1);
        brt.anchoredPosition = new Vector2(30,-30); brt.sizeDelta = new Vector2(150,46);
        backGO.GetComponent<Image>().color = TileBg;
        var bbtn = backGO.GetComponent<Button>();
        bbtn.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));
        var bt = MakeText(brt, "lbl", "\u2190  Menu", 18, TextMain, TextAlignmentOptions.Center);
        bt.fontStyle = FontStyles.Bold;

        // Title
        var title = MakeText(content, "Title", "SEPARATION EXPLORER", 34, Accent, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        MakeText(content, "Sub", "Stage 4 \u00b7 cyclone classifier \u2014 tune the gas velocity to separate char from glass", 16, TextSub, TextAlignmentOptions.Center);

        // Fluidizing slider card
        var slCard = MakeCardRow(content, "FluidCard", 90);
        var flbl = MakeText(slCard, "lbl", "Fluidizing velocity", 16, TextSub, TextAlignmentOptions.MidlineLeft, new Vector2(0,0.5f), new Vector2(0.6f,1f)); flbl.rectTransform.offsetMin = new Vector2(18, 0); flbl.rectTransform.offsetMax = new Vector2(0, -6);
        fluidVal = MakeText(slCard, "val", "0.015 m/s", 18, Accent, TextAlignmentOptions.MidlineRight, new Vector2(0.6f,0.5f), new Vector2(1f,1f)); fluidVal.rectTransform.offsetMin = new Vector2(0, 0); fluidVal.rectTransform.offsetMax = new Vector2(-18, -6);
        var sl = MakeSlider(slCard, 0.001f, 0.05f, 0.015f, v => { model.FluidizingVelocity = v; Recompute(); });
        sl.anchorMin = new Vector2(0f, 0.05f); sl.anchorMax = new Vector2(1f, 0.05f); sl.pivot = new Vector2(0.5f, 0f); sl.offsetMin = new Vector2(0f, 4f); sl.offsetMax = new Vector2(0f, 24f);

        // Particle-size slider card
        var psCard = MakeCardRow(content, "PSCard", 90);
        var pslbl = MakeText(psCard, "lbl", "Particle size", 16, TextSub, TextAlignmentOptions.MidlineLeft, new Vector2(0,0.5f), new Vector2(0.6f,1f)); pslbl.rectTransform.offsetMin = new Vector2(18,0); pslbl.rectTransform.offsetMax = new Vector2(0,-6);
        psVal = MakeText(psCard, "val", "15 \u00b5m", 18, Accent, TextAlignmentOptions.MidlineRight, new Vector2(0.6f,0.5f), new Vector2(1f,1f)); psVal.rectTransform.offsetMin = new Vector2(0,0); psVal.rectTransform.offsetMax = new Vector2(-18,-6);
        var psl = MakeSlider(psCard, 8f, 26f, 15f, v => { model.ParticleSizeMicrons = v; Recompute(); });
        psl.anchorMin = new Vector2(0f,0.05f); psl.anchorMax = new Vector2(1f,0.05f); psl.pivot = new Vector2(0.5f,0f); psl.offsetMin = new Vector2(0f,4f); psl.offsetMax = new Vector2(0f,24f);

        // Terminal velocity readouts (two tiles)
        var tvRow = MakeCardRow(content, "TVRow", 90);
        charVtVal  = MakeStat(tvRow, "char", "CHAR settles at", "0.0032 m/s", CharCol, 0f, 0.5f);
        glassVtVal = MakeStat(tvRow, "glass", "GLASS settles at", "0.0368 m/s", GlassCol, 0.5f, 1f);

        // Separation window bar
        var winCard = MakeCardRow(content, "WinCard", 120);
        MakeText(winCard, "wlbl", "SEPARATION WINDOW", 15, TextSub, TextAlignmentOptions.TopLeft, new Vector2(0,0.78f), new Vector2(1,1f));
        var track = MakeImage(winCard, "track", Hex("E5E8EE")); Anchor(track.rectTransform, 0,0.45f,1,0.62f);
        safeBand = MakeImage(track.rectTransform, "safe", Hex("BBF7D0"));
        var safe = safeBand;
        safe.rectTransform.anchorMin = new Vector2(0.0032f/0.05f, 0);
        safe.rectTransform.anchorMax = new Vector2(0.0368f/0.05f, 1);
        safe.rectTransform.offsetMin = Vector2.zero; safe.rectTransform.offsetMax = Vector2.zero;
        var mk = MakeImage(track.rectTransform, "marker", TextMain);
        mk.rectTransform.sizeDelta = new Vector2(4, 20); windowMarker = mk.rectTransform;
        statusMsg = MakeText(winCard, "smsg", "", 15, Good, TextAlignmentOptions.Left);
        Anchor(statusMsg.rectTransform, 0, 0, 1, 0.3f);

        // Cyclone geometry (spec figures)
        var geoRow = MakeCardRow(content, "GeoRow", 80);
        cycDiaVal = MakeStat(geoRow, "dia", "CYCLONE Ø", "0.731 m", TextMain, 0f, 0.5f);
        cycHtVal  = MakeStat(geoRow, "ht", "CYCLONE HEIGHT", "2.92 m", TextMain, 0.5f, 1f);

        // Extra readouts: efficiency + pressure drop
        var perfRow = MakeCardRow(content, "PerfRow", 80);
        effVal = MakeStat(perfRow, "eff", "COLLECTION EFFICIENCY", "91%", Good, 0f, 0.5f);
        dpVal  = MakeStat(perfRow, "dp", "PRESSURE DROP", "474 Pa", TextMain, 0.5f, 1f);

        // Material streams
        var streamRow = MakeCardRow(content, "StreamRow", 80);
        glassStreamVal = MakeStat(streamRow, "gs", "GLASS OUT (bottom)", "4,550 kg/h", GlassCol, 0f, 0.5f);
        charStreamVal  = MakeStat(streamRow, "cs", "CHAR OUT (side)", "390 kg/h", CharCol, 0.5f, 1f);
    }

    void Recompute()
    {
        var o = model.Compute();
        float v = (float)model.FluidizingVelocity;
        if (fluidVal)  fluidVal.text  = v.ToString("0.###") + " m/s";
        if (charVtVal) charVtVal.text = o.CharTerminalVel.ToString("0.0000") + " m/s";
        if (glassVtVal)glassVtVal.text= o.GlassTerminalVel.ToString("0.0000") + " m/s";

        // safe band redraws to the computed window (char..glass over the 0..0.05 track)
        if (safeBand) {
            float lo = Mathf.Clamp01((float)o.CharTerminalVel / 0.05f);
            float hi = Mathf.Clamp01((float)o.GlassTerminalVel / 0.05f);
            safeBand.rectTransform.anchorMin = new Vector2(lo, 0);
            safeBand.rectTransform.anchorMax = new Vector2(hi, 1);
            safeBand.rectTransform.offsetMin = Vector2.zero; safeBand.rectTransform.offsetMax = Vector2.zero;
        }

        // marker position along 0..0.05 track
        if (windowMarker) {
            float frac = Mathf.Clamp01(v / 0.05f);
            windowMarker.anchorMin = new Vector2(frac, 0);
            windowMarker.anchorMax = new Vector2(frac, 1);
            windowMarker.anchoredPosition = Vector2.zero;
        }

        if (statusMsg) {
            if (o.SeparationOk) {
                statusMsg.text = "Clean separation \u2014 char lifts out, glass falls through."; statusMsg.color = Good;
            } else if (v <= PlantModel.CharTerminalVelocity) {
                statusMsg.text = "Too slow \u2014 char can't lift, it contaminates the glass."; statusMsg.color = Bad;
            } else {
                statusMsg.text = "Too fast \u2014 glass blows out with the char. Yield lost."; statusMsg.color = Bad;
            }
        }

        // cyclone geometry is fixed by spec; show baseline
        if (cycDiaVal) cycDiaVal.text = "0.731 m";
        if (cycHtVal)  cycHtVal.text  = "2.92 m";

        if (psVal) psVal.text = model.ParticleSizeMicrons.ToString("0") + " \u00b5m";
        if (effVal) { effVal.text = o.CollectionEfficiencyPct.ToString("0") + "%"; effVal.color = o.SeparationOk ? Good : Bad; }
        if (dpVal) dpVal.text = o.CyclonePressureDropPa.ToString("0") + " Pa";
        if (glassStreamVal) glassStreamVal.text = o.GlassStreamKgH.ToString("#,0") + " kg/h";
        if (charStreamVal) charStreamVal.text = o.CharStreamKgH.ToString("#,0") + " kg/h";

        if (viz != null) viz.SetSeparation(o.SeparationOk, v);
    }

    // ---------- tiny UI helpers ----------
    static Color Hex(string h){ ColorUtility.TryParseHtmlString("#"+h, out var c); return c; }

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
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return t;
    }

    TMP_Text MakeText(Transform parent, string name, string text, float size, Color col, TextAlignmentOptions align, Vector2 aMin, Vector2 aMax)
    {
        var t = MakeText(parent, name, text, size, col, align);
        var rt = t.rectTransform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return t;
    }

    RectTransform MakeCardRow(Transform parent, string name, float height)
    {
        var card = MakeImage(parent, name, TileBg);
        var le = card.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = height;
        return card.rectTransform;
    }

    TMP_Text MakeStat(Transform parent, string name, string label, string value, Color valCol, float xMin, float xMax)
    {
        var holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        var hrt = holder.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(xMin, 0); hrt.anchorMax = new Vector2(xMax, 1);
        hrt.offsetMin = new Vector2(14,10); hrt.offsetMax = new Vector2(-14,-10);
        MakeText(hrt, "l", label, 13, TextSub, TextAlignmentOptions.TopLeft, new Vector2(0,0.55f), new Vector2(1,1));
        return MakeText(hrt, "v", value, 22, valCol, TextAlignmentOptions.BottomLeft, new Vector2(0,0), new Vector2(1,0.55f));
    }

    RectTransform MakeSlider(Transform parent, float min, float max, float val, UnityEngine.Events.UnityAction<float> onChanged)
    {
        // Use Unity's built-in slider factory for correct structure, then restyle.
        var res = new UnityEngine.UI.DefaultControls.Resources();
        var go = UnityEngine.UI.DefaultControls.CreateSlider(res);
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, 20f);

        var sl = go.GetComponent<Slider>();
        sl.minValue = min; sl.maxValue = max; sl.value = val;
        sl.onValueChanged.AddListener(onChanged);

        // Restyle to match theme
        var bg = go.transform.Find("Background");
        if (bg) bg.GetComponent<Image>().color = Hex("E5E8EE");
        var fill = go.transform.Find("Fill Area/Fill");
        if (fill) fill.GetComponent<Image>().color = Accent;
        var handle = go.transform.Find("Handle Slide Area/Handle");
        if (handle) { handle.GetComponent<Image>().color = Accent; handle.GetComponent<RectTransform>().sizeDelta = new Vector2(20f, 20f); }
        return rt;
    }

    void Anchor(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }
    }
}
