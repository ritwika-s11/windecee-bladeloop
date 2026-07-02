using UnityEngine;

/// <summary>
/// Organic gas-flame flicker: jitters the flame's scale and (optionally) a light's
/// intensity using per-instance Perlin noise. Attach to a flame mesh or a light.
/// </summary>
public class BurnerFlicker : MonoBehaviour
{
    public float speed = 9f;
    public float heightJitter = 0.25f;
    public float widthJitter = 0.10f;
    public Light flickerLight;
    public float lightJitter = 0.3f;

    Vector3 baseScale;
    float baseIntensity;
    float seed;

    void Start()
    {
        baseScale = transform.localScale;
        seed = Random.Range(0f, 100f);
        if (flickerLight != null) baseIntensity = flickerLight.intensity;
    }

    void Update()
    {
        float t = Time.time * speed;
        float nH = Mathf.PerlinNoise(seed, t) - 0.5f;
        float nW = Mathf.PerlinNoise(seed + 37f, t * 1.3f) - 0.5f;
        transform.localScale = new Vector3(
            baseScale.x * (1f + nW * 2f * widthJitter),
            baseScale.y * (1f + nH * 2f * heightJitter),
            baseScale.z * (1f + nW * 2f * widthJitter));
        if (flickerLight != null)
            flickerLight.intensity = baseIntensity * (1f + nH * 2f * lightJitter);
    }
}
