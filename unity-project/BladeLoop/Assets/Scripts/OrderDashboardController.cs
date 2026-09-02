using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// Custom Order screen (Task 3). The user names a customer, picks a target grade and a
/// quantity, presses SOLVE, and the solver returns the plant settings that hit that grade.
/// The found settings then show as adjustable sliders (feed bounded by particle size), the
/// five output tanks, purity/tensile with a grade badge, and the campaign figures.
///
/// Built entirely in C# at runtime, same pattern and light theme as PlantExplorerController.
/// Reads OrderContext / OrderSolver (owned by Ritwika/Akshat); never edits them.
/// </summary>
public class OrderDashboardController : MonoBehaviour
{
    // ---- shared palette (mirrors PlantExplorerController so it feels like one product) ----
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

    void Awake()
    {
        InitPalette();
        EnsureEventSystem();
        var canvas = BuildCanvas();
        BuildUI(canvas.transform);
    }

    Canvas BuildCanvas()
    {
        var go = new GameObject("OrderDashboardCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay;
        var s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080); s.matchWidthOrHeight = 0.5f;
        return c;
    }

    void BuildUI(Transform root)
    {
        var panel = MakeImage(root, "Panel", PanelBg); panel.raycastTarget = true;
        panel.rectTransform.anchorMin = new Vector2(0, 0); panel.rectTransform.anchorMax = new Vector2(1, 1);
        panel.rectTransform.offsetMin = Vector2.zero; panel.rectTransform.offsetMax = Vector2.zero;

        // Header: eyebrow + big title, with a Menu button top-right.
        var eyebrow = MakeText(panel.rectTransform, "eyebrow", "ORDER · PLAN · PROVE", 13, TextSub, TextAlignmentOptions.TopLeft);
        eyebrow.rectTransform.anchorMin = new Vector2(0,1); eyebrow.rectTransform.anchorMax = new Vector2(1,1); eyebrow.rectTransform.pivot = new Vector2(0.5f,1);
        eyebrow.rectTransform.anchoredPosition = new Vector2(0,-50); eyebrow.rectTransform.sizeDelta = new Vector2(-96, 18); eyebrow.characterSpacing = 5;
        var title = MakeText(panel.rectTransform, "title", "CUSTOM ORDER", 44, Accent, TextAlignmentOptions.TopLeft);
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.anchorMin = new Vector2(0,1); title.rectTransform.anchorMax = new Vector2(1,1); title.rectTransform.pivot = new Vector2(0.5f,1);
        title.rectTransform.anchoredPosition = new Vector2(0,-72); title.rectTransform.sizeDelta = new Vector2(-96, 60);

        MakeNavButton(panel.rectTransform, "MenuButton", "\u2190  Menu", new Vector2(1, 1), new Vector2(-48, -48),
                      TileBg, TextMain, () => SceneManager.LoadScene("MainMenu"));

        BuildOrderForm(panel.rectTransform);
    }

    // ---- order form state ----
    TMP_InputField customerInput;
    Grade selectedGrade = Grade.Mid;
    readonly Image[] gradeChips = new Image[3];
    readonly TMP_Text[] gradeChipLabels = new TMP_Text[3];
    Slider qtySlider; TMP_Text qtyValue;
    Button solveButton; TMP_Text solveLabel;

