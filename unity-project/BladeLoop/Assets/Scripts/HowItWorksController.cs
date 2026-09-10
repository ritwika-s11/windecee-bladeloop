using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// How It Works (Task 5) - explains what the app models, where the numbers come from,
/// and states our assumptions openly. Content from docs/how-it-works-content.md (PR 47).
///
/// ---------------------------------------------------------------------------
///  WHY THIS IS DIAGRAMS AND NOT PARAGRAPHS
///
///  The page previously ran as eighteen full-width paragraphs of 16pt body text.
///  Every fact on it was correct and carefully sourced, and almost none of it was
///  being read: a reader arriving from the home page wants to know what the plant
///  does and what they can do with it, and a wall of prose asks them to reconstruct
///  a process diagram in their head from a description of one.
///
///  So the same content is now carried by the shapes it is actually about. The
///  process is a flow. The mass balance is a bar that sums to the feed. The four
///  inputs are ranges with a set-point marked on them and their real influence
///  weights drawn to scale. The three grades are three cards. NOTHING WAS CUT -
///  every sentence of the sourcing and the assumptions survives, because the
///  honesty of that section is the strongest thing on the page.
///
///  EVERY NUMBER IS PULLED LIVE. Set-points come from ProcessModel, tier thresholds
///  and the reference split from OrderContext. The page cannot drift from the app.
///  The two exceptions are noted at their use sites.
///
///  Layout is MANUAL absolute positioning (a running y cursor), not Unity auto-layout
///  groups - the layout-group + fitter route rendered blank here, so every block is
///  placed at an explicit height. Built at runtime, no prefabs.
/// ---------------------------------------------------------------------------
/// </summary>
public class HowItWorksController : MonoBehaviour
{
    RectTransform content;
    float cursor;                       // y offset from top of content, grows downward (positive)
    const float ContentW = 1480f;       // width used to MEASURE text (see note in Block)
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

