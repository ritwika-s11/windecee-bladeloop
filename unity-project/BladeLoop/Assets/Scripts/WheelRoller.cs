using UnityEngine;

/// <summary>
/// Rolls wheel transforms based on how far this object actually moves each frame.
/// Purely visual, additive - does not affect driving logic.
/// </summary>
public class WheelRoller : MonoBehaviour
{
    [Tooltip("Wheel transforms to roll (axle assumed along world Z).")]
    public Transform[] wheels;
    public float wheelRadius = 0.62f;

    Vector3 lastPos;

    void Start() { lastPos = transform.position; }

    void LateUpdate()
    {
        Vector3 delta = transform.position - lastPos;
        lastPos = transform.position;
        float dist = delta.x; // truck drives along world X
        if (Mathf.Approximately(dist, 0f) || wheels == null) return;
        float deg = (dist / wheelRadius) * Mathf.Rad2Deg;
        foreach (var w in wheels)
            if (w != null) w.Rotate(Vector3.back, deg, Space.World);
    }
}
