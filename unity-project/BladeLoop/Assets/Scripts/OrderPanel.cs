using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using TMPro;

/// <summary>
/// The right-hand order panel, and the viewport split that makes room for it.
///
/// Implements Task 3 of docs/handover-akshat.md and section 4 of
/// docs/interface-contract.md: the 3D tour renders into the left
/// OrderContext.TourSplitWidth of the window, this panel occupies the rest, and
/// both persist across all five scenes of the chain.
///
/// ---------------------------------------------------------------------------
///  WHY IT IS BUILT THIS WAY
///
///  ONE OBJECT, NO SCENE EDITS. Stage scenes have a single owner (Anirban) and
///  cannot be git-merged, so this creates itself at runtime, marks itself
///  DontDestroyOnLoad, and reacts to SceneManager.sceneLoaded. No prefab, no
///  per-scene wiring, and it applies to all four stages the moment it exists.
///
///  THE CANVAS IS SCREEN SPACE - OVERLAY. An Overlay canvas ignores Camera.rect
///  entirely and renders straight to the framebuffer, which is exactly what this
///  panel wants: it must sit in the 28% the 3D view is NOT using. Anirban's
///  TourViewportFrame solves the mirror-image problem for the stage overlays,
///  pushing them the other way. Both read OrderContext.TourSplitWidth.
///
///  THE CONTENT IS REBUILT PER STAGE. Each stage shows a different set of
///  sections - the output split only appears at Separation, because until the
///  plant has run there is nothing to report. Rebuilding from a single top-down
///  cursor keeps the layout honest; toggling objects would leave the gaps where
///  the hidden sections used to be.
///
///  IT SELF-HEALS ON EXIT. Several code paths leave a stage without going
///  through TourRunner - StoryModeController.BackToMenu() on the Escape key is
///  one, and it loads MainMenu directly. Rather than patch every exit (and miss
///  one), this watches what scene loaded: anything outside the tour chain means
///  the run is over, so it restores the camera and destroys itself.
/// ---------------------------------------------------------------------------
///
/// Owner: Akshat.
/// </summary>
[DefaultExecutionOrder(1000)]   // after CinemachineBrain, which writes the lens in LateUpdate
public class OrderPanel : MonoBehaviour
{
    public static OrderPanel Instance { get; private set; }

    // Scenes that are part of a run. Anything else means the run has ended.
    static readonly string[] FallbackTourScenes =
    {
        "FullPlantTour",
        "Stage1_StoryMode", "Transport_StoryMode",
        "Stage2_StoryMode", "Stage3_StoryMode", "Stage4_V2"
    };

    readonly HashSet<string> tourScenes = new HashSet<string>(FallbackTourScenes);

    Camera splitCam;
    CinemachineBrain brain;
    RectTransform root;      // the panel background, built once
    RectTransform content;    // everything inside it, rebuilt per stage
    RectTransform viewport;   // masked window the content scrolls behind
    RectTransform hintBox;    // pinned to the panel foot, outside the scroll
    ScrollRect    scroll;

    // ------------------------------------------------------------- lifecycle --

    /// <summary>Creates the panel if it does not exist. Safe to call twice -
    /// spawning a second one would double-draw and fight over Camera.rect.</summary>
    public static OrderPanel Create()
    {
        if (Instance != null) return Instance;

        var go = new GameObject("~OrderPanel (runtime)");
        DontDestroyOnLoad(go);
        return Instance = go.AddComponent<OrderPanel>();
    }

    /// <summary>Restores the camera and removes the panel. Safe when none exists.</summary>
    public static void Teardown()
    {
        if (Instance != null) Instance.DestroySelf();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        BladeLoopTheme.Init();
        BuildShell();

        SceneManager.sceneLoaded += OnSceneLoaded;

        // Normally created from the menu one line before LoadScene(FullPlantTour),
        // so the current scene is NOT yet part of the tour - splitting its camera
        // would crop the menu for the frame before it unloads. Only adopt straight
        // away when we were created inside the chain already.
        if (IsTourScene(SceneManager.GetActiveScene().name)) AdoptCamera();

        Refresh();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        // Left the tour, or the order was cleared underneath us: the run is over.
        if (!OrderContext.HasOrder || !IsTourScene(s.name))
        {
            DestroySelf();
            return;
        }

        // Camera.main is frequently null for a frame after a load, so try now and
        // again next frame rather than assuming either one works.
        AdoptCamera();
        Refresh();
        StartCoroutine(AdoptCameraNextFrame());
    }

    bool IsTourScene(string name)
    {
        // Prefer the sequencer's own list so the two can never disagree.
        var seq = FindAnyObjectByType<TourSceneSequencer>();
        if (seq != null && seq.sceneSequence != null)
            foreach (var n in seq.sceneSequence)
                if (!string.IsNullOrEmpty(n)) tourScenes.Add(n);

        return tourScenes.Contains(name);
    }

    IEnumerator AdoptCameraNextFrame()
    {
        yield return null;
        AdoptCamera();
    }

    // ------------------------------------------------------- the actual split --

    void AdoptCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;

        // Releasing the previous camera matters: stage scenes are unloaded on a
        // Single load, but an editor-only or additive camera could survive with a
        // 72% rect burned in and render cropped forever.
        if (splitCam != null && splitCam != cam) ReleaseCamera(splitCam);

