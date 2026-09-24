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

    [Tooltip("Beyond this distance the label fades out again.\n\n" +
             "Infinity by default, so Stage 3 - which has only four of these, spread apart - " +
             "is unaffected.\n\n" +
             "Stage 4 needs it. Sixteen labels sit along one plant, and from most shots half " +
             "of them line up behind each other; held at constant screen size they converge " +
             "and overprint until the top of frame is unreadable. Naming only the machines " +
             "near the camera is what a real annotated walkthrough does - the label arrives " +
             "as you reach the equipment, rather than every label shouting at once.")]
    public float hideFartherThan = Mathf.Infinity;

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

        // TWO DIFFERENT MEASURES, AND THEY ARE NOT INTERCHANGEABLE.
        //
        // vp.z is the depth along the camera's FORWARD axis, not the distance to the
        // label. For anything off the centre of frame the two diverge badly, because
        // vp.z is the distance times the cosine of the angle off-axis.
        //
        // The original code used vp.z for both jobs. For the scale that is correct -
        // perspective foreshortening really does go with depth, so vp.z is what holds a
        // label at a constant size on screen. For the FADE it is wrong, and measurably so.
        // Stage 3's kiln cameras look along the drum, which puts the zone headings far
        // off-axis:
        //
        //     vCam_S3_08   ZONE 1 PREHEAT   really 8.6 m away, read as 5.5 m -> alpha 0.49
        //     vCam_S3_10   ZONE 1 PREHEAT   really 5.8 m away, read as 3.9 m -> alpha 0.00
        //     vCam_S3_09   ZONE 1 PREHEAT   really 6.7 m away, read as 4.6 m -> alpha 0.03
        //
        // So the headings were being faded for being off to the side of frame rather than
        // for being close, and because the angle changes as the shot moves, they fade in
        // and out DURING the shot. That is the "sometimes two, sometimes three" - they were
        // never switching, they were sitting at partial alpha.
        //
        // The fade asks "is the camera close enough that this heading is just clutter?".
        // That question is about distance, so it gets distance.
        float depth = vp.z;
        float dist  = Vector3.Distance(anchor, cam.transform.position);

        // Fade out once the camera is close enough that the label is just clutter, and
        // again once it is far enough away that this machine is not what the shot is about.
        float a = Mathf.Clamp01((dist - hideNearerThan) / Mathf.Max(fadeBand, 0.01f));
        if (!float.IsInfinity(hideFartherThan))
            a = Mathf.Min(a, Mathf.Clamp01((hideFartherThan - dist) / Mathf.Max(fadeBand, 0.01f)));
        SetAlpha(a);
        if (a <= 0.001f) return;

        // Constant apparent size: counteract perspective foreshortening. DEPTH, not
        // distance - see above.
        float k = Mathf.Clamp(depth / Mathf.Max(referenceDistance, 0.01f), minScale, maxScale);
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

        // SCREEN-ALIGNED, not aimed at the camera.
        //
        // LookRotation(position - camera.position) points each label AT the lens, which is
        // a spherical billboard: a label straight ahead comes out square, but one off to
        // the side is turned toward the centre and reads as tilted. With several labels
        // spread across a wide shot they all lean at different angles.
        //
        // Copying the camera's rotation makes every label parallel to the near plane, so
        // the text is horizontal on screen wherever it sits in frame.
        if (faceCamera)
            transform.rotation = cam.transform.rotation;
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
