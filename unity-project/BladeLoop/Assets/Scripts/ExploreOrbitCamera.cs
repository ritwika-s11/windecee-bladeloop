using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// Drag-to-orbit / scroll-to-zoom controller for Explore mode.
/// Only active while StoryModeController.IsPaused. While paused the
/// CinemachineBrain is disabled (see StoryModeController.TogglePause), so this
/// script drives Camera.main directly: it initialises from wherever the story
/// camera currently is, then orbits it around 'target'. On resume the Brain is
/// re-enabled and snaps back to the story shot, so nothing here persists.
/// New Input System only (Mouse.current).
/// </summary>
public class ExploreOrbitCamera : MonoBehaviour
{
    public StoryModeController controller;
    public Transform target;

    [Header("Orbit")]
    public float orbitSpeed = 0.25f;   // degrees per pixel
    public float minPitch = 5f;
    public float maxPitch = 75f;

    [Header("Zoom")]
    public float zoomSpeed = 0.05f;      // fraction of distance per scroll notch
    public float keyZoomSpeed = 12f;     // units/sec via Up/Down or W/S (unscaled time — works while paused)
    public float minDistance = 3f;
    public float maxDistance = 40f;
    [Tooltip("Explore never starts closer than this, even if the story shot was a close-up.")]
    public float startDistance = 13f;

    [Tooltip("How quickly the view eases to a new zoom level. Higher = snappier, lower = more glide.")]
    public float zoomDamping = 12f;

    [Tooltip("How quickly the view eases to a new angle. Higher = snappier, lower = more glide.")]
    public float rotationDamping = 10f;

    [Tooltip("How long the view keeps drifting after you let go of the mouse. 0 = stops dead.")]
    [Range(0f, 1f)] public float spinInertia = 0.35f;

    float yaw, pitch, distance;
    float targetYaw, targetPitch, targetDistance;
    float spinYaw, spinPitch;      // leftover velocity after release
    bool initialised;

    void InitFromCamera(Transform cam)
    {
        Vector3 offset = cam.position - target.position;
        distance = Mathf.Clamp(Mathf.Max(offset.magnitude, startDistance), minDistance, maxDistance);
        targetDistance = distance;   // start settled, so there is no ease-in on the first frame
        // Negated on purpose: the pose is rebuilt below with Vector3.back, which
        // yields (-x, +y, -z) of the direction. Deriving yaw from the un-negated
        // offset mirrored the camera to the far side of the target on every pause.
        yaw = Mathf.Atan2(-offset.x, -offset.z) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(offset.y / Mathf.Max(offset.magnitude, 0.01f), -1f, 1f)) * Mathf.Rad2Deg, minPitch, maxPitch);
        // start settled so the first paused frame does not ease in from nowhere
        targetYaw = yaw;
        targetPitch = pitch;
        spinYaw = spinPitch = 0f;
        initialised = true;
    }

    void Update()
    {
        if (controller == null || target == null || !controller.IsPaused)
        {
            initialised = false; // re-init at next pause from the current story shot
            return;
        }

        var cam = Camera.main;
        if (cam == null) return;
        if (!initialised) InitFromCamera(cam.transform);

        var mouse = Mouse.current;
        if (mouse == null) return;

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // Drag moves the *target* angle; the live angle eases toward it below.
        // Applying the raw mouse delta straight to yaw/pitch made every hand
        // tremor land as a hard step, which is what read as notchy.
        if (mouse.leftButton.isPressed && !overUI)
        {
            Vector2 d = mouse.delta.ReadValue();
            float dy = d.x * orbitSpeed;
            float dp = -d.y * orbitSpeed;
            targetYaw += dy;
            targetPitch = Mathf.Clamp(targetPitch + dp, minPitch, maxPitch);

            // remember the last motion so the view can drift on after release
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            spinYaw = dy / dt;
            spinPitch = dp / dt;
        }
        else if (spinInertia > 0f && (Mathf.Abs(spinYaw) > 0.01f || Mathf.Abs(spinPitch) > 0.01f))
        {
            // coast to a stop instead of halting the instant the button lifts
            targetYaw += spinYaw * Time.unscaledDeltaTime;
            targetPitch = Mathf.Clamp(targetPitch + spinPitch * Time.unscaledDeltaTime, minPitch, maxPitch);
            float decay = Mathf.Exp(-(1f - spinInertia * 0.9f) * 12f * Time.unscaledDeltaTime);
            spinYaw *= decay;
            spinPitch *= decay;
        }
        else { spinYaw = spinPitch = 0f; }

        // Scroll zoom, normalised across devices.
        // Windows mice report ~120 units per detent; trackpads report small
        // fractional deltas. Treating both as raw made the wheel always clamp
        // (every click an identical jump) and trackpads barely move at all.
        // Anything large is treated as one discrete detent; anything small is
        // treated as a proportional gesture.
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.001f && !overUI)
        {
            float notches = Mathf.Abs(scroll) >= 20f ? Mathf.Sign(scroll) : scroll / 20f;
            notches = Mathf.Clamp(notches, -2f, 2f);
            // exponential so each notch is the same *proportional* step at any distance
            targetDistance = Mathf.Clamp(
                targetDistance * Mathf.Pow(1f - Mathf.Clamp(zoomSpeed * 3f, 0.01f, 0.9f), notches),
                minDistance, maxDistance);
        }

        // keyboard zoom fallback (device-independent, demo-safe):
        // Up/W = zoom in, Down/S = zoom out. Unscaled time — game time is frozen while paused.
        var kb = Keyboard.current;
        if (kb != null)
        {
            float dir = 0f;
            if (kb.upArrowKey.isPressed || kb.wKey.isPressed) dir = -1f;
            else if (kb.downArrowKey.isPressed || kb.sKey.isPressed) dir = 1f;
            if (dir != 0f)
                targetDistance = Mathf.Clamp(targetDistance + dir * keyZoomSpeed * Time.unscaledDeltaTime, minDistance, maxDistance);
        }

        // Ease toward the requested angle and distance instead of snapping.
        // Unscaled: game time is frozen while paused.
        float dtu = Time.unscaledDeltaTime;
        float kRot  = 1f - Mathf.Exp(-Mathf.Max(rotationDamping, 0.01f) * dtu);
        float kZoom = 1f - Mathf.Exp(-Mathf.Max(zoomDamping, 0.01f) * dtu);

        yaw      = Mathf.LerpAngle(yaw, targetYaw, kRot);
        pitch    = Mathf.Lerp(pitch, targetPitch, kRot);
        distance = Mathf.Lerp(distance, targetDistance, kZoom);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        cam.transform.position = target.position + rot * (Vector3.back * distance);
        cam.transform.LookAt(target.position);
    }
}
