using UnityEngine;

/// <summary>
/// Spins the GameObject on its local Z axis at a steady RPM.
/// Used by the procedural turbine spawner for the rotor hub.
/// </summary>
public class BladeRotor : MonoBehaviour
{
    public float rpm = 12f;
    void Update()
    {
        transform.Rotate(Vector3.forward, rpm * 6f * Time.deltaTime, Space.Self);
    }
}
