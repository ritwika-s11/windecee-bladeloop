using UnityEngine;

/// <summary>
/// Rotates the kiln body around a WORLD-space axis.
/// Use this on the horizontal kiln drum which has a compound rotation from FBX import.
/// Default axis = world X (which matches the kiln's long horizontal axis in this project).
/// </summary>
public class KilnRotator : MonoBehaviour
{
    [Tooltip("World-space axis to rotate around. Default (1,0,0) = world X, which matches the kiln long axis.")]
    public Vector3 worldAxis = Vector3.right;

    [Tooltip("Rotations per minute. ~1 RPM is realistic for a large industrial kiln.")]
    public float rpm = 1.5f;

    void Update()
    {
        // Rotate around world-space axis, applied about this object's world position
        transform.Rotate(worldAxis.normalized, rpm * 6f * Time.deltaTime, Space.World);
    }
}
