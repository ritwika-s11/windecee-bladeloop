using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// The tour's navigation controls: let the viewer move at their own pace.
///
/// SKIP INTRO    past the wind farm and the drive to the plant, straight to
///               Shredding - the first stage where something happens to the
///               material.
/// NEXT STAGE    cut to the next stage instead of watching this one out.
///
/// WHY THESE EXIST
/// A full run is about four minutes. Most people will not sit through four
/// minutes of anything to reach the part they came for, and the part they came
/// for is the plant, not the establishing shots. Without these the tour asks for
/// a commitment before it has earned one; with them the viewer can get to the
/// kiln in seconds and come back for the opening later if they want it.
///
/// They are also honest about the product's own claim. The whole argument is
/// that the SETTINGS change what the plant does - so the fastest possible path
/// from picking an order to seeing its consequences is the path that makes the
/// argument, and every second of intro is a second spent not making it.
///
/// WHY IT BUILDS ITSELF
/// Same reason OrderPanel does: the five stage scenes have a single owner and
/// cannot be git-merged, so anything that must appear in all of them arrives at
/// runtime rather than as a component added to each file. No scene is touched.
///
/// PLACEMENT
/// Skip Intro sits top-right, level with the Menu button on the left, because the
/// two are the same kind of control: leave what you are watching. Next Stage sits
/// bottom-right, out of the way of the shot but on the side the eye already goes
/// for the order panel.
///
/// Both anchor to the TOUR VIEWPORT, not the window. Once Camera.rect splits the
/// screen the 3D view is only the left OrderContext.TourSplitWidth of it, and a
/// button anchored to the window's right edge would sit on top of the ledger.
/// </summary>
public class TourControls : MonoBehaviour
{
    const string HostName   = "~TourControls";
    const string FirstPlant = "Stage2_StoryMode";   // where SKIP INTRO lands

    // Matched to Btn_BackToMenu so the three read as one set: same 30 px margin,
    // same 46 px height, same white plate at 0.93, same 21 px bold navy label.
    const float Margin = 30f;
    const float Height = 46f;
    const float SkipW  = 168f;
    const float NextW  = 182f;

    static readonly Color PlateColour = new Color(1f, 1f, 1f, 0.93f);
    static readonly Color LabelColour = new Color(0.118f, 0.161f, 0.231f, 1f);
    const float LabelSize = 21f;

    static TourControls instance;

