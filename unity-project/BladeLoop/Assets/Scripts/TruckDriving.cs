using UnityEngine;

public class TruckDriving : MonoBehaviour
{
    [Tooltip("Wait this long before truck starts driving")]
    public float startDelay = 25f;

    [Tooltip("Speed in units per second")]
    public float speed = 3f;

    [Tooltip("World-space direction to drive (1,0,0 = +X axis)")]
    public Vector3 direction = new Vector3(1, 0, 0);

    [Tooltip("Stop driving after this distance")]
    public float maxDistance = 60f;

    private float timer = 0f;
    private float distanceTraveled = 0f;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer < startDelay) return;
        if (distanceTraveled >= maxDistance) return;

        float step = speed * Time.deltaTime;
        transform.Translate(direction.normalized * step, Space.World);
        distanceTraveled += step;
    }
}