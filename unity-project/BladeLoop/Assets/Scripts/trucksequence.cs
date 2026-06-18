using System.Collections;
using UnityEngine;

public class TruckSequence : MonoBehaviour
{
    [Header("Drive In")]
    [Tooltip("How far away the truck starts from its current scene position. Try (0,0,-10).")]
    public Vector3 startOffset = new Vector3(0f, 0f, -10f);
    public float driveInDuration = 4f;
    public float startDelay = 0f;

    [Header("Pause Before Tilt")]
    public float pauseBeforeTilt = 1f;

    [Header("Bed Tilt")]
    [Tooltip("Drag S2_Truck_Bed here.")]
    public Transform bedToTilt;
    [Tooltip("Which local axis the bed hinges around. Try (1,0,0) first.")]
    public Vector3 tiltAxis = new Vector3(1f, 0f, 0f);
    [Tooltip("Tilt angle in degrees. Positive or negative depending on direction.")]
    public float tiltAngle = 45f;
    public float tiltDuration = 2f;

    [Header("Hold At Top")]
    public float holdDuration = 3f;

    private Vector3 endPosition;
    private Quaternion bedStartRotation;

    void Start()
    {
        endPosition = transform.position;
        transform.position = endPosition + startOffset;

        if (bedToTilt != null)
            bedStartRotation = bedToTilt.localRotation;

        StartCoroutine(RunSequence());
    }

    IEnumerator RunSequence()
    {
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        // Drive in
        Vector3 startPos = transform.position;
        float t = 0f;
        while (t < driveInDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, t / driveInDuration);
            transform.position = Vector3.Lerp(startPos, endPosition, u);
            yield return null;
        }
        transform.position = endPosition;

        yield return new WaitForSeconds(pauseBeforeTilt);

        // Tilt bed
        if (bedToTilt == null) yield break;

        Quaternion tiltDelta = Quaternion.AngleAxis(tiltAngle, tiltAxis.normalized);
        Quaternion targetRot = bedStartRotation * tiltDelta;

        t = 0f;
        while (t < tiltDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, t / tiltDuration);
            bedToTilt.localRotation = Quaternion.Slerp(bedStartRotation, targetRot, u);
            yield return null;
        }
        bedToTilt.localRotation = targetRot;

        yield return new WaitForSeconds(holdDuration);
    }
}
