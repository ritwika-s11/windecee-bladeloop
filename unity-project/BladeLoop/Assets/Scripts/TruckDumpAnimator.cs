using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Stage 2 truck cycle:
///   Phase A (0.0 - 0.35): drive forward from spawn to parked position (approaching shredder).
///   Phase B (0.35 - 0.55): brief pause (truck settles).
///   Phase C (0.55 - 0.80): bed tilts back (Z-axis rotation of BedPivot), cargo slides off.
///   Phase D (0.80 - 0.95): tilt hold — blades are "in hopper" (hide cargo children).
///   Phase E (0.95 - 1.00): drive away in +X direction.
///
/// The bed pivots around its rear axle. Cargo blades are children of BedPivot so they
/// rotate with it. At the peak tilt we hide them (they've "fallen into the hopper").
/// </summary>
public class TruckDumpAnimator : MonoBehaviour
{
    [Header("Rig")]
    public Transform bedPivot;    // parent of the tilting bed + cargo
    public Transform[] cargoBlades;

    [Header("Motion")]
    public Vector3 startPos = new Vector3(-8f, -0.31f, 3.5f);
    public Vector3 parkedPos = new Vector3(-2.5f, -0.31f, 3.5f);
    public Vector3 exitPos = new Vector3(-15f, -0.31f, 3.5f);
    public float tiltAngle = 55f;    // degrees tilt around Z axis
    public float cycleDuration = 12f;
    public float startDelay = 0f;
    public bool loop = false;

    void Start()
    {
        transform.position = startPos;
        if (bedPivot != null) bedPivot.localRotation = Quaternion.identity;
        foreach (var b in cargoBlades) if (b != null) b.gameObject.SetActive(true);
    }

    void Update()
    {
        float t = Time.time - startDelay;
        if (t < 0f) return;
        float u = t / cycleDuration;
        if (!loop && u > 1f) u = 1f;
        else u = u - Mathf.Floor(u);

        System.Func<float, float, float, float> ease = (a, b, x) => Mathf.Lerp(a, b, x * x * (3f - 2f * x));

        Vector3 pos = startPos;
        float tilt = 0f;
        bool cargoVisible = true;

        if (u < 0.35f) {
            // Approach
            float k = u / 0.35f;
            pos = Vector3.Lerp(startPos, parkedPos, ease(0f, 1f, k));
        } else if (u < 0.55f) {
            pos = parkedPos;
        } else if (u < 0.80f) {
            // Tilt bed
            pos = parkedPos;
            float k = (u - 0.55f) / 0.25f;
            tilt = ease(0f, tiltAngle, k);
        } else if (u < 0.95f) {
            // Bed at peak, cargo has fallen — hide it
            pos = parkedPos;
            tilt = tiltAngle;
            cargoVisible = false;
        } else {
            // Drive away, bed returns
            float k = (u - 0.95f) / 0.05f;
            pos = Vector3.Lerp(parkedPos, exitPos, ease(0f, 1f, k));
            tilt = Mathf.Lerp(tiltAngle, 0f, ease(0f, 1f, k));
            cargoVisible = false;
        }

        transform.position = pos;
        if (bedPivot != null) bedPivot.localRotation = Quaternion.Euler(0f, 0f, tilt);
        foreach (var b in cargoBlades) if (b != null) b.gameObject.SetActive(cargoVisible);
    }
}
