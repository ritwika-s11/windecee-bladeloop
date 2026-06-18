using UnityEngine;

public class ContinuousMover : MonoBehaviour
{
    [Tooltip("World-space direction to move (will be normalized)")]
    public Vector3 direction = Vector3.right;

    [Tooltip("Speed in units per second")]
    public float speed = 0.5f;

    [Tooltip("Loop back to start position after travelling this far")]
    public bool loop = true;
    public float resetDistance = 4f;

    [Tooltip("Random offset to start time so particles don't move in sync")]
    public bool randomizeStart = true;

    private Vector3 startPos;
    private float traveled = 0f;

    void Start()
    {
        startPos = transform.position;
        if (randomizeStart) traveled = Random.Range(0f, resetDistance);
        transform.position = startPos + direction.normalized * traveled;
    }

    void Update()
    {
        float step = speed * Time.deltaTime;
        transform.Translate(direction.normalized * step, Space.World);
        traveled += step;

        if (loop && traveled > resetDistance)
        {
            transform.position = startPos;
            traveled = 0f;
        }
    }
}