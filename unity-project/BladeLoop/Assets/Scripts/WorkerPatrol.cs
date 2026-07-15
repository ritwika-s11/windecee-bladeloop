using UnityEngine;

/// <summary>
/// Purposeful worker patrol: walks between two points, pauses at each end
/// (as if inspecting), drives a humanoid Animator 'Speed' float. Additive.
/// </summary>
public class WorkerPatrol : MonoBehaviour
{
    public Vector3 pointA;
    public Vector3 pointB;
    public float walkSpeed = 1.2f;
    public float pauseTime = 4f;
    public Animator animator;

    Vector3 target;
    float pauseUntil;

    void Start()
    {
        transform.position = new Vector3(pointA.x, 0f, pointA.z);
        target = pointB;
        pauseUntil = Time.time + Random.Range(0f, pauseTime);
    }

    void Update()
    {
        if (Time.time < pauseUntil) { Set(0f); return; }
        Vector3 flat = new Vector3(target.x, 0f, target.z);
        Vector3 pos = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 to = flat - pos;
        if (to.magnitude < 0.1f)
        {
            target = (Vector3.Distance(flat, new Vector3(pointA.x,0,pointA.z)) < 0.2f) ? pointB : pointA;
            pauseUntil = Time.time + pauseTime;
            Set(0f);
            return;
        }
        Vector3 step = to.normalized * walkSpeed * Time.deltaTime;
        if (step.magnitude > to.magnitude) step = to;
        transform.position = pos + step;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), 8f * Time.deltaTime);
        Set(1f);
    }

    void Set(float s) { if (animator != null) animator.SetFloat("Speed", s); }
}
