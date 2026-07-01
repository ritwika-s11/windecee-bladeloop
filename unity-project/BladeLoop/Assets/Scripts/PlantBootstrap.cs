using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Per-stage scene bootstrap. For each Cinemachine vCam named vCam_&lt;suffix&gt;:
///   1. Copy world position from the matching CAM_&lt;suffix&gt; empty (embedded in the
///      Blender FBX) onto the vCam.
///   2. Add a CinemachineHardLookAt aim and aim the vCam at the scene-center target
///      (the Blender CAM_* anchors have wrong rotations — looking away from the plant —
///      so we ignore baked rotation and aim at the actual subject).
///   3. Configure FOV per-shot (wide for "reveal" shots, narrow for "closeup"/"hero" shots).
/// The Free-Orbit camera is skipped (it has its own target wired in the scene).
/// </summary>
public class PlantBootstrap : MonoBehaviour
{
    [Tooltip("Empty placed at the centre of the plant. All non-FreeOrbit vCams aim at this.")]
    public Transform lookAtTarget;

    [Header("FOV Tuning")]
    public float wideShotFov = 60f;
    public float closeShotFov = 35f;

    void Awake()
    {
        // Fallback: try to find FreeOrbit_Target (auto-created by AddFreeOrbit pass)
        if (lookAtTarget == null)
        {
            var fo = GameObject.Find("FreeOrbit_Target");
            if (fo != null) lookAtTarget = fo.transform;
        }

        var cams = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var cam in cams)
        {
            string n = cam.name;
            if (n == "vCam_FreeOrbit") continue;

            int dashIdx = n.IndexOf("vCam_");
            if (dashIdx < 0) continue;
            string anchorName = "CAM_" + n.Substring(dashIdx + 5);
            var anchor = GameObject.Find(anchorName);
            if (anchor == null) continue;

            // Copy anchor's world position onto the vCam.
            cam.transform.position = anchor.transform.position;

            // Per-vCam look target overrides the global one. Look for
            // "LookTarget_<suffix>" (e.g. LookTarget_S1_02_RedCluster). If not
            // found, fall back to the scene-centre target.
            string suffix = n.Substring(dashIdx + 5);
            var perCamTarget = GameObject.Find("LookTarget_" + suffix);
            Transform target = perCamTarget != null ? perCamTarget.transform : lookAtTarget;

            if (target != null)
            {
                cam.LookAt = target;
                cam.Follow = null;
                if (cam.GetComponent<CinemachineHardLookAt>() == null
                    && cam.GetComponent<CinemachineRotationComposer>() == null)
                {
                    cam.gameObject.AddComponent<CinemachineHardLookAt>();
                }
            }

            // FOV: wide shots ("reveal", "wideaerial", "tower", "powerloop") get 60°,
            // close-ups ("hero", "closeup", "teeth", "feedinput") get 35°.
            string lower = n.ToLowerInvariant();
            bool isWide = lower.Contains("reveal") || lower.Contains("wideaerial")
                         || lower.Contains("tower") || lower.Contains("powerloop")
                         || lower.Contains("plantentry") || lower.Contains("dismantling")
                         || lower.Contains("truckleaving");
            var lens = cam.Lens;
            lens.FieldOfView = isWide ? wideShotFov : closeShotFov;
            cam.Lens = lens;
        }
    }
}
