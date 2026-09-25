using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Take the controls while the run is paused.
///
/// WHAT IT IS
/// ----------
/// Press Space on a stage that owns a setpoint and this appears in the bottom-left of
/// the tour viewport. Drag the slider and the plant answers on the frozen frame - the
/// shredded heap re-forms, the kiln's glow and readout move - and the ledger on the
/// right recomputes purity, yield and fibre per hour live. Press APPLY to keep it, or
/// resume and it reverts.
///
/// WHY IT IS WORTH HAVING
/// ----------------------
/// The app's whole argument is that the settings change what the plant does. Until now
/// the user made that choice once on the Custom Order screen and then watched for four
/// minutes, learning what it meant only from a report at the end. This makes the
/// argument in one gesture, at the moment they are looking at the machine the setting
/// controls. It is the same claim the product already makes, shown instead of stated.
///
/// WHY PAUSE IS THE RIGHT MOMENT, NOT AN OBSTACLE
/// ----------------------------------------------
/// StoryModeController sets Time.timeScale = 0 while paused. That freezes the frame -
/// which is exactly what this needs. A pile that re-forms under a still camera is
/// unmissable; the same change during a moving shot would scroll past unnoticed, which
/// is the existing complaint about the dynamic visuals. Nothing in this file uses
/// Time.deltaTime, so a zero clock costs it nothing.
///
/// WHAT IT DOES NOT TOUCH
/// ----------------------
/// No scene is edited, no existing script is modified, nothing is added to any stage.
/// Like TourControls and OrderPanel it builds itself at runtime and follows the scene
/// chain. Subtitles, timelines, cameras, audio and every other stage behaviour are
/// untouched. With no order active it never appears at all, so free play and editor
/// playback are exactly as they were.
///
/// TWO CHANGES PER RUN
/// -------------------
/// See SetpointLog.MaxChanges. Briefly: a change you can undo for free is not a
/// decision, and an unlimited slider turns the plant into a toy.
/// </summary>
public class PauseSetpointPanel : MonoBehaviour
{
    const string HostName = "~PauseSetpointPanel";

    // Which stage owns which setting. Keyed by scene NAME, not by index into
    // TourSceneSequencer.sceneSequence, so reordering the chain cannot silently point
    // the shredder control at the kiln.
    const string ShredScene = "Stage2_StoryMode";
    const string KilnScene  = "Stage3_StoryMode";

    enum Knob { None, Particle, Temperature }

    static PauseSetpointPanel instance;

    // ---- layout ----
    const float Margin = 30f;
    const float PanelW = 470f;
    const float PanelH = 300f;

    RectTransform frame, panel;
    Slider slider;
    TMP_Text titleTxt, valueTxt, unitTxt, deltaTxt, budgetTxt, hintTxt;
    Image applyImg; Button applyBtn; TMP_Text applyTxt;

    StoryModeController controller;
    Knob knob = Knob.None;
    bool wasVisible;
    float valueAtPause;          // what to fall back to if they resume without applying
    bool  pendingPreview;        // the live value differs from the committed one

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject(HostName);
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PauseSetpointPanel>();
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        Build();
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        // Leaving the tour ends the run's history. Without this a second run would
        // inherit the first one's baseline and report changes that never happened.
        if (s.name == TourRunner.MenuSceneName)
        {
            SetpointLog.Clear();
            LiveSetpoints.ForgetScene();
            knob = Knob.None;
            Hide();
            return;
        }

        // The captured glow baseline belongs to the renderers of one loaded scene.
        LiveSetpoints.ForgetScene();
        SetpointLog.BeginRun();

        knob = s.name == ShredScene ? Knob.Particle
             : s.name == KilnScene  ? Knob.Temperature
                                    : Knob.None;

