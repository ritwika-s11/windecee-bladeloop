using UnityEngine;

public class CutawayLabelFollower : MonoBehaviour
{
    public Vector3 worldOffset = Vector3.up * 0.5f;
    Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
    }

    void LateUpdate()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;
        transform.position = transform.parent != null ? transform.parent.position + worldOffset : transform.position;
        transform.LookAt(transform.position + mainCam.transform.rotation * Vector3.forward,
                         mainCam.transform.rotation * Vector3.up);
    }
}
