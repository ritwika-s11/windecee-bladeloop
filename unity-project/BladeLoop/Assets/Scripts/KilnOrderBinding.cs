using UnityEngine;

/// <summary>
/// Task 3: makes the kiln show the order's temperature and retention.
///
/// Deliberately additive. TemperatureRampAnimator, KilnRotator and
/// AirlockFlowController all work today, and the brief's rule is that free play must be
/// untouched, so this reads their authored values on Start, applies the order on top,
/// and changes nothing at all when no order is running. No existing script is edited.
///
/// Three things move:
///
///   Temperature  - the ramp's tempEnd is hardcoded to 620 C. With an order it becomes
///                  OrderContext.Model.TempC, and the existing colour lerp and label
///                  follow for free.
///
///   Colour range - the authored cool/hot colours are tuned around 620 C, so 550 and 600
///                  land almost on top of each other. The gap is re-mapped so the useful
///                  band is 540-610 rather than 25-620: 600 C reads as bright even orange,
///                  580 C noticeably duller and redder, 550 C visibly under-fired.
///
///   Rotation     - longer retention means slower rotation, per the brief:
///                      rpm = authored * (35 / RetentionMin)
///                  Kept subtle; it is a supporting cue, not the headline.
/// </summary>
[DefaultExecutionOrder(60)]
public class KilnOrderBinding : MonoBehaviour
{
    [Header("Wiring (auto-found if left empty)")]
    public TemperatureRampAnimator ramp;
    public KilnRotator rotator;

    [Header("Readable temperature band")]
    [Tooltip("Temperature that should read as fully cold. The authored ramp starts at room " +
             "ambient, which wastes almost the whole colour range on temperatures the plant " +
             "never runs at - so 550 and 600 end up looking identical.")]
    public float visualFloorC = 540f;
    [Tooltip("Temperature that should read as fully hot.")]
    public float visualCeilingC = 610f;

    [Header("Ramp timing")]
    [Tooltip("Second by which the kiln should be at full temperature.\n\n" +
             "The authored ramp finishes at t=62, but the kiln beauty shots run 31.5-39s. " +
             "That means both a 550 C run and a 600 C run are still part-heated while the kiln " +
             "is actually on screen, and they look identical - the whole point of the task is " +
             "lost. Bringing the ramp home before the first kiln shot is what makes the " +
             "temperature readable.")]
    public float hotByTime = 28f;
    [Tooltip("How long the warm-up takes. The authored ramp is t=42 to t=62 - which is after " +
             "every kiln shot has already been and gone (31.5-39s), so the drum is cold in each " +
             "one. The whole ramp moves earlier, it does not just finish earlier.")]
    public float rampDurationSec = 16f;

    [Header("Retention")]
    [Tooltip("Retention the authored rpm corresponds to.")]
    public float referenceRetentionMin = 35f;
    [Tooltip("Clamp so the drum never stops or spins comically.")]
    public float minRpmFactor = 0.6f;
    public float maxRpmFactor = 1.6f;

    void Start()
    {
        if (ramp == null)    ramp    = FindAnyObjectByType<TemperatureRampAnimator>();
        if (rotator == null) rotator = FindAnyObjectByType<KilnRotator>();

        // No order: leave every authored value exactly as it is.
        if (!OrderContext.HasOrder) return;

        var m = OrderContext.Model;
        if (m == null) return;

        if (ramp != null)
        {
            ramp.tempEnd = m.TempC;

            // Move the whole warm-up earlier so the drum is at temperature before the
            // kiln shots rather than after them.
            if (hotByTime > 2f)
            {
                ramp.rampEndTime   = hotByTime;
                ramp.rampStartTime = Mathf.Max(1f, hotByTime - Mathf.Max(rampDurationSec, 2f));
            }

            // Re-map the visual range onto the band the plant actually runs in. Without
            // this the difference between a 550 C run and a 600 C run is about 8% of the
            // colour ramp - technically correct and completely invisible.
            ramp.tempStart = visualFloorC;

            // Push the hot end further for hotter runs so the three presets separate.
            float hot = Mathf.InverseLerp(visualFloorC, visualCeilingC, m.TempC);
            ramp.hotIntensity  = Mathf.Lerp(2.2f, 7.5f, hot);
            ramp.coolIntensity = Mathf.Lerp(0.15f, 0.5f, hot);
            ramp.hotColor  = Color.Lerp(new Color(0.85f, 0.22f, 0.06f),   // dull red, under-fired
                                        new Color(1.00f, 0.62f, 0.20f),  // bright even orange
                                        hot);

            // The authored 0.15 keeps the shell almost cold-looking whatever the intensity,
            // which is why 550 and 600 were indistinguishable. Push it so the drum itself
            // carries the temperature, and scale it with the run so a cool run stays dull.
            ramp.shellHeatStrength = Mathf.Lerp(0.30f, 0.75f, hot);
        }

        if (rotator != null)
        {
            float ret = Mathf.Max(m.RetentionMin, 1f);
            float f = Mathf.Clamp(referenceRetentionMin / ret, minRpmFactor, maxRpmFactor);
            rotator.rpm *= f;
        }
    }
}
