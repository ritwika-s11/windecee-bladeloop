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
        guide = Img(panel, "Guide", new Color(1f, 1f, 1f, 0.022f), 12).rectTransform;
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
        Anchor(guideBody.rectTransform, 0.06f, 0.666f, 0.94f, 0.826f);

        Anchor(Img(guide, "gr1", BladeLoopTheme.Rule).rectTransform, 0.06f, 0.650f, 0.94f, 0.6515f);

        // ---- your selection so far ------------------------------------------
        // The panel used to leave a 135-unit hole under the body and another 119
        // under an empty note. This fills both with the thing a user actually wants
        // on screen while they work: what they have chosen so far.
        var selHdr = Label(guide, "gsh", "YOUR SELECTION", TypeMicro, BladeLoopTheme.Muted,
                           TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
        selHdr.characterSpacing = 1.6f;
        Anchor(selHdr.rectTransform, 0.06f, 0.611f, 0.94f, 0.641f);

        float[] rowTop = { 0.5941f, 0.5550f, 0.5159f, 0.4768f };
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

        Anchor(Img(guide, "gr2", BladeLoopTheme.Rule).rectTransform, 0.06f, 0.423f, 0.94f, 0.4245f);

        gMetricALbl = Label(guide, "gal", "", TypeMicro, BladeLoopTheme.Muted,
                            TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
        gMetricALbl.characterSpacing = 1.6f;
        Anchor(gMetricALbl.rectTransform, 0.06f, 0.384f, 0.94f, 0.413f);
        gMetricA = Label(guide, "ga", "", TypeHuge, BladeLoopTheme.Bone,
                         TextAlignmentOptions.Left, BladeLoopTheme.MonoBold);
        Anchor(gMetricA.rectTransform, 0.06f, 0.320f, 0.94f, 0.379f);

        gMetricBLbl = Label(guide, "gbl", "", TypeMicro, BladeLoopTheme.Muted,
                            TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
        gMetricBLbl.characterSpacing = 1.6f;
        Anchor(gMetricBLbl.rectTransform, 0.06f, 0.279f, 0.94f, 0.308f);
        gMetricB = Label(guide, "gbv", "", TypeHuge, BladeLoopTheme.Bone,
                         TextAlignmentOptions.Left, BladeLoopTheme.MonoBold);
        Anchor(gMetricB.rectTransform, 0.06f, 0.215f, 0.94f, 0.274f);

        Anchor(Img(guide, "gr3", BladeLoopTheme.Rule).rectTransform, 0.06f, 0.198f, 0.94f, 0.1995f);

        guideNote = Label(guide, "gn", "", TypeBody, BladeLoopTheme.Oxide,
                          TextAlignmentOptions.TopLeft, BladeLoopTheme.Sans);
        guideNote.textWrappingMode = TextWrappingModes.Normal;
        guideNote.lineSpacing = 8f;
        Anchor(guideNote.rectTransform, 0.06f, 0.049f, 0.94f, 0.183f);
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

        if (step == 0)
        {
            title = "Start from what you have";
            body  = "Most people know one of three things: who is buying, what material is sitting in the yard, "
                  + "or what their equipment can manage. Any one of them is enough.";
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
            }
            else if (mode == Mode.Supply)
            {
                title = "What the yard holds";
                body  = "With material fixed rather than an order, the question flips: not how fast can we run, "
                      + "but how much fibre can we get out of what is already here.";
                aL = "BLADE MATERIAL"; aV = $"{blades * OrderContext.BladeMassTonnes:N0} t";
                bL = "TURBINES WORTH"; bV = $"{blades / OrderContext.BladesPerTurbine:N0}";
            }
            else
            {
                title = "Your shredder sets the ceiling";
                body  = "Finer grinding is slower grinding, so particle size caps how fast material can be fed. "
                      + "That one limit is what stops the plant running flat out at perfect quality.";
                aL = "MOST YOU CAN FEED"; aV = $"{OrderSolver.MaxFeed(shredMm):N0} kg/h";
                bL = "AT PARTICLE SIZE";  bV = $"{shredMm:0.#} mm";
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
            if (mode != Mode.Constraint && grade == Grade.Low)
                note = "Low grade offers the same options as precast concrete. Running dirtier than this costs "
                     + "more than it gains.";
        }
        else if (step == 3)
        {
            var m = Current();
            if (m != null)
            {
                bool off;
                note   = Verdict(m, out off);
                title  = off ? "Something better is available" : "A plan worth running";
                body   = "The four settings stay live. Temperature and time decide how completely the resin "
                       + "breaks down; particle size decides both quality and how fast you can feed.";
                accent = off ? BladeLoopTheme.Oxide : BladeLoopTheme.StreamGas;
                aL = "FIBRE PER HOUR"; aV = $"{m.OutputSplit().GlassKgH:N0} kg/h";
                bL = "PURITY";         bV = $"{m.FiberPurityPct:0.0}%";
            }
        }

        guideTitle.text   = title;
        guideBody.text    = body;
        guideNote.text    = note;
        guideNote.color   = accent;
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

    void Rebuild()
    {
        if (stepHolder != null)
        {
            stepHolder.gameObject.SetActive(false);
            Destroy(stepHolder.gameObject);
        }
        stepHolder = Rect(stepCol, "Steps");
        Anchor(stepHolder, 0f, 0f, 1f, 1f);

        // Fractions of the STEP COLUMN (0.03..0.835 of screen), not of the screen.
        // Each is sized from the content it holds plus the 62-unit header strip.
        float top = 1f;
        for (int i = 0; i < 4; i++)
        {
            bool open = i == step;
            float h = !open ? 0.072f
                    : i == 0 ? 0.248f
                    : i == 1 ? 0.335f
                    : i == 2 ? 0.373f
                    :          0.683f;

            if (i > reached) h = 0.072f;
            BuildStep(i, top - h, top, open && i <= reached);
            top -= h + 0.0124f;
        }

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
                             : new Color(1f, 1f, 1f, 0.020f), 10);
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

        // Mono for the index - the digits column-align down the stack.
        var num = Label(head, "n", $"{i + 1:00}", TypeMicro,
                        locked ? BladeLoopTheme.Rule : (open ? BladeLoopTheme.Oxide : BladeLoopTheme.Faint),
                        TextAlignmentOptions.Left, BladeLoopTheme.Mono);
        Anchor(num.rectTransform, 0.022f, 0f, 0.055f, 1f);

        var name = Label(head, "t", StepName[i], TypeMicro,
                         locked ? BladeLoopTheme.Rule : (open ? BladeLoopTheme.Bone : BladeLoopTheme.Muted),
                         TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
        name.characterSpacing = 1.6f;
        Anchor(name.rectTransform, 0.068f, 0f, 0.45f, 1f);

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
        float gap = 0.012f, w = (1f - 0.024f - 2f * gap) / 3f;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            float x = 0.012f + i * (w + gap);
            bool on = (int)mode == i;

            // Selection reads as a lifted surface plus an accent edge, not a box
            // outline. Outlines on every card is what made this look like a form.
            var card = Img(rt, "m" + i, on ? new Color(BladeLoopTheme.Oxide.r, BladeLoopTheme.Oxide.g,
                                                       BladeLoopTheme.Oxide.b, 0.16f)
                                           : new Color(1f, 1f, 1f, 0.028f), 10);
            card.raycastTarget = true;
            AnchorIn(rt, card.rectTransform, x, 0.03f, x + w, 0.97f, 0f, 1f);

            if (on)
            {
                var accent = Img(card.rectTransform, "acc", BladeLoopTheme.Oxide);
                Anchor(accent.rectTransform, 0f, 0f, 1f, 0.022f);
            }

            var b = card.gameObject.AddComponent<Button>();
            b.targetGraphic = card;
            b.onClick.AddListener(() => { mode = (Mode)idx; frontier.Clear(); edited = null;
                                          reached = 1; step = 1; Rebuild(); });

            var t = Label(card.rectTransform, "t", ModeTitle[i], TypeStepTitle,
                          on ? BladeLoopTheme.Bone : BladeLoopTheme.Muted,
                          TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
            Anchor(t.rectTransform, 0.06f, 0.46f, 0.96f, 0.86f);

            var bl = Label(card.rectTransform, "b", ModeBlurb[i], TypeBody, BladeLoopTheme.Faint,
                           TextAlignmentOptions.TopLeft, BladeLoopTheme.Sans);
            bl.textWrappingMode = TextWrappingModes.Normal;
            Anchor(bl.rectTransform, 0.06f, 0.10f, 0.96f, 0.44f);
        }
    }

    // -------------------------------------------------------- step 2: your numbers

    void StepNumbers(RectTransform rt)
    {
        if (mode == Mode.Buyer || mode == Mode.Supply) BuyerRow(rt, 0.62f);

        float sy = (mode == Mode.Constraint) ? 0.62f : 0.30f;

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
        Anchor(derived.rectTransform, 0.012f, 0.06f, 0.62f, 0.24f);

        FixedBtn(rt, "go", "FIND THE OPTIONS  →", 0.66f, 0.985f, 0.145f, 46f, true, ComputeFrontier);
        Refresh();
    }

    TMP_Text derived;

    void BuyerRow(RectTransform rt, float y)
    {
        Micro(rt, "WHO IS BUYING", 0.012f, y + 0.26f);
        float gap = 0.010f, w = (0.62f - 0.012f - 2f * gap) / 3f;
        for (int i = 0; i < 3; i++)
        {
            var g = (Grade)i;
            float x = 0.012f + i * (w + gap);
            bool on = grade == g;

            var chip = Img(rt, "g" + i, on ? BladeLoopTheme.Oxide : new Color(1f, 1f, 1f, 0.028f), 8);
            chip.raycastTarget = true;
            AnchorIn(rt, chip.rectTransform, x, y, x + w, y + 0.24f, 0f, 1f);
            var b = chip.gameObject.AddComponent<Button>();
            b.targetGraphic = chip;
            b.onClick.AddListener(() => { grade = g; frontier.Clear(); Rebuild(); });

            var l = Label(chip.rectTransform, "l", BuyerName(g), TypeLabel,
                          on ? BladeLoopTheme.Hex("15110E") : BladeLoopTheme.Bone,
                          TextAlignmentOptions.Center, BladeLoopTheme.SansBold);
            Anchor(l.rectTransform, 0.04f, 0.46f, 0.96f, 0.96f);

            var u = Label(chip.rectTransform, "u", EndUseShort(g), TypeMicro,
                          on ? BladeLoopTheme.Hex("3A2418") : BladeLoopTheme.Faint,
                          TextAlignmentOptions.Center, BladeLoopTheme.Sans);
            Anchor(u.rectTransform, 0.04f, 0.04f, 0.96f, 0.46f);
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

        Micro(rt, leftCap,  0.012f, 0.60f);
        var rc = Label(rt, "rc", rightCap, TypeMicro, BladeLoopTheme.Muted,
                       TextAlignmentOptions.Right, BladeLoopTheme.SansBold);
        rc.characterSpacing = 1.6f;
        Anchor(rc.rectTransform, 0.52f, 0.60f, 0.985f, 0.68f);

        // ---- the rail: one notch per optimal plan ----
        var track = Img(rt, "rail", BladeLoopTheme.Rule, 4);
        AnchorIn(rt, track.rectTransform, 0.012f, 0.50f, 0.985f, 0.57f, 0f, 1f);

        var fill = Img(track.rectTransform, "fill", BladeLoopTheme.Oxide);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(
            frontier.Count <= 1 ? 1f : railIndex / (float)(frontier.Count - 1), 1f);
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        // The slider carries its own invisible raycast target so the whole rail is
        // clickable, not just the handle - with the graphic on the track instead,
        // the track swallows the click and the rail feels dead everywhere except
        // the 16 px handle. Taller than the visible rail for a forgiving hit area.
        var s = new GameObject("railSlider", typeof(RectTransform), typeof(Image), typeof(Slider));
        s.transform.SetParent(track.transform, false);
        Anchor((RectTransform)s.transform, 0f, -1.6f, 1f, 2.6f);
        var hit = s.GetComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;
        var handleArea = Rect((RectTransform)s.transform, "HA"); Anchor(handleArea, 0f, 0f, 1f, 1f);
        var handle = Img(handleArea, "H", BladeLoopTheme.Bone, 6);
        handle.rectTransform.sizeDelta = new Vector2(16f, 34f);
        handle.raycastTarget = true;
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
        Anchor(railA.rectTransform, 0.012f, 0.278f, 0.48f, 0.488f);
        railB = Label(rt, "cb", "", TypeHuge, BladeLoopTheme.Bone,
                      TextAlignmentOptions.Right, BladeLoopTheme.MonoBold);
        Anchor(railB.rectTransform, 0.50f, 0.278f, 0.985f, 0.488f);

        railALbl = Label(rt, "cal", "", TypeMicro, BladeLoopTheme.Muted,
                         TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
        Anchor(railALbl.rectTransform, 0.012f, 0.178f, 0.48f, 0.278f);
        railBLbl = Label(rt, "cbl", "", TypeMicro, BladeLoopTheme.Muted,
                         TextAlignmentOptions.Right, BladeLoopTheme.SansBold);
        Anchor(railBLbl.rectTransform, 0.50f, 0.178f, 0.985f, 0.278f);

        railNote = Label(rt, "rn", "", TypeBody, BladeLoopTheme.Muted,
                         TextAlignmentOptions.TopLeft, BladeLoopTheme.Sans);
        railNote.textWrappingMode = TextWrappingModes.Normal;
        Anchor(railNote.rectTransform, 0.012f, 0.02f, 0.68f, 0.135f);

        railFill = fill;
        FixedBtn(rt, "go2", "SEE THE PLAN  →", 0.70f, 0.985f, 0.058f, 46f, true, () =>
        {
            reached = Mathf.Max(reached, 3);
            step = 3;
            Commit();
            Rebuild();
        });

        PaintRail();
    }

    TMP_Text railA, railB, railALbl, railBLbl, railNote;
    Image railFill;

    void PaintRail()
    {
        if (railA == null || frontier.Count == 0) return;
        var p = frontier[Mathf.Clamp(railIndex, 0, frontier.Count - 1)];

        if (railFill != null)
            railFill.rectTransform.anchorMax = new Vector2(
                frontier.Count <= 1 ? 1f : railIndex / (float)(frontier.Count - 1), 1f);

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

        planBadge = Img(rt, "badge", BladeLoopTheme.Oxide, 6);
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
        snapBtn = FixedBtn(rt, "snap", "BACK TO BEST", 0.012f, 0.30f, 0.492f, 42f, false, () =>
        {
            edited = null;
            suppress = true;
            var b = Current();
            sT.value = b.TempC; sR.value = b.RetentionMin; sP.value = b.ParticleSizeMm; sF.value = b.FeedKgH;
            suppress = false;
            Commit(); PaintPlan();
        });

        Anchor(Img(rt, "rr", BladeLoopTheme.RuleSoft).rectTransform, 0.012f, 0.415f, 0.985f, 0.4165f);

        Micro(rt, "WHAT YOU WOULD GET", 0.012f, 0.320f);

        // Rebuild() destroys the step's objects but these lists outlive it. Without
        // clearing, a second visit to this step leaves five DESTROYED entries at the
        // front, PaintPlan walks them, and the MissingReferenceException aborts it
        // half-built - which is why the grade badge and the output bar came up empty.
        segs.Clear();
        legend.Clear();
        legendSub.Clear();

        bar = Img(rt, "bar", BladeLoopTheme.SkyWarm, 5);
        AnchorIn(rt, bar.rectTransform, 0.012f, 0.255f, 0.985f, 0.335f, 0f, 1f);

        // Swatch + bone text, never coloured text. Char is #2E2823 and Loss #5A524A -
        // as type on a #12100D panel those two legend entries were invisible.
        const float lw = 0.1926f;
        for (int i = 0; i < 5; i++)
        {
            segs.Add(Img(bar.rectTransform, "s" + i, BladeLoopTheme.StreamColours[i]));

            float x = 0.012f + i * lw;
            var sw = Img(rt, "sw" + i, BladeLoopTheme.StreamColours[i]);
            Anchor(sw.rectTransform, x, 0.205f, x + 0.011f, 0.235f);

            var t = Label(rt, "l" + i, "", 16f, BladeLoopTheme.Bone,
                          TextAlignmentOptions.TopLeft, BladeLoopTheme.SansBold);
            Anchor(t.rectTransform, x + 0.018f, 0.195f, x + lw - 0.01f, 0.245f);
            legend.Add(t);

            var d = Label(rt, "d" + i, "", 13f, BladeLoopTheme.Faint,
                          TextAlignmentOptions.TopLeft, BladeLoopTheme.Sans);
            d.textWrappingMode = TextWrappingModes.Normal;
            Anchor(d.rectTransform, x, 0.112f, x + lw - 0.01f, 0.186f);
            legendSub.Add(d);
        }

        quality = Label(rt, "q", "", 25f, BladeLoopTheme.Bone,
                        TextAlignmentOptions.Left, BladeLoopTheme.MonoBold);
        Anchor(quality.rectTransform, 0.012f, 0.015f, 0.62f, 0.100f);

        FixedBtn(rt, "watch", "WATCH THIS RUN  →", 0.70f, 0.985f, 0.055f, 46f, true, () =>
        {
            Commit();
            TourRunner.StartRun();
        });

        PaintPlan();
    }

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
        float cur = 0f;
        for (int i = 0; i < 5; i++)
        {
            float f = total > 0.01f ? pct[i] / total : 0f;
            segs[i].rectTransform.anchorMin = new Vector2(cur, 0f);
            segs[i].rectTransform.anchorMax = new Vector2(cur + f, 1f);
            segs[i].rectTransform.offsetMin = new Vector2(i == 0 ? 0f : 1.5f, 0f);
            segs[i].rectTransform.offsetMax = Vector2.zero;
            cur += f;
            legend[i].text    = $"{StreamName[i]}  {pct[i]:0.0}%";
            legendSub[i].text = $"{kgh[i]:N0} kg/h\n{Destination(i)}";
        }

        var g = OrderContext.GradeOf(m.FiberPurityPct, m.TensileRetentionPct);
        planBadge.color = g == Grade.High ? BladeLoopTheme.StreamGas
                        : g == Grade.Mid  ? BladeLoopTheme.Oxide : BladeLoopTheme.Faint;
        planBadgeTxt.text = OrderContext.GradeLabel(g);
        quality.text = $"{m.FiberPurityPct:0.0}% pure    {m.TensileRetentionPct:0}% strength";

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
    void Micro(RectTransform p, string s, float x, float y)
    {
        var t = Label(p, "m_" + s, s, TypeMicro, BladeLoopTheme.Muted,
                      TextAlignmentOptions.Left, BladeLoopTheme.SansBold);
        t.characterSpacing = 1.6f;
        Anchor(t.rectTransform, x, y, x + 0.5f, y + 0.105f);
    }

    void BigSlider(RectTransform rt, float y, string label, float min, float max, bool whole,
                   float start, UnityEngine.Events.UnityAction<float> onChange, System.Func<string> fmt)
    {
        Micro(rt, label, 0.012f, y + 0.20f);

        var val = Label(rt, "bv", fmt(), TypeHuge, BladeLoopTheme.Oxide,
                        TextAlignmentOptions.Right, BladeLoopTheme.MonoBold);
        Anchor(val.rectTransform, 0.62f, y, 0.985f, y + 0.22f);

        var go = new GameObject("bs", typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(rt, false);
        AnchorIn(rt, (RectTransform)go.transform, 0.012f, y + 0.06f, 0.60f, y + 0.13f, 0f, 1f);

        var bg = Img((RectTransform)go.transform, "bg", BladeLoopTheme.Rule, 4);
        Anchor(bg.rectTransform, 0f, 0f, 1f, 1f); bg.raycastTarget = true;
        var fa = Rect((RectTransform)go.transform, "FA"); Anchor(fa, 0f, 0f, 1f, 1f);
        var fill = Img(fa, "F", BladeLoopTheme.Oxide);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.sizeDelta = new Vector2(8f, 0f);
        var ha = Rect((RectTransform)go.transform, "HA"); Anchor(ha, 0f, 0f, 1f, 1f);
        var h = Img(ha, "H", BladeLoopTheme.Bone, 5);
        h.rectTransform.sizeDelta = new Vector2(16f, 28f); h.raycastTarget = true;

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

        val = Label(rt, "sv_" + label, "", TypeValue, BladeLoopTheme.Bone,
                    TextAlignmentOptions.Right, BladeLoopTheme.MonoBold);
        Anchor(val.rectTransform, x1 - 0.15f, y + 0.05f, x1, y + 0.11f);

        var go = new GameObject("s_" + label, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(rt, false);
        Anchor((RectTransform)go.transform, x0, y, x1, y + 0.038f);

        var bg = Img((RectTransform)go.transform, "bg", BladeLoopTheme.Rule, 4);
        Anchor(bg.rectTransform, 0f, 0f, 1f, 1f); bg.raycastTarget = true;
        var fa = Rect((RectTransform)go.transform, "FA"); Anchor(fa, 0f, 0f, 1f, 1f);
        var fill = Img(fa, "F", BladeLoopTheme.Oxide);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.sizeDelta = new Vector2(6f, 0f);
        var ha = Rect((RectTransform)go.transform, "HA"); Anchor(ha, 0f, 0f, 1f, 1f);
        var h = Img(ha, "H", BladeLoopTheme.Bone, 5);
        h.rectTransform.sizeDelta = new Vector2(12f, 22f); h.raycastTarget = true;

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

    /// <summary>
    /// A surface with rounded corners. uGUI has no corner radius, so this uses the
    /// 9-sliced sprite from BladeLoopTheme - which is what turns a screen built out
    /// of hard rectangles into one that looks designed. Radius is in sprite pixels.
    /// </summary>
    static Image Img(Transform parent, string name, Color c, int radius)
    {
        var i = Img(parent, name, c);
        i.sprite = BladeLoopTheme.Rounded(radius);
        i.type   = Image.Type.Sliced;
        // Sliced borders are measured in sprite pixels. Without a multiplier the
        // corner arcs swell as the canvas scales up and eat small controls whole.
        i.pixelsPerUnitMultiplier = 2.4f;
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
        img.sprite = BladeLoopTheme.Rounded(8);
        img.type   = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 2.4f;

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
