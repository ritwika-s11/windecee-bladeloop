using UnityEngine;

/// <summary>
/// Slow camera drift for the home page's wind farm backdrop.
///
/// The point is atmosphere, not animation: the frame should feel alive without
/// pulling attention off the order tiles sitting on top of it. Anything you
/// consciously notice moving is too fast.
///
/// A very slow yaw sweep with a slight vertical breathe, both sine-driven, so it
/// never repeats visibly and never arrives anywhere. Unscaled time, because the
/// menu should keep drifting even if something left Time.timeScale at zero.
///
/// Owner: Ritwika.
/// </summary>
[RequireComponent(typeof(Camera))]
public class HomeStageDrift : MonoBehaviour
{
    [Header("Sweep")]
    [Tooltip("Degrees either side of the starting yaw.")]
    public float yawAmplitude = 3.2f;
    [Tooltip("Seconds for one full left-right-left sweep. Deliberately long.")]
    public float yawPeriod = 46f;

    [Header("Breathe")]
    public float heightAmplitude = 0.5f;
    public float heightPeriod = 31f;   // deliberately not a multiple of yawPeriod

    [Header("Push")]
    [Tooltip("Slow dolly toward the farm, so the shot is never quite static.")]
    public float dollyAmplitude = 1.4f;
    public float dollyPeriod = 67f;

    Vector3 basePos;
    Vector3 baseEuler;

    void Start()
    {
        basePos = transform.position;
        baseEuler = transform.eulerAngles;
    }

    void Update()
    {
        float t = Time.unscaledTime;

        float yaw = Mathf.Sin(t / Mathf.Max(yawPeriod, 0.01f) * Mathf.PI * 2f) * yawAmplitude;
        float lift = Mathf.Sin(t / Mathf.Max(heightPeriod, 0.01f) * Mathf.PI * 2f) * heightAmplitude;
        float push = Mathf.Sin(t / Mathf.Max(dollyPeriod, 0.01f) * Mathf.PI * 2f) * dollyAmplitude;

        transform.eulerAngles = new Vector3(baseEuler.x, baseEuler.y + yaw, baseEuler.z);
        transform.position = basePos + Vector3.up * lift + transform.forward * push;
    }
}