        MakeButton(root, "MenuButton", "←  MENU", new Vector2(0.86f,0.90f), new Vector2(0.94f,0.945f),
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

    // ======================================================= content ==========

    void Populate()
    {
        cursor = 8f;

        // ---- the lead ------------------------------------------------------
        Lead("BladeLoop simulates the thermal co-processing of decommissioned wind turbine blades — recovering clean glass fibre from blade waste by heating it, without oxygen, until the resin binding the fibre breaks down and can be driven off.");
        Body("This is a real chemical engineering process (pyrolysis), and a real end-of-life route for the tens of thousands of tonnes of blade material now reaching retirement.");

        // ---- 01 the process -------------------------------------------------
        Section("01", "The process");
        FlowDiagram();
        Body("You control four process inputs. From those, the model computes five output streams plus two quality measures for the recovered fibre.");

        // ---- 02 mass balance -------------------------------------------------
        Section("02", "Nothing appears, nothing disappears");
        Body("The five output streams always add up to the feed rate — the mass balance is closed. At the design set-point the feed divides like this:");
        SplitBar();
        Body("Losses are fugitive dust past the baghouse, moisture flash-off, and residue left inside the plant. They come out of the feed first; everything else shares what is left.");

        // ---- 03 the four inputs ----------------------------------------------
        Section("03", "The four inputs, and why they matter");
        Body("Each input has a set-point. The further you drift from it, the more the plant gives up — but not equally. The influence bar shows how much each input actually moves the result.");

        InputCard("KILN TEMPERATURE", 400f, 700f, ProcessModel.OptTemp, "°C", "0", 0.30f,
            $"Around {ProcessModel.OptTemp:0} °C the resin cracks cleanly and the glass fibre comes through intact. Too cold and the resin never fully cracks, so residue stays stuck to the fibre and purity falls. Too hot and the fibre itself weakens while more of the material turns to char.");

        InputCard("RETENTION TIME", 30f, 45f, ProcessModel.OptRetention, "min", "0", 0.22f,
            "How long material spends inside the rotary kiln. On target, the fibres are fully freed of resin without over-cooking. Too short and some resin stays bound to the fibre; too long and the fibre embrittles and char output climbs.");

        InputCard("FEED RATE", 4000f, 9000f, ProcessModel.OptFeed, "kg/h", "N0", 0.16f,
            "How fast shredded material is fed in. At design throughput each particle gets its ideal time inside the kiln. Push the feed far above capacity and residence time per particle is cut short — the extra material is worth having, but each piece is processed less completely.");

        InputCard("PARTICLE SIZE", 1f, 20f, ProcessModel.OptParticle, "mm", "0.#", 0.32f,
            $"How finely the blade is shredded before the kiln. This is the most influential setting in the model — it carries more weight than temperature. At about {ProcessModel.OptParticle:0.#} mm heat penetrates evenly and every particle decomposes completely. Coarser feedstock leaves particle cores that never fully decompose, meaning poorer fibre and more waste.");

        Callout("Feed rate and particle size are linked. Finer shredding is slower shredding, so the finer you grind, the less material the shredder can pass per hour. The maximum feed rate therefore depends on particle size — you cannot ask for the finest grind and the highest throughput at once. On the Custom Order screen the feed control is bounded by the particle-size control for exactly this reason.");

        // ---- 04 grades --------------------------------------------------------
        Section("04", "Three grades, three real markets");
        Body("The point of the app is not to run a perfect plant. It is to show that there is no single right setting — only a different customer. Run for the highest quality and you recover clean, structural-grade fibre slowly; push more material through and you recover more fibre per hour at a lower grade. There is a real buyer for each.");
        GradeCards();
        Body("The tiering approach is real; the specific threshold numbers are our own project assumptions. Grading recovered fibre by quality and routing each grade to a different market is exactly how this material is handled in practice. But there is no published grading standard for recovered composite glass fibre against which to set the cutoffs — the closest analogue, PAS 101, covers container cullet glass only. Every research group defines its own quality bar. So we set ours, and label them as assumptions rather than claiming a standard that does not exist.");
        Body("Where the tiers are grounded is in demonstrated performance. Real recovered fibre has been measured across roughly a 72–93 % tensile-retention range: ordinary single-step pyrolysis of real wind-blade waste lands in the low-to-mid 70s, while a published two-step study on wind-blade epoxy reported 76 % tensile strength and 88 % modulus retention. Our mid tier sits around that demonstrated result. Our high tier is deliberately aspirational: its ≥ 90 % purity / ≥ 85 % tensile bar describes best-in-class recovery, not the routine output of a standard thermal process.");
        Body("Both markets below the top tier already exist. Recovered blade fibre is sold into precast concrete today (for example by Regen Fiber), and cement co-processing — where the glass substitutes for raw silica and the resin burns as kiln fuel in place of coal — is currently the most commercially mature end-of-life route at scale. We did not invent these customers.");
        Quote("Low grade is not something you choose — it is where you land. Contaminated or oversized feedstock, a shredder at its limit, an under-fired kiln, or a deadline that forces throughput all produce low-grade output. The point of a grade-tiered market is that the material still has somewhere to go when the plant can't do better.");

        // ---- 05 what the app lets you do (NEW) --------------------------------
        Section("05", "What you can do here");
        FeatureCards();

        // ---- 06 provenance ----------------------------------------------------
        Section("06", "Where the numbers come from");
        // Baseline split derived live from the design case, so the copy tracks
        // ProcessModel instead of hardcoding 70/16/8/6.
        //
        // Taken from OrderContext.ReferenceSplit rather than building a fresh
        // ProcessModel here: the design case is the reference the order panel marks
        // its bars against and the reference this page describes, and those must be
        // the same thing by construction, not by two places agreeing. It is cached,
        // so this also stops allocating a model and re-running the split per visit.
        //
        // Divided by the RECOVERED stream, not the feed. Losses take 1.5 % off the
        // top at the design case, so dividing by feed would print 69/16/8/6 and
        // contradict the sentence, which says the recovered stream splits this way.
        var split = OrderContext.ReferenceSplit;
        float recovered = split.GlassKgH + split.OilKgH + split.SyngasKgH + split.CharKgH;
        int glassPct  = Mathf.RoundToInt(split.GlassKgH  / recovered * 100f);
        int oilPct    = Mathf.RoundToInt(split.OilKgH    / recovered * 100f);
        int syngasPct = Mathf.RoundToInt(split.SyngasKgH / recovered * 100f);
        int charPct   = Mathf.RoundToInt(split.CharKgH   / recovered * 100f);
        Body($"The baseline output proportions — that at good conditions the recovered stream splits into roughly {glassPct} % glass fibre, {oilPct} % oil, {syngasPct} % syngas and {charPct} % char — come from the CEE reference model for this process. Those are the proportions the plant is calibrated to.");
        Body("Everything that describes how the plant behaves away from those ideal conditions — how much each input can drift before quality suffers, how deviations are weighted, how losses grow, and how purity and strength fall — is our own model, calibrated to that baseline. It reproduces the reference case exactly at the design set-point and models the trade-offs around it.");

        Section("07", "What “purity” means here");
        Body("The recycling literature reports tensile and modulus retention — how much mechanical strength survives recovery — but it does not generally report a “purity %”. That figure is our own definition: purity is the mass fraction of recovered material that is fibre, rather than adhered char and resin residue. Because it isn't a standard literature metric, the purity half of each threshold cannot be independently benchmarked the way tensile retention can — a second reason the thresholds are labelled as project assumptions.");
        Body($"Where the app translates an order into a number of blades or turbines, it assumes an average 2 MW-class blade at about {OrderContext.BladeMassTonnes:0.#} tonnes (LM 56.8 P design), cross-checked against the 10–14 t/MW rule of thumb. Actual blade mass varies widely by turbine class.");
        Callout("Order quantities are illustrative; the plant and the orders are not real. The preset orders name a type of buyer, not a specific company, and every end-use claim is drawn from the sourced CEE material.");

        // ---- 08 sources -------------------------------------------------------
        Section("08", "Sources");
        Body("Grade-tier evidence and blade-mass figures come from the CEE team's sourcing (docs/CEE-deliverable.md, docs/grade-threshold-reasoning.md; Anjani Lohith Kosana & Hari Krishna Kondam, 30 Aug 2026):");
        SourceList(new[]{
            "Two-step pyrolysis (425 °C + 475 °C) of wind-blade epoxy GFRP — 76 % tensile / 88 % modulus retention.",
            "Microwave / single-step pyrolysis of real wind-blade waste — ~72 % tensile retention.",
            "Two-temperature-step pyrolysis of E-glass thermoset composites — up to 19 % tensile improvement over single-step (OSTI, peer-reviewed).",
            "Review of glass-fibre recovery by pyrolysis — up to ~93 % tensile retention under optimal conditions.",
            "Molten-salt-assisted pyrolysis — specialised, non-standard process approaching near-virgin performance.",
            "PAS 101 — confirms no equivalent grading standard exists for recovered composite glass fibre.",
            "Cement co-processing and Regen Fiber precast-concrete use — see CEE-deliverable §3.",
            $"Blade mass: LM 56.8 P (2 MW class, {OrderContext.BladeMassTonnes:0.#} t) — see CEE-deliverable §2.",
        });
    }

    // ======================================================= visual blocks ====

    /// <summary>Blade to product, as the five things that actually happen to it.
    /// The stages named here are the five scenes the tour walks through, in order
    /// (TourSceneSequencer), so the diagram doubles as a map of the run.</summary>
    void FlowDiagram()
    {
        const float H = 118f;
        var band = Panel(H, 0f);

        string[] step = { "RETIRED\nBLADE", "TRANSPORT", "SHRED", "KILN\nno oxygen", "SEPARATE" };
        int n = step.Length;
        float slotW = 1f / n;

        for (int i = 0; i < n; i++)
        {
            float x0 = i * slotW, x1 = x0 + slotW;
            bool kiln = i == 3;

            var cell = MakeImage(band, "s" + i, new Color(1f, 1f, 1f, kiln ? 0.06f : 0.028f));
            AnchorIn(cell.rectTransform, x0 + 0.008f, 0.20f, x1 - 0.008f, 0.94f);
            if (kiln)
            {
                var lip = MakeImage(cell.rectTransform, "lip", BladeLoopTheme.Oxide);
                AnchorIn(lip.rectTransform, 0f, 0f, 1f, 0.035f);
            }

            var t = MakeText(cell.rectTransform, "t", step[i], 15f,
                             kiln ? BladeLoopTheme.Bone : BladeLoopTheme.Muted, BladeLoopTheme.SansBold);
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.lineSpacing = -14f;
            Stretch(t.rectTransform);

            // The arrow sits ENTIRELY in the gap between two cells. Given a wider rect
            // it still draws in the gap - the glyph is centred - but it then overlaps
            // both neighbouring labels' rectangles, which is indistinguishable from a
            // real collision to anything checking this layout later.
            if (i < n - 1)
            {
                var a = MakeText(band, "a" + i, "→", 17f, BladeLoopTheme.Faint, BladeLoopTheme.Sans);
                a.alignment = TextAlignmentOptions.Center;
                AnchorIn(a.rectTransform, x1 - 0.0078f, 0.42f, x1 + 0.0078f, 0.72f);
            }
        }

        var cap = MakeText(band, "cap", "The five stages the guided tour walks through, in order.",
                           13f, BladeLoopTheme.Faint, BladeLoopTheme.Sans);
        cap.alignment = TextAlignmentOptions.Left;
        AnchorIn(cap.rectTransform, 0f, 0f, 1f, 0.17f);

        cursor += H + 20f;
    }

    /// <summary>The design-case split as one stacked bar summing to the feed, drawn in
    /// the same stream colours the order panel and the home page ledger use, so a
    /// reader who has seen either already knows how to read this.</summary>
    void SplitBar()
    {
        var sp = OrderContext.ReferenceSplit;
        float[] pct   = { sp.GlassPct, sp.OilPct, sp.SyngasPct, sp.CharPct, sp.LossPct };
        string[] name = { "FIBRE", "OIL", "SYNGAS", "CHAR", "LOSS" };

        float total = 0f; foreach (var p in pct) total += p;
        if (total < 0.01f) total = 100f;

        const float H = 96f;
        var band = Panel(H, 0f);

        float x = 0f;
        for (int i = 0; i < 5; i++)
        {
            float w = pct[i] / total;
            var seg = MakeImage(band, "seg" + i, BladeLoopTheme.StreamColours[i]);
            AnchorIn(seg.rectTransform, x, 0.60f, x + w, 1f);

            // Only label inside the block when the block is wide enough to hold it;
            // LOSS is ~1.5 % of the bar and a label there would spill across CHAR.
            if (w > 0.09f)
            {
                var v = MakeText(seg.rectTransform, "v", $"{pct[i]:0.#}%", 15f,
                                 i == 0 ? BladeLoopTheme.Panel : BladeLoopTheme.Bone, BladeLoopTheme.MonoBold);
                v.alignment = TextAlignmentOptions.Center; Stretch(v.rectTransform);
            }
            x += w;

            // THE KEY IS ON EVEN SLOTS, NOT UNDER ITS OWN SEGMENT. Aligning each label
            // to the block it describes reads well for FIBRE and OIL and then collapses:
            // CHAR and LOSS together hold under 8 % of the width and their labels
            // overlapped each other and ran off the end of the bar.
            float k0 = i * 0.2f;
            var key = MakeImage(band, "k" + i, BladeLoopTheme.StreamColours[i]);
            AnchorIn(key.rectTransform, k0, 0.30f, k0 + 0.009f, 0.46f);
            var lbl = MakeText(band, "l" + i, $"{name[i]}   {pct[i]:0.#}%",
                               13f, BladeLoopTheme.Muted, BladeLoopTheme.SansBold);
            lbl.alignment = TextAlignmentOptions.Left; lbl.characterSpacing = 1.4f;
            AnchorIn(lbl.rectTransform, k0 + 0.016f, 0.26f, k0 + 0.19f, 0.50f);
        }

        var cap = MakeText(band, "cap", "Percentage of everything fed in, at the design set-point. The five always sum to 100.",
                           13f, BladeLoopTheme.Faint, BladeLoopTheme.Sans);
        cap.alignment = TextAlignmentOptions.Left;
        AnchorIn(cap.rectTransform, 0f, 0f, 1f, 0.18f);

        cursor += H + 20f;
    }

    /// <summary>
    /// One input: its range with the set-point marked in place, how much it actually
    /// moves the result, and the explanation.
    ///
    /// THE RANGE AND THE WEIGHT ARE MIRRORED, NOT READ. min/max mirror the sliders in
    /// OrderDashboardController.StepPlan; <paramref name="weight"/> mirrors the term
    /// for this input in ProcessModel.OverallDeviation. Neither is exposed as a public
    /// constant. If either is ever changed there, change it here too - the set-point
    /// itself does come from ProcessModel and will not drift.
    /// </summary>
    void InputCard(string label, float min, float max, float opt, string unit, string fmt,
                   float weight, string blurb)
    {
        float textH = Measure(blurb, 15f, BladeLoopTheme.Sans, ContentW - 80f);
        float H = 96f + textH;
        var card = Panel(H, 0.026f);

        var name = MakeText(card, "n", label, 14f, BladeLoopTheme.Bone, BladeLoopTheme.SansBold);
        name.alignment = TextAlignmentOptions.Left; name.characterSpacing = 1.8f;
        AnchorInPx(name.rectTransform, 0.022f, 0.30f, 22f, 26f);

        var big = MakeText(card, "v", opt.ToString(fmt) + " " + unit, 22f, BladeLoopTheme.Oxide, BladeLoopTheme.MonoBold);
        big.alignment = TextAlignmentOptions.Left;
        AnchorInPx(big.rectTransform, 0.022f, 0.30f, 52f, 30f);

        // ---- the range, with the set-point standing on it ----
        var track = MakeImage(card, "tr", BladeLoopTheme.RuleSoft);
        AnchorInPx(track.rectTransform, 0.33f, 0.72f, 44f, 10f);

        float t01 = Mathf.Clamp01((opt - min) / Mathf.Max(0.0001f, max - min));
        var mark = MakeImage(track.rectTransform, "m", BladeLoopTheme.Oxide);
        mark.rectTransform.anchorMin = new Vector2(t01, -0.55f);
        mark.rectTransform.anchorMax = new Vector2(t01, 1.55f);
        mark.rectTransform.sizeDelta = new Vector2(4f, 0f);
        mark.rectTransform.anchoredPosition = Vector2.zero;

        // Two HALF rects, not one shared rect with opposed alignments. Sharing it puts
        // two labels in the same rectangle: nothing collides while the numbers are
        // short, and the layout audit can never tell the difference between that and a
        // real overlap.
        var lo = MakeText(card, "lo", min.ToString(fmt), 12f, BladeLoopTheme.Faint, BladeLoopTheme.Mono);
        lo.alignment = TextAlignmentOptions.Left;
        AnchorInPx(lo.rectTransform, 0.33f, 0.52f, 60f, 20f);
        var hi = MakeText(card, "hi", max.ToString(fmt) + " " + unit, 12f, BladeLoopTheme.Faint, BladeLoopTheme.Mono);
        hi.alignment = TextAlignmentOptions.Right;
        AnchorInPx(hi.rectTransform, 0.53f, 0.72f, 60f, 20f);

        var setLbl = MakeText(card, "sl", "SET-POINT", 11f, BladeLoopTheme.Faint, BladeLoopTheme.SansBold);
        setLbl.alignment = TextAlignmentOptions.Left; setLbl.characterSpacing = 1.6f;
        AnchorInPx(setLbl.rectTransform, 0.33f, 0.72f, 22f, 18f);

        // ---- how much this input actually matters ----
        var wLbl = MakeText(card, "wl", "INFLUENCE", 11f, BladeLoopTheme.Faint, BladeLoopTheme.SansBold);
        wLbl.alignment = TextAlignmentOptions.Left; wLbl.characterSpacing = 1.6f;
        AnchorInPx(wLbl.rectTransform, 0.76f, 0.978f, 22f, 18f);

        var wTrack = MakeImage(card, "wt", BladeLoopTheme.RuleSoft);
        AnchorInPx(wTrack.rectTransform, 0.76f, 0.90f, 46f, 10f);
        var wFill = MakeImage(wTrack.rectTransform, "wf", BladeLoopTheme.Oxide);
        wFill.rectTransform.anchorMin = Vector2.zero;
        wFill.rectTransform.anchorMax = new Vector2(weight / 0.32f, 1f);   // 0.32 is the largest weight
        wFill.rectTransform.offsetMin = Vector2.zero; wFill.rectTransform.offsetMax = Vector2.zero;

        var wPct = MakeText(card, "wp", $"{weight * 100f:0} %", 16f, BladeLoopTheme.Bone, BladeLoopTheme.MonoBold);
        wPct.alignment = TextAlignmentOptions.Right;
        AnchorInPx(wPct.rectTransform, 0.90f, 0.978f, 46f, 22f);

        // ---- the explanation ----
        var body = MakeText(card, "b", blurb, 15f, BladeLoopTheme.Muted, BladeLoopTheme.Sans);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TextWrappingModes.Normal;
        AnchorInPx(body.rectTransform, 0.022f, 0.978f, 88f, textH);

        cursor += H + 12f;
    }

    /// <summary>The three grades as three cards, thresholds pulled live from OrderContext
    /// so this page can never contradict what the app actually grades to.</summary>
    void GradeCards()
    {
        const float H = 168f;
        var band = Panel(H, 0f);

        string[] tier = { "HIGH", "MID", "LOW" };
        string[] pur  = { $"≥ {OrderContext.HighPurity:0} %", $"≥ {OrderContext.MidPurity:0} %", "below" };
        string[] ten  = { $"≥ {OrderContext.HighTensile:0} %", $"≥ {OrderContext.MidTensile:0} %", "below" };
        string[] to   = { "Composite manufacturing", "Precast concrete, casting", "Cement kiln co-processing" };
        string[] note = { "Back into new structural parts.",
                          "Not structural, but sold today as reinforcing filler.",
                          "Glass replaces sand, resin replaces coal." };
        Color[]  hue  = { BladeLoopTheme.StreamGas, BladeLoopTheme.Oxide, BladeLoopTheme.Faint };

        for (int i = 0; i < 3; i++)
        {
            float x0 = i / 3f, x1 = x0 + 1f / 3f;
            var card = MakeImage(band, "g" + i, new Color(1f, 1f, 1f, 0.028f));
            AnchorIn(card.rectTransform, x0 + (i == 0 ? 0f : 0.007f), 0f, x1 - (i == 2 ? 0f : 0.007f), 1f);

            var edge = MakeImage(card.rectTransform, "e", hue[i]);
            AnchorIn(edge.rectTransform, 0f, 0f, 0.004f, 1f);

            var t = MakeText(card.rectTransform, "t", tier[i] + " GRADE", 15f, hue[i], BladeLoopTheme.SansBold);
            t.alignment = TextAlignmentOptions.Left; t.characterSpacing = 1.8f;
            AnchorIn(t.rectTransform, 0.06f, 0.79f, 0.96f, 0.94f);

            var m = MakeText(card.rectTransform, "m",
                             $"<mspace=0.60em>PURITY   {pur[i]}\nTENSILE  {ten[i]}</mspace>",
                             15f, BladeLoopTheme.Bone, BladeLoopTheme.Mono);
            m.alignment = TextAlignmentOptions.TopLeft;
            AnchorIn(m.rectTransform, 0.06f, 0.47f, 0.96f, 0.77f);

            var d = MakeText(card.rectTransform, "d", to[i], 14f, BladeLoopTheme.Bone, BladeLoopTheme.SansBold);
            d.alignment = TextAlignmentOptions.TopLeft;
            d.textWrappingMode = TextWrappingModes.Normal;
            AnchorIn(d.rectTransform, 0.06f, 0.28f, 0.96f, 0.45f);

            var nn = MakeText(card.rectTransform, "n", note[i], 13f, BladeLoopTheme.Faint, BladeLoopTheme.Sans);
            nn.alignment = TextAlignmentOptions.TopLeft;
            nn.textWrappingMode = TextWrappingModes.Normal;
            AnchorIn(nn.rectTransform, 0.06f, 0.05f, 0.96f, 0.26f);
        }

        cursor += H + 20f;
    }

    /// <summary>What the app actually offers. Every claim here is a screen that exists:
    /// the three presets are OrderContext's preset table, the tour is the five scenes in
    /// TourSceneSequencer, and the report is OutcomeReport.</summary>
    void FeatureCards()
    {
        string[] head = { "THREE WORKED EXAMPLES", "CUSTOM ORDER", "THE GUIDED TOUR", "A RUN REPORT" };
        string[] blurb = {
            "One farm, three outcomes. Run the same feedstock for a composite manufacturer, a precast concrete producer or a cement works, and watch where the material goes.",
            "Bring your own order. Say who is buying, what is in the yard, or what your shredder can manage — the plant works out every set-point that is optimal for somebody, and you choose along the trade-off.",
            "Follow the material through all five stages with narration, with your order's numbers carried on a panel beside the plant the whole way.",
            "A self-contained report at the end: what you asked for, what you set, what the plant produced, and whether it filled the order."
        };

        const float CardH = 158f;
        var band = Panel(CardH * 2f + 14f, 0f);

        for (int i = 0; i < 4; i++)
        {
            int col = i % 2, row = i / 2;
            float x0 = col * 0.5f, x1 = x0 + 0.5f;
            float yTop = 1f - row * (CardH + 14f) / (CardH * 2f + 14f);
            float yBot = yTop - CardH / (CardH * 2f + 14f);

            var card = MakeImage(band, "f" + i, new Color(1f, 1f, 1f, 0.028f));
            AnchorIn(card.rectTransform, x0 + (col == 0 ? 0f : 0.007f), yBot, x1 - (col == 1 ? 0f : 0.007f), yTop);

            var num = MakeText(card.rectTransform, "i", $"{i + 1:00}", 13f, BladeLoopTheme.Oxide, BladeLoopTheme.MonoBold);
            num.alignment = TextAlignmentOptions.Left;
            AnchorIn(num.rectTransform, 0.035f, 0.72f, 0.12f, 0.90f);

            var h = MakeText(card.rectTransform, "h", head[i], 15f, BladeLoopTheme.Bone, BladeLoopTheme.SansBold);
            h.alignment = TextAlignmentOptions.Left; h.characterSpacing = 1.8f;
            AnchorIn(h.rectTransform, 0.10f, 0.72f, 0.97f, 0.90f);

            var b = MakeText(card.rectTransform, "b", blurb[i], 14f, BladeLoopTheme.Muted, BladeLoopTheme.Sans);
            b.alignment = TextAlignmentOptions.TopLeft;
            b.textWrappingMode = TextWrappingModes.Normal;
            AnchorIn(b.rectTransform, 0.10f, 0.08f, 0.97f, 0.66f);
        }

        cursor += CardH * 2f + 14f + 20f;
    }

    /// <summary>Numbered section break: a hairline, an index, and a title.</summary>
    void Section(string index, string title)
    {
        cursor += 22f;
        const float H = 54f;
        var band = Panel(H, 0f);

        var rule = MakeImage(band, "r", BladeLoopTheme.Rule);
        AnchorIn(rule.rectTransform, 0f, 0.97f, 1f, 1f);

        var idx = MakeText(band, "i", index, 13f, BladeLoopTheme.Oxide, BladeLoopTheme.MonoBold);
        idx.alignment = TextAlignmentOptions.Left;
        AnchorIn(idx.rectTransform, 0f, 0.08f, 0.05f, 0.72f);

        var t = MakeText(band, "t", title, 20f, BladeLoopTheme.Bone, BladeLoopTheme.SansBold);
        t.alignment = TextAlignmentOptions.Left;
        AnchorIn(t.rectTransform, 0.045f, 0.05f, 1f, 0.78f);

        cursor += H + 6f;
    }

    /// <summary>An aside with an accent edge - used where a sentence is a caveat about
    /// the model rather than a description of it.</summary>
    void Callout(string text)
    {
        float th = Measure(text, 15f, BladeLoopTheme.Sans, ContentW - 60f);
        float H = th + 36f;
        var box = Panel(H, 0.030f);

        var edge = MakeImage(box, "e", BladeLoopTheme.Oxide);
        AnchorIn(edge.rectTransform, 0f, 0f, 0.003f, 1f);

        var t = MakeText(box, "t", text, 15f, BladeLoopTheme.Muted, BladeLoopTheme.Sans);
        t.alignment = TextAlignmentOptions.TopLeft;
        t.textWrappingMode = TextWrappingModes.Normal;
        AnchorInPx(t.rectTransform, 0.018f, 0.982f, 18f, th);

        cursor += H + 18f;
    }

    void SourceList(string[] items)
    {
        foreach (var s in items)
        {
            float th = Measure(s, 14f, BladeLoopTheme.Sans, ContentW - 40f);
            var row = Panel(th + 8f, 0f);

            var dot = MakeImage(row, "d", BladeLoopTheme.Faint);
            AnchorInPx(dot.rectTransform, 0.004f, 0.012f, 9f, 5f);

            var t = MakeText(row, "t", s, 14f, BladeLoopTheme.Muted, BladeLoopTheme.Sans);
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.Normal;
            AnchorInPx(t.rectTransform, 0.020f, 1f, 0f, th);

            cursor += th + 8f + 6f;
        }
        cursor += 10f;
    }

    // ======================================================= text blocks ======

    // place a text block at the current cursor, advance the cursor by its measured height
    //
    // MEASURED AT ContentW, RENDERED WIDER. The viewport is ~1630 units of usable
    // width and ContentW is 1480, so every measurement is deliberately conservative:
    // the reserved height is a little taller than the text needs. That direction is
    // safe (a small gap); the other direction clips, which is what a manual cursor
    // cannot recover from.
    void Block(string text, float size, Color col, TMP_FontAsset font, float extraGap)
    {
        var go = new GameObject("block", typeof(RectTransform));
        go.transform.SetParent(content, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.font = font; t.fontSize = size; t.color = col;
        t.alignment = TextAlignmentOptions.TopLeft; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0,1); rt.anchorMax = new Vector2(1,1); rt.pivot = new Vector2(0.5f,1);
        rt.offsetMin = new Vector2(PadX, 0); rt.offsetMax = new Vector2(-PadX, 0);
        float h = t.GetPreferredValues(text, ContentW, 0f).y;
        rt.sizeDelta = new Vector2(0, h);
        rt.anchoredPosition = new Vector2(0, -cursor);
        cursor += h + extraGap;
    }

    void Body(string text) { Block(text, 16, BladeLoopTheme.Muted, BladeLoopTheme.Sans, 16f); }
    void Lead(string text) { Block(text, 20, BladeLoopTheme.Bone,  BladeLoopTheme.Sans, 16f); }

    void Quote(string text)
    {
        // measure, draw a raised surface, then the italic text inside it
        var box = MakeImage(content, "quote", BladeLoopTheme.SkyWarm);
        var brt = box.rectTransform;
        brt.anchorMin = new Vector2(0,1); brt.anchorMax = new Vector2(1,1); brt.pivot = new Vector2(0.5f,1);
        brt.offsetMin = new Vector2(PadX,0); brt.offsetMax = new Vector2(-PadX,0);

        var bar = MakeImage(box.rectTransform, "bar", BladeLoopTheme.Oxide);
        AnchorIn(bar.rectTransform, 0f, 0f, 0.003f, 1f);

        var go = new GameObject("qt", typeof(RectTransform));
        go.transform.SetParent(box.transform, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.font = BladeLoopTheme.Sans; t.fontSize = 16; t.color = BladeLoopTheme.Bone; t.fontStyle = FontStyles.Italic;
        t.alignment = TextAlignmentOptions.TopLeft; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        var trt = go.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0,1); trt.anchorMax = new Vector2(1,1); trt.pivot = new Vector2(0.5f,1);
        trt.offsetMin = new Vector2(28,0); trt.offsetMax = new Vector2(-20,0);
        float th = t.GetPreferredValues(text, ContentW - 48f, 0f).y;
        trt.sizeDelta = new Vector2(0, th); trt.anchoredPosition = new Vector2(0, -18);
        brt.sizeDelta = new Vector2(0, th + 36);
        brt.anchoredPosition = new Vector2(0, -cursor);
        cursor += th + 36 + 18f;
    }

    // ======================================================= helpers ==========

    /// <summary>A full-width container of an exact height at the current cursor. Every
    /// visual block hangs off one of these, so each one owns a known rectangle and the
    /// cursor arithmetic stays in the caller.</summary>
    RectTransform Panel(float height, float fillAlpha)
    {
        var img = MakeImage(content, "panel", new Color(1f, 1f, 1f, fillAlpha));
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0,1); rt.anchorMax = new Vector2(1,1); rt.pivot = new Vector2(0.5f,1);
        rt.offsetMin = new Vector2(PadX, 0); rt.offsetMax = new Vector2(-PadX, 0);
        rt.sizeDelta = new Vector2(0, height);
        rt.anchoredPosition = new Vector2(0, -cursor);
        img.raycastTarget = false;
        return rt;
    }