    void BuildOrderForm(RectTransform panel)
    {
        // Centred form card in the upper area; the solved plan will fill the space below it.
        var card = MakeImage(panel, "OrderForm", TileBg);
        var crt = card.rectTransform;
        crt.anchorMin = new Vector2(0.5f, 1); crt.anchorMax = new Vector2(0.5f, 1); crt.pivot = new Vector2(0.5f, 1);
        crt.sizeDelta = new Vector2(720, 300); crt.anchoredPosition = new Vector2(0, -150);

        var pad = new GameObject("p", typeof(RectTransform)).GetComponent<RectTransform>();
        pad.SetParent(card.transform, false); Stretch(pad); Inset(pad, 32, 26);

        // Row 1: Customer name (label + text input)
        MakeFieldLabel(pad, "Customer", 0.82f);
        var inputGO = new GameObject("CustomerInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputGO.transform.SetParent(pad, false);
        var irt = inputGO.GetComponent<RectTransform>();
        Anchor(irt, 0.24f, 0.78f, 1f, 0.96f);
        inputGO.GetComponent<Image>().color = PanelBg;
        var inputText = MakeText(irt, "Text", "", 15, TextMain, TextAlignmentOptions.Left);
        inputText.raycastTarget = true; inputText.rectTransform.offsetMin = new Vector2(12, 0); inputText.rectTransform.offsetMax = new Vector2(-12, 0);
        var placeholder = MakeText(irt, "Placeholder", "Custom order", 15, TextSub, TextAlignmentOptions.Left);
        placeholder.fontStyle = FontStyles.Italic; placeholder.rectTransform.offsetMin = new Vector2(12, 0); placeholder.rectTransform.offsetMax = new Vector2(-12, 0);
        customerInput = inputGO.GetComponent<TMP_InputField>();
        customerInput.textComponent = inputText; customerInput.placeholder = placeholder;
        customerInput.textViewport = irt; customerInput.text = "";

        // Row 2: Grade selector (three chips)
        MakeFieldLabel(pad, "Grade", 0.52f);
        string[] names = { "High", "Mid", "Low" };
        Grade[] grades = { Grade.High, Grade.Mid, Grade.Low };
        float chipW = 150f, gap = 12f, x0 = 0f;
        for (int i = 0; i < 3; i++)
        {
            int idx = i; Grade g = grades[i];
            var chip = new GameObject("grade_" + names[i], typeof(RectTransform), typeof(Image), typeof(Button));
            chip.transform.SetParent(pad, false);
            var chrt = chip.GetComponent<RectTransform>();
            chrt.anchorMin = new Vector2(0.24f, 0.46f); chrt.anchorMax = new Vector2(0.24f, 0.64f); chrt.pivot = new Vector2(0, 0.5f);
            chrt.sizeDelta = new Vector2(chipW, 0); chrt.anchoredPosition = new Vector2(x0 + i * (chipW + gap), 0);
            var chipImg = chip.GetComponent<Image>(); gradeChips[i] = chipImg;
            var cl = MakeText(chrt, "l", names[i], 16, TextMain, TextAlignmentOptions.Center); cl.fontStyle = FontStyles.Bold;
            gradeChipLabels[i] = cl;
            chip.GetComponent<Button>().onClick.AddListener(() => SelectGrade(g, idx));
        }
        SelectGrade(Grade.Mid, 1);   // default

        // Row 3: Quantity slider
        MakeFieldLabel(pad, "Quantity", 0.22f);
        var sGO = new GameObject("QtySlider", typeof(RectTransform), typeof(Slider));
        sGO.transform.SetParent(pad, false);
        var srt = sGO.GetComponent<RectTransform>(); Anchor(srt, 0.24f, 0.24f, 0.80f, 0.34f);
        var qbg = MakeImage(srt, "bg", Line); Stretch(qbg.rectTransform);
        var qfillArea = new GameObject("FillArea", typeof(RectTransform)).GetComponent<RectTransform>();
        qfillArea.SetParent(srt, false); Stretch(qfillArea);
        var qfill = MakeImage(qfillArea, "Fill", Accent); qfill.rectTransform.anchorMin = new Vector2(0,0); qfill.rectTransform.anchorMax = new Vector2(0,1); qfill.rectTransform.sizeDelta = new Vector2(8,0);
        var qhandleArea = new GameObject("HandleArea", typeof(RectTransform)).GetComponent<RectTransform>();
        qhandleArea.SetParent(srt, false); Stretch(qhandleArea);
        var qhandle = MakeImage(qhandleArea, "Handle", TextMain); qhandle.rectTransform.sizeDelta = new Vector2(16,16);
        qtySlider = sGO.GetComponent<Slider>();
        qtySlider.fillRect = qfill.rectTransform; qtySlider.handleRect = qhandle.rectTransform; qtySlider.targetGraphic = qhandle;
        qtySlider.direction = Slider.Direction.LeftToRight; qtySlider.minValue = 1000; qtySlider.maxValue = 10000; qtySlider.wholeNumbers = true; qtySlider.value = 4000;
        qtyValue = MakeText(pad, "qtyv", "4,000 t", 16, Accent, TextAlignmentOptions.Right); qtyValue.fontStyle = FontStyles.Bold;
        Anchor(qtyValue.rectTransform, 0.82f, 0.20f, 1f, 0.36f);
        qtySlider.onValueChanged.AddListener(v => qtyValue.text = v.ToString("N0") + " t");

        // SOLVE button (big, centred, below the card)
        var solveGO = new GameObject("SolveButton", typeof(RectTransform), typeof(Image), typeof(Button));
        solveGO.transform.SetParent(panel, false);
        var solrt = solveGO.GetComponent<RectTransform>();
        solrt.anchorMin = new Vector2(0.5f, 1); solrt.anchorMax = new Vector2(0.5f, 1); solrt.pivot = new Vector2(0.5f, 1);
        solrt.sizeDelta = new Vector2(260, 56); solrt.anchoredPosition = new Vector2(0, -470);
        solveGO.GetComponent<Image>().color = Accent;
        solveButton = solveGO.GetComponent<Button>();
        solveButton.onClick.AddListener(OnSolve);
        solveLabel = MakeText(solrt, "l", "S O L V E", 18, Color.white, TextAlignmentOptions.Center); solveLabel.fontStyle = FontStyles.Bold;
    }

    void MakeFieldLabel(RectTransform pad, string text, float y)
    {
        var l = MakeText(pad, "lbl_" + text, text, 15, TextMain, TextAlignmentOptions.Left); l.fontStyle = FontStyles.Bold;
        Anchor(l.rectTransform, 0, y, 0.24f, y + 0.16f);
    }

    void SelectGrade(Grade g, int idx)
    {
        selectedGrade = g;
        for (int i = 0; i < 3; i++)
        {
            bool on = i == idx;
            gradeChips[i].color = on ? Accent : PanelBg;
            gradeChipLabels[i].color = on ? Color.white : TextMain;
        }
    }

    // Wired in slice 3 (OrderSolver.Solve + solved-plan display).
    // ---- solved-plan state (slice 3) ----
    RectTransform resultsArea;
    TMP_Text foundSettingsText;
    TMP_Text infeasibleText;
    bool solving;

    void OnSolve()
    {
        if (solving) return;
        StartCoroutine(SolveRoutine());
    }

    System.Collections.IEnumerator SolveRoutine()
    {
        // Feedback: a solve takes ~275 ms and blocks; disable the button and change the
        // label so it doesn't look frozen. Yield one frame so the label actually paints
        // before the synchronous solve runs.
        solving = true;
        solveButton.interactable = false;
        solveLabel.text = "Solving\u2026";
        yield return null;
        yield return null;

        var result = OrderSolver.Solve(selectedGrade);

        if (!result.feasible)
        {
            ShowInfeasible(result.note);
        }
        else
        {
            // Build the order and hand it to the shared context. Fibre/quality numbers come
            // from result.model; campaign figures come from OrderContext (per the contract).
            string name = string.IsNullOrWhiteSpace(customerInput.text) ? "Custom order" : customerInput.text.Trim();
            var order = new Order(name, "Custom order", selectedGrade, qtySlider.value);
            OrderContext.SetOrder(order, result.model);
            ShowSolvedPlan(result.model);
        }

        solving = false;
        solveButton.interactable = true;
        solveLabel.text = "S O L V E";
    }

    void EnsureResultsArea()
    {
        if (resultsArea != null) return;
        var panel = Object.FindFirstObjectByType<Canvas>().transform.Find("Panel") as RectTransform;
        var card = MakeImage(panel, "ResultsCard", TileBg);
        resultsArea = card.rectTransform;
        resultsArea.anchorMin = new Vector2(0.5f, 1); resultsArea.anchorMax = new Vector2(0.5f, 1); resultsArea.pivot = new Vector2(0.5f, 1);
        resultsArea.sizeDelta = new Vector2(720, 150); resultsArea.anchoredPosition = new Vector2(0, -545);

        var hdr = MakeText(resultsArea, "hdr", "THESE SETTINGS DELIVER IT", 12, TextSub, TextAlignmentOptions.TopLeft);
        hdr.characterSpacing = 3; Anchor(hdr.rectTransform, 0, 0.80f, 1, 0.98f); hdr.rectTransform.offsetMin = new Vector2(28, 0);

        foundSettingsText = MakeText(resultsArea, "found", "", 20, TextMain, TextAlignmentOptions.Left);
        foundSettingsText.fontStyle = FontStyles.Bold;
        Anchor(foundSettingsText.rectTransform, 0, 0.30f, 1, 0.74f); foundSettingsText.rectTransform.offsetMin = new Vector2(28, 0); foundSettingsText.rectTransform.offsetMax = new Vector2(-28, 0);

        infeasibleText = MakeText(resultsArea, "infeasible", "", 15, Crit, TextAlignmentOptions.Left);
        infeasibleText.enableWordWrapping = true;
        Anchor(infeasibleText.rectTransform, 0, 0.10f, 1, 0.74f); infeasibleText.rectTransform.offsetMin = new Vector2(28, 0); infeasibleText.rectTransform.offsetMax = new Vector2(-28, 0);
    }

    void ShowSolvedPlan(ProcessModel m)
    {
        EnsureResultsArea();
        resultsArea.gameObject.SetActive(true);
        infeasibleText.gameObject.SetActive(false);
        foundSettingsText.gameObject.SetActive(true);
        foundSettingsText.text =
            m.TempC.ToString("0") + " \u00b0C     " +
            m.RetentionMin.ToString("0") + " min     " +
            m.FeedKgH.ToString("N0") + " kg/h     " +
            m.ParticleSizeMm.ToString("0.#") + " mm";
    }

    void ShowInfeasible(string note)
    {
        EnsureResultsArea();
        resultsArea.gameObject.SetActive(true);
        foundSettingsText.gameObject.SetActive(false);
        infeasibleText.gameObject.SetActive(true);
        infeasibleText.text = string.IsNullOrEmpty(note)
            ? "No settings in the operating envelope reach that grade at this feed rate. Try a coarser target or a lower quantity."
            : note;
    }

    // =====================================================================================
    //  Shared UI helpers (mirrored from PlantExplorerController so the two screens match).
    // =====================================================================================

    void MakeNavButton(Transform parent, string name, string label, Vector2 anchorMinMax, Vector2 pos,
                       Color bg, Color fg, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMinMax; rt.anchorMax = anchorMinMax; rt.pivot = anchorMinMax;
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(150, 46);
        go.GetComponent<Image>().color = bg;
        go.GetComponent<Button>().onClick.AddListener(onClick);
        var t = MakeText(rt, "lbl", label, 18, fg, TextAlignmentOptions.Center); t.fontStyle = FontStyles.Bold;
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
