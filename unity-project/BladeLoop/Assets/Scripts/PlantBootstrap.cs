using UnityEngine;

/// <summary>
/// Lightweight scene-side bootstrap. Place once per stage scene on an empty GameObject.
/// Auto-wires Cinemachine vCams' Follow/LookAt targets to embedded CAM_* empties if not already set.
/// </summary>
public class PlantBootstrap : MonoBehaviour
{
    void Awake()
    {
        var cams = FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None);
        foreach (var cam in cams)
        {
            // If vCam name encodes its anchor (e.g. "vCam_S3_01_PlantReveal"), find the anchor
            string n = cam.name;
            int dashIdx = n.IndexOf("vCam_");
            if (dashIdx < 0) continue;
            string anchorName = "CAM_" + n.Substring(dashIdx + 5);
            var anchor = GameObject.Find(anchorName);
            if (anchor == null) continue;

            if (cam.Follow == null)  cam.Follow = anchor.transform;
            if (cam.LookAt == null)  cam.LookAt = anchor.transform;
            cam.transform.position = anchor.transform.position;
            cam.transform.rotation = anchor.transform.rotation;
        }
    }
}
