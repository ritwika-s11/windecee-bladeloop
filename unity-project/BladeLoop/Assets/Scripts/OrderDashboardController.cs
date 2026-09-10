using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Custom Order. Four steps, one question at a time.
///
/// ---------------------------------------------------------------------------
///  THE IDEA
///
///  There is no correct objective. Filling an order QUICKLY and using as LITTLE
///  blade material as possible pull in opposite directions, and which one matters
///  depends entirely on the customer - a recycler with a deadline and a recycler
///  with a limited yard want different plants. So this screen does not pick one
///  and hand back "the answer". It computes every plan that is optimal for
///  somebody (OrderSolver.SolveFrontier) and lets the user say what they care
///  about, in days and blades rather than in kilns and millimetres.
///
///  That is also why the parameter sliders exist after a plan is chosen: moving
///  along the frontier is a real choice, and moving OFF it is a mistake worth
///  seeing. The panel says which one you just made.
///
///  FOUR STEPS, ONE OPEN. Everything used to be on screen at once, which is why
///  it read as a wall of small text. Completed steps collapse to a single line
///  you can click to reopen.
///
///  Same palette and IBM Plex as MainMenuController so the two screens belong to
///  one product. Built entirely at runtime.
/// ---------------------------------------------------------------------------
///
///  SCOPE: this file, and additive methods on OrderSolver. ProcessModel, the three
///  presets, and every other screen are untouched.
///
/// Owner: Akshat.
/// </summary>
public class OrderDashboardController : MonoBehaviour
{
    enum Mode { Buyer = 0, Supply = 1, Constraint = 2 }

    Mode  mode = Mode.Buyer;
    Grade grade = Grade.Mid;
    int   step;                       // which step is expanded, 0..3
    int   reached;                    // furthest step unlocked

    float orderTonnes = 4000f;
    float blades      = 600f;
    float shredMm     = 10f;

    List<OrderSolver.Plan> frontier = new List<OrderSolver.Plan>();
    int   railIndex;
    ProcessModel edited;              // null while the plan sits on the frontier

    RectTransform panel, stepHolder, stepCol, guide;
    TMP_Text guideTitle, guideBody, guideNote;
    TMP_Text gMetricA, gMetricB, gMetricALbl, gMetricBLbl;
    Image guideAccent;

    // ---------------------------------------------------------- type scale ----
    // One modular scale at ratio 1.25, six steps, and nothing off it. The screen
    // previously used thirteen arbitrary sizes, which is most of why it read as
    // unfinished however carefully each rect was placed.
    //
    //   13  eyebrow / label      20  value
    //   16  body                 25  title
    //   31  display              36  hero number
    //
    // Words are set in Sans; Mono is reserved for numerals, where fixed advance
    // width makes columns line up. Small ALL-CAPS labels moved OFF Mono - Plex
    // Mono at 13-15 with heavy tracking on a dark panel was the least readable
    // thing here, and tracking is now 1.6 rather than 3.5-6.
    const float TypeStatement = 31f, TypeEyebrow = 20f, TypeStepTitle = 25f;
    const float TypeMicro = 13f, TypeBody = 16f, TypeHuge = 36f;
    const float TypeLabel = 16f, TypeValue = 20f, TypeButton = 16f;

    const float Left = 0.055f, Right = 0.945f;

    // ============================================================== lifecycle ==

