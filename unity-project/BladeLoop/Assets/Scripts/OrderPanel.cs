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
    RectTransform content;   // everything inside it, rebuilt per stage

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
        scaler.referenceResolution = new Vector2(1920f, 1080f);
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
                c.blockHdr  = "WHAT SIZE TO SHRED TO";
                c.blockBody = m.ParticleInfo() + "\n\n" +
                              "Particle size carries the heaviest weight in the model — more than " +
                              "temperature. This is where the biggest decision is made.";
                break;

            case "Stage3_StoryMode":
                c.title = "ROTARY KILN";  c.chapter = "STAGE 3 OF 4";
                c.shown[3] = true;                       // carried in from shredding
                c.shown[0] = c.shown[1] = true;          // temperature and retention
                c.lit[0]   = c.lit[1]   = true;
                c.showExplore = true;
                c.blockHdr  = "HOW HOT, HOW LONG";
                c.blockBody = m.TempInfo() + "\n\n" + m.RetentionInfo();
                break;

            case "Stage4_V2":
                c.title = "SEPARATION";  c.chapter = "STAGE 4 OF 4";
                c.shown[0] = c.shown[1] = c.shown[2] = c.shown[3] = true;
                c.lit[2]   = true;                       // feed rate sets the kg/h
                c.showOutput  = true;                    // only now is there a result
                c.showExplore = true;
                // At Separation the output section IS the payoff, so it carries the
                // block's heading and body instead of the panel running two headers
                // that say the same thing. Purity and tensile are rendered with the
                // bars, where they belong - they describe the same product.
                c.blockHdr  = "WHAT YOU ACTUALLY GOT";
                c.blockBody = OrderContext.EndUseFor(OrderContext.AchievedGrade);
                break;

            case "Transport_StoryMode":
                // A pass-through: it plays in the chain but is not a chapter of its
                // own (interface contract, section 4). Nothing is decided here, so
                // the settings block stays empty and the copy says why.
                c.title = "IN TRANSIT";  c.chapter = "";
                c.blockHdr  = "ON THE ROAD";
                c.blockBody = $"{OrderContext.BladesNeeded:N0} blades, cut down on site and trucked to " +
                              "the plant.\n\nNothing is decided on the road. The first real choice comes " +
                              "at the shredder.";
                break;

            default:
                // Stage 1, and FullPlantTour for the frame before it hands over.
                // The wind farm decides quantity, not quality, so it shows the
                // campaign figures and no settings at all.
                c.title = "WIND FARM";  c.chapter = "STAGE 1 OF 4";
                c.blockHdr  = "THIS ORDER NEEDS";
                c.blockBody = $"{OrderContext.FeedTonnesNeeded:N0} t of blade material\n" +
                              $"{OrderContext.BladesNeeded:N0} blades   ·   {OrderContext.TurbinesNeeded:N0} turbines\n\n" +
                              $"Running without stopping, the plant takes " +
                              $"{OrderContext.CampaignDays:0.0} days to fill this order.";
                break;
        }

        return c;
    }

    /// <summary>Rebuilds the panel for the current scene and settings.</summary>
    public void Refresh()
    {
        if (root == null || !OrderContext.HasOrder) return;

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

        content = MakeRect(root, "Content");
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        float y = 34f;

        // ---- who this run is for ---------------------------------------------
        Text("Grade", OrderContext.GradeLabel(order.targetGrade), 30f,
             BladeLoopTheme.Oxide, BladeLoopTheme.MonoBold, ref y);

        Text("Buyer", string.IsNullOrEmpty(order.customerName) ? order.customerType
                                                               : order.customerName,
             18f, BladeLoopTheme.Muted, BladeLoopTheme.Sans, ref y);

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

                y += 21f * LineFactor + 6f;
            }
        }

        // ---- the output split, at Separation only -----------------------------
        // Showing it earlier was a spoiler AND a lie: the plant has not run yet,
        // so there is nothing that "came out".
        if (card.showOutput)
        {
            y += 10f;
            y = Divider(y) + 24f;
            SectionHeader(card.blockHdr, ref y);

            string[] streams = { "Fibre", "Oil", "Syngas", "Char", "Loss" };
            var cols = BladeLoopTheme.StreamColours;
            var sp = m.OutputSplit();
            float[] pcts = { sp.GlassPct, sp.OilPct, sp.SyngasPct, sp.CharPct, sp.LossPct };

            for (int i = 0; i < streams.Length; i++)
            {
                Text(streams[i] + "Lbl", streams[i], 18f, BladeLoopTheme.Bone,
                     BladeLoopTheme.Sans, ref y, advance: false);

                var track = MakeRect(content, streams[i] + "Track");
                track.anchorMin = new Vector2(0f, 1f);
                track.anchorMax = new Vector2(1f, 1f);
                track.pivot     = new Vector2(0.5f, 1f);
                track.offsetMin = new Vector2(Pad + LabelW, 0f);
                track.offsetMax = new Vector2(-(Pad + PctW), 0f);
                track.anchoredPosition = new Vector2(track.anchoredPosition.x, -(y + 9f));
                track.sizeDelta = new Vector2(track.sizeDelta.x, 9f);

                var trackImg = track.gameObject.AddComponent<Image>();
                trackImg.color = BladeLoopTheme.RuleSoft;
                trackImg.raycastTarget = false;

                // Fill width comes from anchorMax.x, so it is resolution independent.
                var fill = MakeRect(track, streams[i] + "Fill");
                fill.anchorMin = Vector2.zero;
                // Scaled against 72% rather than 100%, or four of the five bars are
                // slivers: fibre is ~69% and loss ~1.5%. Same divisor for every bar,
                // so the relative lengths stay honest.
                fill.anchorMax = new Vector2(Mathf.Clamp01(pcts[i] / 72f), 1f);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;

                var fillImg = fill.gameObject.AddComponent<Image>();
                fillImg.color = cols[i];
                fillImg.raycastTarget = false;

                var pct = Text(streams[i] + "Pct", pcts[i].ToString("0.0") + "%", 18f,
                               BladeLoopTheme.Bone, BladeLoopTheme.Mono, ref y, advance: false);
                pct.alignment = TextAlignmentOptions.TopRight;

                y += 18f * LineFactor + 8f;
            }

            // Quality sits with the bars: it describes the same product they do.
            y += 4f;
            Text("QualityLbl", "Purity", 17f, BladeLoopTheme.Muted,
                 BladeLoopTheme.Sans, ref y, advance: false);
            var q = Text("QualityVal",
                         $"{m.FiberPurityPct:0.0}%   ·   tensile {m.TensileRetentionPct:0.0}%",
                         17f, BladeLoopTheme.Bone, BladeLoopTheme.MonoBold,
                         ref y, advance: false);
            q.alignment = TextAlignmentOptions.TopRight;
            y += 17f * LineFactor + 14f;
        }
        else
        {
            // Stages that have no output section still need the block's own heading.
            y += 12f;
            y = Divider(y) + 24f;
            SectionHeader(card.blockHdr, ref y);
        }

        // ---- the one line that says what this stage is deciding -----------------
        var body = Text("BlockBody", card.blockBody, 17.5f, BladeLoopTheme.Bone,
                        BladeLoopTheme.Sans, ref y, advance: false, wrap: true,
                        height: 240f);
        body.lineSpacing = 8f;

        // ---- how to look around ----------------------------------------------
        // Anchored to the foot of the panel rather than flowing after the block:
        // it is a persistent control hint, not part of the stage's argument, and
        // it should sit in the same place every stage so the eye can ignore it.
        if (card.showExplore) BuildExploreHint();
    }

    /// <summary>The pause-and-look-around hint, pinned to the bottom of the panel.</summary>
    void BuildExploreHint()
    {
        var box = MakeRect(content, "ExploreHint");
        box.anchorMin = new Vector2(0f, 0f);
        box.anchorMax = new Vector2(1f, 0f);
        box.pivot     = new Vector2(0.5f, 0f);
        box.offsetMin = new Vector2(Pad, 0f);
        box.offsetMax = new Vector2(-Pad, 0f);
        box.anchoredPosition = new Vector2(box.anchoredPosition.x, 30f);
        box.sizeDelta = new Vector2(box.sizeDelta.x, 128f);

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

        var hdr = HintText(box, "HintHdr", "LOOK AROUND", 15f, BladeLoopTheme.Muted,
                           BladeLoopTheme.SansBold, 22f, 23f);
        hdr.characterSpacing = 6f;

        // SPACE, not Backspace: StoryModeController binds spaceKey and pKey, and
        // the in-scene overlay already says "PRESS SPACE TO EXPLORE". Two different
        // instructions for the same action is worse than none.
        //
        // The clicking sentence is added ONLY where clicking actually works. The
        // professor's outstanding note is literally "it is not possible to click any
        // part" - printing that promise into a stage with no ClickableParts would
        // restate the complaint as a feature. Because the check is made live, the
        // sentence appears by itself the moment Anirban applies the Explore spec;
        // no edit here is needed to turn it on.
        string hint = "Press <b>SPACE</b> to pause, then drag to orbit and scroll to zoom.";
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

    void SectionHeader(string label, ref float y)
    {
        var t = Text(label.Replace(" ", "  "), label, 15f, BladeLoopTheme.Muted,
                     BladeLoopTheme.SansBold, ref y, advance: false);
        t.characterSpacing = 6f;
        y += 15f * LineFactor + 12f;
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
