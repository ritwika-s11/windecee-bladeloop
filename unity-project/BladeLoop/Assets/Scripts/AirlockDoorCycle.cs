using UnityEngine;

/// <summary>
/// Cycles the upper + lower pneumatic slide-gate doors on a 6-second loop per the CEE spec:
///  Phase 1 (0-3s): ACCUMULATION            - both doors closed, material piles in Room 1
///  Phase 2 (3-4s): UPPER DROP              - upper open, lower closed, batch falls Room1 -> Room2
///  Phase 3 (4-6s): LOWER PURGE + DISCHARGE - upper closed, lower open, N2 purge, batch to kiln
/// The two doors are NEVER open at the same time.
/// Doors are horizontal slide gates: they retract toward their pneumatic actuator (-X) to open.
/// </summary>
public class AirlockDoorCycle : MonoBehaviour
{
    public Transform upperDoor;
    public Transform lowerDoor;

    [Tooltip("Cycle length in seconds. CEE spec = 6.")]
    public float cycleLength = 6f;

    [Tooltip("Local X offset from the exported pose to the CLOSED position (gate under the chute).")]
    public float closedOffsetX = 0.35f;
    [Tooltip("Local X offset from the exported pose to the OPEN position (gate tucked into pocket).")]
    public float openOffsetX = -0.18f;
    [Tooltip("Smooth open/close time in seconds.")]
    public float slideTime = 0.22f;

    // --- public state for the flow controller / status panel ---
    public int Phase { get; private set; } = 1;
    public float CycleT { get; private set; }
    public bool UpperOpen { get; private set; }
    public bool LowerOpen { get; private set; }
    public bool N2PurgeActive { get; private set; }

    Vector3 upperBase, lowerBase;

    void Start()
    {
        if (upperDoor != null) upperBase = upperDoor.localPosition;
        if (lowerDoor != null) lowerBase = lowerDoor.localPosition;
    }

    void Update()
    {
        CycleT = Time.time % cycleLength;
        float t = CycleT;

        UpperOpen = t >= 3f && t < 4f;
        LowerOpen = t >= 4f;
        N2PurgeActive = t >= 4.6f;
        Phase = t < 3f ? 1 : (t < 4f ? 2 : 3);

        if (upperDoor != null) Slide(upperDoor, upperBase, UpperOpen);
        if (lowerDoor != null) Slide(lowerDoor, lowerBase, LowerOpen);
    }

    void Slide(Transform door, Vector3 basePos, bool open)
    {
        Vector3 target = basePos + new Vector3(open ? openOffsetX : closedOffsetX, 0f, 0f);
        door.localPosition = Vector3.Lerp(door.localPosition, target, Time.deltaTime / slideTime);
    }
}
