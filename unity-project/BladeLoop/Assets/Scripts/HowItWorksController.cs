using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// How It Works (Task 5) - explains what the app models, where the numbers come from,
/// and states our assumptions openly. Content from docs/how-it-works-content.md (PR 47).
/// A plain, scrollable, readable page in the home-page theme (BladeLoopTheme).
///
/// Layout is done with MANUAL absolute positioning (a running y cursor), not Unity
/// auto-layout groups - the layout-group + fitter route rendered blank here, so every
/// block is placed at an explicit height measured from TMP. Built at runtime, no prefabs.
/// </summary>
public class HowItWorksController : MonoBehaviour
{
    RectTransform content;
    float cursor;                       // y offset from top of content, grows downward (positive)
    const float ContentW = 1480f;       // usable text width inside the viewport
    const float PadX = 20f;

    void Start()
    {
        BladeLoopTheme.Init();
        EnsureEventSystem();
        SetupCamera();
        var canvas = BuildCanvas();
        BuildChrome(canvas.transform);
        var viewport = BuildScroll(canvas.transform);
        Populate();
        // Size the content rect to everything we placed, so the ScrollRect can scroll it.
        content.sizeDelta = new Vector2(0f, cursor + 40f);
    }

    void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BladeLoopTheme.Panel;
    }

    Canvas BuildCanvas()
    {
        var go = new GameObject("HowItWorksCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay;
        var sc = go.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
        return c;
    }

    void BuildChrome(Transform root)
    {
        var bg = MakeImage(root, "Panel", BladeLoopTheme.Panel); Stretch(bg.rectTransform);

        var eyebrow = MakeText(root, "eyebrow", "HOW IT WORKS", 13, BladeLoopTheme.Faint, BladeLoopTheme.Mono);
        eyebrow.alignment = TextAlignmentOptions.Left; eyebrow.characterSpacing = 6f;
        Anchor(eyebrow.rectTransform, 0.06f, 0.90f, 0.6f, 0.94f);
        var tick = MakeImage(root, "tick", BladeLoopTheme.Oxide); Anchor(tick.rectTransform, 0.06f, 0.892f, 0.09f, 0.896f);
        var title = MakeText(root, "title", "What this app models", 34, BladeLoopTheme.Bone, BladeLoopTheme.SansBold);
        title.alignment = TextAlignmentOptions.Left; title.fontStyle = FontStyles.Bold;
        Anchor(title.rectTransform, 0.06f, 0.82f, 0.85f, 0.89f);

        MakeButton(root, "MenuButton", "\u2190  MENU", new Vector2(0.86f,0.90f), new Vector2(0.94f,0.945f),
                   () => SceneManager.LoadScene("MainMenu"));
    }

    RectTransform BuildScroll(Transform root)
    {
        var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportGO.transform.SetParent(root, false);
        var vrt = viewportGO.GetComponent<RectTransform>();
        Anchor(vrt, 0.06f, 0.05f, 0.93f, 0.80f);
        viewportGO.GetComponent<Image>().color = new Color(0,0,0,0.001f);

        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewportGO.transform, false);
        content = contentGO.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0,1); content.anchorMax = new Vector2(1,1); content.pivot = new Vector2(0.5f,1);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(0, 3000);

        var scroll = viewportGO.GetComponent<ScrollRect>();
        scroll.content = content; scroll.viewport = vrt;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.scrollSensitivity = 32f; scroll.movementType = ScrollRect.MovementType.Clamped;

        var sbGO = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        sbGO.transform.SetParent(root, false);
        var sbrt = sbGO.GetComponent<RectTransform>(); Anchor(sbrt, 0.935f, 0.05f, 0.94f, 0.80f);
        sbGO.GetComponent<Image>().color = BladeLoopTheme.RuleSoft;
        var handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGO.transform.SetParent(sbGO.transform, false); Stretch(handleGO.GetComponent<RectTransform>());
        handleGO.GetComponent<Image>().color = BladeLoopTheme.Faint;
        var sb = sbGO.GetComponent<Scrollbar>(); sb.handleRect = handleGO.GetComponent<RectTransform>(); sb.direction = Scrollbar.Direction.BottomToTop;
        sb.targetGraphic = handleGO.GetComponent<Image>();
        scroll.verticalScrollbar = sb; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        return vrt;
    }

    // ---- content, placed with a manual y cursor -----------------------------

    void Populate()
    {
        cursor = 8f;
        Body("BladeLoop simulates the thermal co-processing of decommissioned wind turbine blades \u2014 the recovery of clean glass fibre from blade waste by heating it, without oxygen, until the resin that binds the fibre breaks down and can be driven off. This is a real chemical engineering process (pyrolysis), and a real end-of-life route for the tens of thousands of tonnes of blade material now reaching retirement.");
        Body("You control four process inputs \u2014 kiln temperature, how long material stays in the kiln, how fast material is fed in, and how finely it is shredded first. From those, the model computes five output streams \u2014 reclaimed glass fibre, pyrolytic oil, syngas, char dust, and losses \u2014 plus two quality measures for the recovered fibre. The output streams always add up to the feed rate: nothing appears or disappears, so the mass balance is closed.");
        Body("The point of the app is not to run a perfect plant. It is to show that there is no single right setting \u2014 only a different customer. Run for the highest quality and you recover clean, structural-grade fibre slowly; push more material through and you recover more fibre per hour at a lower grade \u2014 and there is a real buyer for each.");

        Heading("The four inputs, and why they matter");
        Body("<b>Kiln temperature (400\u2013700 \u00b0C, optimum 600 \u00b0C).</b>  Around 600 \u00b0C the resin cracks cleanly and the glass fibre comes through intact. Too cold and the resin never fully cracks, so residue stays stuck to the fibre and purity falls. Too hot and the fibre itself weakens while more of the material turns to char.");
        Body("<b>Retention time (30\u201345 min, optimum 35 min).</b>  How long material spends inside the rotary kiln. On target, the fibres are fully freed of resin without over-cooking. Too short and some resin stays bound to the fibre; too long and the fibre embrittles and char output climbs.");
        Body("<b>Feed rate (4,000\u20139,000 kg/h, optimum 6,500 kg/h).</b>  How fast shredded material is fed in. At design throughput each particle gets its ideal time inside the kiln. Push the feed far above capacity and residence time per particle is cut short \u2014 the extra material is worth having, but each piece is processed less completely.");
        Body("<b>Particle size (1\u201320 mm, optimum 2 mm).</b>  How finely the blade is shredded before the kiln. This is the most influential setting in the model \u2014 it carries more weight than temperature. At about 2 mm, heat penetrates evenly and every particle decomposes completely. Coarser feedstock leaves particle cores that never fully decompose, meaning poorer fibre and more waste.");
        Body("Feed rate and particle size are linked. Finer shredding is slower shredding, so the finer you grind, the less material the shredder can pass per hour. The maximum feed rate therefore depends on particle size \u2014 you cannot ask for the finest grind and the highest throughput at once. On the Custom Order screen, the feed control is bounded by the particle-size control for exactly this reason.");

        Heading("Where the numbers come from");
        Body("The baseline output proportions \u2014 that at good conditions the recovered stream splits into roughly 70 % glass fibre, 16 % oil, 8 % syngas and 6 % char \u2014 come from the CEE reference model for this process. Those are the proportions the plant is calibrated to.");
        Body("Everything that describes how the plant behaves away from those ideal conditions \u2014 how much each input can drift before quality suffers, how deviations are weighted, how losses grow, and how purity and strength fall \u2014 is our own model, calibrated to that baseline. It reproduces the reference case exactly at the design set-point and models the trade-offs around it.");

        Heading("Our assumptions, stated honestly");
        Body("We sort recovered fibre into three grades, each routed to a real end market:");
        TierTable();
        Body("The tiering approach is real; the specific threshold numbers are our own project assumptions. Grading recovered fibre by quality and routing each grade to a different market is exactly how this material is handled in practice. But there is no published grading standard for recovered composite glass fibre against which to set the cutoffs \u2014 the closest analogue, PAS 101, covers container cullet glass only. Every research group defines its own quality bar. So we set ours, and label them as assumptions rather than claiming a standard that does not exist.");
        Body("Where the tiers are grounded is in demonstrated performance. Real recovered fibre has been measured across roughly a 72\u201393 % tensile-retention range: ordinary single-step pyrolysis of real wind-blade waste lands in the low-to-mid 70s, while a published two-step study on wind-blade epoxy reported 76 % tensile strength and 88 % modulus retention. Our mid tier sits around that demonstrated result. Our high tier (\u2265 90 %) is deliberately aspirational \u2014 best-in-class recovery, not the routine output of a standard thermal process.");
        Body("Both markets below the top tier already exist. Recovered blade fibre is sold into precast concrete today (for example by Regen Fiber), and cement co-processing \u2014 where the glass substitutes for raw silica and the resin burns as kiln fuel in place of coal \u2014 is currently the most commercially mature end-of-life route at scale. We did not invent these customers.");
        Quote("Low grade is not something you choose \u2014 it is where you land. Contaminated or oversized feedstock, a shredder at its limit, an under-fired kiln, or a deadline that forces throughput all produce low-grade output. The point of a grade-tiered market is that the material still has somewhere to go when the plant can't do better.");

        Heading("What \u201cpurity\u201d means here");
        Body("The recycling literature reports tensile and modulus retention \u2014 how much mechanical strength survives recovery \u2014 but it does not generally report a \u201cpurity %\u201d. That figure is our own definition: purity is the mass fraction of recovered material that is fibre, rather than adhered char and resin residue. Because it isn't a standard literature metric, the purity half of each threshold cannot be independently benchmarked the way tensile retention can \u2014 a second reason the thresholds are labelled as project assumptions.");
        Body("Where the app translates an order into a number of blades or turbines, it assumes an average 2 MW-class blade at about 11 tonnes (LM 56.8 P design), cross-checked against the 10\u201314 t/MW rule of thumb. Actual blade mass varies widely by turbine class.");
        Body("<b>Order quantities are illustrative; the plant and the orders are not real.</b>  The preset orders name a type of buyer, not a specific company, and every end-use claim is drawn from the sourced CEE material.");

        Heading("Sources");
        Body("Grade-tier evidence and blade-mass figures come from the CEE team's sourcing (docs/CEE-deliverable.md, docs/grade-threshold-reasoning.md; Anjani Lohith Kosana & Hari Krishna Kondam, 30 Aug 2026):");
        Body("\u2022  Two-step pyrolysis (425 \u00b0C + 475 \u00b0C) of wind-blade epoxy GFRP \u2014 76 % tensile / 88 % modulus retention.\n\u2022  Microwave / single-step pyrolysis of real wind-blade waste \u2014 ~72 % tensile retention.\n\u2022  Two-temperature-step pyrolysis of E-glass thermoset composites \u2014 up to 19 % tensile improvement over single-step (OSTI, peer-reviewed).\n\u2022  Review of glass-fibre recovery by pyrolysis \u2014 up to ~93 % tensile retention under optimal conditions.\n\u2022  Molten-salt-assisted pyrolysis \u2014 specialised, non-standard process approaching near-virgin performance.\n\u2022  PAS 101 \u2014 confirms no equivalent grading standard exists for recovered composite glass fibre.\n\u2022  Cement co-processing and Regen Fiber precast-concrete use \u2014 see CEE-deliverable \u00a73.\n\u2022  Blade mass: LM 56.8 P (2 MW class, 11.3 t) \u2014 see CEE-deliverable \u00a72.");
    }

    // place a text block at the current cursor, advance the cursor by its measured height
    void Block(string text, float size, Color col, TMP_FontAsset font, float extraGap)
    {
        var go = new GameObject("block", typeof(RectTransform));
        go.transform.SetParent(content, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.font = font; t.fontSize = size; t.color = col;
        t.alignment = TextAlignmentOptions.TopLeft; t.enableWordWrapping = true; t.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0,1); rt.anchorMax = new Vector2(1,1); rt.pivot = new Vector2(0.5f,1);
        rt.offsetMin = new Vector2(PadX, 0); rt.offsetMax = new Vector2(-PadX, 0);
        float h = t.GetPreferredValues(text, ContentW, 0f).y;
        rt.sizeDelta = new Vector2(0, h);
        rt.anchoredPosition = new Vector2(0, -cursor);
        cursor += h + extraGap;
    }

    void Body(string text)     { Block(text, 16, BladeLoopTheme.Bone, BladeLoopTheme.Sans, 16f); }
    void Heading(string text)  { cursor += 14f; Block(text, 20, BladeLoopTheme.Oxide, BladeLoopTheme.MonoBold, 12f); }

    void Quote(string text)
    {
        // measure, draw a warm-charcoal box, then the italic text inside it
        float h = new GameObject().AddComponent<TextMeshProUGUI>().GetPreferredValues(text, ContentW - 40f, 0f).y;
        // (the temp object above is discarded; simpler: measure via a helper)
        var box = MakeImage(content, "quote", BladeLoopTheme.SkyWarm);
        var brt = box.rectTransform;
        brt.anchorMin = new Vector2(0,1); brt.anchorMax = new Vector2(1,1); brt.pivot = new Vector2(0.5f,1);
        brt.offsetMin = new Vector2(PadX,0); brt.offsetMax = new Vector2(-PadX,0);
        var go = new GameObject("qt", typeof(RectTransform));
        go.transform.SetParent(box.transform, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.font = BladeLoopTheme.Sans; t.fontSize = 16; t.color = BladeLoopTheme.Bone; t.fontStyle = FontStyles.Italic;
        t.alignment = TextAlignmentOptions.TopLeft; t.enableWordWrapping = true; t.raycastTarget = false;
        var trt = go.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0,1); trt.anchorMax = new Vector2(1,1); trt.pivot = new Vector2(0.5f,1);
        trt.offsetMin = new Vector2(20,0); trt.offsetMax = new Vector2(-20,0);
        float th = t.GetPreferredValues(text, ContentW - 40f, 0f).y;
        trt.sizeDelta = new Vector2(0, th); trt.anchoredPosition = new Vector2(0, -16);
        brt.sizeDelta = new Vector2(0, th + 32);
        brt.anchoredPosition = new Vector2(0, -cursor);
        cursor += th + 32 + 16f;
    }

    void TierTable()
    {
        string tbl =
            "<mspace=0.62em>Tier   Purity    Tensile   Goes to</mspace>\n" +
            "<mspace=0.62em>High   \u2265 90 %    \u2265 85 %    Composite manufacturing</mspace>\n" +
            "<mspace=0.62em>Mid    \u2265 78 %    \u2265 70 %    Precast concrete, casting</mspace>\n" +
            "<mspace=0.62em>Low    below     below     Cement kiln co-processing</mspace>";
        Block(tbl, 15, BladeLoopTheme.Bone, BladeLoopTheme.Mono, 16f);
    }

    // ---- helpers ------------------------------------------------------------

    void MakeButton(Transform parent, string name, string label, Vector2 aMin, Vector2 aMax, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin=aMin; rt.anchorMax=aMax; rt.offsetMin=Vector2.zero; rt.offsetMax=Vector2.zero;
        go.GetComponent<Image>().color = new Color(1f,1f,1f,0.05f);
        var edge = go.AddComponent<Outline>(); edge.effectColor = BladeLoopTheme.Hex("4A4238"); edge.effectDistance = new Vector2(1.2f,-1.2f);
        go.GetComponent<Button>().onClick.AddListener(onClick);
        var t = new GameObject("l", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(rt, false); Stretch(t.rectTransform);
        t.text = label; t.font = BladeLoopTheme.MonoBold; t.fontSize = 15; t.color = BladeLoopTheme.Bone;
        t.alignment = TextAlignmentOptions.Center; t.characterSpacing = 3f;
    }

    Image MakeImage(Transform parent, string name, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.color=col; return img;
    }

    TMP_Text MakeText(Transform parent, string name, string text, float size, Color col, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text=text; t.font=font; t.fontSize=size; t.color=col; t.raycastTarget=false; t.enableWordWrapping=false;
        Stretch(t.rectTransform); return t;
    }

    void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }

    static void Stretch(RectTransform r){ r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=Vector2.zero; r.offsetMax=Vector2.zero; }
    static void Anchor(RectTransform r, float xmin,float ymin,float xmax,float ymax){ r.anchorMin=new Vector2(xmin,ymin); r.anchorMax=new Vector2(xmax,ymax); r.offsetMin=Vector2.zero; r.offsetMax=Vector2.zero; }
}
