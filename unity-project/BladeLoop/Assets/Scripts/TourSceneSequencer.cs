using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using System.Collections;

/// <summary>
/// Full-plant story mode: plays the stage scenes back-to-back as one continuous story.
/// Persists across scene loads (DontDestroyOnLoad) and advances when each scene's
/// PlayableDirector actually finishes (falls back to sceneDurations if none is found).
/// The stage scenes themselves are untouched - this object just chains them.
/// </summary>
public class TourSceneSequencer : MonoBehaviour
{
    public string[] sceneSequence = {
        "Stage1_StoryMode",
        "Transport_StoryMode",
        "Stage2_StoryMode",
        "Stage3_StoryMode",
        "Stage4_V2"
    };
    [Tooltip("Fallback per-scene duration, used ONLY when a scene has no " +
             "PlayableDirector. Today that is Transport alone (index 1) - every other " +
             "stage is timed by its own timeline and ignores the number here. Keep " +
             "them true anyway: a wrong value is silently inert until the day someone " +
             "removes a director, and then it is a mystery.")]
    public float[] sceneDurations = { 23.8f, 9.6f, 30.4f, 33.6f, 46.7f };
    public CanvasGroup fadeCanvas;
    public float fadeDuration = 0.5f;
    [Tooltip("Extra hold on the last frame of each stage before cutting.")]
    public float endHold = 1.0f;

    // ---------------------------------------------------------------- steering --
    //
    // The chain is one coroutine, so "skip" and "jump" are just requests it checks
    // while it is waiting out the current stage. Nothing here changes how an
    // uninterrupted run plays: with no request pending, every wait condition and
    // every hold is exactly what it was.

    /// <summary>The running sequencer, if a tour is in progress.</summary>
    public static TourSceneSequencer Active { get; private set; }

    /// <summary>Index into sceneSequence of the stage on screen right now.</summary>
    public int CurrentIndex { get; private set; } = -1;

    bool skipRequested;
    int  pendingJump = -1;

    /// <summary>Cut to the next stage without waiting out this one.</summary>
    public void SkipCurrentStage() { skipRequested = true; }

    /// <summary>Cut straight to a stage by its index in sceneSequence.</summary>
    public void JumpToStage(int index)
    {
        if (sceneSequence == null || sceneSequence.Length == 0) return;
        pendingJump   = Mathf.Clamp(index, 0, sceneSequence.Length - 1);
        skipRequested = true;   // stop waiting out the current stage as well
    }

    void OnEnable()  { Active = this; }
    void OnDisable() { if (Active == this) Active = null; }

    IEnumerator Start()
    {
        // A previous run's report retires the tour controls permanently. Starting a
        // new tour is the one event that should bring them back - without this, the
        // second tour of a session has no Skip or Next.
        TourControls.Unsuppress();

        DontDestroyOnLoad(gameObject);
        if (fadeCanvas != null) DontDestroyOnLoad(fadeCanvas.transform.root.gameObject);

        int i = 0;
        while (i < sceneSequence.Length)
        {
            CurrentIndex = i;

            // Cleared before the stage plays, so a request made DURING this stage
            // survives to the end of it. Clearing any later would swallow it.
            skipRequested = false;
            pendingJump   = -1;

            yield return StartCoroutine(Fade(0f, 1f));
            yield return SceneManager.LoadSceneAsync(sceneSequence[i], LoadSceneMode.Single);
            yield return null; // let Awake/Start run
            yield return StartCoroutine(Fade(1f, 0f));

            var director = FindFirstObjectByType<PlayableDirector>();
            if (director != null)
            {
                double dur = director.duration;
                // wait for the story to reach its end (pausing the story pauses the chain too)
                while (director != null && director.time < dur - 0.1 && !skipRequested)
                    yield return null;
            }
            else
            {
                // Hand-rolled rather than WaitForSeconds so it can be interrupted.
                // Time.deltaTime is scaled, exactly as WaitForSeconds was, so the
                // 8x fast-tour toggle still shortens this the same way.
                float dur = (i < sceneDurations.Length) ? sceneDurations[i] : 30f;
                float t = 0f;
                while (t < dur && !skipRequested)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
            }

            // No lingering on a frame the user has just asked to leave.
            if (!skipRequested) yield return new WaitForSeconds(endHold);

            if (pendingJump >= 0) { i = pendingJump; pendingJump = -1; }
            else                  { i++; }
        }

        // ---- the ending ------------------------------------------------------
        //
        // The tour used to fade out and cut to the menu here, which threw away the
        // one thing the whole run was building towards. Instead the stats panel
        // grows across the screen and becomes the run report, over the last frame
        // of Stage 4 - no fade, no scene load, so the plant is still there behind
        // it. OutcomeReportPanel owns the return to the menu from that point.
        //
        // The old path is kept for the case where there is nothing to report on.
        if (showReportAtEnd && OrderContext.HasOrder)
        {
            OutcomeReportPanel.Show();
            yield break;                     // this object stays alive; EndTour() clears it
        }

        yield return StartCoroutine(Fade(0f, 1f));
        SceneManager.LoadScene("MainMenu");
        EndTour();
    }

    [Tooltip("Finish on the run report instead of cutting back to the menu. Off " +
             "restores the original ending.")]
    public bool showReportAtEnd = true;

    /// <summary>Tears the tour down. Called by the report's Back to menu button,
    /// and by the fallback ending when there is no order to report on.
    ///
    /// Does NOT load a scene: the caller decides where to go, because the report
    /// needs to destroy this object BEFORE loading the menu and the fallback path
    /// needs the opposite order.</summary>
    public void EndTour()
    {
        var root = fadeCanvas != null ? fadeCanvas.transform.root.gameObject : null;
        if (Active == this) Active = null;
        if (root != null && root != gameObject) Destroy(root);
        Destroy(gameObject);
    }

    IEnumerator Fade(float from, float to)
    {
        if (fadeCanvas == null) yield break;
        float t = 0f;
        while (t < fadeDuration)
        {
            fadeCanvas.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            t += Time.deltaTime;
            yield return null;
        }
        fadeCanvas.alpha = to;
    }
}