    float Measure(string text, float size, TMP_FontAsset font, float width)
    {
        var probe = new GameObject("probe", typeof(RectTransform));
        probe.transform.SetParent(content, false);
        var t = probe.AddComponent<TextMeshProUGUI>();
        t.font = font; t.fontSize = size; t.textWrappingMode = TextWrappingModes.Normal;
        float h = t.GetPreferredValues(text, width, 0f).y;
        Destroy(probe);
        return h;
    }

    /// <summary>Fractional rect inside the parent.</summary>
    static void AnchorIn(RectTransform r, float xmin, float ymin, float xmax, float ymax)
    {
        r.anchorMin = new Vector2(xmin, ymin); r.anchorMax = new Vector2(xmax, ymax);
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    /// <summary>Fractional in x, but hung from the TOP edge at an exact pixel offset and
    /// height - used wherever a row must sit a known distance below the one above it
    /// regardless of how tall the card ended up.</summary>
    static void AnchorInPx(RectTransform r, float xmin, float xmax, float topPx, float hPx)
    {
        r.anchorMin = new Vector2(xmin, 1f); r.anchorMax = new Vector2(xmax, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        r.sizeDelta = new Vector2(0f, hPx);
        r.anchoredPosition = new Vector2(0f, -topPx);
    }

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
        var img = go.GetComponent<Image>(); img.color=col; img.raycastTarget=false; return img;
    }

    TMP_Text MakeText(Transform parent, string name, string text, float size, Color col, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text=text; t.font=font; t.fontSize=size; t.color=col; t.raycastTarget=false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
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
