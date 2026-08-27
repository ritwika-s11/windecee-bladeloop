using UnityEngine;

/// <summary>
/// Shows a blade segment hanging from the crane hook while it is being carried.
///
/// CraneAnimator never moves a blade - it hides one from the ground pile and
/// reveals one on the truck bed, so the crane appeared to lift nothing. This
/// mirrors that same cycle clock and reveals a payload slung under the hook for
/// the carry phase only, closing the visual gap without touching CraneAnimator.
///
/// Phase map (matches CraneAnimator exactly):
///   0.000 - 0.125  hook descends
///   0.125 - 0.200  grab            <- payload appears
///   0.200 - 0.700  rise, slew, lower   payload carried
///   0.700 - 0.750  release         <- payload hides, bed blade appears
/// </summary>
[DefaultExecutionOrder(60)]
public class CraneCarriedLoad : MonoBehaviour
{
    [Tooltip("Leave empty to find the CraneAnimator on this object or its parents.")]
    public CraneAnimator crane;

    [Tooltip("The object slung under the hook. Hidden except during the carry phase.")]
    public Transform payload;

    [Tooltip("Sway amplitude in degrees as the load swings under the hook.")]
    public float swayDegrees = 3.5f;

    [Tooltip("Sway speed.")]
    public float swayRate = 1.6f;

    const float GRAB_START    = 0.125f;
    const float RELEASE_START = 0.700f;

    float t0;
    int carriedCount;

    void Start()
    {
        if (crane == null) crane = GetComponentInParent<CraneAnimator>();
        t0 = Time.time;                       // same frame as CraneAnimator.Start
        if (payload != null) payload.gameObject.SetActive(false);
    }

    void Update()
    {
        if (crane == null || payload == null) return;

        int maxLoads = Mathf.Min(crane.pileSegments.Count, crane.bedSegments.Count);

        float t = Time.time - t0 - crane.startDelay;
        if (t < 0f) { Hide(); return; }

        int cycleN = Mathf.FloorToInt(t / crane.cycleDuration);
        if (cycleN >= maxLoads) { Hide(); return; }      // everything already loaded

        float cyclePos = (t / crane.cycleDuration) % 1f;
        bool carrying = cyclePos >= GRAB_START && cyclePos < RELEASE_START;

        if (!carrying) { Hide(); return; }

        if (!payload.gameObject.activeSelf)
        {
            payload.gameObject.SetActive(true);
            carriedCount = cycleN;
        }

        // gentle pendulum so the load reads as hanging, not welded on
        float sway = Mathf.Sin((Time.time - t0) * swayRate) * swayDegrees;
        payload.localRotation = Quaternion.Euler(0f, 90f, 90f + sway);
    }

    void Hide()
    {
        if (payload != null && payload.gameObject.activeSelf)
            payload.gameObject.SetActive(false);
    }
}