        splitCam = cam;
        brain    = cam.GetComponent<CinemachineBrain>();
        ApplySplit();
    }

    void ApplySplit()
    {
        if (splitCam == null) return;

        var want = new Rect(0f, 0f, OrderContext.TourSplitWidth, 1f);
        if (splitCam.rect != want) splitCam.rect = want;
    }

    void ReleaseCamera(Camera cam)
    {
        if (cam == null) return;
        cam.rect = new Rect(0f, 0f, 1f, 1f);
    }

    void LateUpdate()
    {
        if (!OrderContext.HasOrder) { DestroySelf(); return; }
        if (splitCam == null) { AdoptCamera(); return; }

        ApplySplit();
        CompensateFov();
    }

    /// <summary>
    /// Widens vertical FOV so the narrowed viewport keeps each shot's original
    /// HORIZONTAL framing. See TourRunner.SplitVFov for the maths and the 65 deg clamp.
    ///
    /// The authored FOV is read from the Brain's current state every frame, never
    /// from Camera.fieldOfView. Reading the camera would compound: frame two would
    /// compensate frame one's already-compensated value, and the lens would open up
    /// until it hit the clamp. Reading the Brain gives the value the shot was
    /// authored with, blended, so the compensation is applied exactly once.
    ///
    /// While the story is paused the Brain is DISABLED - StoryModeController does
    /// that so ExploreOrbitCamera can drive Camera.main directly. There is no
    /// authored lens to read then, so this leaves the FOV alone and the frame stays
    /// exactly as the player paused it.
    /// </summary>
    void CompensateFov()
    {
        if (brain == null || !brain.enabled) return;

        var vcam = brain.ActiveVirtualCamera;
        if (vcam == null) return;

        float authored = vcam.State.Lens.FieldOfView;
        if (authored <= 0.01f) return;

        float want = TourRunner.SplitVFov(authored);
        if (!Mathf.Approximately(splitCam.fieldOfView, want))
            splitCam.fieldOfView = want;
    }

    void DestroySelf()
    {
        ReleaseCamera(splitCam);
        splitCam = null;
        if (this != null && gameObject != null) Destroy(gameObject);
    }

    // =========================================================== the panel UI ==

    const float Pad    = 26f;
    const float LabelW = 118f;
    const float PctW   = 74f;

    // Every text rect is sized at LineFactor x its font size. TMP with an
    // Ellipsis or Truncate overflow renders NOTHING when a line does not fit
    // vertically, so a rect that is merely close is a rect that disappears.
    // IBM Plex needs about 1.3; 1.55 leaves the margin that bug cost us.
    const float LineFactor = 1.55f;

    /// <summary>Canvas reference width for this panel alone. Smaller than the 1920 the
    /// rest of the app uses, which is what makes everything in here render larger.
    /// See BuildShell.</summary>
    const float PanelRefWidth = 1700f;

    void BuildShell()
    {
        var canvasGo = new GameObject("OrderPanelCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the stage overlays, which sit at default order. The panel is the
        // frame around the tour, so nothing in a stage should ever cover it.
        canvas.sortingOrder = 500;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // 1700, not 1920. THIS IS THE FONT SIZE CONTROL.
        //
        // Every size in this file is written in reference units, so scaling the whole
        // canvas raises the type, the rules, the bars and the paddings together and by
        // exactly the same amount. Editing twenty literals by hand would have given a
        // worse result and a much larger diff, and the first thing to drift out of step
        // would have been the y-advances that sit beside each size.
        //
        // 1920/1700 is a 13% lift, and it is the largest the cards survive: the
        // viewport measures 801 units instead of 928, and at that height the kiln card
        // lands eleven units inside the window. Separation is the one card that still
        // overruns, and it says so at its foot.
        scaler.referenceResolution = new Vector2(PanelRefWidth, PanelRefWidth * 9f / 16f);
        scaler.matchWidthOrHeight  = 0.5f;

        // The panel occupies everything the 3D view does not. Anchored as a
        // fraction, so it tracks any window size without arithmetic.
        root = MakeRect(canvasGo.transform, "Panel");
        root.anchorMin = new Vector2(OrderContext.TourSplitWidth, 0f);
        root.anchorMax = new Vector2(1f, 1f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        var bg = root.gameObject.AddComponent<Image>();
        bg.color = BladeLoopTheme.Panel;
        bg.raycastTarget = false;   // never eat clicks meant for Explore mode

        // Hairline against the 3D view so the split reads as a deliberate edge.
        var edge = MakeRect(root, "Edge");
        edge.anchorMin = new Vector2(0f, 0f);
        edge.anchorMax = new Vector2(0f, 1f);
        edge.pivot     = new Vector2(0f, 0.5f);
        edge.sizeDelta = new Vector2(1f, 0f);
        var edgeImg = edge.gameObject.AddComponent<Image>();
        edgeImg.color = BladeLoopTheme.Rule;
        edgeImg.raycastTarget = false;

        // ---- scrolling -----------------------------------------------------
        // The Separation card is the tallest and had about 120 px of headroom on a
        // 1080 canvas before the design-case references were added. Rather than
        // ration what the panel is allowed to say, the panel scrolls: content that
        // fits behaves exactly as it always did, and content that does not is
        // reachable instead of silently cut off at the bottom of the window.
        viewport = MakeRect(root, "Viewport");
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;

        // A transparent graphic, but a RAYCAST TARGET: without one the mouse wheel
        // has nothing to hit over the panel and the ScrollRect never receives it.
        // The panel background deliberately stays non-raycasting, so clicks meant
        // for Explore mode over the 3D view are still untouched - this only covers
        // the panel's own 28%, where there is nothing to explore anyway.
        var vpImg = viewport.gameObject.AddComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0f);
        vpImg.raycastTarget = true;

        viewport.gameObject.AddComponent<RectMask2D>();

        scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.viewport      = viewport;
        scroll.horizontal    = false;
        scroll.vertical      = true;
        scroll.movementType  = ScrollRect.MovementType.Clamped;   // no rubber-banding
        scroll.inertia       = false;                             // a ledger, not a feed
        scroll.scrollSensitivity = 34f;
    }

    /// <summary>What the panel says during one stage.</summary>
    struct StageCard
    {
        public string title;        // "SHREDDING"
        public string chapter;      // "STAGE 2 OF 4", or empty for the pass-through
        public bool[] shown;        // settings decided by this stage OR an earlier one
        public bool[] lit;          // the ones THIS stage decides
        public bool   showOutput;   // the split is only known once the plant has run
        public bool   showExplore;  // the pause/look-around hint
        public bool   showSpec;     // the buyer's quality floor
        public string blockHdr;
        public string blockBody;
    }

    /// <summary>
    /// Which stage we are in and what it is deciding.
    ///
    /// The prose is deliberately NOT written here. ProcessModel already owns the
    /// live explanation strings, they already respond to the actual settings, and
    /// architecture doc section 3 says rewording them propagates everywhere. New
    /// copy in this file would be a second voice that nobody reviews - and the
    /// narration is being rewritten per grade, so these must track it for free.
    /// </summary>
    StageCard CardFor(string scene)
    {
        var m = OrderContext.Model;
        var c = new StageCard
        {
            shown = new[] { false, false, false, false },
            lit   = new[] { false, false, false, false }
        };

        // Settings are introduced as the plant reaches the point that decides them,
        // rather than all four from the wind farm onwards. A temperature listed over
        // a field of turbines is noise - nothing has been decided yet, and the
        // number cannot mean anything to someone seeing it for the first time.
        // Indices: 0 temperature, 1 retention, 2 feed rate, 3 particle size.

        switch (scene)
        {
            case "Stage2_StoryMode":
                c.title = "SHREDDING";  c.chapter = "STAGE 2 OF 4";
                c.shown[3] = true;                       // particle size, decided here
                c.lit[3]   = true;
                c.showExplore = true;
                c.showSpec    = true;
                c.blockHdr  = "WHAT SIZE TO SHRED TO";
                c.blockBody = m.ParticleInfo() + "\n\n" +
                              "Particle size carries the heaviest weight in the model — more than " +
                              "temperature. This is where the biggest decision is made." +
                              NextUp("the kiln", "temperature and how long the material stays in it");
                break;

            case "Stage3_StoryMode":
                c.title = "ROTARY KILN";  c.chapter = "STAGE 3 OF 4";
                c.shown[3] = true;                       // carried in from shredding
                c.shown[0] = c.shown[1] = true;          // temperature and retention
                c.lit[0]   = c.lit[1]   = true;
                c.showExplore = true;
                c.blockHdr  = "HOW HOT, HOW LONG";
                c.blockBody = m.TempInfo() + "\n\n" + m.RetentionInfo()
                            + NextUp("separation", "what the plant actually recovered");
                break;

            case "Stage4_V2":
                c.title = "SEPARATION";  c.chapter = "STAGE 4 OF 4";
                c.shown[0] = c.shown[1] = c.shown[2] = c.shown[3] = true;
                c.lit[2]   = true;                       // feed rate sets the kg/h
                c.showOutput  = true;                    // only now is there a result
                // No hint here: Separation decides no set-point, so pausing on it
                // offers nothing to change. The card is also the tallest in the panel.
                c.showExplore = false;
                // At Separation the output section IS the payoff, so it carries the
                // block's heading and body instead of the panel running two headers
                // that say the same thing. Purity and tensile are rendered with the
                // bars, where they belong - they describe the same product.
                c.blockHdr  = "WHAT YOU ACTUALLY GOT";
                // Named. The grade in the panel's headline is the one that was ORDERED,
                // so on a run where the two differ an unattributed description of a
                // third market reads as a contradiction rather than as the result.
                c.blockBody = "Graded " + OrderContext.GradeLabel(OrderContext.AchievedGrade)
                            + ".\n\n" + OrderContext.EndUseFor(OrderContext.AchievedGrade);
                break;

            case "Transport_StoryMode":
                // A pass-through: it plays in the chain but is not a chapter of its
                // own (interface contract, section 4). Nothing is decided here, so
                // the settings block stays empty and the copy says why.
                c.title = "IN TRANSIT";  c.chapter = "";
                c.showSpec = true;
                c.blockHdr  = "ON THE ROAD";
                c.blockBody = $"{OrderContext.BladesLabel}, cut down on site and trucked to " +
                              "the plant.\n\nNothing is decided on the road. The first real choice comes " +
                              "at the shredder.";
                break;

            default:
                // Stage 1, and FullPlantTour for the frame before it hands over.
                // The wind farm decides quantity, not quality, so it shows the
                // campaign figures and no settings at all.
                c.title = "WIND FARM";  c.chapter = "STAGE 1 OF 4";
                c.showSpec = true;
                // Explore works here too, and the wind farm is where a viewer is
                // No hint on the wind farm: nothing here is adjustable, and the hint
                // now advertises set-point editing rather than looking around.
                c.showExplore = false;
                c.blockHdr  = "THIS ORDER NEEDS";
                c.blockBody = $"{OrderContext.FeedTonnesLabel} of blade material\n" +
                              $"{OrderContext.BladesLabel}   ·   {OrderContext.TurbinesLabel}\n\n" +
                              // The duration used to be stated and never explained, which
                              // left the headline number of the stage unsupported: 44.6
                              // days is only meaningful next to the rate that produces it.
                              // Rate times time is the whole of it, so show both.
                              $"The plant recovers about {OrderContext.FibreKgH:N0} kg of fibre " +
                              $"an hour, so this order is {OrderContext.CampaignLabel} of " +
                              "continuous running." +
                              NextUp("the shredder", "how fine the blade material is cut");
                break;
        }

        return c;
    }

    // ---- filling the column ---------------------------------------------------
    //
    // AN EARLIER ATTEMPT SPREAD THE SLACK INTO THE SECTION GAPS, and it looked exactly
    // like what it was: a short card stretched to fit. Leading is not content, and a
    // padded-out page is visible to anyone.
    //
    // What the sparse cards actually needed was bigger type and more to say, and the
    // dense one needed less room for the same facts. All three are here:
    //
    //   - the panel canvas renders at a smaller reference width (see BuildShell), so
    //     every glyph, rule and bar scales together and the cards keep one type size;
    //   - stages 1-3 carry a spec block and a next-stage line, both real content;
    //   - Separation's output split is one composition bar instead of five rows.

    /// <summary>Where the run goes after this stage, and what gets decided there.
    ///
    /// Two jobs. It orients the viewer - the tour cuts between stages with no map, so
    /// "what am I about to watch decide" is a question the panel can answer for a
    /// line of text. And it is honest content for the space the sparse cards have
    /// going spare, which is a better use of it than leading.
    ///
    /// Not on Separation: there is nothing after it, and a panel promising a fifth
    /// stage would be a lie. Not on Transport either, whose copy already says the
    /// first real choice comes at the shredder.</summary>
    static string NextUp(string where, string what)
        => "\n\nNext: " + where + ", where " + what + " is decided.";

    /// <summary>Rebuilds the panel for the current scene and settings.</summary>
    public void Refresh() { BuildCard(); }

    /// <summary>Lays the card out once. Returns the height the content came to.</summary>
    float BuildCard()
    {
        if (root == null || !OrderContext.HasOrder) return 0f;

        var card  = CardFor(SceneManager.GetActiveScene().name);
        var order = OrderContext.Active;
        var m     = OrderContext.Model;

        // Replace wholesale. Deactivate first: Destroy is deferred to the end of
        // the frame, so without this the old content draws over the new one once.
        if (content != null)
        {
            content.gameObject.SetActive(false);
            Destroy(content.gameObject);
        }

        // Top-anchored with a top pivot, which is what a ScrollRect needs and also
        // what the layout below already assumes - every row is placed by a negative
        // y measured down from the top, so nothing else has to change.
        content = MakeRect(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot     = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(0f, content.offsetMin.y);
        content.offsetMax = new Vector2(0f, content.offsetMax.y);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        if (scroll != null) scroll.content = content;

        // Built before the rows, because it decides how tall the scrolling area is.
        BuildExploreHint(card.showExplore);

        float y = 34f;

        // ---- who this run is for ---------------------------------------------
        Text("Grade", OrderContext.GradeLabel(order.targetGrade), 30f,
             BladeLoopTheme.Oxide, BladeLoopTheme.MonoBold, ref y);

        // customerType, not customerName: no buyer NAME is collected anywhere in the
        // project. Every Order is built with customerName = "" - the three presets and
        // the Custom Order screen alike - so the old fallback could never take its
        // second branch.
        Text("Buyer", order.customerType,
             18f, BladeLoopTheme.Muted, BladeLoopTheme.Sans, ref y);

        // ---- where the run is now heading ------------------------------------
        //
        // THE HEADLINE ABOVE IS THE ORDER, AND THE ORDER DOES NOT CHANGE. That was
        // fine while the settings were fixed at the start: ordered and produced were
        // the same thing, so one label said both. Once a setpoint can be moved
        // mid-run they come apart, and the panel went on announcing HIGH GRADE over
        // a plant that had been dropped to 16 mm and could no longer make it.
        //
        // So the order keeps the headline and the consequence gets its own line.
        //
        // ONLY AFTER THE USER HAS CHANGED SOMETHING. The panel deliberately withholds
        // the output split until Separation, on the grounds that the plant has not run
        // yet and showing a result early is both a spoiler and a lie. Predicting the
        // grade from stage one would break that rule. Predicting it after the user has
        // moved a setpoint does not: they made a decision and are owed its consequence,
        // and it cannot spoil a run they are actively steering. An untouched run shows
        // exactly what it always did.
        if (SetpointLog.Any)
        {
            Grade got = OrderContext.AchievedGrade;
            Grade want = order.targetGrade;
            bool short_ = got > want;

            y += 10f;
            Text("Heading", "HEADING FOR " + OrderContext.GradeLabel(got), 16f,
                 short_ ? BladeLoopTheme.Oxide : BladeLoopTheme.StreamGas,
                 BladeLoopTheme.MonoBold, ref y);

            Text("HeadingWhy",
                 short_ ? "below what this buyer takes"
                        : got < want ? "above what this buyer needs"
                                     : "on spec for this buyer",
                 14f, BladeLoopTheme.Faint, BladeLoopTheme.Sans, ref y);
        }

        y += 12f;
        y = Divider(y) + 24f;

        // ---- where you are ---------------------------------------------------
        var title = Text("StageTitle", card.title, 25f, BladeLoopTheme.Bone,
                         BladeLoopTheme.SansBold, ref y, advance: false);
        title.characterSpacing = 4f;

        if (!string.IsNullOrEmpty(card.chapter))
        {
            var chap = Text("Chapter", card.chapter, 14f, BladeLoopTheme.Faint,
                            BladeLoopTheme.Mono, ref y, advance: false);
            chap.alignment = TextAlignmentOptions.TopRight;
            // Nudge down so the small caps sit on the title's baseline.
            chap.rectTransform.anchoredPosition += new Vector2(0f, -9f);
        }
        y += 25f * LineFactor + 10f;

        y = Divider(y) + 24f;

        // ---- the settings decided so far, with this stage's lit ---------------
        string[] labels = { "Temperature", "Retention", "Feed rate", "Particle" };
        string[] values =
        {
            m.TempC.ToString("0") + " °C",
            m.RetentionMin.ToString("0") + " min",
            m.FeedKgH.ToString("N0") + " kg/h",
            m.ParticleSizeMm.ToString("0.#") + " mm"
        };

        bool anySetting = false;
        for (int i = 0; i < card.shown.Length; i++) anySetting |= card.shown[i];

        // What the design case would have used, for the reference line under each row.
        var refM = OrderContext.Reference;
        string[] refValues =
        {
            "design " + refM.TempC.ToString("0") + " °C",
            "design " + refM.RetentionMin.ToString("0") + " min",
            "design " + refM.FeedKgH.ToString("N0") + " kg/h",
            "design " + refM.ParticleSizeMm.ToString("0.#") + " mm"
        };
        string[] deltas =
        {
            SignedDelta(m.TempC          - refM.TempC,          "0",  "°C"),
            SignedDelta(m.RetentionMin   - refM.RetentionMin,   "0",  "min"),
            SignedDelta(m.FeedKgH        - refM.FeedKgH,        "N0", "kg/h"),
            SignedDelta(m.ParticleSizeMm - refM.ParticleSizeMm, "0.#","mm")
        };

        if (anySetting)
        {
            SectionHeader("SETTINGS", ref y);

            for (int i = 0; i < labels.Length; i++)
            {
                if (!card.shown[i]) continue;
                bool lit = card.lit[i];

                Text(labels[i] + "Lbl", labels[i], 19f,
                     lit ? BladeLoopTheme.Bone : BladeLoopTheme.Faint,
                     lit ? BladeLoopTheme.SansBold : BladeLoopTheme.Sans,
                     ref y, advance: false);

                var v = Text(labels[i] + "Val", values[i], 21f,
                             lit ? BladeLoopTheme.Oxide : BladeLoopTheme.Muted,
                             BladeLoopTheme.MonoBold, ref y, advance: false);
                v.alignment = TextAlignmentOptions.TopRight;

                y += 21f * LineFactor + 1f;

                // ---- the reference line ------------------------------------
                // A bare "550 °C" tells the user nothing: they have no idea what
                // this plant is supposed to run at, and they will never see the
                // other two orders to work it out. Naming the design value turns
                // every setting from a fact into a decision the operator made.
                //
                // Explicit height rather than the shared LineFactor leading: this is
                // an annotation hanging off the value above, not a line of its own,
                // and four rows of 55% leading is 40 px the Separation card does not
                // have. Tight spacing also groups it visually with its value.
                const float RefH = 15f;

                Text(labels[i] + "Ref", refValues[i], 13.5f, BladeLoopTheme.Faint,
                     BladeLoopTheme.Mono, ref y, advance: false, height: RefH);

                var d = Text(labels[i] + "Delta", deltas[i], 13.5f, BladeLoopTheme.Muted,
                             BladeLoopTheme.Mono, ref y, advance: false, height: RefH);
                d.alignment = TextAlignmentOptions.TopRight;

                y += RefH + 5f;
            }
        }

        // ---- the output split, at Separation only -----------------------------
        // Showing it earlier was a spoiler AND a lie: the plant has not run yet,
        // so there is nothing that "came out".
        if (card.showOutput)
        {
            y += 10f;
            y = Divider(y) + 24f;
            SectionHeader(card.blockHdr, ref y, "|  design case");

            // ---- ONE COMPOSITION BAR, NOT FIVE PROGRESS BARS --------------------
            //
            // The five streams are PARTS OF ONE TONNE. Drawn as five separate tracks
            // they read as five unrelated measurements, and because fibre is ~69% and
            // loss ~1.5% every track had to be scaled against 72% so the small ones
            // were not slivers - a divisor the reader could not see and would not have
            // guessed. A single stacked bar states the real relationship directly: the
            // segments ARE the tonne, and their widths are literally their shares.
            //
            // It is also less than half the height, which is what lets this card carry
            // the larger type the sparse cards wanted without losing anything.
            string[] streams = { "Fibre", "Oil", "Syngas", "Char", "Loss" };
            var cols = BladeLoopTheme.StreamColours;
            var sp = m.OutputSplit();
            float[] pcts = { sp.GlassPct, sp.OilPct, sp.SyngasPct, sp.CharPct, sp.LossPct };

            var rsp = OrderContext.ReferenceSplit;
            float[] refPcts = { rsp.GlassPct, rsp.OilPct, rsp.SyngasPct, rsp.CharPct, rsp.LossPct };

            // Normalised against the streams' own sum rather than a literal 100, so a
            // split that rounds to 99.9 still fills the bar exactly.
            float sum = 0f;
            for (int i = 0; i < pcts.Length; i++) sum += Mathf.Max(pcts[i], 0f);
            if (sum <= 0.01f) sum = 100f;

            const float BarH = 34f;
            var barRt = MakeRect(content, "SplitBar");
            barRt.anchorMin = new Vector2(0f, 1f);
            barRt.anchorMax = new Vector2(1f, 1f);
            barRt.pivot     = new Vector2(0.5f, 1f);
            barRt.offsetMin = new Vector2(Pad, 0f);
            barRt.offsetMax = new Vector2(-Pad, 0f);
            barRt.anchoredPosition = new Vector2(barRt.anchoredPosition.x, -y);
            barRt.sizeDelta = new Vector2(barRt.sizeDelta.x, BarH);

            var barBg = barRt.gameObject.AddComponent<Image>();
            barBg.color = BladeLoopTheme.RuleSoft;
            barBg.raycastTarget = false;

            // Fractional anchors, so the segments stay exact at any window width.
            float cursor = 0f;
            for (int i = 0; i < streams.Length; i++)
            {
                float f = Mathf.Max(pcts[i], 0f) / sum;
                var seg = MakeRect(barRt, streams[i] + "Seg");
                seg.anchorMin = new Vector2(cursor, 0f);
                seg.anchorMax = new Vector2(Mathf.Min(cursor + f, 1f), 1f);
                // A hairline of panel colour between segments. Without it two adjacent
                // dark streams merge into one block and the bar stops being readable.
                seg.offsetMin = new Vector2(i == 0 ? 0f : 1f, 0f);
                seg.offsetMax = Vector2.zero;

                var segImg = seg.gameObject.AddComponent<Image>();
                segImg.color = cols[i];
                segImg.raycastTarget = false;

                cursor += f;
            }

            // The design case, as one tick on the fibre boundary. On a stacked bar the
            // only boundary worth marking is where fibre ENDS: everything to its left
            // is product, everything to its right is not, so "did this run beat the
            // design case" is one glance at which side of the mark the cream ends on.
            float refMark = Mathf.Clamp01(refPcts[0] / 100f);
            var tick = MakeRect(barRt, "DesignTick");
            tick.anchorMin = new Vector2(refMark, 0f);
            tick.anchorMax = new Vector2(refMark, 1f);
            tick.offsetMin = new Vector2(-1f, -5f);
            tick.offsetMax = new Vector2( 1f,  5f);
            var tickImg = tick.gameObject.AddComponent<Image>();
            tickImg.color = BladeLoopTheme.Bone;
            tickImg.raycastTarget = false;

            y += BarH + 16f;

            // ---- legend, two columns -------------------------------------------
            // Five rows of swatch-name-percent stacked would cost back everything the
            // bar just saved. Two columns of three fit the width and read as a key to
            // the bar above rather than as five more measurements.
            const float RowH = 26f;
            for (int i = 0; i < streams.Length; i++)
            {
                int col = i / 3, row = i % 3;
                float x0 = col == 0 ? 0f : 0.5f;
                float ry = y + row * RowH;

                var sw = MakeRect(content, streams[i] + "Sw");
                sw.anchorMin = new Vector2(x0, 1f);
                sw.anchorMax = new Vector2(x0, 1f);
                sw.pivot     = new Vector2(0f, 1f);
                sw.anchoredPosition = new Vector2(Pad + (col == 0 ? 0f : 6f), -(ry + 5f));
                sw.sizeDelta = new Vector2(11f, 11f);
                var swImg = sw.gameObject.AddComponent<Image>();
                swImg.color = cols[i];
                swImg.raycastTarget = false;

                // TWO LABELS, NOT ONE WITH <align=right> IN IT. TMP's align tag is a
                // LINE property, not a split point: it right-aligned the stream name
                // along with the number and the two closed up into "Fibre69.0%".
                var lab = MakeRect(content, streams[i] + "Key");
                lab.anchorMin = new Vector2(x0, 1f);
                lab.anchorMax = new Vector2(x0 + 0.5f, 1f);
                lab.pivot     = new Vector2(0.5f, 1f);
                lab.offsetMin = new Vector2(Pad + (col == 0 ? 20f : 26f), 0f);
                lab.offsetMax = new Vector2(col == 0 ? -10f : -Pad, 0f);
                lab.anchoredPosition = new Vector2(lab.anchoredPosition.x, -ry);
                lab.sizeDelta = new Vector2(lab.sizeDelta.x, RowH);
                var lt = lab.gameObject.AddComponent<TextMeshProUGUI>();
                lt.text = streams[i];
                lt.fontSize = 16f;
                lt.color = BladeLoopTheme.Muted;
                lt.font = BladeLoopTheme.Sans;
                lt.alignment = TextAlignmentOptions.TopLeft;
                lt.raycastTarget = false;
                lt.textWrappingMode = TextWrappingModes.NoWrap;
                lt.overflowMode = TextOverflowModes.Overflow;

                var val = MakeRect(content, streams[i] + "KeyV");
                val.anchorMin = lab.anchorMin;
                val.anchorMax = lab.anchorMax;
                val.pivot     = lab.pivot;
                val.offsetMin = lab.offsetMin;
                val.offsetMax = lab.offsetMax;
                val.anchoredPosition = lab.anchoredPosition;
                val.sizeDelta = lab.sizeDelta;
                var vt = val.gameObject.AddComponent<TextMeshProUGUI>();
                vt.text = pcts[i].ToString("0.0") + "%";
                vt.fontSize = 16f;
                vt.color = BladeLoopTheme.Bone;
                vt.font = BladeLoopTheme.MonoBold;
                vt.alignment = TextAlignmentOptions.TopRight;
                vt.raycastTarget = false;
                vt.textWrappingMode = TextWrappingModes.NoWrap;
                vt.overflowMode = TextOverflowModes.Overflow;
            }

            y += RowH * 3f + 10f;

            // The tick is named in the section header above, on its own line, so it
            // costs nothing here. An unexplained mark would just be noise.

            // Quality sits with the bars: it describes the same product they do.
            //
            // TWO ROWS. It used to be one line carrying both measures and both design
            // values, which fitted at the old type size and collided with its own
            // label at this one - "Purity" and "93.0% / 93.0" landed on top of each
            // other. The design value stays alongside each figure: 82.5% sounds poor
            // until you can see that best-in-class recovery is 93%, not 100%.
            y += 6f;
            QualityRow("Purity", m.FiberPurityPct, refM.FiberPurityPct, ref y);
            QualityRow("Tensile", m.TensileRetentionPct, refM.TensileRetentionPct, ref y);
            y += 8f;
        }
        else
        {
            // ---- what this buyer will actually accept -------------------------
            //
            // The grade thresholds were in the app from the start and the viewer never
            // saw one until the run report - so for four minutes "HIGH GRADE" was a
            // label with no definition behind it, and "is this going well" had no
            // answer. These are the spec, not the result: stating them spoils nothing,
            // and they are what make the purity figure at Separation mean something
            // when it finally arrives.
            //
            // ONE LINE, AND NOT ON EVERY CARD. Measured: as a header plus two rows it
            // cost 90 units and pushed three of the four cards into scrolling, which
            // is how a useful addition turns into a worse panel. By the kiln it has
            // been on screen for two stages and the kiln card is the one with three
            // settings to show, so that is where it stops.
            if (card.showSpec)
            {
                y += 12f;
                y = Divider(y) + 24f;
                SectionHeader("WHAT THIS BUYER NEEDS", ref y);

                Grade want = order.targetGrade;
                Text("Spec",
                     want == Grade.Low
                       ? "Takes whatever the plant produces."
                       : "Purity at least " + (want == Grade.High ? OrderContext.HighPurity : OrderContext.MidPurity).ToString("0")
                         + "%   ·   strength at least " + (want == Grade.High ? OrderContext.HighTensile : OrderContext.MidTensile).ToString("0") + "%",
                     17f, BladeLoopTheme.Bone, BladeLoopTheme.Sans, ref y);
            }

            y += 10f;
            y = Divider(y) + 24f;
            SectionHeader(card.blockHdr, ref y);
        }

        // ---- the one line that says what this stage is deciding -----------------
        var body = Text("BlockBody", card.blockBody, 17.5f, BladeLoopTheme.Bone,
                        BladeLoopTheme.Sans, ref y, advance: false, wrap: true,
                        height: 240f);
        body.lineSpacing = 8f;

        // The block body is placed with advance:false and a fixed 240 px rect, so it
        // does NOT move the cursor. Nothing needed that before, because the only
        // thing after it was pinned to the panel foot. The scroll height does need
        // it, or the content stops short and the body cannot be scrolled to.
        y += MeasuredHeight(body) + 18f;

        // ---- size the scroll content -----------------------------------------
        // The measured stack, not the window. Shorter than the viewport and the
        // ScrollRect simply has nothing to scroll, so every card that already fit
        // behaves exactly as it did before.
        float total = y + 30f;
        if (content != null)
            content.sizeDelta = new Vector2(0f, total);

        // A card taller than the window has always been able to scroll; nothing ever
        // said so. With Separation now the one card that overruns, a silent scroll is
        // a card that simply looks truncated. One soft band along the foot of the
        // viewport reads as "there is more under here" without adding a scrollbar to
        // a panel that has no other chrome.
        ShowScrollCue(viewport != null && total > viewport.rect.height + 2f);
        return total;
    }

    RectTransform scrollCue;

    void ShowScrollCue(bool show)
    {
        if (!show)
        {
            if (scrollCue != null) scrollCue.gameObject.SetActive(false);
            return;
        }

        if (scrollCue == null)
        {
            // A sibling of the viewport, not part of the scrolling content - it marks
            // the window's edge, so it must not travel with what it is masking.
            scrollCue = MakeRect(viewport.parent, "ScrollCue");
            scrollCue.anchorMin = new Vector2(0f, 0f);
            scrollCue.anchorMax = new Vector2(1f, 0f);
            scrollCue.pivot     = new Vector2(0.5f, 0f);
            scrollCue.sizeDelta = new Vector2(0f, 1f);

            var img = scrollCue.gameObject.AddComponent<Image>();
            img.color = BladeLoopTheme.Rule;
            img.raycastTarget = false;

            var lbl = MakeRect(scrollCue, "Cue");
            lbl.anchorMin = new Vector2(0f, 0f);
            lbl.anchorMax = new Vector2(1f, 0f);
            lbl.pivot     = new Vector2(0.5f, 1f);
            lbl.offsetMin = new Vector2(Pad, 0f);
            lbl.offsetMax = new Vector2(-Pad, 0f);
            lbl.anchoredPosition = new Vector2(0f, -4f);
            lbl.sizeDelta = new Vector2(lbl.sizeDelta.x, 20f);

            var t = lbl.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = "SCROLL FOR MORE  ↓";
            t.fontSize = 12f;
            t.characterSpacing = 3f;
            t.color = BladeLoopTheme.Faint;
            t.font = BladeLoopTheme.SansBold;
            t.alignment = TextAlignmentOptions.TopRight;
            t.raycastTarget = false;
        }

        // Sits on the viewport's bottom edge, which the hint box already insets.
        scrollCue.anchoredPosition = new Vector2(0f, viewport.offsetMin.y);
        scrollCue.gameObject.SetActive(true);
        scrollCue.SetAsLastSibling();
    }

    /// <summary>The pause-and-look-around hint, pinned to the foot of the panel.
    ///
    /// Deliberately a sibling of the viewport, NOT part of the scrolling content.
    /// It is a persistent control hint, not part of any stage's argument: it should
    /// sit in the same place on every stage, stay put while the statistics scroll
    /// past it, and never be something the reader has to scroll down to discover.
    /// The viewport's bottom is inset by exactly its height, so content scrolls to
    /// the hint and stops rather than sliding underneath it.</summary>
    void BuildExploreHint(bool show)
    {
        const float HintH   = 128f;
        const float HintPad = 24f;

        // Rebuilt per stage because the copy varies, so clear the previous one.
        if (hintBox != null)
        {
            hintBox.gameObject.SetActive(false);
            Destroy(hintBox.gameObject);
            hintBox = null;
        }

        // Give the scrolling area its bottom back when there is no hint.
        if (viewport != null)
            viewport.offsetMin = new Vector2(0f, show ? HintH + HintPad : 0f);

        if (!show) return;

        // The message now lives here and nowhere else. The in-scene chip says the
        // same thing in the opposite corner, and two instructions for one action is
        // worse than one - so while the panel is up, the chip stands down. Found
        // live rather than wired, because the stage scenes cannot be edited; and
        // only reached when a panel exists, so free play keeps its chip untouched.
        var chip = Object.FindFirstObjectByType<ExploreHintChip>(FindObjectsInactive.Include);
        if (chip != null && chip.gameObject.activeSelf) chip.gameObject.SetActive(false);

        var box = MakeRect(root, "ExploreHint");
        hintBox = box;
        box.anchorMin = new Vector2(0f, 0f);
        box.anchorMax = new Vector2(1f, 0f);
        box.pivot     = new Vector2(0.5f, 0f);
        box.offsetMin = new Vector2(Pad, 0f);
        box.offsetMax = new Vector2(-Pad, 0f);
        box.anchoredPosition = new Vector2(box.anchoredPosition.x, HintPad);
        box.sizeDelta = new Vector2(box.sizeDelta.x, HintH);

        var rule = MakeRect(box, "HintRule");
        rule.anchorMin = new Vector2(0f, 1f);
        rule.anchorMax = new Vector2(1f, 1f);
        rule.pivot     = new Vector2(0.5f, 1f);
        rule.offsetMin = new Vector2(0f, 0f);
        rule.offsetMax = new Vector2(0f, 0f);
        rule.sizeDelta = new Vector2(rule.sizeDelta.x, 1f);
        var ruleImg = rule.gameObject.AddComponent<Image>();
        ruleImg.color = BladeLoopTheme.Rule;
        ruleImg.raycastTarget = false;

        var hdr = HintText(box, "HintHdr", "ADJUST THE PLANT", 15f, BladeLoopTheme.Muted,
                           BladeLoopTheme.SansBold, 22f, 23f);
        hdr.characterSpacing = 6f;

        // SPACE, not Backspace: StoryModeController binds spaceKey and pKey.
        //
        // This used to promise drag-to-orbit and scroll-to-zoom. Both are gone - see
        // ExploreOrbitCamera - so the hint now names the thing pausing actually does
        // here: Shredding and the Kiln each own a set-point you can change on the
        // frozen frame. It is only built on those two stages.
        //
        // The clicking sentence is added ONLY where clicking actually works, and the
        // check is live, so it appears by itself wherever click targets exist.
        string hint = "Press <b>SPACE</b> to modify plant settings.";
        if (SceneHasWorkingClickTargets())
            hint += " Click any part of the machine to read what it does.";

        var body = HintText(box, "HintBody", hint,
                            16.5f, BladeLoopTheme.Faint, BladeLoopTheme.Sans, 56f, 76f);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.lineSpacing = 6f;
    }

    TMP_Text HintText(Transform parent, string name, string text, float size,
                      Color col, TMP_FontAsset font, float top, float height)
    {
        var rt = MakeRect(parent, name);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, 0f);
        rt.offsetMax = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -top);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);

        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = col;
        if (font != null) t.font = font;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.raycastTarget = false;
        t.richText = true;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    /// <summary>
    /// True only when this scene can genuinely answer a click: a raycaster to
    /// receive it, and at least one ClickablePart that is actually reachable.
    ///
    /// The reachability test is the point. Stage 3 carries 22 ClickableParts, but
    /// every one sits under the cutaway root, which is off unless the player has
    /// pressed "Show Interior" - so the honest count while paused is zero.
    /// Checking activeInHierarchy walks the parent chain, which is what makes the
    /// difference between 22 and 0 here.
    /// </summary>
    static bool SceneHasWorkingClickTargets()
    {
        if (FindAnyObjectByType<ExploreClickRaycaster>() == null) return false;

        var parts = FindObjectsByType<ClickablePart>(FindObjectsInactive.Include);
        foreach (var p in parts)
            if (p != null && p.gameObject.activeInHierarchy) return true;

        return false;
    }

    static float MeasuredHeight(TMP_Text t)
    {
        t.ForceMeshUpdate();
        return Mathf.Max(t.preferredHeight, 0f);
    }

    // ---- small builders ------------------------------------------------------

    /// <summary>A section heading, optionally with a small right-aligned note.
    ///
    /// The note rides the SAME line as the heading rather than taking one of its own.
    /// The Separation card is the tallest in the panel and was already within about
    /// 120 px of the bottom of a 1080-tall canvas before any of this was added, so a
    /// spare line here costs more than it looks - it pushes the explore hint box off
    /// the screen on exactly the stage the run has been building towards.</summary>
    void SectionHeader(string label, ref float y, string note = null)
    {
        var t = Text(label.Replace(" ", "  "), label, 15f, BladeLoopTheme.Muted,
                     BladeLoopTheme.SansBold, ref y, advance: false);
        t.characterSpacing = 6f;

        if (!string.IsNullOrEmpty(note))
        {
            var n = Text(label + "Note", note, 13f, BladeLoopTheme.Faint,
                         BladeLoopTheme.Mono, ref y, advance: false);
            n.alignment = TextAlignmentOptions.TopRight;
        }

        y += 15f * LineFactor + 12f;
    }

    /// <summary>How far a setting sits from the design case, as a signed figure.
    ///
    /// Returns "on spec" rather than "+0" at the design case itself, which is not
    /// cosmetic: the high-grade preset IS the design case exactly, so without this
    /// that run would show four rows of "+0" and read like a bug. "on spec" says
    /// the true thing - this order asked for the optimum and got it.
    ///
    /// Plain ASCII +/- deliberately. The TMP atlases are static, so a typographic
    /// minus would render as a missing-glyph box on some builds.</summary>
    static string SignedDelta(float diff, string fmt, string unit)
    {
        if (Mathf.Abs(diff) < 0.0005f) return "on spec";
        return (diff > 0f ? "+" : "-") + Mathf.Abs(diff).ToString(fmt) + " " + unit;
    }

    /// <summary>Adds a line of text at the cursor. When advance is true the cursor
    /// moves past it, which is what most single lines want.</summary>
    TMP_Text Text(string name, string text, float size, Color col, TMP_FontAsset font,
                  ref float y, bool advance = true, bool wrap = false, float height = 0f)
    {
        float h = height > 0f ? height : size * LineFactor;

        var rt = MakeRect(content, name);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(Pad, 0f);
        rt.offsetMax = new Vector2(-Pad, 0f);
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -y);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);

        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = col;
        if (font != null) t.font = font;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.raycastTarget = false;
        t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        // Overflow, never Ellipsis: TMP draws NOTHING when a line is marginally too
        // tall for its rect, which silently deleted the grade badge once already.
        t.overflowMode = TextOverflowModes.Overflow;

        if (advance) y += h + 6f;
        return t;
    }

    /// <summary>A measured quality figure beside the design case's, on one row.
    /// The reference rides the same line in a dimmer colour rather than taking a row
    /// of its own - the Separation card cannot afford four rows here.</summary>
    void QualityRow(string label, float actual, float design, ref float y)
    {
        Text("Q" + label, label, 17f, BladeLoopTheme.Muted,
             BladeLoopTheme.Sans, ref y, advance: false);

        var v = Text("Q" + label + "V",
                     actual.ToString("0.0") + "%<alpha=#77>  / " + design.ToString("0.0") + "<alpha=#FF>",
                     17f, BladeLoopTheme.Bone, BladeLoopTheme.MonoBold, ref y, advance: false);
        v.richText = true;
        v.alignment = TextAlignmentOptions.TopRight;

        y += 17f * LineFactor + 4f;
    }

    /// <summary>One threshold this run has to clear, as a labelled floor.
    ///
    /// Deliberately "at least 90%" rather than a bar. A bar invites the reader to
    /// compare it with something, and on stages 1-3 there is nothing to compare it
    /// with yet - the plant has not run. This is a requirement, and it reads as one.</summary>
    void SpecRow(string label, float pct, ref float y)
    {
        Text("Spec" + label, label, 17f, BladeLoopTheme.Muted,
             BladeLoopTheme.Sans, ref y, advance: false);

        var v = Text("Spec" + label + "V", "at least " + pct.ToString("0") + "%", 17f,
                     BladeLoopTheme.Bone, BladeLoopTheme.MonoBold, ref y, advance: false);
        v.alignment = TextAlignmentOptions.TopRight;

        y += 17f * LineFactor + 6f;
    }

    float Divider(float top)
    {
        var rt = MakeRect(content, "Divider");
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(Pad, 0f);
        rt.offsetMax = new Vector2(-Pad, 0f);
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -top);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, 1f);

        var img = rt.gameObject.AddComponent<Image>();
        img.color = BladeLoopTheme.Rule;
        img.raycastTarget = false;
        return top + 1f;
    }

    static RectTransform MakeRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }
}
