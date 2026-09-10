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

    // Phase boundaries as FRACTIONS of cycleLength, not absolute seconds.
    //
    // These were hardcoded at t = 3.0 / 4.0 / 4.6 against the authored 6 s cycle.
    // That works only while cycleLength is exactly 6: set it to 4 and the upper
    // door opens at 3 s of a 4 s cycle, the lower never opens before the wrap, and
    // the purge never fires at all - the doors quietly stop matching the cycle
    // they belong to.
    //
    // This is the same fault that broke Stage 1's truck, Stage 3's temperature ramp
    // and Stage 4's gas cutaway after their retimes: choreography written in
    // absolute seconds against a duration that later changed. Expressed as
    // fractions, the cycle is correct at any length. 3/6, 4/6 and 4.6/6.
    // Written as the division, not as a rounded decimal. 0.6667 is NOT 4/6: at
    // cycleLength 6 it puts the boundary a hair after t=4.0, so the lower door
    // stays shut for one extra frame and the purge misses its first frame. Two
    // mismatches over a 600-sample sweep, which is exactly the kind of "surely
    // that's close enough" that is not.
    const float AuthoredCycle = 6f;
    const float UpperOpensAt  = 3.0f / AuthoredCycle;
    const float LowerOpensAt  = 4.0f / AuthoredCycle;
    const float PurgeStartsAt = 4.6f / AuthoredCycle;

    void Update()
    {
        float len = Mathf.Max(cycleLength, 0.01f);
        CycleT = Time.time % len;
        float u = CycleT / len;                 // 0..1 through the cycle

        UpperOpen = u >= UpperOpensAt && u < LowerOpensAt;
        LowerOpen = u >= LowerOpensAt;
        N2PurgeActive = u >= PurgeStartsAt;
        Phase = u < UpperOpensAt ? 1 : (u < LowerOpensAt ? 2 : 3);

        if (upperDoor != null) Slide(upperDoor, upperBase, UpperOpen);
        if (lowerDoor != null) Slide(lowerDoor, lowerBase, LowerOpen);
    }

    void Slide(Transform door, Vector3 basePos, bool open)
    {
        Vector3 target = basePos + new Vector3(open ? openOffsetX : closedOffsetX, 0f, 0f);
        door.localPosition = Vector3.Lerp(door.localPosition, target, Time.deltaTime / slideTime);
    }
}
