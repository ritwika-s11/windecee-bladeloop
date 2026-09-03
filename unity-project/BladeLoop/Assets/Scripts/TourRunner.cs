using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Starts and steers a tour run for the active order.
///
/// ===========================================================================
///  STUB - written by Ritwika 1 Sep so the home page had something to call.
///  OWNER: Akshat. Fill in the TODOs; the signatures are fixed by
///  docs/interface-contract.md section 4 and the home page already calls them.
/// ===========================================================================
///
/// What works today:
///   StartRun()   - loads FullPlantTour, which holds the TourSceneSequencer and
///                  chains the five stage scenes. The order is already in
///                  OrderContext, so Anirban's TourViewportFrame components see
///                  HasOrder == true and confine every overlay to the left
///                  portion of the window.
///   SplitVFov()  - implemented, including the 65 degree clamp Anirban asked for.
///
/// What is NOT done, and is the real work:
///   - Camera.rect: the 3D view is still FULL WIDTH. Until that lands, a run
///     looks wrong - overlays squeezed left, 3D behind them across the whole
///     window. That is expected at this stage, not a bug.
///   - The persistent order panel in the right portion.
///   - SkipToResults() and JumpToChapter() - signatures only.
/// </summary>
public static class TourRunner
{
    // Exact scene names. Every one must be ticked in Build Settings.
    public const string TourSceneName    = "FullPlantTour";
    public const string MenuSceneName    = "MainMenu";
    public const string OutcomeSceneName = "OutcomeReport";   // Sharan is building this

    /// <summary>Above this the extra vertical view exposes scenery built to sit just
    /// outside frame, and wide shots start to show perspective stretch at the corners.
    /// Anirban hand-fixes anything that clamps. See his brief, task 6.</summary>
    public const float MaxCompensatedVFov = 65f;

    /// <summary>Begins the four-stage run for OrderContext.Active.
    /// No-ops when there is no order, per Rule 6.</summary>
    public static void StartRun()
    {
        if (!OrderContext.HasOrder)
        {
            Debug.LogWarning("TourRunner.StartRun called with no active order - ignoring. " +
                             "Call OrderContext.ApplyPreset(i) or SetOrder(...) first.");
            return;
        }

        // Creates itself, marks itself DontDestroyOnLoad and hooks
        // SceneManager.sceneLoaded, so the split and the panel follow the chain
        // through all five scenes without a single scene edit. See OrderPanel.
        //
        // Created BEFORE the load so it is already listening when FullPlantTour
        // raises sceneLoaded; it deliberately does not split the menu camera on
        // the way out.
        OrderPanel.Create();

        SceneManager.LoadScene(TourSceneName);
    }

    /// <summary>Ends the run immediately and shows the outcome report.</summary>
    public static void SkipToResults()
    {
        // The stage may be paused by Explore mode; never load frozen and silent.
        Time.timeScale = 1f;
        AudioListener.pause = false;

        OrderPanel.Teardown();
        StopSequencer();

        var cam = Camera.main;
        if (cam != null) cam.rect = new Rect(0f, 0f, 1f, 1f);

        // DELIBERATELY NO OrderContext.Clear() HERE. Contract section 5:
        // OutcomeReportController.Start() reads Active and Model. Clearing on the
        // way in would render an empty report, and it would look like Sharan's bug
        // rather than this line. Clear happens in ReturnToMenu.

        // Sharan's scene does not exist yet. Loading a scene that is not in Build
        // Settings throws and leaves the player on a dead stage with no sequencer,
        // so fail loudly and stay put instead.
        if (!Application.CanStreamedLevelBeLoaded(OutcomeSceneName))
        {
            Debug.LogWarning($"[TourRunner] Scene '{OutcomeSceneName}' is not in Build Settings yet " +
                             "(Sharan is building it). The run has stopped and the viewport is " +
                             "restored, but there is nothing to show. Returning to the menu instead.");
            ReturnToMenu();
            return;
        }

        SceneManager.LoadScene(OutcomeSceneName);
    }

    /// <summary>Stops and destroys the chained tour, with its fade canvas.
    ///
    /// Left alive, TourSceneSequencer keeps loading the next stage on top of
    /// whatever replaced it. This mirrors BackToMenuButton.GoToMainMenu, which
    /// solved the same problem first - if that logic changes, change both.</summary>
    static void StopSequencer()
    {
        var seq = Object.FindAnyObjectByType<TourSceneSequencer>();
        if (seq == null) return;

        seq.StopAllCoroutines();

        if (seq.fadeCanvas != null)
        {
            var fadeRoot = seq.fadeCanvas.transform.root.gameObject;
            if (fadeRoot != seq.transform.root.gameObject) Object.Destroy(fadeRoot);
        }

        Object.Destroy(seq.transform.root.gameObject);
    }

    /// <summary>Jumps to a stage and continues the chain from there.
    /// 0 = Farm, 1 = Shred, 2 = Kiln, 3 = Separate.
    /// Transport_StoryMode is a pass-through and has no chapter index.</summary>
    public static void JumpToChapter(int index)
    {
        // TODO (Akshat): TourSceneSequencer already loads scenes by name from
        // sceneSequence. This is mostly restructuring its coroutine to start from an
        // index rather than always from 0.
        Debug.LogWarning($"TourRunner.JumpToChapter({index}) is not implemented yet.");
    }

    /// <summary>Returns to the menu and clears the run.
    /// Safe to call at any point, including when no run is active.</summary>
    public static void ReturnToMenu()
    {
        // Never leave the game frozen - StoryModeController's pause sets all three.
        Time.timeScale = 1f;
        AudioListener.pause = false;

        // Restores the rect on the camera the panel actually split, then removes
        // itself. Skipping this is the first bug this system would have: the menu
        // renders inside 72% of the window with a black band down the side.
        OrderPanel.Teardown();
        StopSequencer();

        // Belt and braces - Camera.main here may not be the camera the panel held.
        var cam = Camera.main;
        if (cam != null) cam.rect = new Rect(0f, 0f, 1f, 1f);

        // Clear() records the last-run summary for the home page BEFORE discarding
        // the order, so this must happen on the way to the MENU and nowhere else.
        // Notably NOT in SkipToResults: the outcome report reads Active and Model
        // in Start(), and clearing first renders it empty.
        OrderContext.Clear();
        SceneManager.LoadScene(MenuSceneName);
    }

    /// <summary>Vertical FOV to use while the split is active.
    ///
    /// Unity holds vertical FOV fixed and derives horizontal from aspect, so narrowing
    /// the viewport to 72% silently cuts 28% of horizontal field from every shot - all
    /// 37 of them, not just badly framed ones. Widening vertical FOV by 1/0.72 restores
    /// the original horizontal framing.
    ///
    /// Clamped, because the compensation is not free: it also adds 27-35% more vertical
    /// view. Stage 4's widest lens (66 deg) would otherwise land at 84 deg, which shows
    /// perspective stretch at the corners and exposes the hill ring and ground plane.
    /// The clamp bites on any shot originally above about 49 deg.</summary>
    public static float SplitVFov(float originalVFov)
    {
        float halfRad = originalVFov * 0.5f * Mathf.Deg2Rad;
        float compensated = 2f * Mathf.Atan(Mathf.Tan(halfRad) / OrderContext.TourSplitWidth) * Mathf.Rad2Deg;
        return Mathf.Min(compensated, MaxCompensatedVFov);
    }
}
