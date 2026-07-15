using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Additive driver choreography driven by the story timeline clock.
/// Seated (riding in cab) -> steps out when truck stops -> walks to oversee spot ->
/// watches the crane load -> walks back -> seated before departure.
/// Drives an optional humanoid Animator via a 'Speed' float.
/// </summary>
public class DriverController : MonoBehaviour
{
    public PlayableDirector director;
    public Transform truck;
    public Vector3 seatLocalOffset = new Vector3(-1.8f, 1.05f, 0.45f);
    public Vector3 doorLocalOffset = new Vector3(-1.2f, 0.05f, 1.7f);
    public Vector3 overseeSpot = new Vector3(-2.5f, 0.05f, 11.0f);
    public Vector3 lookAtWhileIdle = new Vector3(-4f, 2f, 16f);
    public float exitTime = 20.3f;
    public float returnStartTime = 34.5f;
    public float walkSpeed = 1.8f;
    public Animator characterAnimator;

    bool boarded;

    void Update()
    {
        if (director == null || truck == null) return;
        float t = (float)director.time;

        if (t < exitTime) { boarded = false; SitInSeat(); }
        else if (t < returnStartTime)
        {
            if (WalkTowards(overseeSpot)) { FacePoint(lookAtWhileIdle); SetSpeed(0f); }
        }
        else
        {
            if (boarded) { SitInSeat(); return; }
            Vector3 door = truck.TransformPoint(doorLocalOffset);
            door.y = 0.05f;
            if (WalkTowards(door)) { boarded = true; SitInSeat(); }
        }
    }

    void SitInSeat()
    {
        transform.position = truck.TransformPoint(seatLocalOffset);
        transform.rotation = truck.rotation;
        SetSpeed(0f);
    }

    bool WalkTowards(Vector3 target)
    {
        Vector3 pos = transform.position;
        if (pos.y > 0.8f) // first frame after leaving the seat: step down at the door
        {
            Vector3 door = truck.TransformPoint(doorLocalOffset);
            pos = new Vector3(door.x, 0.05f, door.z);
            transform.position = pos;
        }
        Vector3 flatTarget = new Vector3(target.x, 0.05f, target.z);
        Vector3 flatPos = new Vector3(pos.x, 0.05f, pos.z);
        Vector3 to = flatTarget - flatPos;
        if (to.magnitude < 0.08f) { transform.position = flatTarget; return true; }
        Vector3 step = to.normalized * walkSpeed * Time.deltaTime;
        if (step.magnitude > to.magnitude) step = to;
        transform.position = flatPos + step;
        if (to.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), 10f * Time.deltaTime);
        SetSpeed(1f);
        return false;
    }

    void FacePoint(Vector3 p)
    {
        Vector3 dir = p - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), 5f * Time.deltaTime);
    }

    void SetSpeed(float s)
    {
        if (characterAnimator != null) characterAnimator.SetFloat("Speed", s);
    }
}
