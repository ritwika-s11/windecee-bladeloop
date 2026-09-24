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
             "gets in the way. Fades out rather than popping.\n\n" +
             "Measured as TRUE DISTANCE from the camera to the label. It used to be read " +
             "off vp.z, the depth along the camera's forward axis, which is smaller for " +
             "anything off the centre of frame - so labels were faded for being off to the " +
             "side rather than for being close, and they faded in and out as a shot moved. " +
             "Stage 3's kiln cameras look along the drum, which is exactly that case.")]
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

    [Tooltip("How many metres the fade takes to run from invisible to full, at either end.\n\n" +
             "Worth knowing: with hideNearerThan 4.5 and this at 2, everything within 6.5 m " +
             "is at least partly transparent. A label at half alpha over a lit kiln reads as " +
             "missing rather than as faded, so a wide band here is not the gentle option it " +
             "looks like.")]
    public float fadeBand = 2.0f;
    [Tooltip("Hide entirely when the anchor is behind the camera.")]
    public bool hideWhenBehind = true;

    [Tooltip("How far outside the safe area a label may be rescued from, in viewport units. " +
             "Beyond this it fades instead, because it belongs to equipment outside the " +
             "shot.\n\n" +
             "9 or more disables the limit and restores the old unbounded clamp, which is " +
             "what piled ten Stage 4 labels into a column down the right edge.")]
    [Range(0.05f, 9f)] public float maxRescue = 9f;

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

        // THE 3D VIEW IS NOT THE WHOLE SCREEN.
        //
        // During a run the order panel occupies the right of the frame, from
        // OrderContext.TourSplitWidth (0.72) outward. This used to clamp against the full
        // screen width, so a label rescued toward the right edge was pushed to 0.94 -
        // behind the panel, where the words are simply not there. That is the Stage 4
        // complaint about text disappearing under the black box on the right.
        //
        // OrderContext's own comment says "read this constant; do not type 0.72 anywhere",
        // and every other component does. This one did not. TourControls line 126 already
        // uses exactly this test, and it is also what keeps FREE PLAY unchanged: with no
        // panel there is no OrderPanel.Instance, the limit is 1, and nothing moves.
        float right = OrderPanel.Instance != null ? OrderContext.TourSplitWidth : 1f;

        // Clamp the label's EDGES into the view, not its centre. A centre-only clamp still
        // lets a wide label such as "GAS-TO-AIR HEAT EXCHANGER" hang past the split.
        //
        // The label is screen-aligned and flat, so its viewport size follows from its world
        // size and its depth alone - no need to place it first.
        float halfW = 0f, halfH = 0f;
        if (label != null)
        {
            // DEPTH here, not distance - the frustum's height is measured on the plane
            // parallel to the near plane, which is what vp.z indexes.
            float frustumH = 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            if (frustumH > 1e-4f)
            {
                var size = label.textBounds.size;
                halfH = (size.y * transform.localScale.y) / frustumH * 0.5f;
                halfW = (size.x * transform.localScale.x) / (frustumH * cam.aspect) * 0.5f;
            }
        }

        float loX = marginX + halfW, hiX = right - marginX - halfW;
        float loY = marginY + halfH, hiY = 1f - marginY - halfH;
        // A label wider than the view cannot satisfy both edges; centre it rather than
        // letting the clamp invert and fling it off-screen.
        float cx = hiX >= loX ? Mathf.Clamp(vp.x, loX, hiX) : right * 0.5f;
        float cy = hiY >= loY ? Mathf.Clamp(vp.y, loY, hiY) : 0.5f;

        // RESCUE HAS A RANGE. Past it, the label is not in this shot.
        //
        // Clamping is meant to save a heading that is ALMOST in frame - the original bug,
        // where a kiln zone title sat just off the top edge. But an unbounded clamp also
        // drags in labels for machinery that is nowhere near the shot: their anchors land
        // far outside the viewport and every one of them is pulled to the same margin,
        // where the de-overlap then stacks them into a column. That is the crowded right
        // edge in Stage 4 - ten labels in a vertical pile, most naming equipment the
        // audience cannot see.
        //
        // So: rescue a label that is at most maxRescue outside the safe box, and fade the
        // rest. This is NOT the distance cutoff that failed before - that asked "how far
        // away is this machine", which says nothing about whether it is the subject. This
        // asks "is it in the picture", which is the actual question.
        if (maxRescue < 9f)
        {
            float pull = Mathf.Max(Mathf.Abs(cx - vp.x), Mathf.Abs(cy - vp.y));
            if (pull > maxRescue) { SetAlpha(0f); return; }
        }

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
