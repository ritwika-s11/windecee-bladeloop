using UnityEngine;

/// <summary>
/// Additive: a small persistent "PRESS SPACE TO EXPLORE" chip shown during a stage's
/// story playback, so viewers discover that Explore mode exists.
///
/// Behaviour:
///  - pulses up to full opacity once when the scene starts, then settles to a subtle idle alpha
///  - hides itself while the story is paused, because StoryModeController already shows its own
///    "PAUSED — drag to orbit..." hint at that point (avoids two hints on screen at once)
///  - runs on unscaled time, so it still animates when Explore mode sets Time.timeScale = 0
///
/// Purely presentational: it never writes to StoryModeController or the timeline.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ExploreHintChip : MonoBehaviour
{
    [Tooltip("Leave empty to auto-find the StoryModeController in this scene.")]
    public StoryModeController controller;

    [Header("Opacity")]
    public float idleAlpha  = 0.55f;   // steady, subtle
    public float pulseAlpha = 1.00f;   // peak of the intro pulse

    [Header("Timing (seconds, unscaled)")]
    public float startDelay   = 0.6f;  // let the scene fade in first
    public float fadeInTime   = 0.45f;
    public float holdTime     = 1.10f;
    public float settleTime   = 0.70f;
    public float hideFadeTime = 0.18f; // when the story gets paused

    CanvasGroup group;
    float t;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;          // never steals clicks from the scene
        if (controller == null) controller = FindFirstObjectByType<StoryModeController>();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // While paused, StoryModeController shows its own hint — stay out of the way.
        if (controller != null && controller.IsPaused)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, 0f, dt / Mathf.Max(hideFadeTime, 0.01f));
            return;
        }

        t += dt;

        float target;
        if (t < startDelay)                                   target = 0f;
        else if (t < startDelay + fadeInTime)                 target = Mathf.Lerp(0f, pulseAlpha, (t - startDelay) / fadeInTime);
        else if (t < startDelay + fadeInTime + holdTime)      target = pulseAlpha;
        else if (t < startDelay + fadeInTime + holdTime + settleTime)
            target = Mathf.Lerp(pulseAlpha, idleAlpha, (t - startDelay - fadeInTime - holdTime) / settleTime);
        else                                                  target = idleAlpha;

        group.alpha = Mathf.MoveTowards(group.alpha, target, dt / 0.2f);
    }
}
