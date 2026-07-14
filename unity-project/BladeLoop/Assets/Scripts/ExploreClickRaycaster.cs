using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// New-Input-System replacement for ClickablePart's OnMouseDown (which never
/// fires when Active Input Handling is set to Input System only).
/// Put ONE of these in the scene. On mouse release (with negligible drag, so
/// orbiting doesn't trigger clicks) it raycasts and opens the PartInfoPanel
/// for any ClickablePart in the hit object's parents.
/// If a StoryModeController exists, clicks only work while paused (Explore mode).
/// </summary>
public class ExploreClickRaycaster : MonoBehaviour
{
    public StoryModeController controller;   // optional; auto-found if null
    public float maxRayDistance = 200f;
    public float clickDragTolerance = 6f;    // pixels

    Vector2 pressPos;
    bool pressed;

    void Start()
    {
        if (controller == null)
            controller = FindFirstObjectByType<StoryModeController>();
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        if (controller != null && !controller.IsPaused) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            pressed = true;
            pressPos = mouse.position.ReadValue();
        }

        if (pressed && mouse.leftButton.wasReleasedThisFrame)
        {
            pressed = false;
            Vector2 pos = mouse.position.ReadValue();
            if ((pos - pressPos).magnitude > clickDragTolerance) return;      // was a drag
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            var cam = Camera.main;
            if (cam == null) return;
            Ray ray = cam.ScreenPointToRay(pos);
            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance))
            {
                var part = hit.collider.GetComponentInParent<ClickablePart>();
                if (part != null && PartInfoPanel.Instance != null)
                    PartInfoPanel.Instance.Show(part.partTitle, part.partDescription);
            }
        }
    }
}
