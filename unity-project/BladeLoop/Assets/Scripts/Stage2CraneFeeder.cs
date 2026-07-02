using UnityEngine;

/// <summary>
/// Stage 2 crane feed loop: picks blades one-by-one from the parked truck bed and
/// drops them into the shredder hopper. Drives the existing MiniCrane rig
/// (slew / trolley / hookRig). A carried-blade visual rides the hook between
/// grab and release. Each release triggers a shred burst + granules on the conveyor.
/// When every blade is fed, the crane parks and tells the truck to depart.
/// </summary>
public class Stage2CraneFeeder : MonoBehaviour
{
    [Header("Crane rig")]
    public Transform slew;
    public Transform trolley;
    public Transform hookRig;

    [Header("Blades on the truck bed (hidden one-by-one)")]
    public Transform[] blades;
    [Tooltip("Blade visual parented to the hook, shown while carrying.")]
    public GameObject carriedBlade;

    [Header("Effects")]
    public ParticleSystem shredBurst;       // chunks tumbling in the hopper
    public ParticleSystem outputGranules;   // shredded stream on the conveyor
    public ParticleSystem outputFallout;    // shreds falling from under the shredder onto the belt
    public TruckDumpAnimator truck;

    [Header("Positions")]
    public Vector3 pickupWorldPos = new Vector3(-3f, 1.5f, -4f);
    public Vector3 dropWorldPos = new Vector3(5f, 5.5f, 0f);

    [Header("Timing")]
    public float startDelay = 9f;
    public float cycleDuration = 7f;
    public float hookHighY = -0.4f;
    public float hookLowPickupY = -8.5f;
    public float hookLowDropY = -6.5f;
    public float granulesRunTime = 8f;

    float pickupYaw, dropYaw, pickupTrolleyX, dropTrolleyX;
    int fed = 0;
    bool grabbed, released, toldTruck;
    int lastN = -1;
    float granulesUntil = -1f;
    float sceneT0; // scene-relative clock for the chained tour

    void Start()
    {
        if (slew == null || trolley == null || hookRig == null) return;
        Vector3 sw = slew.position;
        Vector2 po = new Vector2(pickupWorldPos.x - sw.x, pickupWorldPos.z - sw.z);
        Vector2 dr = new Vector2(dropWorldPos.x - sw.x, dropWorldPos.z - sw.z);
        pickupYaw = -Mathf.Atan2(po.y, po.x) * Mathf.Rad2Deg;
        dropYaw = -Mathf.Atan2(dr.y, dr.x) * Mathf.Rad2Deg;
        pickupTrolleyX = po.magnitude;
        dropTrolleyX = dr.magnitude;
        sceneT0 = Time.time;
        Pose(pickupYaw, pickupTrolleyX, hookHighY);
        if (carriedBlade != null) carriedBlade.SetActive(false);
        foreach (var b in blades) if (b != null) b.gameObject.SetActive(true);
        SetGranules(false);
    }

    void Update()
    {
        if (slew == null || trolley == null || hookRig == null) return;

        // granule stream runs for a while after each drop
        SetGranules(Time.time < granulesUntil);

        if (fed >= blades.Length)
        {
            Pose(pickupYaw, pickupTrolleyX, hookHighY);
            if (!toldTruck && truck != null) { truck.Depart(); toldTruck = true; }
            return;
        }

        float t = Time.time - sceneT0 - startDelay;
        if (t < 0f) { Pose(pickupYaw, pickupTrolleyX, hookHighY); return; }

        float u = (t / cycleDuration) % 1f;
        int n = Mathf.FloorToInt(t / cycleDuration);
        if (n != lastN) { grabbed = false; released = false; lastN = n; }

        System.Func<float, float, float, float> e = (a, b, x) => Mathf.Lerp(a, b, x * x * (3f - 2f * x));
        float yaw = pickupYaw, tx = pickupTrolleyX, hy = hookHighY;

        if (u < 0.14f) { hy = e(hookHighY, hookLowPickupY, u / 0.14f); }
        else if (u < 0.20f)
        {
            hy = hookLowPickupY;
            if (!grabbed)
            {
                if (fed < blades.Length && blades[fed] != null) blades[fed].gameObject.SetActive(false);
                if (carriedBlade != null) carriedBlade.SetActive(true);
                grabbed = true;
            }
        }
        else if (u < 0.34f) { hy = e(hookLowPickupY, hookHighY, (u - 0.20f) / 0.14f); }
        else if (u < 0.56f) { float k = (u - 0.34f) / 0.22f; yaw = e(pickupYaw, dropYaw, k); tx = e(pickupTrolleyX, dropTrolleyX, k); }
        else if (u < 0.68f) { yaw = dropYaw; tx = dropTrolleyX; hy = e(hookHighY, hookLowDropY, (u - 0.56f) / 0.12f); }
        else if (u < 0.74f)
        {
            yaw = dropYaw; tx = dropTrolleyX; hy = hookLowDropY;
            if (!released)
            {
                if (carriedBlade != null) carriedBlade.SetActive(false);
                if (shredBurst != null) shredBurst.Play();
                granulesUntil = Time.time + granulesRunTime;
                released = true;
                fed++;
            }
        }
        else if (u < 0.84f) { yaw = dropYaw; tx = dropTrolleyX; hy = e(hookLowDropY, hookHighY, (u - 0.74f) / 0.10f); }
        else { float k = (u - 0.84f) / 0.16f; yaw = e(dropYaw, pickupYaw, k); tx = e(dropTrolleyX, pickupTrolleyX, k); }

        Pose(yaw, tx, hy);
    }

    void Pose(float yaw, float tx, float hy)
    {
        slew.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var tp = trolley.localPosition; tp.x = tx; trolley.localPosition = tp;
        var hp = hookRig.localPosition; hp.y = hy; hookRig.localPosition = hp;
        var cable = hookRig.Find("Cable");
        if (cable != null)
        {
            var s = cable.localScale; s.y = Mathf.Abs(hy) * 0.5f; cable.localScale = s;
            var cp = cable.localPosition; cp.y = -hy * 0.5f; cable.localPosition = cp;
        }
    }

    void SetGranules(bool on)
    {
        if (outputGranules == null) return;
        var em = outputGranules.emission;
        if (em.enabled != on) em.enabled = on;
        if (outputFallout != null)
        {
            var fem = outputFallout.emission;
            if (fem.enabled != on) fem.enabled = on;
        }
    }
}
