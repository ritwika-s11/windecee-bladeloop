using UnityEngine;

/// <summary>
/// Keeps a world-space label inside the visible frame and facing the camera.
///
/// The kiln zone headings sat above the drum, which reads well in the wide shot
/// but puts them far outside the frame on the close-up zone shots - the camera
/// there is low and angled down, so the labels were roughly 50 degrees off axis
/// and never appeared at all. Moving them down would have broken the wide shot
/// instead, so this clamps the label into a safe area per-frame rather than
/// picking one compromise position.
///
/// The label keeps its authored world position whenever that position is already
/// comfortably in frame; it only slides toward the safe area when it would
/// otherwise be clipped, and only along the camera's own axes so it never
/// appears to drift in depth.
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

    [Header("Behaviour")]
    public bool faceCamera = true;
    [Tooltip("Hide entirely when the anchor is behind the camera.")]
    public bool hideWhenBehind = true;
    [Tooltip("How quickly the label slides into the safe area.")]
    public float damping = 10f;

    Renderer rend;
    Vector3 current;
    bool init;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        if (anchor == Vector3.zero) anchor = transform.position;
        current = anchor;
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 vp = cam.WorldToViewportPoint(anchor);

        if (vp.z <= 0f)
        {
            if (hideWhenBehind && rend.enabled) rend.enabled = false;
            return;
        }
        if (!rend.enabled) rend.enabled = true;

        // clamp into the safe area, keeping the same depth
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
}