    RectTransform frame;                 // the tour viewport, mirrored
    GameObject skipBtn, nextBtn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject(HostName);
        DontDestroyOnLoad(go);
        instance = go.AddComponent<TourControls>();
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        Build();
    }

    void Update()
    {
        var seq = TourSceneSequencer.Active;

        if (seq == null)
        {
            if (skipBtn.activeSelf) skipBtn.SetActive(false);
            if (nextBtn.activeSelf) nextBtn.SetActive(false);
            return;
        }

        // Stage 1 and Stage 2 ship without an EventSystem, and without one no uGUI
        // button responds - on exactly the two stages SKIP INTRO exists to escape.
        // Checked every frame because the chain moves between scenes that have one
        // (3, 4) and scenes that do not (1, 2). EventSystem.current is a static
        // field, so this costs nothing until it is actually null.
        if (UnityEngine.EventSystems.EventSystem.current == null) EnsureEventSystem();

        // Follow the viewport split. OrderPanel is what narrows Camera.rect, so its
        // presence is the honest signal - not a constant, which would be wrong on
        // whichever side of the split's arrival it was written for.
        float right = OrderPanel.Instance != null ? OrderContext.TourSplitWidth : 1f;
        if (frame != null && !Mathf.Approximately(frame.anchorMax.x, right))
        {
            frame.anchorMax = new Vector2(right, 1f);
            frame.offsetMin = Vector2.zero;
            frame.offsetMax = Vector2.zero;
        }

        int i    = seq.CurrentIndex;
        int last = seq.sceneSequence.Length - 1;
        int plant = System.Array.IndexOf(seq.sceneSequence, FirstPlant);

        // SKIP INTRO is meaningless once you have reached the plant, so it goes away
        // rather than sitting there doing nothing. NEXT STAGE goes away on the final
        // stage for the same reason - there is nothing after Separation, and a button
        // promising one more stage would be a lie. They sit in different corners, so
        // neither leaves a hole when it goes.
        bool showSkip = plant > 0 && i >= 0 && i < plant;
        bool showNext = i >= 0 && i < last;

        if (skipBtn.activeSelf != showSkip) skipBtn.SetActive(showSkip);
        if (nextBtn.activeSelf != showNext) nextBtn.SetActive(showNext);

        // New Input System only - the legacy Input class never fires in this project.
        // Right arrow is the only unbound navigation key: up, down, W, A, S, R, P,
        // Space and Escape are all already taken by explore mode and story controls.
        var kb = Keyboard.current;
        if (kb != null && showNext && kb.rightArrowKey.wasPressedThisFrame) NextStage();
    }

    // ---------------------------------------------------------------- actions --

    public static void SkipIntro()
    {
        var seq = TourSceneSequencer.Active;
        if (seq == null) return;

        // Found by name, not hardcoded to an index, so reordering sceneSequence
        // cannot silently send this to the wrong stage.
        int target = System.Array.IndexOf(seq.sceneSequence, FirstPlant);
        if (target < 0)
        {
            Debug.LogWarning($"[TourControls] '{FirstPlant}' is not in sceneSequence.");
            return;
        }
        seq.JumpToStage(target);
    }

    public static void NextStage()
    {
        var seq = TourSceneSequencer.Active;
        if (seq != null) seq.SkipCurrentStage();
    }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var es = new GameObject("EventSystem",
                                typeof(UnityEngine.EventSystems.EventSystem),
                                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        DontDestroyOnLoad(es);
    }

    // ------------------------------------------------------------------- ui ----

    void Build()
    {
        BladeLoopTheme.Init();   // safe to call repeatedly, by contract

        var canvasGo = new GameObject("TourControlsCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        // Below the order panel so it can never draw over the ledger, above the
        // stage overlays so scenery cannot swallow it.
        canvas.sortingOrder = 900;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        // One frame child anchored to the tour viewport, exactly the trick
        // TourViewportFrame uses on the scene canvases. Both buttons then keep their
        // own corner anchors and follow the split for free.
        frame = new GameObject("Frame", typeof(RectTransform)).GetComponent<RectTransform>();
        frame.SetParent(canvasGo.transform, false);
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.offsetMin = Vector2.zero;
        frame.offsetMax = Vector2.zero;

        // Top-right, level with the Menu button on the opposite side.
        skipBtn = MakeButton("Btn_SkipIntro", "Skip Intro", SkipW,
                             new Vector2(1f, 1f), new Vector2(-Margin, -Margin), SkipIntro);

        // Bottom-right.
        nextBtn = MakeButton("Btn_NextStage", "Next Stage  →", NextW,
                             new Vector2(1f, 0f), new Vector2(-Margin, Margin), NextStage);

        skipBtn.SetActive(false);
        nextBtn.SetActive(false);
    }

    /// <summary>A button built to match Btn_BackToMenu: white plate at 0.93, no
    /// sprite, 21 px bold navy label, no letterspacing. Deliberately not the ledger
    /// styling - these are chrome, they belong with Menu, and three controls that
    /// do the same kind of job should not look like three different products.</summary>
    GameObject MakeButton(string name, string label, float w,
                          Vector2 anchor, Vector2 offset, System.Action onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(frame, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot     = anchor;              // pivot on the same corner it hangs from
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(w, Height);

        var img = go.GetComponent<Image>();
        img.color = PlateColour;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var cols = btn.colors;
        cols.normalColor      = Color.white;
        cols.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        cols.pressedColor     = new Color(0.82f, 0.82f, 0.82f, 1f);
        cols.fadeDuration     = 0.08f;
        btn.colors = cols;
        btn.onClick.AddListener(() => onClick());

        var txtGo = new GameObject("Label", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var trt = (RectTransform)txtGo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        var t = txtGo.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.fontSize = LabelSize;
        t.fontStyle = FontStyles.Bold;
        t.characterSpacing = 0f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = LabelColour;
        t.raycastTarget = false;
        if (BladeLoopTheme.SansBold != null) t.font = BladeLoopTheme.SansBold;

        return go;
    }
}
