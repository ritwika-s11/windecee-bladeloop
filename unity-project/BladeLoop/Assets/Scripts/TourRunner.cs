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

        // TODO (Akshat): create the DontDestroyOnLoad object that owns the order panel
        // and hooks SceneManager.sceneLoaded to apply Camera.rect. Suggested shape:
        //
        //   void OnSceneLoaded(Scene s, LoadSceneMode m)
        //   {
        //       var cam = Camera.main;
        //       if (cam != null && OrderContext.HasOrder)
        //           cam.rect = new Rect(0f, 0f, OrderContext.TourSplitWidth, 1f);
        //   }
        //
        // Camera.main can be null on the first frame after a load - defer a frame if
        // the split fails on stage 1 but works on later ones.
        //
        // Use OrderContext.TourSplitWidth, never a literal. Anirban's overlay frames
        // read the same number; if the two disagree the subtitles sit off the edge of
        // the 3D view and it is very hard to see why.

        SceneManager.LoadScene(TourSceneName);
    }

    /// <summary>Ends the run immediately and shows the outcome report.</summary>
    public static void SkipToResults()
    {
        // TODO (Akshat): stop the sequencer, tear down the panel, restore Camera.rect,
        // then load OutcomeSceneName. Contract section 5: the sequencer loads the scene,
        // Sharan's controller just reads OrderContext in Start().
        //
        // BackToMenuButton.cs already has teardown logic for the sequencer and its fade
        // canvas - read it before writing new cleanup, and do not spawn a second sequencer.
        Debug.LogWarning("TourRunner.SkipToResults is not implemented yet.");
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

        // TODO (Akshat): also restore Camera.rect to (0,0,1,1) and destroy the panel.
        // Skipping the rect reset is the first bug this system will have: the menu
        // renders inside 72% of the window with a black band down the side.
        var cam = Camera.main;
        if (cam != null) cam.rect = new Rect(0f, 0f, 1f, 1f);

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
