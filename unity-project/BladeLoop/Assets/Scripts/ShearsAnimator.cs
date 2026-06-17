using UnityEngine;

public class ShearsAnimator : MonoBehaviour
{
    [Tooltip("Maximum closing angle (degrees)")]
    public float closeAngle = 35f;

    [Tooltip("Speed of swing")]
    public float swingSpeed = 0.8f;

    [Tooltip("Axis to rotate around — try (1,0,0), (0,1,0), or (0,0,1)")]
    public Vector3 axis = new Vector3(0, 0, 1);

    private Quaternion startRot;

    void Start()
    {
        startRot = transform.localRotation;
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * swingSpeed) + 1f) * 0.5f;  // 0..1 oscillation
        float angle = Mathf.Lerp(0f, closeAngle, t);
        transform.localRotation = startRot * Quaternion.AngleAxis(angle, axis.normalized);
    }
}