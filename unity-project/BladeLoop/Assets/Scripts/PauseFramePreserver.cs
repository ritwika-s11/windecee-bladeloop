using UnityEngine;
using UnityEngine.InputSystem;

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
    bool seededThisPause;

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

        // ---- the one-frame race this component was losing -------------------
        //
        // This runs at order -100, but StoryModeController sets IsPaused in ITS
        // Update at order 0. So on the frame Space is pressed we still read
        // "not paused" - and ExploreOrbitCamera, also at order 0, initialises
        // that very frame from whatever target it was last given, which is the
        // fixed FreeOrbit_Target. It latches `initialised` and never re-reads
        // the pivot we set a frame later. The seed was correct; it arrived late.
        //
        // In Stages 1, 2 and 4 FreeOrbit_Target sits near the shot subject so
        // the jump is small enough to miss. Stage 3 pauses on a 4.9 m close-up
        // and the view swings across the whole kiln.
        //
        // So we watch the SAME keys StoryModeController watches. Both read the
        // same frame's input, and wasPressedThisFrame is true for every reader
        // in that frame, so seeding here lands before the flag flips and before
        // the orbit rig initialises - without touching either script.
        //
        // NOT by disabling ExploreOrbitCamera, which was my first attempt and
        // was worse: its Update is the only thing that resets its private
        // `initialised` flag while unpaused, so a disabled component reused the
        // previous pause's yaw, pitch and distance against a fresh pivot and
        // threw the camera inside the geometry.
        var kb = Keyboard.current;
        bool aboutToPause = !paused && kb != null &&
                            (kb.spaceKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame);

        // The IsPaused edge is kept as a fallback for anything that pauses
        // without those keys (a UI button calling TogglePause directly). One
        // frame late, but that is the old behaviour rather than a new fault.
        // seededThisPause stops the fallback re-seeding one frame after the key
        // path already did. A second seed would re-derive the pivot from a
        // camera the orbit rig has meanwhile taken over - harmless on frame one,
        // but it would reset the user's distance mid-gesture if it ever slipped.
        if (aboutToPause && !seededThisPause)
        {
            SeedFromCurrentShot();
            seededThisPause = true;
        }
        else if (paused && !wasPaused && !seededThisPause)
        {
            SeedFromCurrentShot();
            seededThisPause = true;
        }

        if (!paused && !aboutToPause) seededThisPause = false;   // armed for the next pause

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
        //
        // The shot's own LookAt target is asked FIRST, because the probe ray is
        // unreliable: Stage 3's kiln has no colliders at all, so the ray sails
        // straight through the drum and hits the plant floor several metres
        // past it. The pose still reproduces (any pivot on the forward axis
        // does that), but the orbit then pivots around a point behind the
        // subject, so dragging swings the kiln out of frame instead of turning
        // around it.
        float dist = fallbackDistance;
        bool haveDist = false;

        var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
        // ICinemachineCamera has no LookAt in Cinemachine 3.x - it lives on the
        // concrete base class, so cast rather than assume the interface has it.
        var live = brain != null
                 ? brain.ActiveVirtualCamera as Unity.Cinemachine.CinemachineVirtualCameraBase
                 : null;
        var lookAt = live != null ? live.LookAt : null;
        if (lookAt != null)
        {
            // Project onto the camera's forward axis rather than using the raw
            // separation: the pivot has to stay ON that axis or the frame moves.
            float along = Vector3.Dot(lookAt.position - camPos, fwd);
            if (along > minPivotDistance) { dist = along; haveDist = true; }
        }

        RaycastHit hit;
        if (!haveDist && Physics.Raycast(camPos, fwd, out hit, probeDistance))
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
