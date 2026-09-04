using UnityEngine;

/// <summary>
/// Keeps a world-space label readable: constant size on screen, inside the frame, one
/// consistent colour, and out of the way when the camera is too close to need it.
///
/// The first version only clamped the label into a safe area. That fixed the original
/// bug - kiln zone headings sitting far outside the frame on the close-up shots - but
/// created a worse one. A world-space label grows as the camera approaches, and the zone
/// cameras sit 2.8 m from the kiln, so the headings filled the screen and overlapped
/// each other. Clamping three of them into the same safe area then stacked them on top
/// of one another.
///
/// So this now does three things instead of one:
///   - scales the label by distance, so it occupies the same fraction of the screen
///     whether the camera is 3 m away or 30
///   - fades it out below a minimum distance, because a heading is pointless when the
///     subject already fills the frame
///   - only pulls a label into the safe area when it would otherwise be clipped, and
///     never past the point where it would sit on top of another one
/// </summary>
[DefaultExecutionOrder(120)]
[RequireComponent(typeof(Renderer))]
public class ScreenSafeLabel : MonoBehaviour
{
    [Tooltip("Authored position. Captured on Awake if left at zero.")]
    public Vector3 anchor;

    [Header("Safe area (viewport 0-1)")]
    [Range(0f, 0.4f)] public float marginX = 0.06f;
    [Range(0f, 0.4f)] public float marginY = 0.10f;

    [Header("Constant screen size")]
    [Tooltip("Hold the label at the size it has when the camera is this far away. " +
             "Without this a world-space label balloons as the camera closes in - which is " +
             "exactly what happened on the zone shots.")]
    public float referenceDistance = 12f;
    [Tooltip("Clamp so it never becomes microscopic or enormous.")]
    public float minScale = 0.45f;
    public float maxScale = 2.2f;

    [Header("Fade")]
    [Tooltip("Below this distance the subject already fills the frame, so the heading only " +
             "gets in the way. Fades out rather than popping.")]
    public float hideNearerThan = 4.5f;
    public float fadeBand = 2.0f;
    [Tooltip("Hide entirely when the anchor is behind the camera.")]
    public bool hideWhenBehind = true;

    [Header("Behaviour")]
    public bool faceCamera = true;
    public float damping = 10f;

    Renderer rend;
    TMPro.TMP_Text label;
    Vector3 current;
    Vector3 baseScale;
    bool init;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        label = GetComponent<TMPro.TMP_Text>();
        if (anchor == Vector3.zero) anchor = transform.position;
        current = anchor;
        baseScale = transform.localScale;
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 vp = cam.WorldToViewportPoint(anchor);

        if (vp.z <= 0f)
        {
            if (hideWhenBehind) SetAlpha(0f);
            return;
        }

        float dist = vp.z;

        // Fade out once the camera is close enough that the label is just clutter.
        float a = Mathf.Clamp01((dist - hideNearerThan) / Mathf.Max(fadeBand, 0.01f));
        SetAlpha(a);
        if (a <= 0.001f) return;

        // Constant apparent size: counteract perspective foreshortening.
        float k = Mathf.Clamp(dist / Mathf.Max(referenceDistance, 0.01f), minScale, maxScale);
        transform.localScale = baseScale * k;

        // Only rescue it when it would actually be clipped; otherwise leave the authored
        // position alone, so three labels never converge on the same safe-area corner.
        float cx = Mathf.Clamp(vp.x, marginX, 1f - marginX);
        float cy = Mathf.Clamp(vp.y, marginY, 1f - marginY);
        Vector3 target = cam.ViewportToWorldPoint(new Vector3(cx, cy, vp.z));

        if (!init) { current = target; init = true; }
        current = Vector3.Lerp(current, target,
                               1f - Mathf.Exp(-Mathf.Max(damping, 0.01f) * Time.unscaledDeltaTime));
        transform.position = current;

        if (faceCamera)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, cam.transform.up);
    }

    void SetAlpha(float a)
    {
        if (label != null)
        {
            var c = label.color;
            if (!Mathf.Approximately(c.a, a)) { c.a = a; label.color = c; }
            if (rend != null && rend.enabled != (a > 0.001f)) rend.enabled = a > 0.001f;
        }
        else if (rend != null && rend.enabled != (a > 0.001f))
        {
            rend.enabled = a > 0.001f;
        }
    }
}
