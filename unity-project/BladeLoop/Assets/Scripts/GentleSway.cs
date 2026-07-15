using UnityEngine;

/// <summary>
/// Gentle organic motion: sways foliage or drifts clouds. Additive, visual only.
/// </summary>
public class GentleSway : MonoBehaviour
{
    public enum Mode { Sway, Drift }
    public Mode mode = Mode.Sway;
    [Tooltip("Sway: rotation degrees. Drift: units/second.")]
    public float amount = 2.5f;
    public float speed = 1.2f;
    [Tooltip("Drift only: wrap after this distance from start.")]
    public float wrapDistance = 260f;

    Vector3 basePos;
    Quaternion baseRot;
    float seed;

    void Start()
    {
        basePos = transform.position;
        baseRot = transform.localRotation;
        seed = Random.Range(0f, 20f);
    }

    void Update()
    {
        if (mode == Mode.Sway)
        {
            float t = Time.time * speed + seed;
            float a = (Mathf.PerlinNoise(seed, t) - 0.5f) * 2f * amount;
            float b = (Mathf.PerlinNoise(seed + 7f, t * 0.8f) - 0.5f) * 2f * amount;
            transform.localRotation = baseRot * Quaternion.Euler(a, 0f, b);
        }
        else
        {
            transform.position += Vector3.right * amount * Time.deltaTime;
            if (transform.position.x - basePos.x > wrapDistance)
                transform.position = new Vector3(basePos.x - wrapDistance * 0.2f, transform.position.y, transform.position.z);
        }
    }
}