        controller = null;
        pendingPreview = false;
        Hide();
    }

    void Update()
    {
        // Re-found per stage: each scene carries its own controller, and the one from
        // the previous stage is destroyed with it.
        if (controller == null) controller = FindFirstObjectByType<StoryModeController>();

        bool show = knob != Knob.None
                 && OrderContext.HasOrder
                 && OrderContext.Model != null
                 && controller != null && controller.IsPaused
                 && !OutcomeShowing();

        if (show == wasVisible) return;
        wasVisible = show;

        if (show) { EnsureEventSystem(); Open(); }
        else      { RevertPreview(); Hide(); }
    }

    /// <summary>The run report takes the screen at the end of the tour; a plant control
    /// floating over it would be wired to a stage that is no longer running.</summary>
    static bool OutcomeShowing() => FindFirstObjectByType<OutcomeReportPanel>() != null;

    // ----------------------------------------------------------------- open ----

    void Open()
    {
        var m = OrderContext.Model;
        float v = knob == Knob.Particle ? m.ParticleSizeMm : m.TempC;
        valueAtPause = v;
        pendingPreview = false;

        bool particle = knob == Knob.Particle;
        titleTxt.text = particle ? "SHREDDER — PARTICLE SIZE" : "KILN — TEMPERATURE";
        unitTxt.text  = particle ? "mm" : "°C";
        hintTxt.text  = particle
            ? "Finer shred decomposes more completely, but the shredder feeds slower."
            : "Hotter runs decompose more of the resin, at more energy per tonne.";

        slider.onValueChanged.RemoveAllListeners();
        slider.minValue     = particle ? 1f : 400f;
        slider.maxValue     = particle ? 20f : 700f;
        slider.wholeNumbers = false;
        slider.SetValueWithoutNotify(v);
        slider.onValueChanged.AddListener(OnSlide);

        slider.interactable = true;
        SetApplyEnabled(false);
        PaintBudget();
        Paint(v);
        panel.gameObject.SetActive(true);
    }

    void Hide()
    {
        if (panel != null) panel.gameObject.SetActive(false);
        wasVisible = false;
    }

    // --------------------------------------------------------------- driving ----

    /// <summary>Snap to something a person would actually set. A shredder is specified
    /// in half-millimetres and a kiln in fives, not in whatever float the pixel under
    /// the handle happens to be.</summary>
    float Snap(float v) => knob == Knob.Particle
                        ? Mathf.Round(v * 2f) / 2f
                        : Mathf.Round(v / 5f) * 5f;

    void OnSlide(float raw)
    {
        float v = Snap(raw);
        if (!Mathf.Approximately(v, raw)) slider.SetValueWithoutNotify(v);

        // PREVIEW, not commit. The scene and the ledger move immediately - that is the
        // whole point - but nothing is written to the run's history until APPLY. Both
        // appliers write absolute values, so previewing back and forth cannot drift.
        ApplyValue(v);
        pendingPreview = !Mathf.Approximately(v, valueAtPause);
        SetApplyEnabled(pendingPreview);
        Paint(v);
    }

    void ApplyValue(float v)
    {
        if (knob == Knob.Particle) LiveSetpoints.ApplyParticle(v);
        else                       LiveSetpoints.ApplyTemperature(v);
        LiveSetpoints.NotifyChanged();
    }

    /// <summary>Resuming without pressing APPLY puts the plant back exactly as it was.
    /// A preview that silently stuck would make the change budget a lie.</summary>
    void RevertPreview()
    {
        if (!pendingPreview) return;
        ApplyValue(valueAtPause);
        pendingPreview = false;
    }

    void Commit()
    {
        if (!pendingPreview) return;

        float v = Snap(slider.value);
        bool particle = knob == Knob.Particle;

        SetpointLog.Record(particle ? "Particle size" : "Kiln temperature",
                           particle ? "Shredding" : "Kiln",
                           valueAtPause, v,
                           particle ? " mm" : " °C",
                           particle ? "0.#" : "0");

        valueAtPause   = v;      // the new fallback: this value is now the run's truth
        pendingPreview = false;

        SetApplyEnabled(false);
        PaintBudget();
        Paint(v);

        // APPLY means "do it and let the plant run". Leaving the viewer paused on a
        // panel with a spent budget and a dead button made them reach for Space to
        // finish an action they had already finished.
        //
        // LAST, after the log and the labels: resuming makes Update hide the panel on
        // the next frame, and RevertPreview runs on the way out. It is a no-op here
        // only because pendingPreview was cleared above - which is why the order
        // matters.
        if (controller != null && controller.IsPaused) controller.TogglePause();
    }

    // ---------------------------------------------------------------- readout ----

    /// <summary>What the change is worth, against the settings the run STARTED with -
    /// not against the last preview. A delta that moves its own goalposts tells the
    /// user nothing about the decision they are making.</summary>
    void Paint(float v)
    {
        valueTxt.text = knob == Knob.Particle ? v.ToString("0.#") : v.ToString("0");

        var entry = SetpointLog.Entry;
        var now   = OrderContext.Model;
        if (entry == null || now == null) { deltaTxt.text = ""; return; }

        float p0 = entry.FiberPurityPct,  p1 = now.FiberPurityPct;
        float f0 = entry.OutputSplit().GlassKgH, f1 = now.OutputSplit().GlassKgH;

        if (Mathf.Abs(p1 - p0) < 0.05f && Mathf.Abs(f1 - f0) < 1f)
        {
            deltaTxt.text  = "As the run started.";
            deltaTxt.color = BladeLoopTheme.Faint;
            return;
        }

        deltaTxt.text = $"{Signed(p1 - p0, "0.0")} pts purity   ·   "
                      + $"{Signed(f1 - f0, "N0")} kg/h fibre   vs the run's start";
        // Purity is the grade-bearing number, so it decides the colour. Fibre per hour
        // usually moves the other way - that trade is the point of the control.
        deltaTxt.color = p1 >= p0 ? BladeLoopTheme.StreamGas : BladeLoopTheme.Oxide;
    }

    static string Signed(float v, string fmt) => (v >= 0f ? "+" : "−") + Mathf.Abs(v).ToString(fmt);

    /// <summary>The top-right line. It used to count down a change budget; with the cap
    /// gone it carries the one fact the delta line below needs to be readable - what
    /// this setting was when the run began - plus how many times it has moved.</summary>
    void PaintBudget()
    {
        var entry = SetpointLog.Entry;
        if (entry == null) { budgetTxt.text = ""; return; }

        float start = knob == Knob.Particle ? entry.ParticleSizeMm : entry.TempC;
        string s = "RUN STARTED AT " + (knob == Knob.Particle
                 ? start.ToString("0.#") + " MM" : start.ToString("0") + " °C");

        if (SetpointLog.Used > 0)
            s += "  ·  " + SetpointLog.Used + (SetpointLog.Used == 1 ? " CHANGE" : " CHANGES");

        budgetTxt.text = s;
    }

    void SetApplyEnabled(bool on)
    {
        if (applyBtn == null) return;
        applyBtn.interactable = on;
        applyImg.color = on ? BladeLoopTheme.Oxide : new Color(1f, 1f, 1f, 0.10f);
        applyTxt.color = on ? BladeLoopTheme.Hex("15110E") : BladeLoopTheme.Faint;
    }

    // --------------------------------------------------------------------- ui ----

    static void EnsureEventSystem()
    {
        // Stage 1 and Stage 2 ship without an EventSystem, and Stage 2 is the shredder -
        // exactly where this panel's slider has to work. TourControls already does this,
        // but it returns early once the tour is suppressed, so this does not rely on it.
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var es = new GameObject("EventSystem",
                                typeof(UnityEngine.EventSystems.EventSystem),
                                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        DontDestroyOnLoad(es);
    }

    void Build()
    {
        BladeLoopTheme.Init();   // safe to call repeatedly, by contract

        var canvasGo = new GameObject("PauseSetpointCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above TourControls (900) so its Previous button cannot sit on the panel,
        // below the run report (1200) which owns the screen when it appears.
        canvas.sortingOrder = 950;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        frame = NewRect(canvasGo.transform, "Frame");
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.offsetMin = Vector2.zero;
        frame.offsetMax = Vector2.zero;

        // TOP-LEFT, tucked under the Menu button.
        //
        // The bottom-left was the obvious choice and it was wrong: the stage's own Back
        // control sits there, the PAUSED hint bar runs along the bottom centre, and the
        // panel landed on both. The top-left strip below Menu is the one region of the
        // viewport nothing else claims, and it reads correctly anyway - Menu and this
        // are the two things you can do to a paused run.
        panel = NewRect(frame, "Panel");
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot     = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(Margin, -(Margin + 46f + 18f));
        panel.sizeDelta = new Vector2(PanelW, PanelH);

        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(BladeLoopTheme.Panel.r, BladeLoopTheme.Panel.g,
                             BladeLoopTheme.Panel.b, 0.96f);

        var edge = Img(panel, "edge", BladeLoopTheme.Oxide);
        edge.rectTransform.anchorMin = Vector2.zero;
        edge.rectTransform.anchorMax = new Vector2(0f, 1f);
        edge.rectTransform.sizeDelta = new Vector2(3f, 0f);
        edge.rectTransform.pivot = new Vector2(0f, 0.5f);
        edge.rectTransform.anchoredPosition = Vector2.zero;

        titleTxt = Text(panel, "title", 15f, BladeLoopTheme.Muted, BladeLoopTheme.SansBold,
                        TextAlignmentOptions.Left);
        titleTxt.characterSpacing = 2f;
        Place(titleTxt.rectTransform, 22f, -18f, PanelW - 44f, 20f);

        valueTxt = Text(panel, "value", 52f, BladeLoopTheme.Bone, BladeLoopTheme.MonoBold,
                        TextAlignmentOptions.Left);
        Place(valueTxt.rectTransform, 22f, -44f, 200f, 56f);

        unitTxt = Text(panel, "unit", 20f, BladeLoopTheme.Muted, BladeLoopTheme.Mono,
                       TextAlignmentOptions.Left);
        Place(unitTxt.rectTransform, 150f, -60f, 70f, 26f);

        budgetTxt = Text(panel, "budget", 12f, BladeLoopTheme.Faint, BladeLoopTheme.SansBold,
                         TextAlignmentOptions.Right);
        budgetTxt.characterSpacing = 1.5f;
        // Right-aligned, so the box may start left of the title's rect without the two
        // ever touching: the text hugs the right edge and the title's is far shorter
        // than its own box. Widened from 200 for "RUN STARTED AT 600 °C · 4 CHANGES".
        Place(budgetTxt.rectTransform, PanelW - 292f, -18f, 270f, 18f);

        BuildSlider();

        deltaTxt = Text(panel, "delta", 14f, BladeLoopTheme.Faint, BladeLoopTheme.Sans,
                        TextAlignmentOptions.Left);
        Place(deltaTxt.rectTransform, 22f, -156f, PanelW - 44f, 20f);

        // Full width. APPLY used to share this line and drew straight through the last
        // few words of it; it now has its own row along the panel's bottom edge.
        hintTxt = Text(panel, "hint", 13f, BladeLoopTheme.Faint, BladeLoopTheme.Sans,
                       TextAlignmentOptions.TopLeft);
        hintTxt.textWrappingMode = TextWrappingModes.Normal;
        Place(hintTxt.rectTransform, 22f, -182f, PanelW - 44f, 44f);

        BuildApply();

        panel.gameObject.SetActive(false);
    }

    void BuildSlider()
    {
        var go = new GameObject("knob", typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(panel, false);
        Place((RectTransform)go.transform, 22f, -128f, PanelW - 44f, 18f);

        var track = Img((RectTransform)go.transform, "track", BladeLoopTheme.RuleSoft);
        Fill(track.rectTransform);
        // A Slider is driven by pointer events, so SOMETHING under the cursor has to be
        // raycastable or the click sails through to the scene behind. Img() switches
        // raycasts off by default - right for the decoration this panel is mostly made
        // of, wrong for the two graphics that are the control itself. This is the whole
        // reason the slider rendered correctly and would not move.
        //
        // The track carries click-anywhere-to-jump; the handle carries the drag.
        track.raycastTarget = true;

        var fillArea = NewRect((RectTransform)go.transform, "FA"); Fill(fillArea);
        var fill = Img(fillArea, "F", BladeLoopTheme.Oxide);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.sizeDelta = new Vector2(8f, 0f);

        var handleArea = NewRect((RectTransform)go.transform, "HA"); Fill(handleArea);
        // Invisible handleRect with the visual in a child: Slider rewrites BOTH anchor
        // pairs on the rect it drives every frame, so a sizeDelta set there becomes an
        // offset and the grip stretches into a slab.
        var h = Img(handleArea, "H", new Color(0f, 0f, 0f, 0f));
        h.rectTransform.sizeDelta = new Vector2(26f, 0f);
        // Invisible, but it still has to be hit-testable - a fully transparent Image is
        // a perfectly good raycast target, and this is what the user actually grabs.
        h.raycastTarget = true;
        var plate = Img(h.rectTransform, "plate", new Color(1f, 1f, 1f, 0.10f));
        Point(plate.rectTransform, 22f, 30f);
        var blade = Img(h.rectTransform, "blade", BladeLoopTheme.Bone);
        Point(blade.rectTransform, 3f, 30f);

        slider = go.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = h.rectTransform;
        slider.targetGraphic = h;
    }

    void BuildApply()
    {
        var go = new GameObject("apply", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(panel, false);
        // Bottom-right of the panel, on its own row under the hint.
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-22f, 18f);
        rt.sizeDelta = new Vector2(160f, 42f);

        applyImg = go.GetComponent<Image>();
        applyBtn = go.GetComponent<Button>();
        applyBtn.targetGraphic = applyImg;
        applyBtn.onClick.AddListener(Commit);

        applyTxt = Text(rt, "l", 14f, BladeLoopTheme.Hex("15110E"), BladeLoopTheme.SansBold,
                        TextAlignmentOptions.Center);
        applyTxt.characterSpacing = 2f;
        applyTxt.text = "APPLY";
        Fill(applyTxt.rectTransform);
    }

    // ------------------------------------------------------------- ui helpers --

    static RectTransform NewRect(Transform parent, string name)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    static Image Img(RectTransform parent, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var im = go.GetComponent<Image>();
        im.color = c;
        im.raycastTarget = false;
        return im;
    }

    static TMP_Text Text(RectTransform parent, string name, float size, Color c,
                         TMP_FontAsset font, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.color = c;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        if (font != null) t.font = font;
        return t;
    }

    /// <summary>Hang a rect from the panel's TOP-LEFT at an exact pixel offset and size.
    /// Fractional anchors would float the content to the middle of whatever box it
    /// landed in as soon as the panel's own size changed.</summary>
    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void Fill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Point(RectTransform rt, float w, float h)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
    }
}
