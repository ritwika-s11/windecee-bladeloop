using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Two security guards on the plant gate: one works the barrier for the incoming
/// load, one holds station at the gatehouse.
///
/// WHY IT BUILDS ITSELF AT RUNTIME
/// Transport_StoryMode is one of the five tour scenes Ritwika owns, and Unity scene
/// files cannot be merged. So this adds nothing to the scene file: it spawns after
/// the scene loads and finds what it needs by name. Same pattern as TourControls and
/// OrderPanel, and for the same reason.
///
/// WHY IT FOLLOWS THE TRUCK, NOT A CLOCK
/// The truck reaches the gate at t=13.2 s but the segment is 13 s, so it never
/// quite arrives on screen. Timing the guard off the clock would mean guessing at a
/// beat that does not happen. Everything here keys off the truck's actual X, so the
/// barrier is already up when the truck reaches camera B, and it stays correct if
/// anyone retimes the drive.
///
/// WHAT IT DOES NOT TOUCH
/// No shared material is edited - the hi-vis vest is a per-renderer property block,
/// because Ch17's body material is shared with the Stage 1 driver and tinting the
/// asset would put a hi-vis vest on him too.
/// </summary>
public class TransportGateGuards : MonoBehaviour
{
    const string Scene      = "Transport_StoryMode";
    const string GuardAsset = "TR_GateGuard";       // Assets/Resources/TR_GateGuard.prefab

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        // the tour may already be sitting in Transport when this first runs
        if (SceneManager.GetActiveScene().name == Scene) Spawn();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        if (s.name == Scene) Spawn();
    }

    static void Spawn()
    {
        if (Object.FindFirstObjectByType<TransportGateGuards>() != null) return;
        var host = new GameObject("TR_GateGuards");
        host.AddComponent<TransportGateGuards>();
    }

    // ── choreography, all in metres of truck travel ────────────────────────────
    [Tooltip("Truck X at which the barrier guard leaves the gatehouse and walks to the post.")]
    public float walkOutAtTruckX = 6f;
    [Tooltip("Truck X at which the boom starts to lift. Must leave enough road for the " +
             "lift to finish before the truck is on top of it.")]
    public float raiseAtTruckX = 30f;
    [Tooltip("Seconds for the boom to travel from horizontal to vertical.")]
    public float boomLiftSeconds = 1.8f;
    public float boomUpAngle = 78f;
    public float walkSpeed = 1.35f;
    [Tooltip("Top of the driveway slab. The guards' soles are placed here, measured " +
             "from their own bounds rather than assumed from the pivot.")]
    public float groundY = 0.08f;

    Transform truck;
    float boomX = 68.8f;          // read from the real boom on Start
    float truckLength = 11.2f;    // measured, not assumed
    Transform boomA, boomB;
    Vector3 boomAHome, boomBHome;
    Quaternion boomARot, boomBRot;
    float postAZ, postBZ;
    Transform guardOperator, guardStation;
    Animator animOperator;
    Vector3 postSpot, gateHouseSpot;
    float lift;                       // 0 = down, 1 = up

    [Header("Free play")]
    [Tooltip("Off by default so free play is byte-identical to today, per the brief's rule.\n\n" +
             "These are set dressing rather than order-driven behaviour, so an argument exists " +
             "for having them always on - a plant gate with nobody on it looks odder than one " +
             "with. But that rule was written after a change leaked into free play once already, " +
             "and the tour is what ships, so the letter of it wins. Tick this to show them " +
             "everywhere.")]
    public bool showWithoutOrder = false;

    void Start()
    {
        // free play must look exactly as it does today
        if (!showWithoutOrder && !OrderContext.HasOrder) { enabled = false; return; }

        var t = GameObject.Find("TR_BladeTruck");
        truck = t != null ? t.transform : null;

        var canopy = GameObject.Find("TR_GateCanopy");
        if (canopy == null || truck == null) { enabled = false; return; }

        boomA = canopy.transform.Find("Boom_0");
        boomB = canopy.transform.Find("Boom_1");
        var postA = canopy.transform.Find("BoomPost_0");
        var postB = canopy.transform.Find("BoomPost_1");
        if (boomA != null) { boomAHome = boomA.position; boomARot = boomA.rotation; }
        if (boomB != null) { boomBHome = boomB.position; boomBRot = boomB.rotation; }
        postAZ = postA != null ? postA.position.z : -4.6f;
        postBZ = postB != null ? postB.position.z :  4.6f;
        if (boomA != null) boomX = boomA.position.x;

        // measure the load rather than assume it - the trailer length changed twice
        // while Transport was being rebuilt
        var trs = truck.GetComponentsInChildren<Renderer>(true);
        if (trs.Length > 0)
        {
            var tb = trs[0].bounds;
            for (int i = 1; i < trs.Length; i++) tb.Encapsulate(trs[i].bounds);
            truckLength = tb.size.x;
        }

        // Positions verified by rendering from vCam_TR_B_Entrance, not chosen on paper.
        // Tucked beside the gatehouses the canopy columns hid both of them completely;
        // out in the gate opening they read as two figures at the barrier from 35 m.
        //
        // z = -3.9 is deliberate: the carriageway is +/-3.5, so this is just OUTSIDE it.
        // The first version put him at -3.0, standing in the path of an arriving 11 m
        // truck - which reads as an accident, not a gate crew.
        gateHouseSpot = new Vector3(66.6f, 0f, -6.8f);   // waiting, by the gatehouse door
        postSpot      = new Vector3(68.4f, 0f, -3.9f);   // at the boom, clear of the road

        guardOperator = MakeGuard("Guard_BarrierOperator", gateHouseSpot, new Vector3(0f, 250f, 0f));
        guardStation  = MakeGuard("Guard_Gatehouse",       new Vector3(67.2f, 0f, 5.2f), new Vector3(0f, 285f, 0f));
        if (guardOperator != null) animOperator = guardOperator.GetComponentInChildren<Animator>();
    }

    Transform MakeGuard(string name, Vector3 pos, Vector3 euler)
    {
        var prefab = Resources.Load<GameObject>(GuardAsset);
        if (prefab == null) { Debug.LogWarning("[TransportGateGuards] Resources/" + GuardAsset + " missing"); return null; }
        var go = Instantiate(prefab, pos, Quaternion.Euler(euler), transform);
        go.name = name;

        // Ch17's pivot is not at the soles - dropped straight in, the guards stand
        // 13 cm into the tarmac. Measure and lift so the feet meet the driveway.
        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends.Length > 0)
        {
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            go.transform.position += new Vector3(0f, groundY - b.min.y, 0f);
        }

        // Hi-vis, per instance. Ch17's body material is shared with the Stage 1 driver,
        // so the tint goes on a property block and never on the asset.
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.gameObject.name.Contains("Vest")) continue;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", new Color(0.95f, 0.72f, 0.06f));
            mpb.SetColor("_Color",     new Color(0.95f, 0.72f, 0.06f));
            r.SetPropertyBlock(mpb);
        }
        return go.transform;
    }

    void Update()
    {
        if (truck == null) return;
        float x = truck.position.x;

        // The full cycle, so the gate is a complete piece of behaviour rather than a
        // barrier that goes up and is abandoned. At the shipped timing only the first
        // three phases are ever seen - the segment ends at 13 s with the truck at
        // x 67.9, just short of the boom at 68.8 - but the close-down is correct if
        // anyone lengthens the drive, and costs nothing while it never fires.
        bool trucking   = x > walkOutAtTruckX;          // load on approach
        bool passedGate = x > boomX + truckLength;      // rear of the trailer is clear

        // ---- the operator: wait, walk out, hold clear, then return ----
        if (guardOperator != null)
        {
            Vector3 target = (trucking && !passedGate) ? postSpot : gateHouseSpot;
            bool arrived = StepTowards(guardOperator, target, out float moved);
            if (animOperator != null) animOperator.SetFloat("Speed", moved > 0.001f ? 1f : 0f);
            // Watch the load in while it is coming, then go back to the gatehouse.
            // Facing the truck the whole time would leave him staring at a wall.
            if (arrived && !passedGate) Face(guardOperator, new Vector3(x, 0f, 0f));
        }

        // ---- the station guard tracks it past and then settles back to the road ----
        if (guardStation != null)
            Face(guardStation, passedGate ? new Vector3(40f, 0f, 0f) : new Vector3(x, 0f, 0f));

        // ---- boom up on approach, down once the trailer is clear ----
        float want = (trucking && x > raiseAtTruckX && !passedGate) ? 1f : 0f;
        lift = Mathf.MoveTowards(lift, want, Time.deltaTime / Mathf.Max(boomLiftSeconds, 0.05f));
        float a = Mathf.SmoothStep(0f, 1f, lift) * boomUpAngle;
        SetBoom(boomA, boomAHome, boomARot, postAZ, a);
        SetBoom(boomB, boomBHome, boomBRot, postBZ, a);
    }

    /// <summary>Rotates a boom about its post so the free end swings up.</summary>
    void SetBoom(Transform boom, Vector3 home, Quaternion homeRot, float postZ, float angle)
    {
        if (boom == null) return;
        // Sign so the FREE end rises whichever side of the road the barrier is on.
        // Negative because a positive rotation about X carries a +Z offset toward -Y:
        // the first version drove both booms into the ground instead of lifting them.
        float dir = -Mathf.Sign(home.z - postZ);
        var pivot = new Vector3(home.x, home.y, postZ);
        var rot = Quaternion.AngleAxis(angle * dir, Vector3.right);
        boom.position = pivot + rot * (home - pivot);
        boom.rotation = rot * homeRot;
    }

    bool StepTowards(Transform who, Vector3 target, out float moved)
    {
        Vector3 p = who.position;
        Vector3 flat = new Vector3(target.x, p.y, target.z);
        Vector3 to = flat - p;
        moved = 0f;
        if (to.magnitude < 0.06f) { who.position = flat; return true; }
        Vector3 step = to.normalized * walkSpeed * Time.deltaTime;
        if (step.magnitude > to.magnitude) step = to;
        who.position = p + step;
        moved = step.magnitude;
        who.rotation = Quaternion.Slerp(who.rotation, Quaternion.LookRotation(to.normalized), 8f * Time.deltaTime);
        return false;
    }

    void Face(Transform who, Vector3 point)
    {
        Vector3 d = point - who.position; d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        who.rotation = Quaternion.Slerp(who.rotation, Quaternion.LookRotation(d.normalized), 3.5f * Time.deltaTime);
    }
}
