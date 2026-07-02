using UnityEngine;

/// <summary>
/// Stage 2 truck: drives in with blades on the flatbed, parks next to the crane,
/// and waits while the crane unloads blades one-by-one into the shredder.
/// Departs (drives off +X) when Depart() is called after the last blade is taken.
/// (Bed no longer tilts - the crane does the unloading per the CEE story.)
/// </summary>
public class TruckDumpAnimator : MonoBehaviour
{
    [Header("Rig")]
    public Transform bedPivot;
    public Transform[] cargoBlades;

    [Header("Motion")]
    public Vector3 startPos = new Vector3(-17f, -0.31f, -4f);
    public Vector3 parkedPos = new Vector3(-2.5f, -0.31f, -4f);
    public Vector3 exitPos = new Vector3(14f, -0.31f, -4f);
    public float approachDuration = 7f;
    public float exitDuration = 6f;
    public float startDelay = 0f;

    float departT = -1f;
    float sceneT0; // scene-relative clock for the chained tour

    void Start()
    {
        sceneT0 = Time.time;
        transform.position = startPos;
        if (bedPivot != null) bedPivot.localRotation = Quaternion.identity;
        foreach (var b in cargoBlades) if (b != null) b.gameObject.SetActive(true);
    }

    public void Depart()
    {
        if (departT < 0f) departT = Time.time;
    }

    void Update()
    {
        System.Func<float, float> ease = x => x * x * (3f - 2f * x);
        if (departT >= 0f)
        {
            float k = Mathf.Clamp01((Time.time - departT) / exitDuration);
            transform.position = Vector3.Lerp(parkedPos, exitPos, ease(k));
            return;
        }
        float t = Time.time - sceneT0 - startDelay;
        if (t < 0f) { transform.position = startPos; return; }
        transform.position = Vector3.Lerp(startPos, parkedPos, ease(Mathf.Clamp01(t / approachDuration)));
    }
}
