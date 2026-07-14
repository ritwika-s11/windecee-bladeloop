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

    float yaw, pitch, distance;
    bool initialised;

    void InitFromCamera(Transform cam)
    {
        Vector3 offset = cam.position - target.position;
        distance = Mathf.Clamp(Mathf.Max(offset.magnitude, startDistance), minDistance, maxDistance);
        yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(offset.y / Mathf.Max(offset.magnitude, 0.01f), -1f, 1f)) * Mathf.Rad2Deg, minPitch, maxPitch);
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

        if (mouse.leftButton.isPressed && !overUI)
        {
            Vector2 d = mouse.delta.ReadValue();
            yaw += d.x * orbitSpeed;
            pitch = Mathf.Clamp(pitch - d.y * orbitSpeed, minPitch, maxPitch);
        }

        // scroll zoom: multiplicative, clamped per event so mice (large deltas)
        // and trackpads (tiny deltas) both feel sane
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.001f && !overUI)
        {
            float notch = Mathf.Clamp(scroll, -3f, 3f);
            distance = Mathf.Clamp(distance * (1f - notch * zoomSpeed), minDistance, maxDistance);
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
                distance = Mathf.Clamp(distance + dir * keyZoomSpeed * Time.unscaledDeltaTime, minDistance, maxDistance);
        }

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        cam.transform.position = target.position + rot * (Vector3.back * distance);
        cam.transform.LookAt(target.position);
    }
}
