using UnityEngine;
using TMPro;

/// <summary>
/// World-space status readout for the airlock tower, mirroring the CEE infographic:
/// current phase of the 6-second cycle, both door states, and the N2 purge indicator.
/// Billboards toward the rendering camera.
/// </summary>
public class TowerStatusPanel : MonoBehaviour
{
    public AirlockDoorCycle cycle;
    [Tooltip("Optional: only show the panel during this timeline window.")]
    public UnityEngine.Playables.PlayableDirector director;
    public float showFrom = 11f;
    public float showTo = 42f;
    public TextMeshPro label;
    public bool billboard = true;

    static readonly string GREEN = "#35E06A";
    static readonly string RED = "#FF5040";
    static readonly string BLUE = "#58B6FF";
    static readonly string GREY = "#9AA7B4";

    void LateUpdate()
    {
        if (cycle == null || label == null) return;

        if (director != null)
        {
            bool show = director.time >= showFrom && director.time <= showTo;
            if (label.enabled != show) label.enabled = show;
            if (!show) return;
        }

        string phaseName;
        string phaseCol;
        switch (cycle.Phase)
        {
            case 1: phaseName = "PHASE 1 - ACCUMULATION"; phaseCol = BLUE; break;
            case 2: phaseName = "PHASE 2 - THE UPPER DROP"; phaseCol = GREEN; break;
            default: phaseName = "PHASE 3 - LOWER PURGE & DISCHARGE"; phaseCol = "#C77DFF"; break;
        }

        string upper = cycle.UpperOpen ? "<color=" + GREEN + ">OPEN</color>" : "<color=" + RED + ">CLOSED</color>";
        string lower = cycle.LowerOpen ? "<color=" + GREEN + ">OPEN</color>" : "<color=" + RED + ">CLOSED</color>";
        string n2 = cycle.N2PurgeActive ? "<color=" + GREEN + ">N2 PURGE ACTIVE</color>" : "<color=" + GREY + ">N2 STANDBY</color>";

        label.text =
            "<color=" + phaseCol + "><b>" + phaseName + "</b></color>  <color=" + GREY + ">" +
            cycle.CycleT.ToString("0.0") + "s / 6s</color>\n" +
            "UPPER DOOR: " + upper + "    LOWER DOOR: " + lower + "\n" + n2;

        if (billboard)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 dir = transform.position - cam.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(dir);
            }
        }
    }
}
