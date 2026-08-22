using UnityEngine;

/// <summary>
/// Removes the camera jump that happens when the story is paused.
///
/// ExploreOrbitCamera orbits around a fixed scene object (FreeOrbit_Target) and
/// forces a minimum start distance, so pausing swung the view across the scene
/// and pulled close-ups backwards. This component runs one frame ahead of it
/// (execution order -100) and, at the instant the story pauses, re-points the
/// orbit rig at a pivot placed directly along the camera's own forward axis.
///
/// Because the pivot sits on that axis at exactly the current distance, the
/// yaw/pitch/distance the orbit script derives reproduce the current pose
/// exactly - the frame does not move. Orbiting then rotates around whatever the
/// shot was actually looking at.
///
/// Writes only to public fields on ExploreOrbitCamera. No existing script is
/// modified. Safe to drop onto every stage.
/// </summary>
[DefaultExecutionOrder(-100)]
public class PauseFramePreserver : MonoBehaviour
{
    [Tooltip("Leave empty to find the StoryModeController automatically.")]
    public StoryModeController controller;

    [Tooltip("Leave empty to find the ExploreOrbitCamera automatically.")]
    public ExploreOrbitCamera orbit;

    [Header("Pivot placement")]
    [Tooltip("Used when nothing is hit by the probe ray - how far ahead of the camera to place the orbit pivot.")]
    public float fallbackDistance = 12f;

    [Tooltip("How far ahead to look for the subject the shot is framing.")]
    public float probeDistance = 300f;

    [Tooltip("Never let the pivot end up closer than this.")]
    public float minPivotDistance = 2f;

    [Header("Debug")]
    public bool logOnPause = false;

    Transform pivot;
    bool wasPaused;

    void Awake()
    {
        if (controller == null) controller = FindFirstObjectByType<StoryModeController>();
        if (orbit == null) orbit = FindFirstObjectByType<ExploreOrbitCamera>();

        var go = new GameObject("~ExplorePivot (runtime)");
        go.hideFlags = HideFlags.DontSave;
        pivot = go.transform;
    }

    void Update()
    {
        if (controller == null || orbit == null) return;

        bool paused = controller.IsPaused;
        if (paused && !wasPaused) SeedFromCurrentShot();
        wasPaused = paused;
    }

    /// <summary>
    /// Places the pivot on the camera's forward axis and widens the orbit rig's
    /// limits so none of them clamp the current pose.
    /// </summary>
    void SeedFromCurrentShot()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 camPos = cam.transform.position;
        Vector3 fwd = cam.transform.forward;

        // Prefer the actual thing being framed, so orbiting feels natural.
        float dist = fallbackDistance;
        RaycastHit hit;
        if (Physics.Raycast(camPos, fwd, out hit, probeDistance))
            dist = hit.distance;

        dist = Mathf.Max(dist, minPivotDistance);

        // Pivot on the forward axis => reconstructed pose == current pose.
        pivot.position = camPos + fwd * dist;

        Vector3 offset = camPos - pivot.position;
        float mag = Mathf.Max(offset.magnitude, 0.01f);
        float pitch = Mathf.Asin(Mathf.Clamp(offset.y / mag, -1f, 1f)) * Mathf.Rad2Deg;

        // Widen every limit that could otherwise clamp the current framing.
        orbit.target        = pivot;
        orbit.startDistance = dist;
        orbit.minDistance   = Mathf.Min(orbit.minDistance, dist * 0.4f);
        orbit.maxDistance   = Mathf.Max(orbit.maxDistance, dist * 3f);
        orbit.minPitch      = Mathf.Min(orbit.minPitch, pitch - 5f);
        orbit.maxPitch      = Mathf.Max(orbit.maxPitch, pitch + 5f);

        if (logOnPause)
            Debug.Log($"[PauseFramePreserver] pivot {pivot.position} dist {dist:F2} pitch {pitch:F1}" +
                      (dist < probeDistance ? " (ray hit)" : " (fallback)"));
    }
}