    void Awake()
    {
        BladeLoopTheme.Init();
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem),
                           typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BladeLoopTheme.Panel;
            // A previous run may have left the tour's 72% viewport on the camera.
            cam.rect = new Rect(0f, 0f, 1f, 1f);
        }

        BuildShell();
        Rebuild();
    }

    void BuildShell()
    {
        var go = new GameObject("OrderDashboardCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        var sc = go.GetComponent<CanvasScaler>();
        sc.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.matchWidthOrHeight  = 0.5f;

        // CanvasScaler latches its mode in OnEnable, which fires the instant the
        // component is added - i.e. BEFORE the three lines above run. Left alone it
        // stayed at ConstantPixelSize, so the canvas measured 691x344 instead of
        // ~2041x1016 and every font size and fixed offset below was interpreted
        // against a canvas a third of the intended height. Toggling re-runs OnEnable
        // with the settings actually applied.
        sc.enabled = false;
        sc.enabled = true;
        Canvas.ForceUpdateCanvases();

        panel = Img((RectTransform)go.transform, "Panel", BladeLoopTheme.Panel).rectTransform;
        Anchor(panel, 0f, 0f, 1f, 1f);

        var eyebrow = Label(panel, "eyebrow", "CUSTOM ORDER", TypeEyebrow, BladeLoopTheme.Bone,
                            TextAlignmentOptions.Left, BladeLoopTheme.Mono);
        eyebrow.characterSpacing = 14f;
        Anchor(eyebrow.rectTransform, Left, 0.915f, 0.6f, 0.965f);
        Anchor(Img(panel, "tick", BladeLoopTheme.Oxide).rectTransform, Left, 0.903f, 0.088f, 0.907f);

        var st = Label(panel, "statement", "Tell us what you know. The plant works out the rest.",
                       TypeStatement, BladeLoopTheme.Bone, TextAlignmentOptions.Left, BladeLoopTheme.Sans);
        Anchor(st.rectTransform, Left, 0.845f, 0.82f, 0.898f);

        FixedBtn(panel, "Menu", "←  MENU", 0.868f, Right, 0.938f, 40f, false, BackToMenu);

        // Two columns. The steps had the full width and used about half of it, which
        // is where the empty space came from; the guide fills the other half with
        // something that changes as you work rather than padding.
        stepCol = Rect(panel, "StepColumn");
        Anchor(stepCol, Left, 0.03f, 0.655f, 0.835f);

        BuildGuide();
        BuildTipLayer();
    }

    // ================================================================ tooltips ==

    /// <summary>
    /// WHY THIS EXISTS, AND WHY IT IS NOT A ROW OF (i) BUTTONS.
    ///
    /// Half the words on this screen are process-engineering terms. A planner who has
    /// never run a pyrolysis kiln cannot infer what "retention" or "particle size"
    /// mean, and getting them wrong is not a small mistake - those two inputs carry
    /// 54% of the efficiency weighting between them.
    ///
    /// The obvious fix is an info icon beside every term. That is fourteen icons on
    /// one screen, each one a permanent little smudge competing with the numbers they
    /// are meant to support, and the eye has to learn to ignore them - at which point
    /// they have stopped working.
    ///
    /// So: the TERM ITSELF is the affordance. A one-pixel dashed rule under a label
    /// means "there is a definition behind this". It is always visible, so the user
    /// can see at a glance which words are explainable without hunting for hover
    /// targets; it adds no new objects to the layout; and it is the convention every
    /// data tool already uses for a glossary term. It is deliberately NOT a solid
    /// underline, because that is a hyperlink and these go nowhere.
    ///
    /// A reveal-on-hover icon was the other candidate and was rejected on
    /// DISCOVERABILITY: with nothing on screen by default, a user who does not already
    /// suspect help exists never moves the mouse to find it. In a demo they simply
    /// never see it.
    ///
    /// The text is SHORT AND LIVE: one or two lines that describe the metric by saying
    /// what it is doing at this moment - "shredded blade goes into the kiln at 7,321
    /// kg/h", not "feed rate is the rate at which material enters the kiln". The reader
    /// is looking at a number and wants to know what it means; handing them an abstract
    /// definition leaves them to make that connection themselves.
    ///
    /// An earlier version appended ProcessModel's full Info() paragraphs. Those are
    /// good writing but they turned every tooltip into a wall of text, and a wall of
    /// text on hover is one nobody reads. The diagnosis belongs in the guide panel,
    /// which the user can dwell on; the tooltip gets the reading and stops.
    /// </summary>
    void BuildTipLayer()
    {
        tipRoot = Img(panel, "Tip", BladeLoopTheme.SkyWarm).rectTransform;
        tipRoot.pivot = new Vector2(0f, 0f);
        tipRoot.anchorMin = tipRoot.anchorMax = new Vector2(0f, 0f);
        tipRoot.sizeDelta = new Vector2(TipW, 100f);

        // A lit top edge and a hard left accent: the same "raised instrument surface"
        // language the step cards use, so the tooltip reads as part of this product
        // rather than an OS control that wandered in.
        var lip = Img(tipRoot, "lip", new Color(1f, 1f, 1f, 0.14f));
        Anchor(lip.rectTransform, 0f, 0.995f, 1f, 1f);
        var acc = Img(tipRoot, "acc", BladeLoopTheme.Oxide);
        Anchor(acc.rectTransform, 0f, 0f, 0.004f, 1f);

        tipTitle = Label(tipRoot, "tt", "", TypeMicro, BladeLoopTheme.Bone,
                         TextAlignmentOptions.TopLeft, BladeLoopTheme.SansBold);
        tipTitle.characterSpacing = 1.6f;

        tipBody = Label(tipRoot, "tb", "", TypeBody, BladeLoopTheme.Muted,
                        TextAlignmentOptions.TopLeft, BladeLoopTheme.Sans);
        tipBody.textWrappingMode = TextWrappingModes.Normal;
        tipBody.lineSpacing = 6f;

        tipGroup = tipRoot.gameObject.AddComponent<CanvasGroup>();
        tipGroup.alpha = 0f;
        tipGroup.blocksRaycasts = false;   // must never eat the hover that spawned it
        tipGroup.interactable   = false;
        tipRoot.SetAsLastSibling();        // above every card, always
    }

    const float TipW = 430f, TipPad = 18f;

    RectTransform tipRoot;
    TMP_Text tipTitle, tipBody;
    CanvasGroup tipGroup;
    Coroutine tipFade;

    /// <summary>Mark a label as having a definition: draw the dashed rule under it and
    /// wire the hover. <paramref name="body"/> is a delegate so the numbers inside it
    /// are read at hover time, not at build time.</summary>
    void Tip(TMP_Text label, string title, System.Func<string> body)
    {
        if (label == null) return;

        // The label's own rect is usually much wider than the glyphs (it is a layout
        // box, not a text box), so a rule stretched across it would underline empty
        // space. Force a mesh update and use the RENDERED width.
        label.ForceMeshUpdate();
        float textW = Mathf.Min(label.rectTransform.rect.width,
                                label.GetRenderedValues(true).x + 2f);
        if (textW < 4f) textW = label.rectTransform.rect.width;

        var rule = Img(label.rectTransform, "tipRule", new Color(1f, 1f, 1f, 0.22f));
        rule.sprite = BladeLoopTheme.DashedRule();
        rule.type   = Image.Type.Tiled;
        rule.raycastTarget = false;
        // THE RULE HAS TO FOLLOW THE LABEL'S ALIGNMENT. Pinned to the left edge
        // regardless, a right-aligned label like "BLADES CONSUMED" - whose box is 558
        // units wide but whose glyphs sit in the last 123 of it - got a dashed line
        // stranded in empty space 400 units from the word it belonged to.
        var ha = label.horizontalAlignment;
        float ax = ha == HorizontalAlignmentOptions.Right  ? 1f
                 : ha == HorizontalAlignmentOptions.Center ? 0.5f
                                                           : 0f;
        var rr = rule.rectTransform;
        rr.anchorMin = new Vector2(ax, 0.5f);
        rr.anchorMax = new Vector2(ax, 0.5f);
        rr.pivot     = new Vector2(ax, 0.5f);
        rr.sizeDelta = new Vector2(textW, 1f);
        // Below the descender line of a 13pt cap, not glued to the baseline.
        rr.anchoredPosition = new Vector2(0f, -label.fontSize * 0.62f);

        label.raycastTarget = true;
        var trig = label.gameObject.GetComponent<EventTrigger>()
                ?? label.gameObject.AddComponent<EventTrigger>();

        var baseCol = label.color;
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ =>
        {
            label.color = BladeLoopTheme.Bone;
            rule.color  = BladeLoopTheme.Oxide;
            // Anchor to the RULE, not the label: the rule is exactly as wide as the
            // glyphs and sits under them, so the tooltip lands beside the word instead
            // of beside the left edge of whatever layout box the word happens to be in.
            ShowTip(title, body(), rr);
        });
        trig.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ =>
        {
            label.color = baseCol;
            rule.color  = new Color(1f, 1f, 1f, 0.22f);
            HideTip();
        });
        trig.triggers.Add(exit);
    }

    /// <summary>
    /// The definition behind one term, keyed by the exact label text so the same word
    /// gets the same explanation wherever it appears - "FEED RATE" is a set-point
    /// readout on step three and a slider on step four, and a user who learned it once
    /// must not be told something different the second time.
    ///
    /// Returns null for a term with no definition, which is the signal not to draw a
    /// dashed rule at all. Plain-English labels ("BLADES YOU HAVE", "WHO IS BUYING")
    /// are deliberately absent: underlining a phrase that needs no explanation trains
    /// the user to ignore the rule everywhere else.
    ///
    /// KEEP THESE SHORT - one or two lines. Each one says what the metric is BY SAYING
    /// WHAT IT IS DOING RIGHT NOW: not "feed rate is the material entering the kiln per
    /// hour" but "shredded blade goes into the kiln at 4,000 kg/h". A definition in the
    /// abstract makes the reader do the work of connecting it to the figure beside it;
    /// a definition with the live number in it has already done that.
    ///
    /// Returned as a DELEGATE, evaluated on hover, so a term read after moving a slider
    /// reports the new value rather than the one that was there when the card was built.
    ///
    /// Where the screen already renders a figure, these read that rendered STRING rather
    /// than recomputing it - a tooltip that disagreed with the number three centimetres
    /// above it would be worse than no tooltip at all.
    /// </summary>
    System.Func<string> TipFor(string label)
    {
        switch (label)
        {
            // ---- the four kiln set-points -------------------------------------
            case "KILN TEMPERATURE":
            case "TEMPERATURE":
                return () => $"The kiln is held at {Live().TempC:0} °C. At the 600 °C set-point the "
                           + "resin cracks off the fibre cleanly.";

            case "RETENTION":
            case "RESIDENCE":
                return () => $"Each particle spends {Live().RetentionMin:0} minutes inside the kiln. "
                           + "The set-point is 35 minutes.";

            case "FEED RATE":
                return () => $"Shredded blade goes into the kiln at {Live().FeedKgH:N0} kg/h. Design "
                           + "throughput is 6,500 kg/h.";

            case "PARTICLE SIZE":
            case "PARTICLE":
                return () => $"The blade is shredded to {Live().ParticleSizeMm:0.#} mm before it enters "
                           + "the kiln. At the 2 mm set-point heat reaches every core.";

            // ---- what the user typed in ---------------------------------------
            case "FINEST YOUR SHREDDER GOES":
                return () => $"Your shredder grinds no finer than {shredMm:0.#} mm. That is what caps "
                           + "how fast material can be fed.";

            case "TONNES OF RECOVERED FIBRE":
                return () => $"You are asking for {orderTonnes:N0} t of finished fibre — not of blade. "
                           + "The plant works backwards to the blade tonnage it has to put in.";

            case "BLADES YOU HAVE":
                return () => $"{blades:N0} blades in the yard, about "
                           + $"{blades * OrderContext.BladeMassTonnes:N0} t of material.";

            // ---- the two rail consequences, in all three modes ------------------
            // Read straight off the rendered figure so the tooltip cannot drift from it.
            case "DAYS TO FILL IT":
                return () => $"At this plan the kiln runs {RailA()} days to fill the order. Continuous "
                           + "running only — no shifts, downtime, warm-up or shredding counted.";

            case "DAYS OF RUNNING":
                return () => $"At this plan the kiln runs {RailA()} days to work through your blades. "
                           + "Continuous running only — no shifts, downtime or shredding counted.";

            case "BLADES CONSUMED":
                return () => $"This plan gets through {RailB()} blades, at "
                           + $"{OrderContext.BladeMassTonnes:0.#} t of material each.";

            case "TONNES OF FIBRE":
                return () => $"Your blades yield {RailB()} of finished fibre at this plan.";

            case "KG PER HOUR":
                return () => $"This plan produces {RailA()} kg of reclaimed fibre every hour it runs.";

            case "OF EVERY TONNE BECOMES FIBRE":
                return () => $"{RailB()} of each tonne fed in leaves as fibre. The rest leaves as oil, "
                           + "syngas, char and loss.";

            // ---- the five output streams ---------------------------------------
            case "FIBRE":
                return () => { var s = Live().OutputSplit();
                    return $"Reclaimed glass fibre — the product you sell. Right now {s.GlassPct:0.0}% "
                         + $"of the feed, {s.GlassKgH:N0} kg/h."; };
            case "OIL":
                return () => { var s = Live().OutputSplit();
                    return $"Pyrolysis oil, condensed and burned as plant fuel. Right now {s.OilPct:0.0}% "
                         + $"of the feed, {s.OilKgH:N0} kg/h."; };
            case "SYNGAS":
                return () => { var s = Live().OutputSplit();
                    return $"Light gas piped back to the kiln burners. Right now {s.SyngasPct:0.0}% of "
                         + $"the feed, {s.SyngasKgH:N0} kg/h."; };
            case "CHAR":
                return () => { var s = Live().OutputSplit();
                    return $"Carbon residue left behind on the fibre, sold on. Right now {s.CharPct:0.0}% "
                         + $"of the feed, {s.CharKgH:N0} kg/h."; };
            case "LOSS":
                return () => { var s = Live().OutputSplit();
                    return $"Dust and moisture that never becomes product. Right now {s.LossPct:0.0}% of "
                         + $"the feed, {s.LossKgH:N0} kg/h."; };

            default: return null;
        }
    }

    /// <summary>
    /// The plan the tooltips describe. STEP-AWARE on purpose: on "what matters to you"
    /// the truth is wherever the rail handle currently sits, and on "your plan" it is
    /// whatever the sliders have been moved to. Asking Current() on both would report a
    /// committed plan while the rail was showing a different one.
    /// </summary>
    ProcessModel Live()
    {
        if (step >= 3)
        {
            var m = Current();
            if (m != null) return m;
        }
        if (frontier.Count > 0) return frontier[Mathf.Clamp(railIndex, 0, frontier.Count - 1)].model;
        return Current() ?? new ProcessModel();
    }

    string RailA() { return railA != null ? railA.text : "—"; }
    string RailB() { return railB != null ? railB.text : "—"; }

    /// <summary>Attach a tip to a label if the term has a definition. Safe on null.</summary>
    void TipIfKnown(TMP_Text label, string term)
    {
        var body = TipFor(term);
        if (body != null) Tip(label, term, body);
    }

    void ShowTip(string title, string body, RectTransform near)
    {
        if (tipRoot == null) return;

        tipTitle.text = title.ToUpperInvariant();
        tipBody.text  = body;

        // Measure the wrapped body, then size the panel to it. A fixed-height tooltip
        // either clips the long entries or leaves a hole under the short ones.
        float innerW = TipW - TipPad * 2f;
        tipBody.rectTransform.sizeDelta = new Vector2(innerW, 0f);
        float bodyH = tipBody.GetPreferredValues(body, innerW, 0f).y;
        float titleH = TypeMicro * 1.55f;
        float h = TipPad + titleH + 8f + bodyH + TipPad;
        tipRoot.sizeDelta = new Vector2(TipW, h);

        TopPin(tipTitle.rectTransform, TipPad / TipW, 1f - TipPad / TipW, TipPad, titleH);
        TopPin(tipBody.rectTransform,  TipPad / TipW, 1f - TipPad / TipW, TipPad + titleH + 8f, bodyH);

        // Position in PANEL space, anchored to the label, then clamped so the tooltip
        // can never leave the canvas - the terms nearest the right edge and the bottom
        // row of set-points are exactly the ones that would have pushed it off.
        var c = new Vector3[4];
        near.GetWorldCorners(c);
        var pr = panel.rect;
        Vector2 bl = panel.InverseTransformPoint(c[0]);   // bottom-left of the label
        Vector2 tr = panel.InverseTransformPoint(c[2]);   // top-right

        float leftX  = bl.x - pr.xMin;
        float rightX = tr.x - pr.xMin;

        // OPEN TOWARDS THE MIDDLE. A term in the right half opens leftward from its
        // right edge; anything else opens rightward from its left edge. Growing
        // rightward from a term near the right edge threw the panel across the guide
        // column - covering the very figures the user was trying to understand - and
        // then the clamp below shoved it back so it no longer lined up with anything.
        bool rightHalf = (leftX + rightX) * 0.5f > pr.width * 0.55f;
        float x = rightHalf ? rightX - TipW : leftX;

        float y = tr.y - pr.yMin + 10f;                   // sit above the term
        if (y + h > pr.height - 8f) y = (bl.y - pr.yMin) - h - 10f;   // flip below
        x = Mathf.Clamp(x, 8f, pr.width  - TipW - 8f);
        y = Mathf.Clamp(y, 8f, pr.height - h    - 8f);
        tipRoot.anchoredPosition = new Vector2(x, y);

        if (tipFade != null) StopCoroutine(tipFade);
        tipFade = StartCoroutine(FadeTip(1f, 0.10f));
    }

    void HideTip()
    {
        if (tipRoot == null) return;
        if (tipFade != null) StopCoroutine(tipFade);
        tipFade = StartCoroutine(FadeTip(0f, 0.08f));
    }

    IEnumerator FadeTip(float to, float seconds)
    {
        float from = tipGroup.alpha, t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            tipGroup.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        tipGroup.alpha = to;
    }

    // ================================================================== guide ==

    /// <summary>
    /// The right-hand guide. Not decoration and not static help: it says what the
    /// thing you are touching right now actually does, and carries the two live
    /// numbers that matter at this step. Moving all of that out of the step column
    /// is also what let the steps themselves get quiet.
    /// </summary>
    void BuildGuide()
    {
        guide = Img(panel, "Guide", new Color(1f, 1f, 1f, 0.022f)).rectTransform;
        Anchor(guide, 0.675f, 0.03f, Right, 0.835f);

        guideAccent = Img(guide, "accent", BladeLoopTheme.Oxide);
        Anchor(guideAccent.rectTransform, 0f, 0f, 0.005f, 1f);

        var h = Label(guide, "gh", "GUIDE", TypeMicro, BladeLoopTheme.Muted,
                      TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
        h.characterSpacing = 1.6f;
        Anchor(h.rectTransform, 0.06f, 0.930f, 0.7f, 0.978f);

        guideTitle = Label(guide, "gt", "", 25f, BladeLoopTheme.Bone,
                           TextAlignmentOptions.TopLeft, BladeLoopTheme.SansBold);
        guideTitle.textWrappingMode = TextWrappingModes.Normal;
        Anchor(guideTitle.rectTransform, 0.06f, 0.830f, 0.94f, 0.920f);

        guideBody = Label(guide, "gb", "", TypeBody, BladeLoopTheme.Muted,
                          TextAlignmentOptions.TopLeft, BladeLoopTheme.Sans);
        guideBody.textWrappingMode = TextWrappingModes.Normal;
        guideBody.lineSpacing = 10f;
        // 0.700 not 0.666: the body runs three lines at every step, and a four-line box
        // left a 110-unit hole between the last line and the rule under it. Everything
        // below shifts up by the same 0.038 so the internal rhythm is unchanged.
        Anchor(guideBody.rectTransform, 0.06f, 0.700f, 0.94f, 0.826f);

        Anchor(Img(guide, "gr1", BladeLoopTheme.Rule).rectTransform, 0.06f, 0.688f, 0.94f, 0.6895f);

        // ---- your selection so far ------------------------------------------
        // The panel used to leave a 135-unit hole under the body and another 119
        // under an empty note. This fills both with the thing a user actually wants
        // on screen while they work: what they have chosen so far.
        var selHdr = Label(guide, "gsh", "YOUR SELECTION", TypeMicro, BladeLoopTheme.Muted,
                           TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
        selHdr.characterSpacing = 1.6f;
        Anchor(selHdr.rectTransform, 0.06f, 0.649f, 0.94f, 0.679f);

        float[] rowTop = { 0.6321f, 0.5930f, 0.5539f, 0.5148f };
        for (int i = 0; i < 4; i++)
        {
            selLbl[i] = Label(guide, "sl" + i, "", TypeMicro, BladeLoopTheme.Faint,
                              TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
            selLbl[i].characterSpacing = 1.6f;
            Anchor(selLbl[i].rectTransform, 0.06f, rowTop[i] - 0.0342f, 0.42f, rowTop[i]);

            selVal[i] = Label(guide, "sv" + i, "", TypeValue, BladeLoopTheme.Bone,
                              TextAlignmentOptions.Right, BladeLoopTheme.MonoBold);
            Anchor(selVal[i].rectTransform, 0.42f, rowTop[i] - 0.0342f, 0.94f, rowTop[i]);
        }

        Anchor(Img(guide, "gr2", BladeLoopTheme.Rule).rectTransform, 0.06f, 0.461f, 0.94f, 0.4625f);

        gMetricALbl = Label(guide, "gal", "", TypeMicro, BladeLoopTheme.Muted,
                            TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
        gMetricALbl.characterSpacing = 1.6f;
        Anchor(gMetricALbl.rectTransform, 0.06f, 0.422f, 0.94f, 0.451f);
        gMetricA = Label(guide, "ga", "", TypeHuge, BladeLoopTheme.Bone,
                         TextAlignmentOptions.Left, BladeLoopTheme.MonoBold);
        Anchor(gMetricA.rectTransform, 0.06f, 0.358f, 0.94f, 0.417f);

        gMetricBLbl = Label(guide, "gbl", "", TypeMicro, BladeLoopTheme.Muted,
                            TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
        gMetricBLbl.characterSpacing = 1.6f;
        Anchor(gMetricBLbl.rectTransform, 0.06f, 0.317f, 0.94f, 0.346f);
        gMetricB = Label(guide, "gbv", "", TypeHuge, BladeLoopTheme.Bone,
                         TextAlignmentOptions.Left, BladeLoopTheme.MonoBold);
        Anchor(gMetricB.rectTransform, 0.06f, 0.253f, 0.94f, 0.312f);

        Anchor(Img(guide, "gr3", BladeLoopTheme.Rule).rectTransform, 0.06f, 0.236f, 0.94f, 0.2375f);

        guideNote = Label(guide, "gn", "", TypeBody, BladeLoopTheme.Oxide,
                          TextAlignmentOptions.TopLeft, BladeLoopTheme.Sans);
        guideNote.textWrappingMode = TextWrappingModes.Normal;
        guideNote.lineSpacing = 8f;
        Anchor(guideNote.rectTransform, 0.06f, 0.049f, 0.94f, 0.221f);
    }

    readonly TMP_Text[] selLbl = new TMP_Text[4];
    readonly TMP_Text[] selVal = new TMP_Text[4];

    /// <summary>Four rows that always carry something, so the block never leaves a
    /// gap: how you came in, who is buying, what you put in, and the plan itself.</summary>
    void PaintSelection()
    {
        if (selLbl[0] == null) return;

        string l1, v1, l2, v2;
        if (mode == Mode.Buyer)
        {
            l1 = "BUYER";    v1 = BuyerName(grade);
            l2 = "ORDER";    v2 = $"{orderTonnes:N0} t";
        }
        else if (mode == Mode.Supply)
        {
            l1 = "BUYER";    v1 = BuyerName(grade);
            l2 = "MATERIAL"; v2 = $"{blades:N0} blades";
        }
        else
        {
            l1 = "SHREDDER"; v1 = $"{shredMm:0.#} mm";
            l2 = "CEILING";  v2 = $"{OrderSolver.MaxFeed(shredMm):N0} kg/h";
        }

        var m = Current();
        string plan = (m == null || step < 3)
                    ? "not chosen yet"
                    : $"{m.TempC:0}°C · {m.ParticleSizeMm:0.#}mm";

        string[] ls = { "APPROACH", l1, l2, "PLAN" };
        string[] vs = { ModeTitle[(int)mode], v1, v2, plan };

        for (int i = 0; i < 4; i++)
        {
            selLbl[i].text = ls[i];
            selVal[i].text = vs[i];
            selVal[i].color = (i == 3 && (m == null || step < 3))
                            ? BladeLoopTheme.Faint : BladeLoopTheme.Bone;
        }
    }

    void UpdateGuide()
    {
        if (guideTitle == null) return;

        string title = "", body = "", note = "";
        string aL = "", aV = "", bL = "", bV = "";
        var accent = BladeLoopTheme.Oxide;

        // The note is a VERDICT only on the last step. Everywhere else it is a quiet
        // aside, so it gets the muted colour - a panel that shouts an orange sentence
        // at you on every screen teaches you to stop reading it.
        bool noteIsVerdict = false;

        if (step == 0)
        {
            title = "Start from what you have";
            body  = "Most people know one of three things: who is buying, what material is sitting in the yard, "
                  + "or what their equipment can manage. Any one of them is enough.";
            // The metric block used to sit empty on this step, which left a 190-unit
            // hole in the middle of the panel. These two give the reader the scale of
            // the thing before they have typed a single number.
            aL = "ONE BLADE";   aV = $"{OrderContext.BladeMassTonnes:0.#} t";
            bL = "ONE TURBINE"; bV = $"{OrderContext.BladesPerTurbine:N0} blades";
            note = "Nothing here is final. Reopening a step later keeps everything you have already chosen.";
        }
        else if (step == 1)
        {
            if (mode == Mode.Buyer)
            {
                title = "How much fibre, and for whom";
                body  = "The buyer sets the quality bar. Composite makers need clean fibre for structural parts; "
                      + "cement works take almost anything. The quantity is finished fibre, not blade material.";
                aL = "BLADE MATERIAL NEEDED"; aV = $"{orderTonnes / 0.65f:N0} t";
                bL = "ROUGHLY";               bV = $"{orderTonnes / 0.65f / OrderContext.BladeMassTonnes:N0} blades";
                note = "Quantity is finished fibre. The plant works backwards from it to the blade "
                     + "material it has to put in.";
            }
            else if (mode == Mode.Supply)
            {
                title = "What the yard holds";
                body  = "With material fixed rather than an order, the question flips: not how fast can we run, "
                      + "but how much fibre can we get out of what is already here.";
                aL = "BLADE MATERIAL"; aV = $"{blades * OrderContext.BladeMassTonnes:N0} t";
                bL = "TURBINES WORTH"; bV = $"{blades / OrderContext.BladesPerTurbine:N0}";
                note = "With supply fixed, the plant reports how much fibre it can get out rather "
                     + "than how long an order takes.";
            }
            else
            {
                title = "Your shredder sets the ceiling";
                body  = "Finer grinding is slower grinding, so particle size caps how fast material can be fed. "
                      + "That one limit is what stops the plant running flat out at perfect quality.";
                aL = "MOST YOU CAN FEED"; aV = $"{OrderSolver.MaxFeed(shredMm):N0} kg/h";
                bL = "AT PARTICLE SIZE";  bV = $"{shredMm:0.#} mm";
                note = "Every plan offered next respects this ceiling. Nothing is suggested that "
                     + "your shredder could not actually feed.";
            }
        }
        else if (step == 2 && frontier.Count > 0)
        {
            var p = frontier[Mathf.Clamp(railIndex, 0, frontier.Count - 1)];
            title = "There is no single best plan";
            body  = "Running hard fills the order sooner but wastes more of every tonne. Running clean uses less "
                  + "material but takes longer. Every position on the rail is the right answer for somebody.";
            aL = "FIBRE PER HOUR"; aV = $"{p.fibreKgH:N0} kg/h";
            bL = "OF EVERY TONNE"; bV = $"{p.yieldFrac * 100f:0.0}%";
            note = (mode != Mode.Constraint && grade == Grade.Low)
                 ? "Low grade offers the same options as precast concrete. Running dirtier than this costs "
                 + "more than it gains."
                 : $"All {frontier.Count} positions on the rail are optimal. Moving the handle trades one "
                 + "consequence for the other — never for nothing.";
        }
        else if (step == 3)
        {
            var m = Current();
            if (m != null)
            {
                bool off;
                note   = Verdict(m, out off);
                noteIsVerdict = true;
                title  = off ? "Something better is available" : "A plan worth running";
                // Campaign figures come from OrderContext's LABEL properties, not the
                // raw numbers. Ritwika added them because Custom Order is the one
                // screen where the user sets the tonnage, so it is the one most
                // likely to be showing a small order - where the raw figures print
                // "0 t, 0 blades, 0.0 days". Every one of those is arithmetically
                // correct and looks like a broken app.
                body   = $"Filling this order takes {OrderContext.FeedTonnesLabel} of blade material — "
                       + $"{OrderContext.BladesLabel}, {OrderContext.TurbinesLabel} — over "
                       + $"{OrderContext.CampaignLabel} of continuous running.";
                accent = off ? BladeLoopTheme.Oxide : BladeLoopTheme.StreamGas;
                aL = "FIBRE PER HOUR"; aV = $"{m.OutputSplit().GlassKgH:N0} kg/h";
                bL = "PURITY";         bV = $"{m.FiberPurityPct:0.0}%";
            }
        }

        guideTitle.text   = title;
        guideBody.text    = body;
        guideNote.text    = note;
        guideNote.color   = noteIsVerdict ? accent : BladeLoopTheme.Faint;
        guideAccent.color = accent;
        gMetricALbl.text  = aL; gMetricA.text = aV;
        gMetricBLbl.text  = bL; gMetricB.text = bV;

        PaintSelection();
    }

    /// <summary>Leaving must clear the order. Choosing a plan calls SetOrder so the
    /// tour can be handed it directly, which would otherwise leave HasOrder true and
    /// write a run that never happened into the home page's last-run line.</summary>
    void BackToMenu()
    {
        OrderContext.Clear();
        OrderContext.ForgetLastRun();
        SceneManager.LoadScene("MainMenu");
    }

    // ================================================================== steps ==

    static readonly string[] StepName =
    { "WHAT YOU KNOW", "YOUR NUMBERS", "WHAT MATTERS TO YOU", "YOUR PLAN" };

    /// <summary>Text on a step you have not reached yet. Rule (#23272E) is a DIVIDER
    /// colour - on the 0.8%-white locked box it fell below the point where a glyph is
    /// legible at all, so step four read as an empty bar rather than a step waiting for
    /// you. Dim enough to recede, present enough to be a word.</summary>
    static Color Locked { get { return new Color(0.42f, 0.46f, 0.51f, 0.42f); } }

    /// <summary>Distance from the top of an OPEN step box down to the centre of its
    /// node, as a fraction of the step column. The header is a fixed 44-unit strip
    /// inset 10, so the centre is 32 units down; the column is ~810 units tall.</summary>
    float HeadNodeFrac
    {
        get
        {
            float h = stepCol != null ? stepCol.rect.height : 0f;
            return h > 1f ? 32f / h : 0.0395f;
        }
    }

    void Rebuild()
    {
        // Clicking a term's own card rebuilds the column and destroys the label the
        // pointer is over, so its PointerExit never fires. Without this the tooltip
        // stays on screen describing something that is no longer there.
        if (tipGroup != null)
        {
            if (tipFade != null) { StopCoroutine(tipFade); tipFade = null; }
            tipGroup.alpha = 0f;
        }

        if (stepHolder != null)
        {
            stepHolder.gameObject.SetActive(false);
            Destroy(stepHolder.gameObject);
        }
        stepHolder = Rect(stepCol, "Steps");
        Anchor(stepHolder, 0f, 0f, 1f, 1f);

        // Fractions of the STEP COLUMN (0.03..0.835 of screen), not of the screen.
        // Each is sized from the content it holds plus the 44-unit header strip.
        // The open step takes ALL the slack. With fixed per-step heights the column
        // stopped 330 units short of the guide panel beside it and the bottom-left
        // quarter of the page was black, which is what made the screen feel like a
        // form on a page rather than a panel. One card, always the same frame, so the
        // layout does not jump as you move between steps either.
        const float ClosedH = 0.072f, GapH = 0.0124f;
        float openH = 1f - 3f * ClosedH - 3f * GapH;

        float top = 1f;
        var nodeY = new float[4];
        for (int i = 0; i < 4; i++)
        {
            bool open = i == step;
            float h = open ? openH : ClosedH;

            if (i > reached) h = ClosedH;
            bool isOpen = open && i <= reached;
            BuildStep(i, top - h, top, isOpen);

            // Where this step's node sits, as a fraction of the column. An open step
            // pins its header to the top of the box; a closed one centres it.
            nodeY[i] = isOpen ? top - HeadNodeFrac : top - h * 0.5f;
            top -= h + GapH;
        }

        // The spine the progress nodes sit on. Drawn AFTER the steps but pushed to the
        // back, and stretched from the FIRST node to the LAST rather than over the
        // whole column - it used to run 250 px past step four into empty space, which
        // read as an unfinished line rather than a route with four stops on it.
        var spine = Img(stepHolder, "spine", BladeLoopTheme.Rule);
        Anchor(spine.rectTransform, 0.0195f, nodeY[3], 0.0205f, nodeY[0]);
        spine.raycastTarget = false;
        spine.transform.SetAsFirstSibling();

        // The travelled portion. HALF-STRENGTH accent, not full: at full strength this
        // line plus the active step's rail put two hard oxide verticals within 20 units
        // of each other and the column read as a warning rather than a route. The
        // NODES carry the state; the spine only has to connect them.
        var spineDone = Img(stepHolder, "spineDone",
                            new Color(BladeLoopTheme.Oxide.r, BladeLoopTheme.Oxide.g,
                                      BladeLoopTheme.Oxide.b, 0.50f));
        Anchor(spineDone.rectTransform, 0.0195f,
               nodeY[Mathf.Clamp(reached, 0, 3)], 0.0205f, nodeY[0]);
        spineDone.raycastTarget = false;
        spineDone.transform.SetAsFirstSibling();
        spine.transform.SetAsFirstSibling();

        UpdateGuide();
    }

    void BuildStep(int i, float y0, float y1, bool open)
    {
        bool locked = i > reached;

        // Three surfaces, not two: active is clearly lifted, completed sits quietly,
        // locked is barely there. Elevation carries the state instead of an outline
        // around everything, which is what made the panel look busy.
        var box = Img(stepHolder, "step" + i,
                      open   ? new Color(1f, 1f, 1f, 0.050f)
                    : locked ? new Color(1f, 1f, 1f, 0.008f)
                             : new Color(1f, 1f, 1f, 0.020f));
        Anchor(box.rectTransform, 0f, y0, 1f, y1);
        var rt = box.rectTransform;

        // A single accent rail marks where you are. The one piece of oxide on the
        // left column, so the eye lands on the open step immediately.
        if (open)
        {
            var rail = Img(rt, "activeRail", BladeLoopTheme.Oxide);
            Anchor(rail.rectTransform, 0f, 0f, 0.0035f, 1f);
        }

        // The whole header is the affordance for reopening a finished step.
        if (!open && !locked)
        {
            box.raycastTarget = true;
            var b = box.gameObject.AddComponent<Button>();
            b.targetGraphic = box;
            int idx = i;
            b.onClick.AddListener(() => { step = idx; Rebuild(); });
        }

        // The header is a FIXED-HEIGHT strip, not a fraction of the box. Step boxes
        // range from 0.058 to 0.52 of the screen, so a proportional header rendered
        // the labels at four pixels tall in the short ones - which is exactly what
        // made the whole screen look broken.
        var head = Rect(rt, "head");
        if (open)
        {
            head.anchorMin = new Vector2(0f, 1f);
            head.anchorMax = new Vector2(1f, 1f);
            head.pivot     = new Vector2(0.5f, 1f);
            head.offsetMin = Vector2.zero;
            head.offsetMax = Vector2.zero;
            head.sizeDelta = new Vector2(0f, 44f);
            head.anchoredPosition = new Vector2(0f, -10f);
        }
        else Anchor(head, 0f, 0f, 1f, 1f);

        // A progress node rather than a bare digit: a filled square for a step you
        // have completed, an accent ring for the one you are on, an empty outline for
        // one you have not reached. Connected by the spine drawn in Rebuild, so the
        // four steps read as a route with a position on it - which is the single
        // clearest signal that this is a tool being operated rather than a form.
        var node = Img(head, "node",
                       open     ? BladeLoopTheme.Oxide
                     : locked   ? BladeLoopTheme.Rule
                                : BladeLoopTheme.Muted);
        node.rectTransform.anchorMin = new Vector2(0.020f, 0.5f);
        node.rectTransform.anchorMax = new Vector2(0.020f, 0.5f);
        node.rectTransform.sizeDelta = new Vector2(open ? 13f : 9f, open ? 13f : 9f);

        if (!open && !locked)
        {
            // Completed: a smaller accent core inside the marker.
            var core = Img(node.rectTransform, "core", BladeLoopTheme.Oxide);
            Anchor(core.rectTransform, 0.28f, 0.28f, 0.72f, 0.72f);
        }

        // "00" not "02": in a composite format string "0" is a digit placeholder and
        // any other character is a LITERAL, so "02" rendered step one as "12".
        var num = Label(head, "n", $"{i + 1:00}", TypeMicro,
                        locked ? Locked : (open ? BladeLoopTheme.Oxide : BladeLoopTheme.Faint),
                        TextAlignmentOptions.Left, BladeLoopTheme.Mono);
        Anchor(num.rectTransform, 0.038f, 0f, 0.072f, 1f);

        var name = Label(head, "t", StepName[i], TypeMicro,
                         locked ? Locked : (open ? BladeLoopTheme.Bone : BladeLoopTheme.Muted),
                         TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
        name.characterSpacing = 1.6f;
        Anchor(name.rectTransform, 0.085f, 0f, 0.45f, 1f);

        if (!open)
        {
            var sum = Label(head, "s", locked ? "" : Summary(i), TypeLabel,
                            BladeLoopTheme.Bone, TextAlignmentOptions.Right, BladeLoopTheme.MonoBold);
            Anchor(sum.rectTransform, 0.46f, 0f, 0.975f, 1f);
            return;
        }

        // Content gets its OWN rect below the header band, so a step builder can use
        // the full 0..1 range without having to know the header is up there. Drawing
        // straight into the box put "YOUR PLAN" underneath the settings line.
        var content = Rect(rt, "c");
        Anchor(content, 0f, 0f, 1f, 1f);
        content.offsetMax = new Vector2(0f, -62f);   // clear of the fixed header strip
        content.offsetMin = new Vector2(0f, 8f);

        // A short fade as a step opens. Without it the panel snaps between states,
        // which is most of what makes a built-at-runtime UI feel like a form rather
        // than an application.
        var cg = content.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        StartCoroutine(FadeIn(cg, 0.16f));

        switch (i)
        {
            case 0: StepKnow(content);  break;
            case 1: StepNumbers(content); break;
            case 2: StepMatters(content); break;
            case 3: StepPlan(content);  break;
        }
    }

    /// <summary>Eases a freshly built step in. Guards against the object being
    /// destroyed mid-fade, which happens whenever a rebuild lands on top of one.</summary>
    IEnumerator FadeIn(CanvasGroup cg, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (cg == null) yield break;
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / seconds);
            cg.alpha = u * u * (3f - 2f * u);   // smoothstep
            yield return null;
        }
        if (cg != null) cg.alpha = 1f;
    }

    string Summary(int i)
    {
        switch (i)
        {
            case 0: return ModeTitle[(int)mode];
            case 1: return mode == Mode.Buyer      ? $"{BuyerName(grade)}  ·  {orderTonnes:N0} t"
                         : mode == Mode.Supply     ? $"{blades:N0} blades  ·  {BuyerName(grade)}"
                                                   : $"{shredMm:0.#} mm";
            case 2:
                if (frontier.Count == 0) return "";
                var p = frontier[Mathf.Clamp(railIndex, 0, frontier.Count - 1)];
                return ConsequenceShort(p);
            default:
                var m = Current();
                return m == null ? "" : $"{m.TempC:0} °C · {m.RetentionMin:0} min · " +
                                        $"{m.FeedKgH:N0} kg/h · {m.ParticleSizeMm:0.#} mm";
        }
    }

    // ----------------------------------------------------- step 1: what you know

    static readonly string[] ModeTitle =
    { "I know my buyer", "I have material", "I have a constraint" };
    static readonly string[] ModeBlurb =
    { "Selling to someone specific",
      "Blades in a yard, no order yet",
      "A shredder that only goes so fine" };

    void StepKnow(RectTransform rt)
    {
        // THREE ROWS, NOT THREE COLUMNS. The open step now takes the column's full
        // slack, and three side-by-side cards in a box that tall came out 540 units
        // high for two lines of text each - the content sat in a thin band with an
        // empty pit under it. Stacked, each card is a comfortable 170 and the block
        // fills the height honestly. It also puts the three options in reading order
        // rather than making the eye scan sideways.
        float gap = 0.018f, h = (1f - 2f * gap) / 3f;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            float y1 = 1f - i * (h + gap);
            float y0 = y1 - h;
            bool on = (int)mode == i;

            // Selection reads as a lifted surface plus an accent edge, not a box
            // outline. Outlines on every card is what made this look like a form.
            // Selection is ELEVATION plus an accent edge, never a tinted surface. A
            // 16%-oxide wash over the near-black panel came out as a muddy brick that
            // looked like a rendering fault rather than a chosen card - and it broke
            // the rule the file already states, that depth comes from value not hue.
            var card = Img(rt, "m" + i, new Color(1f, 1f, 1f, on ? 0.075f : 0.028f));
            card.raycastTarget = true;
            AnchorIn(rt, card.rectTransform, 0.012f, y0, 0.985f, y1, 0f, 1f);

            if (on)
            {
                // On a wide short card the accent belongs on the LEADING edge, which is
                // also where the step column's own active rail lives - so the selected
                // option lines up with the step that contains it.
                var accent = Img(card.rectTransform, "acc", BladeLoopTheme.Oxide);
                Anchor(accent.rectTransform, 0f, 0f, 0.0035f, 1f);
                var lip = Img(card.rectTransform, "lip", new Color(1f, 1f, 1f, 0.10f));
                Anchor(lip.rectTransform, 0f, 0.988f, 1f, 1f);
            }

            var b = card.gameObject.AddComponent<Button>();
            b.targetGraphic = card;
            b.onClick.AddListener(() => { mode = (Mode)idx; frontier.Clear(); edited = null;
                                          reached = 1; step = 1; Rebuild(); });

            // PINNED TO THE TOP EDGE, not anchored as a fraction. The open card now
            // takes all the column's slack, so a fractional title floated to the middle
            // of a 540-unit box with 200 units of nothing above it.
            // A marker, not a numeral. Numbering the options 01/02/03 put a second "01"
            // on the same row as the step's own "01" and the two read as the same
            // counter. This is the SAME square the progress nodes use, so "filled means
            // chosen" means one thing everywhere on the screen.
            var mk = Img(card.rectTransform, "mk", on ? BladeLoopTheme.Oxide : BladeLoopTheme.Faint);
            mk.rectTransform.anchorMin = new Vector2(0.030f, 0.5f);
            mk.rectTransform.anchorMax = new Vector2(0.030f, 0.5f);
            mk.rectTransform.sizeDelta = new Vector2(on ? 11f : 9f, on ? 11f : 9f);
            mk.rectTransform.anchoredPosition = Vector2.zero;
            mk.raycastTarget = false;

            // Centred as a PAIR in the row. Top-pinned, the title and blurb sat in the
            // top third of a 185-unit row with 120 units of nothing under them.
            var t = Label(card.rectTransform, "t", ModeTitle[i], TypeStepTitle,
                          on ? BladeLoopTheme.Bone : BladeLoopTheme.Muted,
                          TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
            t.textWrappingMode = TextWrappingModes.Normal;
            Anchor(t.rectTransform, 0.055f, 0.485f, 0.70f, 0.730f);

            // Muted, not Faint, on the selected card: Faint (#5F6A77) all but vanishes
            // against the lifted surface, so the one row you had chosen was the one row
            // whose description you could not read.
            var bl = Label(card.rectTransform, "b", ModeBlurb[i], TypeBody,
                           on ? BladeLoopTheme.Muted : BladeLoopTheme.Faint,
                           TextAlignmentOptions.Left, BladeLoopTheme.Sans);
            bl.textWrappingMode = TextWrappingModes.Normal;
            Anchor(bl.rectTransform, 0.055f, 0.290f, 0.70f, 0.490f);

            var st = Label(card.rectTransform, "st", on ? "SELECTED" : "CHOOSE  →", TypeMicro,
                           on ? BladeLoopTheme.Oxide : BladeLoopTheme.Faint,
                           TextAlignmentOptions.Right, BladeLoopTheme.SansBold);
            st.characterSpacing = 1.6f;
            st.raycastTarget = false;
            Anchor(st.rectTransform, 0.72f, 0.36f, 0.975f, 0.64f);
        }
    }

    // -------------------------------------------------------- step 2: your numbers

    void StepNumbers(RectTransform rt)
    {
        // Both blocks pushed up: the card now takes the column's full slack, and at
        // 0.62/0.30 the content sat in the middle with 90 units of nothing above it and
        // another 70 below.
        if (mode == Mode.Buyer || mode == Mode.Supply) BuyerRow(rt, 0.630f);

        float sy = (mode == Mode.Constraint) ? 0.560f : 0.330f;

        if (mode == Mode.Buyer)
            BigSlider(rt, sy, "TONNES OF RECOVERED FIBRE", 1000f, 10000f, true, orderTonnes,
                      v => { orderTonnes = v; Refresh(); }, () => $"{orderTonnes:N0} t");
        else if (mode == Mode.Supply)
            BigSlider(rt, sy, "BLADES YOU HAVE", 50f, 1500f, true, blades,
                      v => { blades = v; Refresh(); }, () => $"{blades:N0}");
        else
            BigSlider(rt, sy, "FINEST YOUR SHREDDER GOES", 1f, 20f, false, shredMm,
                      v => { shredMm = Mathf.Round(v * 2f) / 2f; Refresh(); }, () => $"{shredMm:0.#} mm");

        derived = Label(rt, "d", "", TypeBody, BladeLoopTheme.Faint,
                        TextAlignmentOptions.TopLeft, BladeLoopTheme.Sans);
        derived.textWrappingMode = TextWrappingModes.Normal;
        // A hairline before the consequence line, matching the rail card: figures above,
        // what they mean below.
        var nrule = Img(rt, "nrule", BladeLoopTheme.RuleSoft);
        AnchorIn(rt, nrule.rectTransform, 0.012f, 0.300f, 0.985f, 0.302f, 0f, 1f);
        nrule.raycastTarget = false;

        Anchor(derived.rectTransform, 0.012f, 0.170f, 0.62f, 0.275f);

        FixedBtn(rt, "go", "FIND THE OPTIONS  →", 0.66f, 0.985f, 0.062f, 46f, true, ComputeFrontier);
        Refresh();
    }

    TMP_Text derived;

    void BuyerRow(RectTransform rt, float y)
    {
        Micro(rt, "WHO IS BUYING", 0.012f, y + 0.26f);
        // Full width, not 0.62: three chips crammed into the left five-eighths left a
        // 420-unit empty margin beside them for no reason, and made the row read as an
        // afterthought rather than the question the step is asking.
        float gap = 0.010f, w = (0.985f - 0.012f - 2f * gap) / 3f;
        for (int i = 0; i < 3; i++)
        {
            var g = (Grade)i;
            float x = 0.012f + i * (w + gap);
            bool on = grade == g;

            // Elevation and an accent edge, NOT a solid fill. A fully saturated oxide
            // chip with near-black text on it was the loudest object in the flow and
            // contradicted every other selected state on the screen. It also forced the
            // sub-label into a brown that was unreadable either way round.
            var chip = Img(rt, "g" + i, new Color(1f, 1f, 1f, on ? 0.075f : 0.028f));
            chip.raycastTarget = true;
            AnchorIn(rt, chip.rectTransform, x, y, x + w, y + 0.24f, 0f, 1f);
            var b = chip.gameObject.AddComponent<Button>();
            b.targetGraphic = chip;
            b.onClick.AddListener(() => { grade = g; frontier.Clear(); Rebuild(); });

            if (on)
            {
                var acc = Img(chip.rectTransform, "acc", BladeLoopTheme.Oxide);
                Anchor(acc.rectTransform, 0f, 0f, 1f, 0.030f);
                acc.raycastTarget = false;
                var lip = Img(chip.rectTransform, "lip", new Color(1f, 1f, 1f, 0.10f));
                Anchor(lip.rectTransform, 0f, 0.970f, 1f, 1f);
                lip.raycastTarget = false;
            }

            var l = Label(chip.rectTransform, "l", BuyerName(g), TypeLabel,
                          on ? BladeLoopTheme.Bone : BladeLoopTheme.Muted,
                          TextAlignmentOptions.Center, BladeLoopTheme.SansBold);
            Anchor(l.rectTransform, 0.04f, 0.46f, 0.96f, 0.90f);

            // Muted on the selected chip: Faint against the lifted surface is the same
            // near-invisible pairing that hid the mode-card blurbs.
            var u = Label(chip.rectTransform, "u", EndUseShort(g), TypeMicro,
                          on ? BladeLoopTheme.Muted : BladeLoopTheme.Faint,
                          TextAlignmentOptions.Center, BladeLoopTheme.Sans);
            Anchor(u.rectTransform, 0.04f, 0.10f, 0.96f, 0.44f);
        }
    }

    static string BuyerName(Grade g) =>
        g == Grade.High ? "Composite maker" : g == Grade.Mid ? "Precast concrete" : "Cement works";

    static string EndUseShort(Grade g) =>
        g == Grade.High ? "new structural parts"
      : g == Grade.Mid  ? "reinforcing filler"
                        : "replaces sand and coal";

    void Refresh()
    {
        if (derived == null) return;
        if (mode == Mode.Buyer)
            derived.text = $"Recovered glass fibre, ready to sell on. " +
                           $"Roughly {orderTonnes / 0.65f:N0} t of blade material before losses.";
        else if (mode == Mode.Supply)
            derived.text = $"{blades * OrderContext.BladeMassTonnes:N0} t of material, " +
                           $"about {blades / OrderContext.BladesPerTurbine:N0} turbines' worth.";
        else
            derived.text = $"Feeds at most {OrderSolver.MaxFeed(shredMm):N0} kg/h. " +
                           $"Finer grinding is slower grinding, so the shredder sets the ceiling.";

        UpdateGuide();
    }

    // ------------------------------------------------------ step 3: what matters

    void ComputeFrontier()
    {
        frontier = mode == Mode.Constraint
                 ? OrderSolver.FrontierWhere(m => true, shredMm)
                 : OrderSolver.SolveFrontier(grade);

        edited = null;
        railIndex = 0;
        reached = Mathf.Max(reached, 2);
        step = 2;
        Rebuild();
    }

    void StepMatters(RectTransform rt)
    {
        if (frontier.Count == 0)
        {
            var e = Label(rt, "e", "No plan in the plant's envelope reaches that. Try a coarser target.",
                          TypeBody, BladeLoopTheme.Oxide, TextAlignmentOptions.TopLeft, BladeLoopTheme.Sans);
            e.textWrappingMode = TextWrappingModes.Normal;
            Anchor(e.rectTransform, 0.012f, 0.15f, 0.8f, 0.7f);
            return;
        }

        // Low grade's frontier is identical to mid's. Saying so is better than
        // letting the user wonder why nothing changed.
        if (mode != Mode.Constraint && grade == Grade.Low)
        {
            var n = Label(rt, "same", "Same options as precast concrete — running dirtier than this "
                                    + "costs more than it gains, so there is never a reason to choose it.",
                          TypeMicro, BladeLoopTheme.Faint, TextAlignmentOptions.TopLeft, BladeLoopTheme.Sans);
            n.textWrappingMode = TextWrappingModes.Normal;
            Anchor(n.rectTransform, 0.012f, 0.72f, 0.72f, 0.80f);
        }

        railIndex = Mathf.Clamp(railIndex, 0, frontier.Count - 1);
        var p = frontier[railIndex];

        string leftCap  = mode == Mode.Supply ? "FINISH SOONER" : "FINISH SOONER";
        string rightCap = mode == Mode.Supply     ? "GET MORE FIBRE"
                        : mode == Mode.Constraint ? "MORE FROM EACH TONNE"
                                                  : "USE FEWER BLADES";

        // The card is 44 units of header plus everything below it. The rail block used
        // to start at 0.60 of the box, which left a 60 px dead band under the header
        // and pushed the whole control into the bottom two thirds. It now hangs from
        // the top so the card reads as one composition instead of a header floating
        // above a cluster.
        // Both captions are placed EXPLICITLY on the same band. Micro() sizes its box
        // as y..y+0.105 of the parent, which in this card is 61 units for a 13pt label
        // - so the left caption sat 12 units below the right one and the pair read as
        // crooked. Identical rects, identical baseline.
        var lc = Label(rt, "lc", leftCap, TypeMicro, BladeLoopTheme.Muted,
                       TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
        lc.characterSpacing = 1.6f;
        Anchor(lc.rectTransform, 0.012f, 0.930f, 0.50f, 0.985f);

        var rc = Label(rt, "rc", rightCap, TypeMicro, BladeLoopTheme.Muted,
                       TextAlignmentOptions.Right, BladeLoopTheme.SansBold);
        rc.characterSpacing = 1.6f;
        Anchor(rc.rectTransform, 0.52f, 0.930f, 0.985f, 0.985f);

        // ---- the rail: one notch per optimal plan ----
        // A groove rather than a bar: hairline above, dark channel, so it reads as a
        // machined slot a control travels in. The ticks are the actual plans, so the
        // user can see the frontier is DISCRETE - forty-one real set-points, not a
        // continuous fudge factor.
        var track = Img(rt, "rail", BladeLoopTheme.RuleSoft);
        AnchorIn(rt, track.rectTransform, 0.012f, 0.884f, 0.985f, 0.908f, 0f, 1f);

        var fill = Img(track.rectTransform, "fill", BladeLoopTheme.Oxide);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(
            frontier.Count <= 1 ? 1f : railIndex / (float)(frontier.Count - 1), 1f);
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        // One tick per plan, sitting under the groove. Capped so a dense frontier
        // thins itself out rather than turning into a solid grey block.
        var ticks = Rect(rt, "ticks");
        Anchor(ticks, 0.012f, 0.860f, 0.985f, 0.884f);
        railTicks = ticks;
        int nTick = frontier.Count;
        int strideT = Mathf.Max(1, Mathf.CeilToInt(nTick / 28f));
        railStride = strideT;
        for (int k = 0; k < nTick; k += strideT)
        {
            float u = nTick <= 1 ? 0f : k / (float)(nTick - 1);
            bool onIt = Mathf.Abs(k - railIndex) < strideT;
            var tk = Img(ticks, "tk" + k, onIt ? BladeLoopTheme.Oxide : BladeLoopTheme.Rule);
            tk.rectTransform.anchorMin = new Vector2(u, onIt ? 0f : 0.42f);
            tk.rectTransform.anchorMax = new Vector2(u, 1f);
            tk.rectTransform.sizeDelta = new Vector2(1.5f, 0f);
            tk.rectTransform.anchoredPosition = Vector2.zero;
            tk.raycastTarget = false;
        }

        // The slider carries its own invisible raycast target so the whole rail is
        // clickable, not just the handle - with the graphic on the track instead,
        // the track swallows the click and the rail feels dead everywhere except
        // the handle. Taller than the visible groove for a forgiving hit area.
        var s = new GameObject("railSlider", typeof(RectTransform), typeof(Image), typeof(Slider));
        s.transform.SetParent(track.transform, false);
        Anchor((RectTransform)s.transform, 0f, -1.1f, 1f, 2.1f);
        var hit = s.GetComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;
        var handleArea = Rect((RectTransform)s.transform, "HA"); Anchor(handleArea, 0f, 0f, 1f, 1f);

        // THE HANDLE RECT CANNOT CARRY THE VISUAL. Slider.UpdateVisuals rewrites BOTH
        // of handleRect's anchor pairs every frame - the driven axis to the value, the
        // other one to a full 0..1 stretch - so sizeDelta on the cross axis is an
        // OFFSET on top of the hit area, never a height. Setting it to 34 produced a
        // 130 px white slab sitting on the caption and the headline figure, and
        // point-anchoring the rect did not help because the Slider undid it.
        //
        // So handleRect stays invisible and does nothing but catch the drag; the grip
        // is CHILDREN of it, which the Slider never touches.
        var handle = Img(handleArea, "H", new Color(0f, 0f, 0f, 0f));
        handle.rectTransform.sizeDelta = new Vector2(26f, 0f);
        handle.raycastTarget = true;

        // A dim plate for mass, and a bright blade thin enough not to hide the tick it
        // is standing on. Together they read as a control seated in the groove.
        var plate = Img(handle.rectTransform, "plate", new Color(1f, 1f, 1f, 0.09f));
        PointRect(plate.rectTransform, 22f, 26f);
        var blade = Img(handle.rectTransform, "blade", BladeLoopTheme.Bone);
        PointRect(blade.rectTransform, 3f, 26f);

        var sl = s.GetComponent<Slider>();
        sl.handleRect = handle.rectTransform; sl.targetGraphic = handle;
        sl.direction = Slider.Direction.LeftToRight;
        sl.minValue = 0f; sl.maxValue = frontier.Count - 1; sl.wholeNumbers = true;
        sl.SetValueWithoutNotify(railIndex);
        sl.onValueChanged.AddListener(v =>
        {
            railIndex = Mathf.RoundToInt(v);
            edited = null;
            PaintRail();
        });

        // ---- the two consequences, large ----
        railA = Label(rt, "ca", "", TypeHuge, BladeLoopTheme.Bone,
                      TextAlignmentOptions.Left, BladeLoopTheme.MonoBold);
        Anchor(railA.rectTransform, 0.012f, 0.700f, 0.48f, 0.815f);
        railB = Label(rt, "cb", "", TypeHuge, BladeLoopTheme.Bone,
                      TextAlignmentOptions.Right, BladeLoopTheme.MonoBold);
        Anchor(railB.rectTransform, 0.50f, 0.700f, 0.985f, 0.815f);

        railALbl = Label(rt, "cal", "", TypeMicro, BladeLoopTheme.Muted,
                         TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
        Anchor(railALbl.rectTransform, 0.012f, 0.640f, 0.48f, 0.690f);
        railBLbl = Label(rt, "cbl", "", TypeMicro, BladeLoopTheme.Muted,
                         TextAlignmentOptions.Right, BladeLoopTheme.SansBold);
        Anchor(railBLbl.rectTransform, 0.50f, 0.640f, 0.985f, 0.690f);

        // A hairline between the two figures and the sentence that explains them, so
        // the trade-off reads as a result block and the note as commentary on it.
        var rrule = Img(rt, "rrule", BladeLoopTheme.RuleSoft);
        AnchorIn(rt, rrule.rectTransform, 0.012f, 0.598f, 0.985f, 0.600f, 0f, 1f);
        rrule.raycastTarget = false;

        railNote = Label(rt, "rn", "", TypeBody, BladeLoopTheme.Muted,
                         TextAlignmentOptions.TopLeft, BladeLoopTheme.Sans);
        railNote.textWrappingMode = TextWrappingModes.Normal;
        Anchor(railNote.rectTransform, 0.012f, 0.470f, 0.66f, 0.570f);

        // ---- what this position actually is, in the plant ----
        // The rail says what you GET. Without this the card never says what you would
        // RUN, so the two figures float free of the machine that produces them. These
        // are the four set-points the tour then drives, shown read-only.
        var spHdr = Label(rt, "sph", "SET-POINTS AT THIS POSITION", TypeMicro,
                          BladeLoopTheme.Faint, TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
        spHdr.characterSpacing = 1.6f;
        Anchor(spHdr.rectTransform, 0.012f, 0.355f, 0.6f, 0.400f);

        var sprule = Img(rt, "sprule", BladeLoopTheme.RuleSoft);
        AnchorIn(rt, sprule.rectTransform, 0.012f, 0.331f, 0.985f, 0.333f, 0f, 1f);
        sprule.raycastTarget = false;

        string[] spName = { "TEMPERATURE", "RESIDENCE", "FEED RATE", "PARTICLE" };
        for (int k = 0; k < 4; k++)
        {
            float x0 = 0.012f + k * 0.2435f;
            var l = Label(rt, "spl" + k, spName[k], TypeMicro, BladeLoopTheme.Faint,
                          TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
            l.characterSpacing = 1.6f;
            Anchor(l.rectTransform, x0, 0.255f, x0 + 0.235f, 0.307f);
            TipIfKnown(l, spName[k]);

            setPt[k] = Label(rt, "spv" + k, "", TypeValue, BladeLoopTheme.Bone,
                             TextAlignmentOptions.Left, BladeLoopTheme.MonoBold);
            Anchor(setPt[k].rectTransform, x0, 0.165f, x0 + 0.235f, 0.250f);
        }

        railFill = fill;
        FixedBtn(rt, "go2", "SEE THE PLAN  →", 0.70f, 0.985f, 0.052f, 46f, true, () =>
        {
            reached = Mathf.Max(reached, 3);
            step = 3;
            Commit();
            Rebuild();
        });

        PaintRail();

        // AFTER PaintRail, because these two carry different words per mode and the
        // dashed rule is measured from the rendered glyphs - attached before the text
        // exists, it would be a rule under an empty string.
        TipIfKnown(railALbl, railALbl.text);
        TipIfKnown(railBLbl, railBLbl.text);
    }

    TMP_Text railA, railB, railALbl, railBLbl, railNote;
    readonly TMP_Text[] setPt = new TMP_Text[4];
    Image railFill;
    RectTransform railTicks;
    int railStride = 1;

    void PaintRail()
    {
        if (railA == null || frontier.Count == 0) return;
        var p = frontier[Mathf.Clamp(railIndex, 0, frontier.Count - 1)];

        if (railFill != null)
            railFill.rectTransform.anchorMax = new Vector2(
                frontier.Count <= 1 ? 1f : railIndex / (float)(frontier.Count - 1), 1f);

        // The set-points for wherever the handle now is. Guarded on [0] because the
        // rail is rebuilt whole on every step change and PaintRail is also called from
        // the slider callback, which can fire once before the row exists.
        if (setPt[0] != null)
        {
            setPt[0].text = $"{p.model.TempC:0} °C";
            setPt[1].text = $"{p.model.RetentionMin:0} min";
            setPt[2].text = $"{p.model.FeedKgH:N0} kg/h";
            setPt[3].text = $"{p.model.ParticleSizeMm:0.#} mm";
        }

        // The tick under the handle lights and grows. Without this the ticks are
        // decoration; with it they are a readout of where on the frontier you are.
        if (railTicks != null)
            for (int k = 0; k < railTicks.childCount; k++)
            {
                var tk = railTicks.GetChild(k).GetComponent<Image>();
                if (tk == null) continue;
                int idx;
                if (!int.TryParse(tk.name.Substring(2), out idx)) continue;
                bool onIt = Mathf.Abs(idx - railIndex) < railStride;
                tk.color = onIt ? BladeLoopTheme.Oxide : BladeLoopTheme.Rule;
                var a = tk.rectTransform.anchorMin;
                tk.rectTransform.anchorMin = new Vector2(a.x, onIt ? 0f : 0.42f);
            }

        float days, second;
        string aLbl, bLbl, bTxt;

        if (mode == Mode.Supply)
        {
            float feedT  = blades * OrderContext.BladeMassTonnes;
            float fibreT = feedT * p.yieldFrac;
            days   = fibreT * 1000f / p.fibreKgH / 24f;
            second = fibreT;
            aLbl = "DAYS OF RUNNING"; bLbl = "TONNES OF FIBRE";
            bTxt = $"{second:N0} t";
        }
        else if (mode == Mode.Constraint)
        {
            days   = 4000f * 1000f / p.fibreKgH / 24f;
            second = p.yieldFrac * 100f;
            aLbl = "KG PER HOUR"; bLbl = "OF EVERY TONNE BECOMES FIBRE";
            railA.text = $"{p.fibreKgH:N0}";
            bTxt = $"{second:0.0}%";
            railB.text = bTxt; railALbl.text = aLbl; railBLbl.text = bLbl;
            railNote.text = NoteFor(p);
            return;
        }
        else
        {
            days   = orderTonnes * 1000f / p.fibreKgH / 24f;
            second = (orderTonnes / p.yieldFrac) / OrderContext.BladeMassTonnes;
            aLbl = "DAYS TO FILL IT"; bLbl = "BLADES CONSUMED";
            bTxt = $"{second:N0}";
        }

        railA.text = $"{days:0.0}";
        railB.text = bTxt;
        railALbl.text = aLbl;
        railBLbl.text = bLbl;
        railNote.text = NoteFor(p);
        UpdateGuide();
    }

    /// <summary>What moving the rail costs, stated against the other end.</summary>
    string NoteFor(OrderSolver.Plan p)
    {
        if (frontier.Count < 2) return "Only one plan reaches this target.";
        var fast = frontier[0];
        var lean = frontier[frontier.Count - 1];

        if (mode == Mode.Supply)
        {
            float feedT = blades * OrderContext.BladeMassTonnes;
            float dFast = feedT * fast.yieldFrac * 1000f / fast.fibreKgH / 24f;
            float dLean = feedT * lean.yieldFrac * 1000f / lean.fibreKgH / 24f;
            float extra = feedT * (lean.yieldFrac - fast.yieldFrac);
            return $"From these blades: {extra:N0} t more fibre if you run clean, "
                 + $"or {dLean - dFast:0.0} days sooner if you run hard.";
        }
        if (mode == Mode.Constraint)
            return $"At {shredMm:0.#} mm the plant can run from {lean.fibreKgH:N0} to "
                 + $"{fast.fibreKgH:N0} kg/h. Pushing harder wastes more of every tonne.";

        float bFast = (orderTonnes / fast.yieldFrac) / OrderContext.BladeMassTonnes;
        float bLean = (orderTonnes / lean.yieldFrac) / OrderContext.BladeMassTonnes;
        float daysF = orderTonnes * 1000f / fast.fibreKgH / 24f;
        float daysL = orderTonnes * 1000f / lean.fibreKgH / 24f;
        return $"Across every option: {daysL - daysF:0.0} days sooner, or {bFast - bLean:N0} "
             + $"fewer blades. Both fill the order.";
    }

    string ConsequenceShort(OrderSolver.Plan p)
    {
        if (mode == Mode.Supply)
        {
            float feedT = blades * OrderContext.BladeMassTonnes;
            return $"{feedT * p.yieldFrac:N0} t fibre  ·  "
                 + $"{feedT * p.yieldFrac * 1000f / p.fibreKgH / 24f:0.0} d";
        }
        if (mode == Mode.Constraint)
            return $"{p.fibreKgH:N0} kg/h  ·  {p.yieldFrac * 100f:0.0}%";
        return $"{orderTonnes * 1000f / p.fibreKgH / 24f:0.0} d  ·  "
             + $"{(orderTonnes / p.yieldFrac) / OrderContext.BladeMassTonnes:N0} blades";
    }

    // ------------------------------------------------------------ step 4: plan

    ProcessModel Current()
    {
        if (edited != null) return edited;
        if (frontier.Count == 0) return null;
        return frontier[Mathf.Clamp(railIndex, 0, frontier.Count - 1)].model;
    }

    void Commit()
    {
        var m = Current();
        if (m == null) return;

        float tonnes = mode == Mode.Supply
                     ? blades * OrderContext.BladeMassTonnes * (m.OutputSplit().GlassKgH / m.FeedKgH)
                     : mode == Mode.Buyer ? orderTonnes : 4000f;

        var achieved = OrderContext.GradeOf(m.FiberPurityPct, m.TensileRetentionPct);
        OrderContext.SetOrder(new Order("", BuyerName(achieved), achieved, tonnes), m);
    }

    void StepPlan(RectTransform rt)
    {
        var m = Current();
        if (m == null) return;

        planBig = Label(rt, "big", "", 31f, BladeLoopTheme.Bone,
                        TextAlignmentOptions.Left, BladeLoopTheme.MonoBold);
        Anchor(planBig.rectTransform, 0.012f, 0.855f, 0.74f, 0.985f);

        planBadge = Img(rt, "badge", BladeLoopTheme.Oxide);
        AnchorIn(rt, planBadge.rectTransform, 0.80f, 0.885f, 0.985f, 0.965f, 0f, 1f);
        planBadgeTxt = Label(planBadge.rectTransform, "b", "", TypeLabel, BladeLoopTheme.Hex("15110E"),
                             TextAlignmentOptions.Center, BladeLoopTheme.MonoBold);
        planBadgeTxt.characterSpacing = 2f;
        Anchor(planBadgeTxt.rectTransform, 0f, 0f, 1f, 1f);

        // ---- four sliders, still editable, now with a reason ----
        sT = Slid(rt, "KILN TEMPERATURE", 400f, 700f, true,  0.012f, 0.47f, 0.70f, out vT);
        sR = Slid(rt, "RETENTION",        30f,  45f,  true,  0.52f,  0.985f, 0.70f, out vR);
        sF = Slid(rt, "FEED RATE",        4000f,9000f,true,  0.012f, 0.47f, 0.575f, out vF);
        sP = Slid(rt, "PARTICLE SIZE",    1f,   20f,  false, 0.52f,  0.985f, 0.575f, out vP);

        feedWall = Img((RectTransform)sF.transform, "wall", new Color(0f, 0f, 0f, 0.6f));
        feedWall.rectTransform.anchorMin = new Vector2(1f, 0f);
        feedWall.rectTransform.anchorMax = new Vector2(1f, 1f);
        feedWall.rectTransform.offsetMin = Vector2.zero;
        feedWall.rectTransform.offsetMax = Vector2.zero;
        feedWall.transform.SetAsLastSibling();

        suppress = true;
        sT.value = m.TempC; sR.value = m.RetentionMin; sP.value = m.ParticleSizeMm; sF.value = m.FeedKgH;
        suppress = false;

        sT.onValueChanged.AddListener(_ => OnEdit());
        sR.onValueChanged.AddListener(_ => OnEdit());
        sF.onValueChanged.AddListener(_ => OnEdit());
        sP.onValueChanged.AddListener(_ => OnEdit());

        // The verdict itself now lives in the guide, where there is room for it to be
        // a sentence instead of a squeezed caption. Only the action stays here.
        // RIGHT-HAND END of the "what you would get" band, not the left. At x 0.012 it
        // started at the same edge as the WHAT YOU WOULD GET header and sat on the same
        // line, so the two drew straight through each other - and the whole right half
        // of that band was empty. It is also the correct side on its own merits: this
        // is an action, and every other action on the screen (SEE THE PLAN, WATCH THIS
        // RUN, FIND THE OPTIONS) sits right-aligned.
        snapBtn = FixedBtn(rt, "snap", "BACK TO BEST", 0.720f, 0.985f, 0.450f, 42f, false, () =>
        {
            edited = null;
            suppress = true;
            var b = Current();
            sT.value = b.TempC; sR.value = b.RetentionMin; sP.value = b.ParticleSizeMm; sF.value = b.FeedKgH;
            suppress = false;
            Commit(); PaintPlan();
        });

        Anchor(Img(rt, "rr", BladeLoopTheme.RuleSoft).rectTransform, 0.012f, 0.415f, 0.985f, 0.4165f);

        Micro(rt, "WHAT YOU WOULD GET", 0.012f, 0.430f);

        // Rebuild() destroys the step's objects but these lists outlive it. Without
        // clearing, a second visit to this step leaves five DESTROYED entries at the
        // front, PaintPlan walks them, and the MissingReferenceException aborts it
        // half-built - which is why the grade badge and the output bar came up empty.
        segs.Clear();
        legend.Clear();
        legendSub.Clear();

        // The mass balance stands up here too, matching the home page. Same shape for
        // the same data across both screens means someone who read the ledger already
        // knows how to read this - and a vertical stack shows a 1.5% loss sliver
        // against a 69% fibre block far better than a horizontal one.
        bar = Img(rt, "bar", BladeLoopTheme.SkyWarm);
        AnchorIn(rt, bar.rectTransform, 0.012f, 0.10f, 0.115f, 0.40f, 0f, 1f);

        for (int i = 0; i < 5; i++)
        {
            var seg = Img(bar.rectTransform, "s" + i, BladeLoopTheme.StreamColours[i]);
            seg.raycastTarget = true;
            int si = i;
            HoverPair(seg.gameObject, () => ShowShare(si), HideShare);
            segs.Add(seg);
        }

        // Legend on fixed rows beside it, top-to-bottom in stack order, so the values
        // line up as a column of figures instead of chasing the blocks.
        string[] order = { "Loss", "Char", "Syngas", "Oil", "Fibre" };
        int[]    srcOf = { 4, 3, 2, 1, 0 };
        for (int r = 0; r < 5; r++)
        {
            float ry = 0.352f - r * 0.058f;
            int s = srcOf[r];

            var sw = Img(rt, "sw" + s, BladeLoopTheme.StreamColours[s]);
            Anchor(sw.rectTransform, 0.140f, ry + 0.010f, 0.152f, ry + 0.034f);

            var t = Label(rt, "l" + s, order[r], 15f, BladeLoopTheme.Muted,
                          TextAlignmentOptions.Left, BladeLoopTheme.Sans);
            // 0.26, not 0.30: the label rect used to run under the value rect's left
            // edge. Nothing collided because one is left-aligned and the other right,
            // but a longer stream name would have, and an overlapping pair makes the
            // layout audit cry wolf on every run.
            Anchor(t.rectTransform, 0.163f, ry, 0.26f, ry + 0.046f);
            TipIfKnown(t, order[r].ToUpperInvariant());
            legend.Add(t);

            // Wider than the old percentage column: "1,099 kg/h" and "4,000 t" both
            // need more room than "62.3" did.
            var d = Label(rt, "d" + s, "", 16f, BladeLoopTheme.Bone,
                          TextAlignmentOptions.Right, BladeLoopTheme.MonoBold);
            Anchor(d.rectTransform, 0.27f, ry, 0.455f, ry + 0.046f);
            legendSub.Add(d);

            var dest = Label(rt, "ds" + s, Destination(s), 13f, BladeLoopTheme.Faint,
                             TextAlignmentOptions.Left, BladeLoopTheme.Sans);
            Anchor(dest.rectTransform, 0.480f, ry, 0.985f, ry + 0.046f);

            // Hovering the ROW does the same thing as hovering the block. The Loss
            // sliver is three per cent of a 195-unit bar - six pixels - so if the block
            // were the only way in, the one figure a user most wants to interrogate
            // would be the one they cannot reliably point at.
            int row = r;
            var rowHit = Img(rt, "rowhit" + s, new Color(0f, 0f, 0f, 0f));
            Anchor(rowHit.rectTransform, 0.135f, ry, 0.985f, ry + 0.046f);
            rowHit.raycastTarget = true;
            rowHit.transform.SetAsLastSibling();
            HoverPair(rowHit.gameObject, () => ShowShare(srcOf[row]), HideShare);
        }

        // The column heading, so the unit is stated once rather than repeated on five
        // rows. Set in PaintPlan because it changes with the mode.
        // 0.399..0.429 is the only clear band: the top legend row ends at 0.398 and the
        // WHAT YOU WOULD GET caption starts at 0.430. That caption is drawn by Micro(),
        // whose box is a flat half the card wide whatever the text is, so a header
        // placed by eye overlapped a rect it never visibly touched.
        shareHdr = Label(rt, "shdr", "", TypeMicro, BladeLoopTheme.Faint,
                         TextAlignmentOptions.Right, BladeLoopTheme.SansBold);
        shareHdr.characterSpacing = 1.6f;
        Anchor(shareHdr.rectTransform, 0.27f, 0.399f, 0.455f, 0.429f);

        BuildShareChip(rt);

        quality = Label(rt, "q", "", 25f, BladeLoopTheme.Bone,
                        TextAlignmentOptions.Left, BladeLoopTheme.MonoBold);
        Anchor(quality.rectTransform, 0.012f, 0.012f, 0.66f, 0.082f);

        FixedBtn(rt, "watch", "WATCH THIS RUN  →", 0.70f, 0.985f, 0.055f, 46f, true, () =>
        {
            Commit();
            TourRunner.StartRun();
        });

        // Straight to the numbers, for a planner who does not want to sit through the
        // tour. Secondary styling on purpose: watching the run is still the headline
        // action, this is the shortcut past it.
        //
        // OutcomeReportPanel.Show() is an OVERLAY, not a scene - it builds a
        // DontDestroyOnLoad canvas at sorting order 1200 and slides across whatever is
        // behind it, so it needs nothing loaded first and owns its own exit back to the
        // menu. Two things it does on the way in are safe from here and were checked:
        // OrderPanel.Teardown() no-ops when no tour panel exists, and the
        // TourControls.Suppress() it calls is undone by TourSceneSequencer the next
        // time a tour starts.
        //
        // Commit() first, exactly as WATCH THIS RUN does: the report reads
        // OrderContext.Active and .Model, and Commit is the only thing that writes them.
        FixedBtn(rt, "report", "RUN REPORT  →", 0.720f, 0.985f, 0.252f, 46f, false, () =>
        {
            Commit();
            OutcomeReportPanel.Show();
        });

        PaintPlan();
    }

    // ===================================================== the output breakdown ==

    /// <summary>
    /// WHY TONNES AND NOT PERCENTAGES.
    ///
    /// A percentage answers "how did the feed divide up", which is a question about the
    /// kiln. The heading says WHAT YOU WOULD GET, which is a question about the order -
    /// and "62.3" does not tell a buyer whether that is a lorry-load or a shipload.
    /// So the column carries the mass that actually leaves the plant over the whole run,
    /// and the percentage moves to hover, where it is one click of attention away for
    /// the reader who wants the split rather than the quantity.
    ///
    /// THE ARITHMETIC. Campaign hours differ by mode, so the tonnage does too:
    ///   Buyer      - the order is a fibre tonnage, so hours = orderTonnes / fibre rate.
    ///                Fibre therefore comes back as EXACTLY the ordered figure, which is
    ///                a useful thing for a user to be able to check.
    ///   Supply     - the yard is fixed, so hours = blade tonnage / feed rate, and the
    ///                five streams sum to exactly the blade tonnage put in.
    ///   Constraint - THERE IS NO RUN. This mode fixes a shredder size and asks what the
    ///                plant can do; no quantity is ever entered, so there is no total to
    ///                report and inventing one would be a lie. It falls back to kg/h.
    /// </summary>
    struct Share { public float value; public string unit; public float pct; }

    Share[] ShareOf(ProcessModel m)
    {
        var sp  = m.OutputSplit();
        var kgh = new[] { sp.GlassKgH, sp.OilKgH, sp.SyngasKgH, sp.CharKgH, sp.LossKgH };
        var pct = new[] { sp.GlassPct, sp.OilPct, sp.SyngasPct, sp.CharPct, sp.LossPct };

        float hours = 0f;
        if (mode == Mode.Buyer && sp.GlassKgH > 0.01f)
            hours = orderTonnes * 1000f / sp.GlassKgH;
        else if (mode == Mode.Supply && m.FeedKgH > 0.01f)
            hours = blades * OrderContext.BladeMassTonnes * 1000f / m.FeedKgH;

        var outp = new Share[5];
        for (int i = 0; i < 5; i++)
        {
            outp[i].pct = pct[i];
            if (hours > 0f) { outp[i].value = kgh[i] * hours / 1000f; outp[i].unit = "t"; }
            else            { outp[i].value = kgh[i];                 outp[i].unit = "kg/h"; }
        }
        return outp;
    }

    static string ShareLabel(Share s)
    {
        // Below ten tonnes a whole number hides the difference between 0.4 t and 4 t,
        // and a small custom order lands there routinely.
        string n = s.unit == "t" && s.value < 10f ? s.value.ToString("0.0")
                                                  : s.value.ToString("N0");
        return n + " " + s.unit;
    }

    // ---- the hover read-out -------------------------------------------------

    RectTransform shareChip;
    TMP_Text shareChipTxt, shareHdr;
    Image segLift;
    Share[] curShare;

    /// <summary>The percentage chip, drawn ON the bar so it reads as a label on the
    /// block being pointed at rather than a note about it. Built once per plan card.</summary>
    void BuildShareChip(RectTransform rt)
    {
        shareChip = Img(rt, "shareChip", BladeLoopTheme.Panel).rectTransform;
        shareChip.pivot     = new Vector2(0.5f, 0.5f);
        shareChip.anchorMin = shareChip.anchorMax = new Vector2(0f, 0f);
        shareChip.sizeDelta = new Vector2(78f, 30f);

        var edge = Img(shareChip, "e", BladeLoopTheme.Oxide);
        Anchor(edge.rectTransform, 0f, 0f, 1f, 0.055f);

        shareChipTxt = Label(shareChip, "t", "", TypeLabel, BladeLoopTheme.Bone,
                             TextAlignmentOptions.Center, BladeLoopTheme.MonoBold);
        Anchor(shareChipTxt.rectTransform, 0f, 0.08f, 1f, 1f);

        shareChip.gameObject.SetActive(false);
        shareChip.SetAsLastSibling();

        // One reusable highlight, moved onto whichever block is being pointed at. Lives
        // inside the bar so it inherits the stack's coordinate space exactly.
        //
        // The wash alone is not enough: the five streams run from near-black char to
        // off-white fibre, and a white wash over the fibre block is invisible. Two
        // accent hairlines bracket the band instead, which reads identically on every
        // colour in the stack.
        segLift = Img(bar.rectTransform, "segLift", new Color(1f, 1f, 1f, 0.13f));
        segLift.raycastTarget = false;
        var eTop = Img(segLift.rectTransform, "eT", BladeLoopTheme.Oxide);
        Anchor(eTop.rectTransform, 0f, 1f, 1f, 1f);
        eTop.rectTransform.sizeDelta = new Vector2(0f, 2f);
        eTop.raycastTarget = false;
        var eBot = Img(segLift.rectTransform, "eB", BladeLoopTheme.Oxide);
        Anchor(eBot.rectTransform, 0f, 0f, 1f, 0f);
        eBot.rectTransform.sizeDelta = new Vector2(0f, 2f);
        eBot.raycastTarget = false;
        segLift.gameObject.SetActive(false);
    }

    /// <summary>Attach enter/exit handlers without clobbering any already on the object.</summary>
    static void HoverPair(GameObject go, UnityEngine.Events.UnityAction onEnter,
                                          UnityEngine.Events.UnityAction onExit)
    {
        var trig = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
        var a = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        a.callback.AddListener(_ => onEnter());
        trig.triggers.Add(a);
        var b = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        b.callback.AddListener(_ => onExit());
        trig.triggers.Add(b);
    }

    /// <summary>Reveal one stream's share: the chip lands on that block, the block
    /// brightens, and every other block and row steps back so the pair being compared
    /// is unmistakable.</summary>
    void ShowShare(int stream)
    {
        if (shareChip == null || curShare == null || bar == null) return;
        if (stream < 0 || stream >= segs.Count || segs[stream] == null) return;

        shareChipTxt.text = curShare[stream].pct.ToString("0.0") + "%";

        // Centre of the block, expressed in the plan card's own space.
        var seg = segs[stream].rectTransform;
        var c = new Vector3[4];
        seg.GetWorldCorners(c);
        var parent = (RectTransform)shareChip.parent;
        Vector2 lo = parent.InverseTransformPoint(c[0]);
        Vector2 hi = parent.InverseTransformPoint(c[2]);
        var pr = parent.rect;
        float cx = (lo.x + hi.x) * 0.5f - pr.xMin;
        float cy = (lo.y + hi.y) * 0.5f - pr.yMin;
        // A three-per-cent sliver is thinner than the chip; keep the chip inside the
        // bar's own span so it never floats off the top or bottom of the stack.
        var bc = new Vector3[4];
        bar.rectTransform.GetWorldCorners(bc);
        float barLo = ((Vector2)parent.InverseTransformPoint(bc[0])).y - pr.yMin;
        float barHi = ((Vector2)parent.InverseTransformPoint(bc[2])).y - pr.yMin;
        cy = Mathf.Clamp(cy, barLo + 16f, barHi - 16f);
        shareChip.anchoredPosition = new Vector2(cx, cy);

        // LIFT THE ONE, DO NOT DIM THE OTHER FOUR. Fading the rest to a third of their
        // alpha turned the whole mass balance grey - the fibre block, which is the
        // entire point of the chart, washed out to the same nothing as everything else,
        // and the reader lost the comparison at the exact moment they asked for detail.
        // A white wash over the hovered block reads as a raised key on an instrument and
        // leaves every other quantity exactly as legible as it was.
        segLift.rectTransform.anchorMin = seg.anchorMin;
        segLift.rectTransform.anchorMax = seg.anchorMax;
        segLift.rectTransform.offsetMin = seg.offsetMin;
        segLift.rectTransform.offsetMax = seg.offsetMax;
        segLift.gameObject.SetActive(true);
        segLift.transform.SetAsLastSibling();

        for (int r = 0; r < legend.Count; r++)
        {
            if (legend[r] == null) continue;
            bool on = LegendSrc[r] == stream;
            legend[r].color    = on ? BladeLoopTheme.Bone : BladeLoopTheme.Muted;
            legendSub[r].color = BladeLoopTheme.Bone;
        }

        shareChip.gameObject.SetActive(true);
        shareChip.SetAsLastSibling();
    }

    void HideShare()
    {
        if (shareChip == null) return;
        shareChip.gameObject.SetActive(false);
        if (segLift != null) segLift.gameObject.SetActive(false);
        for (int r = 0; r < legend.Count; r++)
        {
            if (legend[r] == null) continue;
            legend[r].color    = BladeLoopTheme.Muted;
            legendSub[r].color = BladeLoopTheme.Bone;
        }
    }

    /// <summary>Legend row r shows stream LegendSrc[r]. Top-to-bottom the rows read
    /// loss, char, syngas, oil, fibre - the same order the blocks stack.</summary>
    static readonly int[] LegendSrc = { 4, 3, 2, 1, 0 };

    Slider sT, sR, sF, sP;
    TMP_Text vT, vR, vF, vP, planBig, planBadgeTxt, quality;
    Image planBadge, bar, feedWall;
    GameObject snapBtn;
    readonly List<Image> segs = new List<Image>();
    readonly List<TMP_Text> legend = new List<TMP_Text>();
    readonly List<TMP_Text> legendSub = new List<TMP_Text>();
    bool suppress;

    void OnEdit()
    {
        if (suppress) return;

        float cap = OrderSolver.MaxFeed(sP.value);
        if (sF.value > cap) { suppress = true; sF.value = cap; suppress = false; }

        edited = new ProcessModel
        {
            TempC = sT.value, RetentionMin = sR.value,
            FeedKgH = sF.value, ParticleSizeMm = sP.value
        };
        Commit();
        PaintPlan();
    }

    void PaintPlan()
    {
        var m = Current();
        if (m == null || planBig == null) return;

        vT.text = $"{m.TempC:0} °C";  vR.text = $"{m.RetentionMin:0} min";
        vF.text = $"{m.FeedKgH:N0}";  vP.text = $"{m.ParticleSizeMm:0.#} mm";
        planBig.text = $"{m.TempC:0} °C   {m.RetentionMin:0} min   {m.FeedKgH:N0} kg/h   {m.ParticleSizeMm:0.#} mm";

        float cap = OrderSolver.MaxFeed(m.ParticleSizeMm);
        feedWall.rectTransform.anchorMin =
            new Vector2(Mathf.Clamp01((cap - sF.minValue) / (sF.maxValue - sF.minValue)), 0f);

        var sp = m.OutputSplit();
        float[] pct = { sp.GlassPct, sp.OilPct, sp.SyngasPct, sp.CharPct, sp.LossPct };
        float[] kgh = { sp.GlassKgH, sp.OilKgH, sp.SyngasKgH, sp.CharKgH, sp.LossKgH };
        float total = 0f; foreach (var v in pct) total += v;

        // Vertical stack: fibre at the bottom, losses on top.
        float cur = 0f;
        for (int i = 0; i < 5 && i < segs.Count; i++)
        {
            float f = total > 0.01f ? pct[i] / total : 0f;
            segs[i].rectTransform.anchorMin = new Vector2(0f, cur);
            segs[i].rectTransform.anchorMax = new Vector2(1f, cur + f);
            segs[i].rectTransform.offsetMin = new Vector2(0f, i == 0 ? 0f : 1.5f);
            segs[i].rectTransform.offsetMax = Vector2.zero;
            cur += f;
        }

        // The legend was built top-down in stack order, so row r holds stream 4-r.
        // Reading them back with the same mapping is what keeps the figures next to
        // the blocks they describe.
        curShare = ShareOf(m);
        for (int r = 0; r < 5 && r < legendSub.Count; r++)
            legendSub[r].text = ShareLabel(curShare[LegendSrc[r]]);

        if (shareHdr != null)
            shareHdr.text = curShare[0].unit == "t" ? "OVER THE RUN" : "PER HOUR";

        // Values just changed underneath the pointer; the chip would be quoting the
        // previous plan until the mouse moved.
        HideShare();
        quality.text = $"{m.FiberPurityPct:0.0}% pure    {m.TensileRetentionPct:0}% strength    "
                     + $"{kgh[0]:N0} kg/h fibre";

        var g = OrderContext.GradeOf(m.FiberPurityPct, m.TensileRetentionPct);
        planBadge.color = g == Grade.High ? BladeLoopTheme.StreamGas
                        : g == Grade.Mid  ? BladeLoopTheme.Oxide : BladeLoopTheme.Faint;
        planBadgeTxt.text = OrderContext.GradeLabel(g);

        bool off;
        Verdict(m, out off);
        if (snapBtn != null) snapBtn.SetActive(off);
        UpdateGuide();
    }

    /// <summary>
    /// Why the sliders are worth touching - and when they are not.
    ///
    /// Every plan on the frontier is the best available for somebody. A setting
    /// that is beaten on BOTH throughput and yield is beaten outright, and saying
    /// so is more useful than letting the user think they found something. Where a
    /// change genuinely costs nothing, it says that instead.
    /// </summary>
    string Verdict(ProcessModel m, out bool offFrontier)
    {
        offFrontier = false;
        if (frontier.Count == 0) return string.Empty;

        float kg = m.OutputSplit().GlassKgH;
        float yd = kg / m.FeedKgH;

        OrderSolver.Plan? beats = null;
        float bestGain = 0f;
        foreach (var p in frontier)
        {
            if (p.fibreKgH >= kg - 0.5f && p.yieldFrac >= yd - 1e-5f &&
                (p.fibreKgH > kg + 0.5f || p.yieldFrac > yd + 1e-5f))
            {
                float gain = (p.fibreKgH - kg) / Mathf.Max(kg, 1f) + (p.yieldFrac - yd);
                if (gain > bestGain) { bestGain = gain; beats = p; }
            }
        }

        var g = OrderContext.GradeOf(m.FiberPurityPct, m.TensileRetentionPct);
        if (mode != Mode.Constraint && g > grade)
        {
            offFrontier = true;
            return $"This has dropped to {OrderContext.GradeLabel(g).ToLower()} — below what "
                 + $"{BuyerName(grade).ToLower()} will take. It still sells, to a different buyer.";
        }

        if (beats.HasValue)
        {
            offFrontier = true;
            var b = beats.Value;
            return $"There is a better plan: {b.fibreKgH - kg:N0} kg/h more fibre AND "
                 + $"{(b.yieldFrac - yd) * 100f:0.0}% more from every tonne. Nothing is gained here.";
        }

        return "This is one of the best plans available for what you asked. Moving the sliders "
             + "from here trades one thing for another rather than improving both.";
    }

    static readonly string[] StreamName = { "Fibre", "Oil", "Syngas", "Char", "Loss" };

    static string Destination(int i) => i switch
    {
        0 => "back into composite parts",
        1 => "burned as plant fuel",
        2 => "piped back to the burners",
        3 => "carbon residue, sold on",
        _ => "dust and moisture, lost"
    };

    // =============================================================== builders ==

    /// <summary>A section label. Sans SemiBold, light tracking, Muted rather than
    /// Faint - these are meant to be scanned, and the old Mono/Faint/heavy-tracking
    /// combination made them the hardest thing on the panel to read.</summary>
    /// <summary>Hang a rect from the TOP edge of its parent at an exact pixel offset and
    /// height, with the horizontal edges as fractions. Use wherever the parent's height
    /// is not known at design time - a fractional anchor there floats the content to
    /// the middle of whatever box it lands in.</summary>
    static void TopPin(RectTransform rt, float x0, float x1, float top, float h)
    {
        rt.anchorMin = new Vector2(x0, 1f);
        rt.anchorMax = new Vector2(x1, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, h);
        rt.anchoredPosition = new Vector2(0f, -top);
    }

    /// <summary>Centre a rect on its parent at an exact pixel size, immune to whatever
    /// the parent's own anchors do. Needed anywhere a layout-driving component (Slider)
    /// would otherwise reinterpret sizeDelta as an offset.</summary>
    static void PointRect(RectTransform rt, float w, float h)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
        var im = rt.GetComponent<Image>();
        if (im != null) im.raycastTarget = false;
    }

    TMP_Text Micro(RectTransform p, string s, float x, float y)
    {
        var t = Label(p, "m_" + s, s, TypeMicro, BladeLoopTheme.Muted,
                      TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
        t.characterSpacing = 1.6f;
        Anchor(t.rectTransform, x, y, x + 0.5f, y + 0.105f);
        return t;
    }

    void BigSlider(RectTransform rt, float y, string label, float min, float max, bool whole,
                   float start, UnityEngine.Events.UnityAction<float> onChange, System.Func<string> fmt)
    {
        // Same control language as the frontier rail: a shallow groove, a thin accent
        // fill, and a blade-and-plate grip. The previous version was a 30-unit solid
        // orange slab with a white block on it - the loudest object on the screen, and
        // it did not match the one other slider in the flow.
        // y+0.13, not y+0.20: Micro's box is 0.105 of the parent, which in the taller
        // card is 60 units, so the label was floating a long way clear of the groove it
        // names.
        TipIfKnown(Micro(rt, label, 0.012f, y + 0.13f), label);

        var val = Label(rt, "bv", fmt(), TypeHuge, BladeLoopTheme.Bone,
                        TextAlignmentOptions.Right, BladeLoopTheme.MonoBold);
        Anchor(val.rectTransform, 0.74f, y + 0.045f, 0.985f, y + 0.155f);

        var go = new GameObject("bs", typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(rt, false);
        AnchorIn(rt, (RectTransform)go.transform, 0.012f, y + 0.078f, 0.70f, y + 0.106f, 0f, 1f);

        var bg = Img((RectTransform)go.transform, "bg", BladeLoopTheme.RuleSoft);
        Anchor(bg.rectTransform, 0f, 0f, 1f, 1f); bg.raycastTarget = true;
        var fa = Rect((RectTransform)go.transform, "FA"); Anchor(fa, 0f, 0f, 1f, 1f);
        var fill = Img(fa, "F", BladeLoopTheme.Oxide);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.sizeDelta = new Vector2(8f, 0f);
        var ha = Rect((RectTransform)go.transform, "HA"); Anchor(ha, 0f, 0f, 1f, 1f);

        // Invisible handleRect, visual in children - Slider rewrites both anchor pairs
        // on the rect it drives, so a sizeDelta there is an offset, not a size.
        var h = Img(ha, "H", new Color(0f, 0f, 0f, 0f));
        h.rectTransform.sizeDelta = new Vector2(26f, 0f);
        h.raycastTarget = true;
        var hplate = Img(h.rectTransform, "plate", new Color(1f, 1f, 1f, 0.09f));
        PointRect(hplate.rectTransform, 22f, 26f);
        var hblade = Img(h.rectTransform, "blade", BladeLoopTheme.Bone);
        PointRect(hblade.rectTransform, 3f, 26f);

        var s = go.GetComponent<Slider>();
        s.fillRect = fill.rectTransform; s.handleRect = h.rectTransform; s.targetGraphic = h;
        s.minValue = min; s.maxValue = max; s.wholeNumbers = whole;
        s.SetValueWithoutNotify(start);
        s.onValueChanged.AddListener(v => { onChange(v); val.text = fmt(); });
    }

    Slider Slid(RectTransform rt, string label, float min, float max, bool whole,
                float x0, float x1, float y, out TMP_Text val)
    {
        var l = Label(rt, "sl_" + label, label, TypeMicro, BladeLoopTheme.Muted,
                      TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
        l.characterSpacing = 1.6f;
        // A clear gap, not a shared edge: at x1-0.13 for both, the two rects touched
        // and the longer labels ran under their own value.
        Anchor(l.rectTransform, x0, y + 0.055f, x1 - 0.17f, y + 0.105f);
        TipIfKnown(l, label);

        val = Label(rt, "sv_" + label, "", TypeValue, BladeLoopTheme.Bone,
                    TextAlignmentOptions.Right, BladeLoopTheme.MonoBold);
        Anchor(val.rectTransform, x1 - 0.15f, y + 0.05f, x1, y + 0.11f);

        var go = new GameObject("s_" + label, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(rt, false);
        Anchor((RectTransform)go.transform, x0, y, x1, y + 0.038f);

        // Same groove-and-blade control as the frontier rail and the big slider. There
        // are FOUR of these on the plan card; as solid orange slabs with white blocks on
        // them they were most of the colour on the screen, and none of it meant anything.
        var bg = Img((RectTransform)go.transform, "bg", BladeLoopTheme.RuleSoft);
        Anchor(bg.rectTransform, 0f, 0f, 1f, 1f); bg.raycastTarget = true;
        var fa = Rect((RectTransform)go.transform, "FA"); Anchor(fa, 0f, 0f, 1f, 1f);

        // HALF-STRENGTH accent on these four. At full strength the plan card carried
        // four long oxide bars plus the grade badge plus the CTA, and the eye had
        // nowhere to land - the accent has to mean "this is the thing", and it cannot
        // mean that six times on one card.
        var fill = Img(fa, "F", new Color(BladeLoopTheme.Oxide.r, BladeLoopTheme.Oxide.g,
                                          BladeLoopTheme.Oxide.b, 0.50f));
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.sizeDelta = new Vector2(6f, 0f);
        var ha = Rect((RectTransform)go.transform, "HA"); Anchor(ha, 0f, 0f, 1f, 1f);

        var h = Img(ha, "H", new Color(0f, 0f, 0f, 0f));
        h.rectTransform.sizeDelta = new Vector2(22f, 0f);
        h.raycastTarget = true;
        var hp = Img(h.rectTransform, "plate", new Color(1f, 1f, 1f, 0.09f));
        PointRect(hp.rectTransform, 18f, 22f);
        var hb = Img(h.rectTransform, "blade", BladeLoopTheme.Bone);
        PointRect(hb.rectTransform, 3f, 22f);

        var s = go.GetComponent<Slider>();
        s.fillRect = fill.rectTransform; s.handleRect = h.rectTransform; s.targetGraphic = h;
        s.minValue = min; s.maxValue = max; s.wholeNumbers = whole;
        return s;
    }

    static RectTransform Rect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image Img(Transform parent, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var i = go.GetComponent<Image>(); i.color = c; i.raycastTarget = false;
        return i;
    }

    static TMP_Text Label(Transform parent, string name, string text, float size, Color c,
                          TextAlignmentOptions align, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = c; t.alignment = align;
        if (font != null) t.font = font;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        // Overflow, never Ellipsis: TMP draws NOTHING when a line is marginally too
        // tall for its rect, which deletes text rather than clipping it.
        t.overflowMode = TextOverflowModes.Overflow;
        Anchor((RectTransform)go.transform, 0f, 0f, 1f, 1f);
        return t;
    }

    void Btn(Transform parent, string name, string label, float x0, float y0, float x1, float y1,
             bool primary, UnityEngine.Events.UnityAction onClick)
    { BtnObj(parent, name, label, x0, y0, x1, y1, primary, onClick); }

    /// <summary>
    /// A button with a FIXED height in canvas units, pinned to a y position.
    /// Fractional heights made buttons grow with whichever box they landed in, so
    /// the same control was slim in one step and a slab in another.
    /// </summary>
    GameObject FixedBtn(Transform parent, string name, string label,
                        float x0, float x1, float yAnchor, float heightUnits,
                        bool primary, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(x0, yAnchor);
        rt.anchorMax = new Vector2(x1, yAnchor);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(0f, -heightUnits * 0.5f);
        rt.offsetMax = new Vector2(0f,  heightUnits * 0.5f);

        var img = go.GetComponent<Image>();
        img.color = primary ? BladeLoopTheme.Oxide : new Color(1f, 1f, 1f, 0.065f);
        img.raycastTarget = true;

        // Primary carries its own weight in fill; only the quiet variant needs an
        // edge to read as a control at all.
        if (!primary)
        {
            var e = go.AddComponent<Outline>();
            e.effectColor = BladeLoopTheme.Hex("4A4238");
            e.effectDistance = new Vector2(1f, -1f);
        }

        var colors = go.GetComponent<Button>().colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor     = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.fadeDuration     = 0.08f;
        go.GetComponent<Button>().colors = colors;
        go.GetComponent<Button>().onClick.AddListener(onClick);

        var t = Label(rt, "l", label, TypeButton,
                      primary ? BladeLoopTheme.Hex("15110E") : BladeLoopTheme.Bone,
                      TextAlignmentOptions.Center, BladeLoopTheme.MonoBold);
        t.characterSpacing = 2.4f;
        return go;
    }

    GameObject BtnObj(Transform parent, string name, string label,
                      float x0, float y0, float x1, float y1,
                      bool primary, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Anchor((RectTransform)go.transform, x0, y0, x1, y1);
        var img = go.GetComponent<Image>();
        img.color = primary ? BladeLoopTheme.Oxide : new Color(1f, 1f, 1f, 0.05f);
        img.raycastTarget = true;
        var e = go.AddComponent<Outline>();
        e.effectColor = primary ? BladeLoopTheme.Oxide : BladeLoopTheme.Hex("4A4238");
        e.effectDistance = new Vector2(1.2f, -1.2f);
        go.GetComponent<Button>().onClick.AddListener(onClick);
        var t = Label((RectTransform)go.transform, "l", label, TypeButton,
                      primary ? BladeLoopTheme.Hex("15110E") : BladeLoopTheme.Bone,
                      TextAlignmentOptions.Center, BladeLoopTheme.MonoBold);
        t.characterSpacing = 3f;
        return go;
    }

    static void Anchor(RectTransform r, float x0, float y0, float x1, float y1)
    {
        r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1);
        r.offsetMin = Vector2.zero;        r.offsetMax = Vector2.zero;
    }

    /// <summary>Anchors a child using fractions of the PARENT's box, with an optional
    /// vertical inset - so a step's contents can be positioned in its own space
    /// without knowing where on screen the step ended up.</summary>
    static void AnchorIn(RectTransform parent, RectTransform r,
                         float x0, float y0, float x1, float y1, float iy0, float iy1)
    {
        Anchor(r, x0, Mathf.Lerp(y0, y1, iy0), x1, Mathf.Lerp(y0, y1, iy1));
    }
}
